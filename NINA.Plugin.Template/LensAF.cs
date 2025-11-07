#region "copyright"

/*
    Copyright © 2025 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using LensAF.Properties;
using LensAF.Util;
using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using NINA.Profile.Interfaces;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace LensAF
{
    [Export(typeof(IPluginManifest))]
    public class LensAF : PluginBase, INotifyPropertyChanged
    {
        private string lensesConfigPath { get => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lenses.json"); }

        private static LensAF instance;

        [ImportingConstructor]
        public LensAF(ICameraMediator camera, IProfileService profileService)
        {
            instance = this;
            if (Settings.Default.UpdateSettings)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpdateSettings = false;
                CoreUtil.SaveSettings(Settings.Default);
            }
            Camera = camera;
            ProfileService = profileService;

            if (File.Exists(lensesConfigPath))
            {
                var lenses = JsonConvert.DeserializeObject<ObservableCollection<LensConfig>>(File.ReadAllText(lensesConfigPath));
                KnownLenses = lenses;
            }
            else
            {
                KnownLenses = new ObservableCollection<LensConfig>();
            }
        }

        public override Task Teardown()
        {
            File.WriteAllText(lensesConfigPath, JsonConvert.SerializeObject(KnownLenses));
            return base.Teardown();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static ICameraMediator Camera;
        public static IProfileService ProfileService;

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

        public int FocusStopPosition
        {
            get
            {
                return Settings.Default.FocusStopPosition;
            }
            set
            {
                Settings.Default.FocusStopPosition = value;
                CoreUtil.SaveSettings(Settings.Default);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FocusStopPosition)));
            }
        }

        public int CalibrationLargeSteps
        {
            get => Settings.Default.CalibrationLargeSteps;
            set
            {
                Settings.Default.CalibrationLargeSteps = value;
                CoreUtil.SaveSettings(Settings.Default);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CalibrationLargeSteps)));
            }
        }

        private ObservableCollection<LensConfig> knownLenses;
        public ObservableCollection<LensConfig> KnownLenses
        {
            get => knownLenses;
            set
            {
                knownLenses = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KnownLenses)));
                if (knownLenses.Count > 0)
                {
                    SelectedLensName = knownLenses[^1].LensName;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KnownLensesNames)));

            }
        }

        private string lensName;
        public string SelectedLensName
        {
            get => lensName;
            set
            {
                lensName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLensName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLensConfig)));
            }
        }

        public List<string> KnownLensesNames => KnownLenses.Select(x => x.LensName).ToList();

        public LensConfig CurrentLensConfig => KnownLenses.Where(x => x.LensName == SelectedLensName).FirstOrDefault();

        public static void AddLensConfigIfNecessary(string name)
        {
            var matches = instance.KnownLenses.Where(x => x.LensName == name);
            if (!matches.Any())
            {
                instance.KnownLenses.Add(new LensConfig(name, [new FocalLengthConfig(ProfileService.ActiveProfile.TelescopeSettings.FocalLength, 0)]));
                instance.PropertyChanged?.Invoke(instance, new PropertyChangedEventArgs(nameof(instance.KnownLensesNames)));
            }
            else
            {
                // If the lens is already known, but the focal length is not yet in the list, add it.
                if (!matches.First().FocusPosition.Where(x => x.FocalLength == ProfileService.ActiveProfile.TelescopeSettings.FocalLength).Any())
                {
                    matches.First().FocusPosition.Add(new FocalLengthConfig(ProfileService.ActiveProfile.TelescopeSettings.FocalLength, 0));
                }
            }
            var lensConfig = instance.KnownLenses.First(x => x.LensName == name);
            lensConfig.FocusPosition = lensConfig.FocusPosition.OrderBy(x => x.FocalLength).ToList();
            instance.SelectedLensName = name;
        }

        public static int GetFocusPosition(string name)
        {
            double focalLength = ProfileService.ActiveProfile.TelescopeSettings.FocalLength;
            var lens = instance.KnownLenses.Where(x => x.LensName == name).FirstOrDefault();
            if (lens is not null)
            {
                var config = lens.FocusPosition.Where(x => x.FocalLength == focalLength).FirstOrDefault();
                if (config is not null)
                {
                    return config.FocusPosition;
                }
            }
            return 0;
        }
    }
}
