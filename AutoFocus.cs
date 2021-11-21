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
            bool TooClose = false;
            bool TooFar = false;
            const int near = (int)EDSDK.EvfDriveLens_Near2;
            const int far = (int)EDSDK.EvfDriveLens_Far2;
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
                    if (iteration == 0)
                    {
                        EDSDK.EdsSendCommand(canon, EDSDK.CameraCommand_DriveLensEvf, far);
                    }
                    else if (iteration == 1)
                    {
                        EDSDK.EdsSendCommand(canon, EDSDK.CameraCommand_DriveLensEvf, near);
                    }
                    else
                    {
                        if (TooFar)
                        {
                            EDSDK.EdsSendCommand(canon, EDSDK.CameraCommand_DriveLensEvf, near);
                        }
                        else
                        {
                            EDSDK.EdsSendCommand(canon, EDSDK.CameraCommand_DriveLensEvf, far);
                        }
                        Logger.Trace($"Moving Focus... iteration {iteration}");
                    }

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

                    if (iteration == 1)
                    {
                        if (FocusPoints[1].HFR > FocusPoints[0].HFR)
                        {
                            TooClose = true;
                        }
                        else
                        {
                            TooFar = true;
                        }
                    }

                    // Determine if focused
                    if (iteration > 1)
                    {
                        if (TooFar && detection.HFR > FocusPoints[FocusPoints.Count - 2].HFR)
                        {
                            EDSDK.EdsSendCommand(canon, EDSDK.CameraCommand_DriveLensEvf, far);
                            Focused = true;
                        }
                        else if (TooClose && detection.HFR > FocusPoints[FocusPoints.Count - 2].HFR)
                        {
                            EDSDK.EdsSendCommand(canon, EDSDK.CameraCommand_DriveLensEvf, near);
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
