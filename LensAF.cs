#region "copyright"

/*
    Copyright © 2021 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using LensAF.Properties;
using NINA.Core;
using NINA.Core.Utility;
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
    }
}
