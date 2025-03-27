#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using CommunityToolkit.Mvvm.Input;
using Dasync.Collections;
using EDSDKLib;
using LensAF.Properties;
using LensAF.Util;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace LensAF.Dockable
{
    [Export(typeof(IDockableVM))]
    public class FocusControlVM : DockableVM
    {
        private ICameraMediator Camera;
        private bool _manualFocusControl = false;
        private CancellationTokenSource FocusControlToken;

        public static FocusControlVM Instance;

        public AsyncRelayCommand StartFocusControl { get; set; }
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
        public FocusControlVM(IProfileService profileService, ICameraMediator cam, IImagingMediator imaging, IFocuserMediator focuser) : base(profileService)
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


            StartFocusControl = new AsyncRelayCommand(async _ =>
            {
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
            });

            StopFocusControl = new RelayCommand(() =>
            {
                if (ManualFocusControl)
                {
                    FocusControlToken?.Cancel();
                    ManualFocusControl = false;
                }
            });

            MoveRight = new RelayCommand(async () =>
            {
                await focuser.MoveFocuserRelative(2, FocusControlToken.Token);
            });

            MoveRightBig = new RelayCommand(async () =>
            {
                await focuser.MoveFocuserRelative(10, FocusControlToken.Token);
            });

            MoveLeft = new RelayCommand(async () =>
            {
                await focuser.MoveFocuserRelative(-2, FocusControlToken.Token);
            });

            MoveLeftBig = new RelayCommand(async () =>
            {
                await focuser.MoveFocuserRelative(-10, FocusControlToken.Token);
            });
        }
    }
}
