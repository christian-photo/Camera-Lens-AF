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
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel.Equipment.Camera;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace LensAF.Items
{
    [ExportMetadata("Name", "Camera Lens AF")]
    [ExportMetadata("Description", "This item will autofocus a lens that supports AF attached to a Canon Camera")]
    [ExportMetadata("Icon", "Plugin_SVG")]
    [ExportMetadata("Category", "Lens AF")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AFInstruction : SequenceItem, IValidatable
    {
        private readonly ICameraMediator cam;
        private readonly IImagingMediator med;
        private readonly IProfileService profile;

        [ImportingConstructor]
        public AFInstruction(ICameraMediator camera, IImagingMediator imagingMediator, IProfileService profileService)
        {
            cam = camera;
            med = imagingMediator;
            profile = profileService;
        }

        public AFInstruction(AFInstruction copyMe) : this(copyMe.cam, copyMe.med, copyMe.profile)
        {
            CopyMetaData(copyMe);
        }

        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            if (!Validate())
            {
                Logger.Error("Could not run AF");
                Notification.ShowWarning("Skipping AF");
                return;
            }

            AutoFocusSettings settings = new AutoFocusSettings();
            settings.ExposureTime = Settings.Default.ExposureTime;
            settings.StretchFactor = profile.ActiveProfile.ImageSettings.AutoStretchFactor;
            settings.BlackClipping = profile.ActiveProfile.ImageSettings.BlackClipping;

            Logger.Info("Starting Auto focus");
            AutoFocusResult result = await new AutoFocus(token, progress, profile).RunAF(cam, med, settings);

            if (result.Successfull)
            {
                Logger.Info("Auto focus Successfull");
                Notification.ShowSuccess("Auto focus successful!");
            }

            return;
        }

        public override object Clone()
        {
            return new AFInstruction(this);
        }

        public override string ToString()
        {
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

            if (LensAFVM.Instance.AutoFocusIsRunning)
            {
                Issues.Add("Autofocus already running");
            }

            CameraVM cameraVM = (CameraVM)Utility.GetInstanceField((CameraMediator)cam, "handler");

            if (cameraVM.DeviceChooserVM.SelectedDevice.Category != "Canon")
            {
                Issues.Add("No canon camera connected");
            }

            return !(Issues.Count > 0);
        }
    }
}