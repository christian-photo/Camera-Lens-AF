#region "copyright"

/*
    Copyright © 2021 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using LensAF.Util;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.ViewModel;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;

namespace LensAF.Dockable
{
    [Export(typeof(IDockableVM))]
    public class LensAFVM : DockableVM
    {
        private readonly ICameraMediator Camera;
        private readonly IImagingMediator Imaging;
        private List<IntPtr> cameraPtrs;
        private Dictionary<string, IntPtr> camsTable;
        private CancellationTokenSource ActiveToken;
        private List<string> Issues;


        public static LensAFVM Instance;
        public AsyncCommand<bool> RunAF { get; set; }
        public RelayCommand Reload { get; set; }
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

        private int _index = 0;
        public int Index
        {
            get { return _index; }
            set
            {
                _index = value;
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

        [ImportingConstructor]
        public LensAFVM(IProfileService profileService, ICameraMediator camera, IImagingMediator imagingMediator) : base(profileService)
        {
            Title = "Lens AF";

            Camera = camera;
            Imaging = imagingMediator;
            Issues = new List<string>();
            _cams = new List<string>();
            camsTable = new Dictionary<string, IntPtr>();
            Instance = this;
            PlotFocusPoints = new List<DataPoint>();

            RunAF = new AsyncCommand<bool>(async () =>
            {
                if (Validate())
                {
                    ClearCharts();
                    ActiveToken = new CancellationTokenSource();
                    ApplicationStatus status = GetStatus(string.Empty);
                    AutoFocusIsRunning = true;
                    AutoFocusResult result = await new AutoFocus(ActiveToken.Token, new Progress<ApplicationStatus>(p => status = p), profileService).RunAF(GetSelectedDevice(), Camera, Imaging, new AutoFocusSettings());
                    AutoFocusIsRunning = false;
                    return result.Successfull;
                }
                foreach (string issue in Issues)
                {
                    Notification.ShowError($"Can't start AutoFocus: {issue}");
                }
                return false;
            });

            Reload = new RelayCommand(_ =>
            {
                Rescan();
            });

            AbortAF = new RelayCommand(_ =>
            {
                if (ActiveToken != null)
                {
                    Logger.Info("Cancelling AF...");
                    Notification.ShowInformation("Cancelling AF... This may take a few seconds");
                    ActiveToken.Cancel();
                }
            });

            Rescan(); 
        }

        private IntPtr GetSelectedDevice()
        {
            return camsTable[Cams[Index]];
        }

        private ApplicationStatus GetStatus(string status)
        {
            return new ApplicationStatus() { Source = "Lens AF", Status = status };
        }

        private void Rescan()
        {
            cameraPtrs = Utility.GetConnectedCams();

            Dictionary<string, IntPtr> dict = new Dictionary<string, IntPtr>();
            List<string> list = new List<string>();

            if (cameraPtrs.Count == 0)
            {
                list.Add("No Camera Connected");
            }
            else
            {
                foreach (IntPtr ptr in cameraPtrs)
                {
                    list.Add(Utility.GetCamName(ptr));
                    dict.Add(Utility.GetCamName(ptr), ptr);
                }
            }
            Cams = list;
            camsTable = dict;
            Index = 0;
        }

        private bool Validate()
        {
            Issues.Clear();
            bool cameraConnected = Camera.GetInfo().Connected;

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

        private void ClearCharts() 
        {
            PlotFocusPoints.Clear();
        }
    }
}
