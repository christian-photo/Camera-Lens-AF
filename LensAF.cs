#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using LensAF.Dockable;
using LensAF.Properties;
using NINA.Core;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System.ComponentModel.Composition;

namespace LensAF
{
    [Export(typeof(IPluginManifest))]
    public class LensAF : PluginBase
    {
        [ImportingConstructor]
        public LensAF()
        {
            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }
            ResetAF = new RelayCommand(_ =>
            {
                LensAFVM.Instance.AutoFocusIsRunning = false;
                Notification.ShowInformation($"Auto focus is running: {LensAFVM.Instance.AutoFocusIsRunning}");
            });
        }

        public int SelectedIndex
        {
            get
            {
                return Settings.Default.SelectedStepSize;
            }
            set
            {
                Settings.Default.SelectedStepSize = value;
                CoreUtil.SaveSettings(Settings.Default);
            }
        }

        public double ExposureTime
        {
            get
            {
                return Settings.Default.ExposureTime;
            }
            set
            {
                Settings.Default.ExposureTime = value;
                CoreUtil.SaveSettings(Settings.Default);
            }
        }

        public int StepSizeBig
        {
            get
            {
                return Settings.Default.StepSizeBig;
            }
            set
            {
                Settings.Default.StepSizeBig = value;
                CoreUtil.SaveSettings(Settings.Default);
            }
        }

        public int StepSizeSmall
        {
            get
            {
                return Settings.Default.StepSizeSmall;
            }
            set
            {
                Settings.Default.StepSizeSmall = value;
                CoreUtil.SaveSettings(Settings.Default);
            }
        }

        public bool UseMixedStepSize
        {
            get
            {
                return Settings.Default.UseMixedStepSize;
            }
            set
            {
                Settings.Default.UseMixedStepSize = value;
                CoreUtil.SaveSettings(Settings.Default);
            }
        }

        public int StepSizeLogic
        {
            get
            {
                return Settings.Default.StepSizeLogic;
            }
            set
            {
                Settings.Default.StepSizeLogic = value;
                CoreUtil.SaveSettings(Settings.Default);
            }
        }

        public RelayCommand ResetAF { get; set; }
    }
}
