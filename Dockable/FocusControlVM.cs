#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Dasync.Collections;
using EDSDKLib;
using LensAF.Properties;
using LensAF.Util;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel;
using NINA.WPF.Base.ViewModel.Equipment.Camera;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LensAF.Dockable
{
    [Export(typeof(IDockableVM))]
    public class FocusControlVM : DockableVM
    {
        private List<string> Issues;
        private ICameraMediator Camera;
        private bool _manualFocusControl = false;
        private CancellationTokenSource FocusControlToken;

        public static FocusControlVM Instance;

        public AsyncCommand<bool> StartFocusControl { get; set; }
        public RelayCommand StopFocusControl { get; set; }
        public RelayCommand MoveLeft { get; set; }
        public RelayCommand MoveLeftBig { get; set; }
        public RelayCommand MoveRight { get; set; }
        public RelayCommand MoveRightBig { get; set; }

        private BitmapSource image;
        public BitmapSource Image
        {
            get { return image; }
            set
            {
                image = value;
                RaisePropertyChanged();
            }
        }

        public bool ManualFocusControl
        {
            get { return _manualFocusControl; }
            set
            {
                _manualFocusControl = value;
                RaisePropertyChanged();
            }
        }

        [ImportingConstructor]
        public FocusControlVM(IProfileService profileService, ICameraMediator cam, IImagingMediator imaging) : base(profileService)
        {
            Camera = cam;
            Instance = this;

            Title = "Manual Focus Control";
            ResourceDictionary dict = new ResourceDictionary
            {
                Source = new Uri("/LensAF;component/Options.xaml", UriKind.RelativeOrAbsolute)
            };
            ImageGeometry = (GeometryGroup)dict["PluginSVG"];
            ImageGeometry.Freeze();

            Issues = new List<string>();

            StartFocusControl = new AsyncCommand<bool>(async _ =>
            {
                if (!Validate())
                {
                    foreach (string issue in Issues)
                    {
                        Notification.ShowError($"Can't start Focus Control: {issue}");
                        Logger.Error($"Can't start Focus Control: {issue}");
                    }
                    return false;
                }
                FocusControlToken = new CancellationTokenSource();
                IAsyncEnumerable<IExposureData> LiveView = Camera.LiveView(FocusControlToken.Token);
                ManualFocusControl = true;


                await LiveView.ForEachAsync(async exposure => 
                {
                    IImageData data = await exposure.ToImageData();
                    if (Settings.Default.PrepareImage)
                    {
                        IRenderedImage img = data.RenderImage();
                        Image = (await img.Stretch(Settings.Default.Stretchfactor, Settings.Default.Blackclipping, true)).Image;
                    }
                    else
                    {
                        Image = data.RenderBitmapSource();
                    }
                    
                });
                return true;
            });

            StopFocusControl = new RelayCommand(_ =>
            {
                if (ManualFocusControl)
                {
                    FocusControlToken.Cancel();
                    ManualFocusControl = false;
                }
            });

            MoveRight = new RelayCommand(_ =>
            {
                EDSDK.EdsSendCommand(Utility.GetCamera(Camera), EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
            });

            MoveRightBig = new RelayCommand(_ => 
            {
                EDSDK.EdsSendCommand(Utility.GetCamera(Camera), EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far2);
            });

            MoveLeft = new RelayCommand(_ =>
            {
                EDSDK.EdsSendCommand(Utility.GetCamera(Camera), EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
            });

            MoveLeftBig = new RelayCommand(_ =>
            {
                EDSDK.EdsSendCommand(Utility.GetCamera(Camera), EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near2);
            });
        }

        private bool Validate()
        {
            Issues.Clear();
            bool cameraConnected = Camera.GetInfo().Connected;

            if (!cameraConnected)
            {
                Issues.Add("Camera not connected");
            }

            if (LensAFVM.Instance.AutoFocusIsRunning)
            {
                Issues.Add("Can't enable focus control when AF is running");
            }

            CameraVM cameraVM = (CameraVM)Utility.GetInstanceField((CameraMediator)Camera, "handler");

            if (cameraVM.DeviceChooserVM.SelectedDevice.Category != "Canon")
            {
                Issues.Add("No canon camera connected");
            }

            return !(Issues.Count > 0);
        }
    }
}
