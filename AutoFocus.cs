#region "copyright"

/*
    Copyright © 2021 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Dasync.Collections;
using EDSDKLib;
using LensAF.Properties;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using NINA.Image.ImageAnalysis;
using NINA.Profile.Interfaces;

namespace LensAF
{
    public class AutoFocus
    {
        public readonly CancellationToken Token;
        public readonly IProgress<ApplicationStatus> Progress;
        public static DateTime LastAF;
        public readonly IProfileService Profile;
        public AutoFocus(CancellationToken token, IProgress<ApplicationStatus> progress, IProfileService profile)
        {
            Token = token;
            Progress = progress;
            Profile = profile;
        }
        public async Task<AutoFocusResult> RunAF(IntPtr canon, ICameraMediator camera, IImagingMediator imaging, AutoFocusSettings settings)
        {
            DateTime start = DateTime.Now;
            bool Focused = false;
            int iteration = 0;
            List<FocusPoint> FocusPoints = new List<FocusPoint>();
            try
            {
                // Needed Variables
                CancellationTokenSource cts = new CancellationTokenSource();

                IAsyncEnumerable<IExposureData> liveViewEnumerable = camera.LiveView(cts.Token);

                // LiveView Loop
                await liveViewEnumerable.ForEachAsync(_ =>
                {
                    DriveFocus(canon, FocusDirection.Near);
                    cts.Cancel();
                    /* if (iteration == 0)
                    {
                        CalibrateLens(canon);
                    }

                    // Break out of loop it it seems like it is stuck
                    if (iteration > settings.MaxTryCount)
                    {
                        cts.Cancel();
                    }

                    if (Token.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }

                    // Drive Focus
                    DriveFocus(canon, FocusDirection.Near);
                    Logger.Trace($"Moving Focus... iteration {iteration}");

                    // Download and Prepare Image
                    IExposureData data = await imaging.CaptureImage(new CaptureSequence(
                        settings.ExposureTime,
                        "AF Frame",
                        new FilterInfo(),
                        new BinningMode(1, 1),
                        1),
                        Token, Progress);
                    StarDetectionResult detection = await PrepareImage(data, settings, imaging);
                    FocusPoints.Add(new FocusPoint(detection));

                    // Check if focused
                    if (iteration >= 1)
                    {
                        if (detection.AverageHFR > FocusPoints[FocusPoints.Count - 2].HFR)
                        {
                            DriveFocus(canon, FocusDirection.Far);
                            Focused = true;
                            cts.Cancel();
                        }
                    }

                    if (Token.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }

                    // Increment iteration
                    iteration++; */
                });
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                throw e;
            }
            LastAF = DateTime.Now;
            AutoFocusResult res = new AutoFocusResult(Focused, FocusPoints, LastAF - start);
            GenerateLog(settings, res);
            return res;
        }

        private void DriveFocus(IntPtr cam, FocusDirection direction)
        {
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

        private async Task<StarDetectionResult> PrepareImage(IExposureData exposure, AutoFocusSettings settings, IImagingMediator imaging)
        {
            IImageData imageData = await exposure.ToImageData(Progress, Token);
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
                NoiseReduction = NoiseReductionEnum.None
            };


            IRenderedImage image = await imaging.PrepareImage(imageData, new PrepareImageParameters(), Token);
            image = await image.Stretch(settings.StretchFactor, settings.BlackClipping, true);
            StarDetectionResult result = await new StarDetection().Detect(image, pixelFormat, analysisParams, Progress, Token);
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
                Thread.Sleep(500); // Let Focus Settle, EDSDK does not wait for the lens to finish moving the focus
                i++;
            }
        }

        public static void GenerateLog(AutoFocusSettings settings, AutoFocusResult result)
        {
            AutoFocusReport report = new AutoFocusReport()
            {
                Settings = settings,
                Result = result
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

    public class AutoFocusSettings
    {
        public double ExposureTime = 5;
        public double BlackClipping = -2.8;
        public double StretchFactor = 0.15;
        public int MaxTryCount = 15;
    }

    public class AutoFocusResult
    {
        public bool Successfull;
        public List<FocusPoint> FocusPoints;
        public TimeSpan Duration;
        public int StepSize = Settings.Default.SelectedStepSize + 1;
        public AutoFocusResult(bool successfull, List<FocusPoint> focusPoints, TimeSpan duration)
        {
            Successfull = successfull;
            FocusPoints = focusPoints;
            Duration = duration;
        }
    }

    public class FocusPoint
    {
        public int Stars { get; set; }
        public double HFR { get; set; }
        public FocusPoint(int stars, double HFR)
        {
            Stars = stars;
            this.HFR = HFR;
        }
        public FocusPoint(StarDetectionResult analysis)
        {
            Stars = analysis.DetectedStars;
            HFR = analysis.AverageHFR;
        }
    }

    public class AutoFocusReport
    {
        public AutoFocusSettings Settings;
        public AutoFocusResult Result;
    }

    public enum FocusDirection
    {
        Far,
        Near
    }
}
