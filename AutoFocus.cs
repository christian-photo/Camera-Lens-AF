#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

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
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.ImageAnalysis;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LensAF
{
    public class AutoFocus
    {
        public readonly CancellationToken Token;
        public static CancellationTokenSource PublicToken;
        public readonly IProgress<ApplicationStatus> Progress;
        public static DateTime LastAF;
        public readonly IProfileService Profile;

        public AutoFocus(CancellationToken token, IProgress<ApplicationStatus> progress, IProfileService profile)
        {
            Token = token;
            Progress = progress;
            Profile = profile;
        }

        /// <summary>
        /// Runs an automated focusing session
        /// </summary>
        /// <param name="canon">reference to the canon camera</param>
        /// <param name="camera">Instance of ICameraMediator</param>
        /// <param name="imaging">Instance of IImagingMediator</param>
        /// <param name="settings">Instance of the AutoFocusSettings class</param>
        /// <returns>AutoFocusResult containing all important informations</returns>
        public async Task<AutoFocusResult> RunAF(IntPtr canon, ICameraMediator camera, IImagingMediator imaging, AutoFocusSettings settings)
        {
            DateTime start = DateTime.Now;
            bool Focused = false;
            int iteration = 0;
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
                        CalibrateLens(canon);
                    }

                    // All Focuspoints are collected: Compute Final Focus Point
                    if (iteration == settings.Iterations - 1)
                    {
                        int iterations = DetermineFinalFocusPoint(FocusPoints, settings.Iterations);
                        for (int i = 0; i < iterations; i++)
                        {
                            DriveFocus(canon, FocusDirection.Far);
                        }
                        Focused = true;
                        LensAFVM.Instance.AutoFocusIsRunning = false;

                        PublicToken.Cancel();
                    }

                    if (Token.IsCancellationRequested)
                    {
                        LensAFVM.Instance.AutoFocusIsRunning = false;
                        PublicToken.Cancel();
                    }

                    if (!Focused)
                    {
                        // Drive Focus
                        DriveFocus(canon, FocusDirection.Near);
                        Logger.Trace($"Moving Focus... iteration {iteration}");

                        // Download and Prepare Image
                        IRenderedImage data = await imaging.CaptureAndPrepareImage(new CaptureSequence(
                            settings.ExposureTime,
                            "AF Frame",
                            new FilterInfo(),
                            new BinningMode(1, 1),
                            1),
                            new PrepareImageParameters(true),
                            Token, Progress);
                        StarDetectionResult detection = await PrepareImage(data);
                        FocusPoints.Add(new FocusPoint(detection));

                        AddToPlot(detection, iteration);
                    }

                    if (Token.IsCancellationRequested)
                    {
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
            CameraInfo info = new CameraInfo(canon);
            GenerateLog(settings, res, info);
            if (LensAFVM.Instance != null)
            {
                LensAFVM.Instance.LastAF = LastAF.ToString("HH:m");
            }
            return res;
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
            List<double> hfrs = new List<double>();
            List<double> temp = new List<double>();
            foreach (FocusPoint point in points)
            {
                hfrs.Add(point.HFR);
                temp.Add(point.HFR);
            }
            hfrs.Sort();
            int iteration;
            int count = 0;

            do
            {
                iteration = temp.IndexOf(hfrs[count]);
                count++;
            } while (hfrs[count] == 0);
            
            return iterations - iteration;
        }

        private void AddToPlot(StarDetectionResult detection, int iteration)
        {
            if (LensAFVM.Instance != null)
            {
                LensAFVM.Instance.AddToPlot(new DataPoint(iteration, detection.AverageHFR));
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

            Thread.Sleep(1000); // Wait for focus the finish rotating
        }

        private async Task<StarDetectionResult> PrepareImage(IRenderedImage exposure)
        {
            IImageData imageData = exposure.RawImageData;
            System.Windows.Media.PixelFormat pixelFormat;

            if (imageData.Properties.IsBayered && Profile.ActiveProfile.ImageSettings.DebayerImage)
            {
                pixelFormat = System.Windows.Media.PixelFormats.Rgb48;
            }
            else
            {
                pixelFormat = System.Windows.Media.PixelFormats.Gray16;
            }

            StarDetectionParams analysisParams = new StarDetectionParams
            {
                Sensitivity = StarSensitivityEnum.Normal,
                NoiseReduction = NoiseReductionEnum.None,
                IsAutoFocus = true
            };

            StarDetectionResult result = await new StarDetection().Detect(exposure, pixelFormat, analysisParams, Progress, Token);
            return result;
        }

        /// <summary>
        ///     This Calibrates the lens to the complete right (infinite)
        ///     Camera has to be in Live View!!!
        /// </summary>
        /// <param name="ptr">IntPtr for the camera</param>
        /// <returns></returns>
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

        public static void GenerateLog(AutoFocusSettings settings, AutoFocusResult result, CameraInfo info)
        {
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