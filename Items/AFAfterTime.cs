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
using NINA.Sequencer.Container;
using NINA.Sequencer.Interfaces;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using NINA.WPF.Base.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace LensAF.Items
{
    [ExportMetadata("Name", "Camera Lens AF After Time")]
    [ExportMetadata("Description", "This item will autofocus a lens that supports AF attached to a Canon Camera after a specified time")]
    [ExportMetadata("Icon", "Plugin_SVG")]
    [ExportMetadata("Category", "Lens AF")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AFAfterTime : SequenceTrigger, IValidatable
    {
        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        private IImageHistoryVM history;
        private ICameraMediator cam;
        private IImagingMediator imaging;
        private IProfileService profile;
        private List<IntPtr> ptrs;
        private Dictionary<string, IntPtr> camsTable;

        public RelayCommand Reload { get; set; }

        [ImportingConstructor]
        public AFAfterTime(IImageHistoryVM history, ICameraMediator camera, IImagingMediator imaging, IProfileService profile)
        {
            this.history = history;
            cam = camera;
            this.imaging = imaging;
            this.profile = profile;
            ptrs = Utility.GetConnectedCams();
            _cams = new List<string>();
            camsTable = new Dictionary<string, IntPtr>();

            Reload = new RelayCommand(o =>
            {
                Rescan();
            });

            Rescan();
        }

        private List<string> _cams;
        public List<string> Cams
        {
            get { return _cams; }
            set
            {
                _cams = value;
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

        private int afterTime = 60;

        [JsonProperty]
        public int AfterTime
        {
            get => afterTime;
            set
            {
                afterTime = value;
                RaisePropertyChanged();
            }
        }

        public override object Clone()
        {
            return new AFAfterTime(history, cam, imaging, profile)
            {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description
            };
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token)
        {
            if (!Validate())
            {
                Logger.Error("Could not run AF");
                Notification.ShowWarning("Skipping AF");
                return;
            }

            // Get Selected Cam
            IntPtr ptr = camsTable[Cams[Index]];

            AutoFocusSettings settings = new AutoFocusSettings();
            settings.ExposureTime = Settings.Default.ExposureTime;

            Logger.Info("Starting Auto focus");
            AutoFocusResult result = await new AutoFocus(token, progress, profile).RunAF(ptr, cam, imaging, settings);

            if (result.Successfull)
            {
                Logger.Info("AF Successfull");
                Notification.ShowSuccess("AF successful!");
            }

            return;
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem)
        {
            if (nextItem == null) { return false; }
            if (!(nextItem is IExposureItem)) { return false; }

            bool shouldTrigger = false;
            if ((DateTime.Now - AutoFocus.LastAF) > new TimeSpan(0, AfterTime, 0))
            {
                shouldTrigger = true;
            }
            return shouldTrigger;
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(AFAfterTime)}";
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

            if (LensAFVM.Instance.AutoFocusIsRunning)
            {
                Issues.Add("Autofocus already running");
            }

            return !(Issues.Count > 0);
        }

        private void Rescan()
        {
            ptrs = Utility.GetConnectedCams();

            Dictionary<string, IntPtr> dict = new Dictionary<string, IntPtr>();
            List<string> list = new List<string>();

            if (ptrs.Count == 0)
            {
                list.Add("No Camera Connected");
            }
            else
            {
                foreach (IntPtr ptr in ptrs)
                {
                    list.Add(Utility.GetCamName(ptr));
                    dict.Add(Utility.GetCamName(ptr), ptr);
                }
            }
            Cams = list;
            camsTable = dict;
            Index = 0;
        }
    }
}