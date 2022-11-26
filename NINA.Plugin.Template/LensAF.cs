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
using LensAF.Util;
using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Plugin;
using NINA.Plugin.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Numerics;

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
            ResetAF = new RelayCommand(_ =>
            {
                LensAFVM.Instance.AutoFocusIsRunning = false;
                Notification.ShowInformation($"Auto focus is running: {LensAFVM.Instance.AutoFocusIsRunning}");
            });
            if (string.IsNullOrWhiteSpace(Settings.Default.AutoFocusProfiles))
            {
                Profiles = new List<AutoFocusProfile>();
                Profiles.Add(new AutoFocusProfile(0, 2));
                Profiles.Add(new AutoFocusProfile(1, 0));
                SaveProfile();
            }
            else
            {
                Profiles = JsonConvert.DeserializeObject<List<AutoFocusProfile>>(Settings.Default.AutoFocusProfiles);
            }
            Camera = camera;
        }

        private Dictionary<int, Vector2> StepSizes = new Dictionary<int, Vector2>()
        {
            { 0, new Vector2(0, 1) },
            { 1, new Vector2(0, 3) },
            { 2, new Vector2(0, 5) },
            { 3, new Vector2(1, -5) },
            { 4, new Vector2(1, -3) },
            { 5, new Vector2(1, 0) },
            { 6, new Vector2(1, 3) },
            { 7, new Vector2(1, 5) }
        };

        public event PropertyChangedEventHandler PropertyChanged;

        public static List<AutoFocusProfile> Profiles;
        public static ICameraMediator Camera;

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
                SetStepSize();
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepSizeBig)));
                SaveProfile();
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepSizeSmall)));
                SaveProfile();
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseMixedStepSize)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StepSizeLogic)));
            }
        }

        public int AutoFocusLogic
        {
            get 
            { 
                return Settings.Default.AutoFocusLogic;
            }
            set
            {
                Settings.Default.AutoFocusLogic = value;
                CoreUtil.SaveSettings(Settings.Default);
            }
        }

        public bool ButtonEnabled
        {
            get 
            { 
                return Settings.Default.ButtonEnabled; 
            }
            set
            {
                Settings.Default.ButtonEnabled = value;
                CoreUtil.SaveSettings(Settings.Default);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ButtonEnabled)));
            }
        }

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

        public int Iterations
        {
            get => Settings.Default.Iterations;
            set
            {
                Settings.Default.Iterations = value;
                CoreUtil.SaveSettings(Settings.Default);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Iterations)));
            }
        }

        public int InitialOffset
        {
            get => Settings.Default.InitialOffset;
            set
            {
                Settings.Default.InitialOffset = value;
                CoreUtil.SaveSettings(Settings.Default);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InitialOffset)));
            }
        }
        public RelayCommand ResetAF { get; set; }

        private void SetStepSize()
        {
            Vector2 stepSize;
            if (Settings.Default.SelectedStepSize < 8)
            {
                stepSize = StepSizes[Settings.Default.SelectedStepSize];
                ButtonEnabled = true;
            }
            else if (Settings.Default.SelectedStepSize == 8)
            {
                stepSize = new Vector2(Profiles[0].StepsBig, Profiles[0].StepsSmall);
                UseMixedStepSize = true;
                ButtonEnabled = false;
            }
            else
            {
                stepSize = new Vector2(Profiles[1].StepsBig, Profiles[1].StepsSmall);
                UseMixedStepSize = true;
                ButtonEnabled = false;
            }

            if (stepSize.Y < 0)
            {
                StepSizeLogic = 1;
            }
            else
            {
                StepSizeLogic = 0;
            }
            stepSize.Y = Math.Abs(stepSize.Y);
            StepSizeBig = (int)stepSize.X;
            StepSizeSmall = (int)stepSize.Y;
        }

        private void SaveProfile()
        {
            if (Settings.Default.SelectedStepSize == 8)
            {
                Profiles[0].StepsBig = StepSizeBig;
                Profiles[0].StepsSmall = StepSizeSmall;
            }
            else if (Settings.Default.SelectedStepSize == 9)
            {
                Profiles[1].StepsBig = StepSizeBig;
                Profiles[1].StepsSmall = StepSizeSmall;
            }
            Settings.Default.AutoFocusProfiles = JsonConvert.SerializeObject(Profiles);
            CoreUtil.SaveSettings(Settings.Default);
        }
    }
}
