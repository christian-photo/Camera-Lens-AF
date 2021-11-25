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

namespace LensAF
{
    public class AutoFocus
    {
        public readonly CancellationToken Token;
        public readonly IProgress<ApplicationStatus> Progress;
        public static DateTime LastAF;
        public AutoFocus(CancellationToken token, IProgress<ApplicationStatus> progress)
        {
            Token = token;
            Progress = progress;
        }
        public async Task<AutoFocusResult> RunAF(IntPtr canon, ICameraMediator camera, IImagingMediator imaging, AutoFocusSettings settings)
        {
            DateTime start = DateTime.Now;
            bool Focused = false;
            int near;
            int far;
            if (Settings.Default.SelectedStepSize == 0 || Settings.Default.SelectedStepSize == 1)
            {
                near = (int)EDSDK.EvfDriveLens_Near1;
                far = (int)EDSDK.EvfDriveLens_Far1;
            } 
            else
            {
                near = (int)EDSDK.EvfDriveLens_Near2;
                far = (int)EDSDK.EvfDriveLens_Far2;
            }
            int iteration = 0;
            List<FocusPoint> FocusPoints = new List<FocusPoint>();
            try
            {
                // Needed Variables
                CancellationTokenSource cts = new CancellationTokenSource();

                IAsyncEnumerable<IExposureData> liveViewEnumerable = camera.LiveView(cts.Token);

                // LiveView Loop
                await liveViewEnumerable.ForEachAsync(async _ =>
                {
                    if (iteration == 0)
                    {
                        CalibrateLens(canon);
                    }

                    // Break out of loop it it seems like it is stuck
                    if (iteration > settings.MaxTryCount)
                    {
                        cts.Cancel();
                    }

                    // Drive Focus
                    DriveFocus(canon, near);
                    Logger.Trace($"Moving Focus... iteration {iteration}");

                    // Download and Prepare Image
                    IExposureData data = await imaging.CaptureImage(new CaptureSequence(
                        settings.ExposureTime,
                        "AF Frame",
                        new FilterInfo(),
                        new BinningMode(1, 1),
                        1),
                        Token, Progress);
                    IImageData imageData = await data.ToImageData(Progress, Token);
                    IRenderedImage image = await imaging.PrepareImage(imageData, new PrepareImageParameters(), Token);
                    image = await image.Stretch(settings.StretchFactor, settings.BlackClipping, true);
                    image = await image.DetectStars(false, StarSensitivityEnum.Normal, NoiseReductionEnum.None);
                    IStarDetectionAnalysis detection = image.RawImageData.StarDetectionAnalysis;
                    FocusPoints.Add(new FocusPoint(detection));

                    // Check if focused
                    if (iteration >= 1)
                    {
                        if (detection.HFR > FocusPoints[FocusPoints.Count - 2].HFR)
                        {
                            DriveFocus(canon, far, true);
                            Focused = true;
                            cts.Cancel();
                        }
                    }

                    if (Token.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }

                    // Increment iteration
                    iteration++;
                });
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            LastAF = DateTime.Now;
            AutoFocusResult res = new AutoFocusResult(Focused, FocusPoints, LastAF - start);
            GenerateLog(settings, res);
            return res;
        }

        private void DriveFocus(IntPtr cam, int direction, bool isFar = false)
        {
            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, direction);
            if (Settings.Default.SelectedStepSize == 1)
            {
                if (isFar)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                }
            }
            else if (Settings.Default.SelectedStepSize == 2)
            {
                if (isFar)
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                }
                else
                {
                    EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                }
            }
            
            Thread.Sleep(500); // Wait for focus the finish rotating
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
            while (i != 5)
            {
                EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far3);
                Thread.Sleep(750); // Let Focus Settle, EDSDK does not wait for the lens to finish moving the focus
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
        public int ExposureTime = 5;
        public double BlackClipping = -2.8;
        public double StretchFactor = 0.15;
        public int MaxTryCount = 20;
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
        public FocusPoint(IStarDetectionAnalysis analysis)
        {
            Stars = analysis.DetectedStars;
            HFR = analysis.HFR;
        }
    }

    public class AutoFocusReport
    {
        public AutoFocusSettings Settings;
        public AutoFocusResult Result;
    }
}
