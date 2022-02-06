#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using LensAF.Properties;
using NINA.Core.Utility;
using NINA.Image.ImageAnalysis;
using System;
using System.Collections.Generic;

namespace LensAF.Util
{
    public class AutoFocusSettings
    {
        public double ExposureTime = Settings.Default.ExposureTime;
        public double BlackClipping = -2.8;
        public double StretchFactor = 0.15;
        public int Iterations = 9;
        public AutoFocusLogic AutoFocusMethod = AutoFocusLogic.STARHFR;
    }

    public class AutoFocusResult
    {
        public bool Successfull;
        public List<FocusPoint> FocusPoints;
        public TimeSpan Duration;
        public DateTime Time;
        public string StepSize;

        public AutoFocusResult(bool successfull, List<FocusPoint> focusPoints, TimeSpan duration, DateTime time, string stepsize)
        {
            Successfull = successfull;
            FocusPoints = focusPoints;
            Duration = duration;
            Time = time;
            StepSize = stepsize;
        }
    }

    public class FocusPoint
    {
        public int Stars { get; set; } = 0;
        public double HFR { get; set; } = double.NaN;
        public double Contrast { get; set; } = double.NaN;

        public FocusPoint(StarDetectionResult analysis)
        {
            Stars = analysis.DetectedStars;
            HFR = analysis.AverageHFR;
        }

        public FocusPoint(ContrastDetectionResult detection)
        {
            Contrast = detection.AverageContrast;
        }
    }

    public class AutoFocusReport
    {
        public CameraInfo CanonInfo;
        public AutoFocusSettings Settings;
        public AutoFocusResult Result;
    }

    public enum FocusDirection
    {
        Far,
        Near
    }

    public enum AutoFocusLogic
    {
        STARHFR,
        CONTRAST
    }
}
