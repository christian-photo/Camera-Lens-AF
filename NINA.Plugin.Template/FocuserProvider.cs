#region "copyright"

/*
    Copyright © 2024 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using LensAF.Util;
using Nikon;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace LensAF
{
    [Export(typeof(IEquipmentProvider))]
    public class FocuserProvider : IEquipmentProvider<IFocuser>
    {
        public string Name => "AF Lens Driver";

        public IList<IFocuser> GetEquipment()
        {
            var result = new List<IFocuser>();

            AddIfNotNull(result, GetNikonFocuser());
            AddIfNotNull(result, GetCanonFocuser());

            return result;
        }
        private void AddIfNotNull(List<IFocuser> list, IFocuser item)
        {
            if (item != null)
            {
                list.Add(item);
            }
        }

        private IFocuser GetCanonFocuser()
        {
            List<string> errors = Utility.Validate(LensAF.Camera);
            if (errors.Count == 0)
            {
                try
                {
                    CameraInfo info = new CameraInfo(Utility.GetCamera(LensAF.Camera));
                    return new CanonFocuser(info.LensName) { Name = $"Canon Lens Driver ({info.LensName})" };
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
            return null;
        }

        private IFocuser GetNikonFocuser()
        {
            List<string> errors = Utility.ValidateNikon(LensAF.Camera);
            if (errors.Count == 0)
            {
                try
                {
                    var info = new Util.CameraInfo(Utility.GetNikonCamera(LensAF.Camera));
                    return new NikonFocuser(info.LensName) { Name = $"Nikon Lens Driver ({info.LensName})" };
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }

            return null;
        }
    }
}