#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Accord.Imaging.Filters;
using Dasync.Collections;
using EDSDKLib;
using LensAF.Dockable;
using LensAF.Properties;
using LensAF.Util;
using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.ImageAnalysis;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel.Equipment.Camera;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LensAF
{
    public class AutoFocus
    {
        public readonly CancellationToken Token;
        public static CancellationTokenSource PublicToken;
        public readonly IProgress<ApplicationStatus> Progress;
        public static DateTime LastAF;
        public readonly IProfileService Profile;

        private AutoFocusLogic Method;

        public AutoFocus(CancellationToken token, IProgress<ApplicationStatus> progress, IProfileService profile)
        {
            Token = token;
            Progress = progress;
            Profile = profile;
        }

        /// <summary>
        /// Runs an automated focusing session
        /// </summary>
        /// <param name="camera">Instance of ICameraMediator</param>
        /// <param name="imaging">Instance of IImagingMediator</param>
        /// <param name="settings">Instance of the AutoFocusSettings class</param>
        /// <returns>AutoFocusResult containing all important informations</returns>
        public async Task<AutoFocusResult> RunAF(ICameraMediator camera, IImagingMediator imaging, AutoFocusSettings settings)
        {
            IntPtr canon = Utility.GetCamera(camera);

            if (canon == IntPtr.Zero)
            {
                Notification.ShowError("Could not run AF: No canon camera connected");
                ReportUpdate(string.Empty);
                return new AutoFocusResult(false, new List<FocusPoint>(), TimeSpan.Zero, DateTime.Now, StepSizeToString());
            }

            EDSDK.EdsGetPropertyData(canon, 0x00000416, 0, out uint HasLens); // Check if a lens is attached
            if (HasLens == 0)
            {
                Notification.ShowError("Can't start AF: No lens attached");
                Logger.Error("Can't start AF: No lens attached");
                ReportUpdate(string.Empty);
                return new AutoFocusResult(false, new List<FocusPoint>(), TimeSpan.Zero, DateTime.Now, StepSizeToString());
            }
            DateTime start = DateTime.Now;
            bool Focused = false;
            int iteration = 0;
            Method = GetSelectedLogic();
            LensAFVM.Instance.AutoFocusIsRunning = true;
            List<FocusPoint> FocusPoints = new List<FocusPoint>();
            try
            {
                // Needed Variables
                PublicToken = new CancellationTokenSource();

                IAsyncEnumerable<IExposureData> liveViewEnumerable = camera.LiveView(PublicToken.Token);

                // LiveView Loop
                await liveViewEnumerable.ForEachAsync(async _ =>
                {
                    if (iteration == 0)
                    {
                        ReportUpdate("Calibrating lens");
                        CalibrateLens(canon);
                    }

                    // All Focuspoints are collected: Compute Final Focus Point
                    if (iteration == settings.Iterations - 1)
                    {
                        ReportUpdate("Finishing Autofocus");
                        int iterations = DetermineFinalFocusPoint(FocusPoints, settings.Iterations);
                        for (int i = 0; i < iterations; i++)
                        {
                            DriveFocus(canon, FocusDirection.Far);
                        }
                        Focused = true;
                        LensAFVM.Instance.AutoFocusIsRunning = false;

                        ReportUpdate(string.Empty);
                        PublicToken.Cancel();
                    }

                    if (Token.IsCancellationRequested)
                    {
                        ReportUpdate(string.Empty);
                        LensAFVM.Instance.AutoFocusIsRunning = false;
                        PublicToken.Cancel();
                    }

                    if (!Focused)
                    {
                        // Drive Focus
                        ReportUpdate($"Moving focus, iteration {iteration + 1}");
                        DriveFocus(canon, FocusDirection.Near);
                        Logger.Trace($"Moving Focus... iteration {iteration}");

                        // Download and Prepare Image
                        ReportUpdate("Capturing image and detecting stars");
                        IRenderedImage data = await imaging.CaptureAndPrepareImage(new CaptureSequence(
                            settings.ExposureTime,
                            "AF Frame",
                            new FilterInfo(),
                            new BinningMode(1, 1),
                            1),
                            new PrepareImageParameters(true),
                            Token, Progress);

                        if (Method == AutoFocusLogic.STARHFR)
                        {
                            StarDetectionResult detection = await PrepareImageForStarHFR(data);
                            FocusPoints.Add(new FocusPoint(detection));

                            AddToPlot(detection.AverageHFR, iteration);
                        }
                        else
                        {
                            ContrastDetectionResult detection = PrepareImageForContrast(data);
                            FocusPoints.Add(new FocusPoint(detection));

                            AddToPlot(detection.AverageContrast, iteration);
                        }
                    }

                    if (Token.IsCancellationRequested)
                    {
                        ReportUpdate(string.Empty);
                        LensAFVM.Instance.AutoFocusIsRunning = false;
                        PublicToken.Cancel();
                    }

                    // Increment iteration
                    iteration++;
                });
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            LensAFVM.Instance.AutoFocusIsRunning = false;

            LastAF = DateTime.Now;
            AutoFocusResult res = new AutoFocusResult(Focused, FocusPoints, LastAF - start, LastAF, StepSizeToString());
            Util.CameraInfo info = new Util.CameraInfo(canon);
            GenerateLog(settings, res, info);
            if (LensAFVM.Instance != null)
            {
                LensAFVM.Instance.LastAF = LastAF.ToString("HH:m");
            }
            ReportUpdate(string.Empty);
            return res;
        }

        private void ReportUpdate(string update)
        {
            Progress?.Report(new ApplicationStatus() { Status = update });
        }

        private AutoFocusLogic GetSelectedLogic()
        {
            if (Settings.Default.AutoFocusLogic == 0)
            {
                return AutoFocusLogic.STARHFR;
            }
            return AutoFocusLogic.CONTRAST;
        }

        private string StepSizeToString()
        {
            if (Settings.Default.UseMixedStepSize)
            {
                string logic;
                if (Settings.Default.StepSizeLogic == 0)
                {
                    logic = "+";
                }
                else
                {
                    logic = "-";
                }
                return $"{Settings.Default.StepSizeBig} {logic} {Settings.Default.StepSizeSmall}";
            }
            return $"{Settings.Default.SelectedStepSize + 1}";
        }

        private int DetermineFinalFocusPoint(List<FocusPoint> points, int iterations)
        {
            int iteration;
            if (Method == AutoFocusLogic.STARHFR)
            {
                List<double> hfrs = new List<double>();
                List<double> temp = new List<double>();
                foreach (FocusPoint point in points)
                {
                    hfrs.Add(point.HFR);
                    temp.Add(point.HFR);
                }
                hfrs.Sort();
                int count = 0;

                do
                {
                    iteration = temp.IndexOf(hfrs[count]);
                    count++;
                } while (hfrs[count] == 0);
            }
            else
            {
                List<double> contrasts = new List<double>();
                List<double> temp = new List<double>();
                foreach (FocusPoint point in points)
                {
                    contrasts.Add(point.Contrast);
                    temp.Add(point.Contrast);
                }
                contrasts.Sort();
                iteration = temp.IndexOf(contrasts[contrasts.Count - 1]);
            }

            return iterations - iteration;
        }

        private void AddToPlot(double detection, int iteration)
        {
            if (LensAFVM.Instance != null)
            {
                LensAFVM.Instance.AddToPlot(new DataPoint(iteration, detection));
            }
        }

        private void DriveFocus(IntPtr cam, FocusDirection direction)
        {
            if (Settings.Default.UseMixedStepSize)
            {
                for (int i = 0; i < Settings.Default.StepSizeBig; i++)
                {
                    if (direction == FocusDirection.Far)
                    {
                        EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far2);
                    }
                    else
                    {
                        EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near2);
                    }
                    Thread.Sleep(200);
                }
                if (Settings.Default.StepSizeLogic == 1 && Settings.Default.StepSizeBig > 0)
                {
                    for (int i = 0; i < Settings.Default.StepSizeSmall; i++)
                    {
                        if (direction == FocusDirection.Far)
                        {
                            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                        }
                        else
                        {
                            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                        }
                        Thread.Sleep(200);
                    }
                }
                else
                {
                    for (int i = 0; i < Settings.Default.StepSizeSmall; i++)
                    {
                        if (direction == FocusDirection.Far)
                        {
                            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                        }
                        else
                        {
                            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                        }
                        Thread.Sleep(200);
                    }
                }
                return;
            }
            if (Settings.Default.SelectedStepSize == 0)
            {
                if (direction == FocusDirection.Near)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                }
            }
            else if (Settings.Default.SelectedStepSize == 1)
            {
                if (direction == FocusDirection.Near)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200); // Required in oder to mix multiple step sizes
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                }
            }
            else if (Settings.Default.SelectedStepSize == 2)
            {
                if (direction == FocusDirection.Near)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                }
            }
            else if (Settings.Default.SelectedStepSize == 3)
            {
                if (direction == FocusDirection.Near)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near2);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far2);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                }
            }
            else if (Settings.Default.SelectedStepSize == 4)
            {
                if (direction == FocusDirection.Near)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near2);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far2);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                }
            }
            else if (Settings.Default.SelectedStepSize == 5)
            {
                if (direction == FocusDirection.Near)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near2);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far2);
                }
            }
            else if (Settings.Default.SelectedStepSize == 6)
            {
                if (direction == FocusDirection.Near)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near2);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far2);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                }
            }
            else if (Settings.Default.SelectedStepSize == 7)
            {
                if (direction == FocusDirection.Near)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near2);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far2);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    Thread.Sleep(200);
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                }
            }
            else
            {
                for (int i = 0; i < Settings.Default.StepSizeBig; i++)
                {
                    if (direction == FocusDirection.Far)
                    {
                        EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far2);
                    }
                    else
                    {
                        EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near2);
                    }
                    Thread.Sleep(200);
                }
                if (Settings.Default.StepSizeLogic == 1 && Settings.Default.StepSizeBig > 0)
                {
                    for (int i = 0; i < Settings.Default.StepSizeSmall; i++)
                    {
                        if (direction == FocusDirection.Far)
                        {
                            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                        }
                        else
                        {
                            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                        }
                        Thread.Sleep(200);
                    }
                }
                else
                {
                    for (int i = 0; i < Settings.Default.StepSizeSmall; i++)
                    {
                        if (direction == FocusDirection.Far)
                        {
                            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                        }
                        else
                        {
                            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                        }
                        Thread.Sleep(200);
                    }
                }
            }

            Thread.Sleep(1000); // Wait for focus the finish rotating
        }

        private async Task<StarDetectionResult> PrepareImageForStarHFR(IRenderedImage exposure)
        {
            IImageData imageData = exposure.RawImageData;
            PixelFormat pixelFormat;

            if (imageData.Properties.IsBayered && Profile.ActiveProfile.ImageSettings.DebayerImage)
            {
                pixelFormat = PixelFormats.Rgb48;
            }
            else
            {
                pixelFormat = PixelFormats.Gray16;
            }

            StarDetectionParams analysisParams = new StarDetectionParams
            {
                Sensitivity = StarSensitivityEnum.Normal,
                NoiseReduction = NoiseReductionEnum.None,
                IsAutoFocus = true
            };
            return await new StarDetection().Detect(exposure, pixelFormat, analysisParams, Progress, Token);
        }

        private ContrastDetectionResult PrepareImageForContrast(IRenderedImage exposure)
        {
            BitmapSource image = exposure.Image;
            if (exposure.RawImageData.Properties.IsBayered && Profile.ActiveProfile.ImageSettings.DebayerImage)
            {
                using (var source = ImageUtility.BitmapFromSource(exposure.OriginalImage, System.Drawing.Imaging.PixelFormat.Format48bppRgb))
                {
                    using (var img = new Grayscale(0.2125, 0.7154, 0.0721).Apply(source))
                    {
                        image = ImageUtility.ConvertBitmap(img, PixelFormats.Gray16);
                        image.Freeze();
                    }
                }
            }
            ContrastDetectionParams analysisParams = new ContrastDetectionParams()
            {
                Sensitivity = StarSensitivityEnum.Normal,
                NoiseReduction = NoiseReductionEnum.None
            };
            return new Util.ContrastDetection().Measure(image, exposure, analysisParams, Progress, Token);
        }

        private void CalibrateLens(IntPtr ptr)
        {
            int i = 0;
            while (i != 7)
            {
                EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far3);
                Thread.Sleep(350); // Let Focus Settle, EDSDK does not wait for the lens to finish moving the focus
                i++;
            }
        }

        public void GenerateLog(AutoFocusSettings settings, AutoFocusResult result, Util.CameraInfo info)
        {
            settings.AutoFocusMethod = GetSelectedLogic();
            AutoFocusReport report = new AutoFocusReport()
            {
                Settings = settings,
                Result = result,
                CanonInfo = info,
            };
            string ReportDirectory = Path.Combine(CoreUtil.APPLICATIONTEMPPATH, "AutoFocus", "Lens AF");
            if (!Directory.Exists(ReportDirectory))
            {
                Directory.CreateDirectory(ReportDirectory);
            }
            string path = Path.Combine(ReportDirectory, DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss") + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(report));
        }
    }
}