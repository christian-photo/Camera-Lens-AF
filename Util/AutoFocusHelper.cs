#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using LensAF.Properties;
using NINA.Image.ImageAnalysis;
using System;
using System.Collections.Generic;

namespace LensAF.Util
{
    public class AutoFocusSettings
    {
        public double ExposureTime = 5;
        public double BlackClipping = -2.8;
        public double StretchFactor = 0.15;
        public int Iterations = 10;
    }

    public class AutoFocusResult
    {
        public bool Successfull;
        public List<FocusPoint> FocusPoints;
        public TimeSpan Duration;
        public DateTime Time;
        public int StepSize = Settings.Default.SelectedStepSize + 1;

        public AutoFocusResult(bool successfull, List<FocusPoint> focusPoints, TimeSpan duration, DateTime time)
        {
            Successfull = successfull;
            FocusPoints = focusPoints;
            Duration = duration;
            Time = time;
        }
    }

    public class FocusPoint
    {
        public int Stars { get; set; }
        public double HFR { get; set; }

        public FocusPoint(StarDetectionResult analysis)
        {
            Stars = analysis.DetectedStars;
            HFR = analysis.AverageHFR;
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
}
