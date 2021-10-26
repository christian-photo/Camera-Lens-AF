#region "copyright"

/*
    Copyright © 2021 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using EDSDKLib;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Core.Enum;
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
using System.IO;
using Dasync.Collections;
using LensAF.Util;


namespace LensAF.Items
{
    [ExportMetadata("Name", "Camera Lense AF")]
    [ExportMetadata("Description", "This item will autofocus a lense that supports AF attached to a Canon Camera")]
    [ExportMetadata("Icon", "Plugin_SVG")]
    [ExportMetadata("Category", "Lense AF")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class LensAFInstruction : SequenceItem, IValidatable {

        private readonly ICameraMediator cam;
        private readonly IImagingMediator med;
        private readonly Utility utility;
        private List<IntPtr> ptrs;
        private readonly List<string> _cams;
        private readonly Dictionary<string, IntPtr> camsTable;
        public RelayCommand Reload { get; set; }

        [ImportingConstructor]
        public LensAFInstruction(ICameraMediator camera, IImagingMediator imagingMediator)
        {
            cam = camera;
            med = imagingMediator;
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
        public LensAFInstruction(LensAFInstruction copyMe) : this(copyMe.cam, copyMe.med) {
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

        private bool TooFar = false;
        private bool TooClose = false;
        public List<FocusPoint> FocusPoints { get; set; } = new List<FocusPoint>();

        private double _stretchFactor = 0.15;

        [JsonProperty]
        public double StretchFactor
        {
            get {  return _stretchFactor; }
            set
            {
                _stretchFactor = value;
                RaisePropertyChanged();
            }
        }
        private bool focused = false;

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

            try
            {
                // Needed Variables
                focused = false;
                const int near = (int)EDSDK.EvfDriveLens_Near1;
                const int far = (int)EDSDK.EvfDriveLens_Far1;
                int iteration = 0;
                CancellationTokenSource cts = new CancellationTokenSource();

                Logger.Info("Starting Autofocus");
                IAsyncEnumerable<IExposureData> liveViewEnumerable = cam.LiveView(cts.Token);

                // LiveView Loop
                await liveViewEnumerable.ForEachAsync(async exposureData =>
                {

                    // Break out of loop it it seems like it is stuck
                    if (iteration > 40)
                    {
                        cts.Cancel();
                    }

                    // Break out of loop if focused
                    if (focused)
                    {
                        cts.Cancel();
                    }

                    // Drive Focus
                    if (iteration == 0)
                    {
                        EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, far);
                    }
                    else if (iteration == 1)
                    {
                        EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, near);
                    }
                    else
                    {
                        if (TooFar)
                        {
                            EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, near);
                        }
                        else
                        {
                            EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, far);
                        }
                        Logger.Info($"Moving Focus... iteration {iteration}");
                    }

                    // Download and Prepare Image
                    IImageData imageData = await exposureData.ToImageData(progress, cts.Token);
                    IRenderedImage image = await med.PrepareImage(imageData, new PrepareImageParameters(), cts.Token);
                    image = await image.Stretch(StretchFactor, -2.8, true);
                    image = await image.DetectStars(false, StarSensitivityEnum.Normal, NoiseReductionEnum.None);
                    IStarDetectionAnalysis detection = image.RawImageData.StarDetectionAnalysis;
                    FocusPoints.Add(new FocusPoint(detection));

                    if (iteration == 1)
                    {
                        if (FocusPoints[1].HFR > FocusPoints[0].HFR)
                        {
                            TooClose = true;
                        }
                        else
                        {
                            TooFar = true;
                        }
                    }

                    // Determine if focused
                    if (iteration > 1)
                    {
                        if (TooFar && detection.HFR > FocusPoints[FocusPoints.Count - 2].HFR)
                        {
                            EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, far);
                            focused = true;
                        }
                        else if (TooClose && detection.HFR > FocusPoints[FocusPoints.Count - 2].HFR)
                        {
                            EDSDK.EdsSendCommand(ptr, EDSDK.CameraCommand_DriveLensEvf, near);
                            focused = true;
                        }
                    }


                    // Increment iteration
                    iteration++;
                });
            }
            catch (TaskCanceledException)
            {
                Logger.Info("AF Successfull");
                Notification.ShowSuccess("AF successful!");
            }
            catch (Exception e)
            {
                Logger.Error(e);
                Notification.ShowError("AF failed");
                return;
            }

            Directory.CreateDirectory(Path.Combine(CoreUtil.APPLICATIONTEMPPATH, "AutoFocus", "Camera Lens AF"));

            File.WriteAllText(Path.Combine(CoreUtil.APPLICATIONTEMPPATH, "AutoFocus", "Camera Lens AF", Guid.NewGuid().ToString() + ".json"), JsonConvert.SerializeObject(FocusPoints, Formatting.Indented));

            return;
        }

        public override object Clone() {
            return new LensAFInstruction(this);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(LensAFInstruction)}";
        }

        public bool Validate()
        {
            Issues.Clear();
            bool cameraConnected = cam.GetInfo().Connected;

            if (!cameraConnected)
            {
                Issues.Add("Camera not connected");
            }

            if (Cams[Index].Equals("No Camera Connected"))
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
        }
    }

    public class FocusPoint
    {
        public int Stars { get; set; }
        public double HFR { get; set; }
        public FocusPoint(int stars, double HFR)
        {
            Stars = stars;
            this.HFR = HFR;
        }
        public FocusPoint(IStarDetectionAnalysis analysis)
        {
            Stars = analysis.DetectedStars;
            HFR = analysis.HFR;
        }
    }
}