#region "copyright"

/*
    Copyright © 2025 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"


using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Newtonsoft.Json;
using NINA.Core.Utility;

namespace LensAF.Util
{
    public class FocalLengthConfig : BaseINPC
    {
        private double focalLength = 0;
        public double FocalLength
        {
            get => focalLength;
            set
            {
                focalLength = value;
                RaisePropertyChanged();
            }
        }

        private int focusPosition = 0;
        public int FocusPosition
        {
            get => focusPosition;
            set
            {
                focusPosition = value;
                RaisePropertyChanged();
            }
        }

        public FocalLengthConfig(double focalLength, int focusPosition)
        {
            this.FocalLength = focalLength;
            this.FocusPosition = focusPosition;
        }

        public FocalLengthConfig() { }
    }

    public class LensConfig : BaseINPC
    {
        private string lensName = string.Empty;
        private List<FocalLengthConfig> focusPosition = [];

        public string LensName
        {
            get => lensName;
            set
            {
                if (lensName != value)
                {
                    lensName = value;
                    RaisePropertyChanged();
                }
            }
        }


        public List<FocalLengthConfig> FocusPosition
        {
            get => focusPosition;
            set
            {
                focusPosition = value;
                RaisePropertyChanged();
            }
        }

        [JsonConstructor]
        public LensConfig(string lensName, List<FocalLengthConfig> focusPosition)
        {
            LensName = lensName;
            FocusPosition = focusPosition;
        }
    }
}