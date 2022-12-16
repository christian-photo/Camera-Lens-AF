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
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System.ComponentModel;
using System.ComponentModel.Composition;

namespace LensAF
{
    [Export(typeof(IPluginManifest))]
    public class LensAF : PluginBase, INotifyPropertyChanged
    {
        [ImportingConstructor]
        public LensAF(ICameraMediator camera)
        {
            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }
            Camera = camera;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static ICameraMediator Camera;

        public bool PrepareImage
        {
            get
            {
                return Settings.Default.PrepareImage;
            }
            set
            {
                Settings.Default.PrepareImage = value;
                CoreUtil.SaveSettings(Settings.Default);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PrepareImage)));
            }
        }

        public double Stretchfactor
        {
            get
            {
                return Settings.Default.Stretchfactor;
            }
            set
            {
                Settings.Default.Stretchfactor = value;
                CoreUtil.SaveSettings(Settings.Default);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Stretchfactor)));
            }
        }

        public double Blackclipping
        {
            get
            {
                return Settings.Default.Blackclipping;
            }
            set
            {
                Settings.Default.Blackclipping = value;
                CoreUtil.SaveSettings(Settings.Default);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Blackclipping)));
            }
        }
    }
}