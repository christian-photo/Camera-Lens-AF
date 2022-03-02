#region "copyright"

/*
    Copyright © 2021 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Dasync.Collections;
using EDSDKLib;
using LensAF.Util;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Image.Interfaces;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel;
using NINA.WPF.Base.ViewModel.Equipment.Camera;
using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace LensAF.Dockable
{
    [Export(typeof(IDockableVM))]
    public class LensAFVM : DockableVM
    {
        private readonly ICameraMediator Camera;
        private readonly IImagingMediator Imaging;
        private CancellationTokenSource ActiveToken;
        private List<string> Issues;
        private ApplicationStatus _status;
        private IApplicationStatusMediator statusMediator;


        public static LensAFVM Instance;
        public AsyncCommand<bool> RunAF { get; set; }
        public RelayCommand AbortAF { get; set; }

        private List<DataPoint> _plotFocusPoints;
        public List<DataPoint> PlotFocusPoints
        {
            get { return _plotFocusPoints; }
            set
            {
                _plotFocusPoints = value;
                RaisePropertyChanged();
            }
        }

        private List<ScatterErrorPoint> _plotDots;
        public List<ScatterErrorPoint> PlotDots
        {
            get { return _plotDots; }
            set
            {
                _plotDots = value;
                RaisePropertyChanged();
            }
        }

        private string _lastAF = "";
        public string LastAF
        {
            get { return _lastAF; }
            set
            {
                _lastAF = value;
                RaisePropertyChanged();
            }
        }

        private bool _autoFocusIsRunning = false;
        public bool AutoFocusIsRunning
        {
            get { return _autoFocusIsRunning; }
            set
            {
                _autoFocusIsRunning = value;
                RaisePropertyChanged();
            }
        }

        public ApplicationStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                _status.Source = "Lens AF";
                RaisePropertyChanged(nameof(Status));

                statusMediator.StatusUpdate(_status);
            }
        }

        [ImportingConstructor]
        public LensAFVM(IProfileService profileService, ICameraMediator camera, IImagingMediator imagingMediator, IApplicationStatusMediator statusMediator) : base(profileService)
        {
            Title = "Lens AF";
            ResourceDictionary dict = new ResourceDictionary();
            dict.Source = new Uri("/LensAF;component/Options.xaml", UriKind.RelativeOrAbsolute);
            ImageGeometry = (GeometryGroup)dict["PluginSVG"];
            ImageGeometry.Freeze();

            Camera = camera;
            Imaging = imagingMediator;
            this.statusMediator = statusMediator;
            Issues = new List<string>();
            Instance = this;
            PlotFocusPoints = new List<DataPoint>();
            PlotDots = new List<ScatterErrorPoint>();

            RunAF = new AsyncCommand<bool>(async () =>
            {
                if (!Validate())
                {
                    foreach (string issue in Issues)
                    {
                        Notification.ShowError($"Can't start AutoFocus: {issue}");
                        Logger.Error($"Can't start AutoFocus: {issue}");
                    }
                }
                else
                {
                    ClearCharts();
                    ActiveToken = new CancellationTokenSource();
                    AutoFocusIsRunning = true;
                    AutoFocusResult result = await new AutoFocus(ActiveToken.Token, new Progress<ApplicationStatus>(p => Status = p), profileService).RunAF(Camera, Imaging, new AutoFocusSettings());
                    AutoFocusIsRunning = false;
                    return result.Successfull;
                }

                return false;
            });

            AbortAF = new RelayCommand(_ =>
            {
                if ((ActiveToken != null) && AutoFocusIsRunning)
                {
                    Logger.Info("Cancelling AF...");
                    Notification.ShowInformation("Cancelling AF... This may take a few seconds");
                    ActiveToken.Cancel();
                    AutoFocusIsRunning = false;
                }
                else if ((AutoFocus.PublicToken != null) && AutoFocusIsRunning)
                {
                    Logger.Info("Cancelling AF...");
                    Notification.ShowInformation("Cancelling AF... This may take a few seconds");
                    AutoFocus.PublicToken.Cancel();
                    AutoFocusIsRunning = false;
                }
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

            if (AutoFocusIsRunning)
            {
                Issues.Add("Autofocus already running");
            }

            CameraVM cameraVM = (CameraVM)Utility.GetInstanceField((CameraMediator)Camera, "handler");

            if (cameraVM.CameraChooserVM.SelectedDevice.Category != "Canon")
            {
                Issues.Add("No canon camera connected");
            }

            return !(Issues.Count > 0);
        }

        private void ClearCharts()
        {
            PlotFocusPoints = new List<DataPoint>();
            PlotDots = new List<ScatterErrorPoint>();
        }

        public void AddToPlot(DataPoint point)
        {
            List<DataPoint> points = PlotFocusPoints;
            points.Add(point);
            PlotFocusPoints = points;
            AddToDotPlot(point);
        }
        private void AddToDotPlot(DataPoint point)
        {
            List<ScatterErrorPoint> points = PlotDots;
            PlotDots.Add(new ScatterErrorPoint(point.X, point.Y, 0, 0.1));
            PlotDots = points;
        }

    }
}
