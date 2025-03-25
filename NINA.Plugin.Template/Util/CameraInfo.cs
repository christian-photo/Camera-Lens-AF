#region "copyright"

/*
    Copyright © 2024 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using EDSDKLib;
using Nikon;
using System;

namespace LensAF.Util
{
    public class CameraInfo
    {
        public string LensName;
        public string CameraName;
        public string CameraFirmware;
        public CameraInfo(IntPtr camera)
        {
            EDSDK.EdsGetPropertyData(camera, 0x0000040d, 0, out LensName);
            EDSDK.EdsGetPropertyData(camera, 0x00000002, 0, out CameraName);
            EDSDK.EdsGetPropertyData(camera, 0x00000007, 0, out CameraFirmware);
        }

        public CameraInfo(NikonDevice camera)
        {
            try
            {
                CameraFirmware = camera.GetString(eNkMAIDCapability.kNkMAIDCapability_Firmware);
                LensName = camera.GetString(eNkMAIDCapability.kNkMAIDCapability_LensInfo);
                CameraName = camera.GetString(eNkMAIDCapability.kNkMAIDCapability_Name);
            }
            catch
            {
                CameraFirmware = "Unknown";
                LensName = "Nikon Lens";
                CameraName = "Nikon Camera";
            }
        }
    }
}
