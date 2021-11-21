﻿#region "copyright"

/*
    Copyright © 2021 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using LensAF.Util;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
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
    [ExportMetadata("Name", "Camera Lens AF After # Exposures")]
    [ExportMetadata("Description", "This item will autofocus a lens that supports AF attached to a Canon Camera after a specified number of Exposures")]
    [ExportMetadata("Icon", "Plugin_SVG")]
    [ExportMetadata("Category", "Lens AF")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class AFAfterExposures : SequenceTrigger, IValidatable
    {
        public IList<string> Issues { get; set; } = new ObservableCollection<string>();

        private IImageHistoryVM history;
        private ICameraMediator cam;
        private IImagingMediator imaging;
        private Utility utility;
        private List<IntPtr> ptrs;
        private readonly List<string> _cams;
        private readonly Dictionary<string, IntPtr> camsTable;

        public RelayCommand Reload { get; set; }

        [ImportingConstructor]
        public AFAfterExposures(IImageHistoryVM history, ICameraMediator camera, IImagingMediator imaging)
        {
            this.history = history;
            cam = camera;
            this.imaging = imaging;
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

        public int afterExposures = 10;
        [JsonProperty]
        public int AfterExposures
        {
            get => afterExposures;
            set
            {
                afterExposures = value;
                RaisePropertyChanged();
            }
        }

        public override object Clone()
        {
            return new AFAfterExposures(history, cam, imaging)
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
                Logger.Error("Could not run AF. Camera not connected!");
                Notification.ShowWarning("Camera not connected. Skipping AF");
                return;
            }

            // Get Selected Cam
            IntPtr ptr = camsTable[Cams[Index]];

            Logger.Info("Starting Autofocus");
            AutoFocusResult result = await new AutoFocus(token, progress).RunAF(ptr, cam, imaging, new AutoFocusSettings());

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
            if (history.ImageHistory.Count > 0)
            {
                if (history.ImageHistory.Count % AfterExposures == 0)
                {
                    shouldTrigger = true;
                }
            }
            else
            {
                shouldTrigger = false;
            }
            return shouldTrigger;
        }

        public override string ToString()
        {
            return $"Category: {Category}, Item: {nameof(AFAfterExposures)}";
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
            Index = 0;
        }
    }
}