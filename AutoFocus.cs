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
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Model;
using NINA.Image.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
            bool Focused = false;
            const int near = (int)EDSDK.EvfDriveLens_Near2;
            const int far = (int)EDSDK.EvfDriveLens_Far2;
            int iteration = 0;
            List<FocusPoint> FocusPoints = new List<FocusPoint>();
            try
            {
                CalibrateLens(camera, canon);

                // Needed Variables
                CancellationTokenSource cts = new CancellationTokenSource();

                IAsyncEnumerable<IExposureData> liveViewEnumerable = camera.LiveView(cts.Token);

                // LiveView Loop
                await liveViewEnumerable.ForEachAsync(async _ =>
                {

                    // Break out of loop it it seems like it is stuck
                    if (iteration > settings.MaxTryCount)
                    {
                        cts.Cancel();
                    }

                    // Break out of loop if focused
                    if (Focused)
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
                    IImageData imageData = await data.ToImageData(Progress, cts.Token);
                    IRenderedImage image = await imaging.PrepareImage(imageData, new PrepareImageParameters(), cts.Token);
                    image = await image.Stretch(settings.StretchFactor, settings.BlackClipping, true);
                    image = await image.DetectStars(false, StarSensitivityEnum.Normal, NoiseReductionEnum.None);
                    IStarDetectionAnalysis detection = image.RawImageData.StarDetectionAnalysis;
                    FocusPoints.Add(new FocusPoint(detection));

                    // Check if focused
                    if (iteration >= 1)
                    {
                        if (detection.HFR > FocusPoints[FocusPoints.Count - 2].HFR)
                        {
                            DriveFocus(canon, far);
                            Focused = true;
                        }
                    }


                    // Increment iteration
                    iteration++;
                });
            }
            catch (TaskCanceledException)
            {

            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
            LastAF = DateTime.Now;
            return new AutoFocusResult(Focused, FocusPoints);
        }

        private void DriveFocus(IntPtr cam, int direction)
        {
            EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, direction);
            Thread.Sleep(500);
        }

        private void CalibrateLens(ICameraMediator cam, IntPtr ptr)
        {
            CancellationTokenSource t = new CancellationTokenSource();
            cam.LiveView(t.Token).ForEachAsync(async _ =>
            {
                int i = 0;
                while (true)
                {
                    EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far3);
                    Thread.Sleep(750);
                    Logger.Info(i.ToString());
                    i++;
                    if (i == 5)
                    {
                        EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                        Thread.Sleep(750); // Let Focus Settle, EDSDK does not wait for the lens to finish moving the focus
                        break;
                    }
                }
                t.Cancel();
            });
        }
    }

    public class AutoFocusSettings
    {
        public int ExposureTime = 5;
        public double BlackClipping = -2.8;
        public double StretchFactor = 0.2;
        public int MaxTryCount = 20;
    }

    public class AutoFocusResult
    {
        public bool Successfull;
        public List<FocusPoint> FocusPoints;
        public AutoFocusResult(bool successfull, List<FocusPoint> focusPoints)
        {
            Successfull = successfull;
            FocusPoints = focusPoints;
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
}
