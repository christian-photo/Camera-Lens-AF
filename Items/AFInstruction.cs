#region "copyright"

/*
    Copyright © 2021 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Image.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using LensAF.Util;
using EDSDKLib;
using Dasync.Collections;
using LensAF.Properties;
using NINA.Profile.Interfaces;
using OxyPlot.Wpf;
using OxyPlot.Series;

namespace LensAF.Items
{
    [ExportMetadata("Name", "Camera Lens AF")]
    [ExportMetadata("Description", "This item will autofocus a lens that supports AF attached to a Canon Camera")]
    [ExportMetadata("Icon", "Plugin_SVG")]
    [ExportMetadata("Category", "Lens AF")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AFInstruction : SequenceItem, IValidatable {

        private readonly ICameraMediator cam;
        private readonly IImagingMediator med;
        private readonly IProfileService profile;
        private readonly Utility utility;
        private List<IntPtr> ptrs;
        private readonly List<string> _cams;
        private readonly Dictionary<string, IntPtr> camsTable;
        public RelayCommand Reload { get; set; }

        [ImportingConstructor]
        public AFInstruction(ICameraMediator camera, IImagingMediator imagingMediator, IProfileService profileService)
        {
            cam = camera;
            med = imagingMediator;
            profile = profileService;
            utility = new Utility();
            ptrs = utility.GetConnectedCams();
            _cams = new List<string>();
            camsTable = new Dictionary<string, IntPtr>();

            Reload = new RelayCommand(o =>
            {
                Rescan();
            });

            Rescan();
        }
        public AFInstruction(AFInstruction copyMe) : this(copyMe.cam, copyMe.med, copyMe.profile) {
            CopyMetaData(copyMe);
        }

        public List<string> Cams
        {
            get { return _cams; }
            set
            {
                Cams = value;
                RaisePropertyChanged();
            }
        }

        private int _index;

        public int Index
        {
            get { return _index; }
            set 
            { 
                _index = value;
                RaisePropertyChanged();
            }
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            if (!Validate())
            {
                Logger.Error("Could not run AF. Camera not connected!");
                Notification.ShowWarning("Camera not connected. Skipping AF");
                return;
            }

            // Get Selected Cam
            IntPtr ptr = camsTable[Cams[Index]];

            AutoFocusSettings settings = new AutoFocusSettings();
            settings.ExposureTime = Settings.Default.ExposureTime;
            settings.StretchFactor = profile.ActiveProfile.ImageSettings.AutoStretchFactor;
            settings.BlackClipping = profile.ActiveProfile.ImageSettings.BlackClipping;

            Logger.Info("Starting Auto focus");
            AutoFocusResult result = await new AutoFocus(token, progress, profile).RunAF(ptr, cam, med, settings);
            
            if (result.Successfull)
            {
                Logger.Info("Auto focus Successfull");
                Notification.ShowSuccess("Auto focus successful!");
            }

            return;
        }

        public override object Clone() {
            return new AFInstruction(this);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(AFInstruction)}";
        }

        public bool Validate()
        {
            Issues.Clear();
            bool cameraConnected = cam.GetInfo().Connected;

            if (!cameraConnected)
            {
                Issues.Add("Camera not connected");
            }

            if (Cams[Index].Equals("No Camera Connected") && Issues.Count == 0)
            {
                Issues.Add("Non valid Camera selected");
            }

            return cameraConnected;
        }

        // Rescan for new Cameras
        private void Rescan()
        {
            ptrs = utility.GetConnectedCams();

            Cams.Clear();
            camsTable.Clear();

            if (ptrs.Count == 0)
            {
                Cams.Add("No Camera Connected");
            }
            else
            {
                foreach (IntPtr ptr in ptrs)
                {
                    Cams.Add(utility.GetCamName(ptr));
                    camsTable.Add(utility.GetCamName(ptr), ptr);
                }
            }
            RaisePropertyChanged(nameof(Cams));
            Index = 0;
        }
    }
}