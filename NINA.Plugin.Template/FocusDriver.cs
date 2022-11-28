#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"using System;

using Dasync.Collections;
using EDSDKLib;
using LensAF.Util;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Image.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace LensAF
{
    [Export(typeof(IEquipmentProvider))]
    public class FocusDriverProvider : IEquipmentProvider<IFocuser>
    {
        public string Name => "Canon Lens Driver";

        public IList<IFocuser> GetEquipment()
        {
            List<string> errors = Utility.Validate(LensAF.Camera);
            if (errors.Count == 0)
            {
                CameraInfo info = new CameraInfo(Utility.GetCamera(LensAF.Camera));
                return new List<IFocuser>() { new FocusDriver(info.LensName) { Name = $"Canon Lens Driver ({info.LensName})" } };
            }
            return new List<IFocuser>();
        }
    }

    public class FocusDriver : BaseINPC, IFocuser
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

        private int _position = 100;
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


        public FocusDriver(string id)
        {
            Id = id;
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
            CancellationTokenSource token = new CancellationTokenSource();
            IAsyncEnumerable<IExposureData> data = LensAF.Camera.LiveView(token.Token);

            await data.ForEachAsync(_ =>
            {
                if (diff > 0) // Drive focus near
                {
                    while (diff > 0)
                    {
                        EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Near1);
                        Thread.Sleep(200);
                        diff -= StepSize;
                    }
                }
                else // Drive focus far
                {
                    while (diff < 0)
                    {
                        EDSDK.EdsSendCommand(cam, EDSDK.CameraCommand_DriveLensEvf, (int)EDSDK.EvfDriveLens_Far1);
                        Thread.Sleep(200);
                        diff += StepSize;
                    }
                }
                Position = position;
                token.Cancel();
            });
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
