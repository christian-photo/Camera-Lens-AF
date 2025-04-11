#region "copyright"

/*
    Copyright © 2024 Christian Palm (christian@palm-family.de)
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
using NINA.Equipment.Equipment.MyCamera;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Image.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;

namespace LensAF
{
    public class CanonFocuser : BaseINPC, IFocuser
    {
        private bool _isMoving = false;
        public bool IsMoving
        {
            get => _isMoving;
            set
            {
                _isMoving = value;
                RaisePropertyChanged();
            }
        }

        private int _position = Settings.Default.FocusStopPosition;
        public int Position
        {
            get => _position;
            set
            {
                _position = value;
                RaisePropertyChanged();
            }
        }

        private bool _connected;
        public bool Connected
        {
            get => _connected;
            set
            {
                _connected = value;
                RaisePropertyChanged();
            }
        }

        private double _stepSize = 1;
        public double StepSize
        {
            get => _stepSize;
            set
            {
                _stepSize = value;
                Settings.Default.Resolution = value;
                CoreUtil.SaveSettings(Settings.Default);
                RaisePropertyChanged();
            }
        }

        private string _name = "Canon Lens Driver";
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                RaisePropertyChanged();
            }
        }

        public int MaxIncrement { get; set; } = 10000;

        public int MaxStep { get; set; } = 10000;

        public bool TempCompAvailable { get; set; } = false;

        public bool TempComp { get; set; } = false;

        public double Temperature { get; set; } = double.NaN;

        public bool HasSetupDialog { get; set; } = false;

        public string Id { get; set; }

        public string Category { get; set; } = "Canon";

        public string Description { get; set; } = "A lens driver for lenses attached to Canon bodies";

        public string DriverInfo { get; set; } = string.Empty;

        public string DriverVersion { get; set; } = "1.0.0.0";

        public IList<string> SupportedActions { get; set; }

        public RelayCommand CalibrateLens { get; set; }

        public string DisplayName { get; set; } = "Canon Lens Driver";

        public CanonFocuser(string id)
        {
            Id = id;

            CalibrateLens = new RelayCommand(async () =>
            {
                List<string> errors = Utility.Validate(LensAF.Camera);
                if (errors.Count > 0)
                {
                    foreach (string error in errors)
                    {
                        Notification.ShowError(error);
                    }
                    return;
                }
                IntPtr cam = Utility.GetCamera(LensAF.Camera);
                CancellationTokenSource token = new CancellationTokenSource();
                IAsyncEnumerable<IExposureData> data = LensAF.Camera.LiveView(token.Token);
                IsMoving = true;
                await data.ForEachAsync(_ =>
                {
                    for (int i = 0; i < 15; i++)
                    {
                        uint error = EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far3);
                        if (error != EDSDK.EDS_ERR_OK)
                            Logger.Debug(Utility.ErrorCodeToString(error));
                        Thread.Sleep(200);
                    }
                    token.Cancel();
                });
                IsMoving = false;
                int position = Position;
                Position = Settings.Default.FocusStopPosition;
                CancellationToken ct = new CancellationToken();
                IsMoving = true;
                await Move(position, ct, 1000);
                IsMoving = false;
                Notification.ShowSuccess("Calibration finished");
            });
        }

        public string Action(string actionName, string actionParameters)
        {
            return string.Empty;
        }

        public Task<bool> Connect(CancellationToken token)
        {
            return Task.Run(() =>
            {
                List<string> errors = Utility.Validate(LensAF.Camera);
                if (errors.Count > 0)
                {
                    foreach (string error in errors)
                    {
                        Notification.ShowError(error);
                    }
                    return Connected;
                }

                Connected = true;
                return Connected;
            });
        }

        public void Disconnect()
        {
            Connected = false;
        }

        public void Halt()
        {
            return;
        }

        public async Task Move(int position, CancellationToken ct, int waitInMs = 1000)
        {
            List<string> validation = Utility.Validate(LensAF.Camera);
            if (validation.Count > 0)
            {
                foreach (string issue in validation)
                {
                    Notification.ShowError(issue);
                    Logger.Error($"Cannot move focus: {issue}");
                }
            }
            double diff = Position - position;
            IntPtr cam = Utility.GetCamera(LensAF.Camera);
            CancellationTokenSource token = CancellationTokenSource.CreateLinkedTokenSource(ct);
            EDCamera c = Utility.GetCanonCamera(LensAF.Camera);

            bool wasOn = c.LiveViewEnabled;
            if (!wasOn)
                c.StartLiveView(new NINA.Equipment.Model.CaptureSequence());

            if (diff > 0) // Drive focus near
            {
                while (diff > 0)
                {
                    uint error = EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                    if (error != EDSDK.EDS_ERR_OK)
                        Logger.Debug(Utility.ErrorCodeToString(error));
                    try
                    {
                        await Task.Delay(200, ct);
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected if cancellation is requested, no need to propagate the exception.
                    }
                    diff -= StepSize;
                }
            }
            else // Drive focus far
            {
                while (diff < 0)
                {
                    uint error = EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                    if (error != EDSDK.EDS_ERR_OK)
                        Logger.Debug(Utility.ErrorCodeToString(error));
                    try
                    {
                        await Task.Delay(200, ct);
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected if cancellation is requested, no need to propagate the exception.
                    }
                    diff += StepSize;
                }
            }
            Position = position;
            if (!wasOn)
            {
                c.StopLiveView();
            }
            return;
        }

        public void SendCommandBlind(string command, bool raw = true)
        {
            return;
        }

        public bool SendCommandBool(string command, bool raw = true)
        {
            return false;
        }

        public string SendCommandString(string command, bool raw = true)
        {
            return string.Empty;
        }

        public void SetupDialog()
        {
            Logger.Warning("Canon lens driver SetupDialog not implemented");
            return;
        }
    }
}
