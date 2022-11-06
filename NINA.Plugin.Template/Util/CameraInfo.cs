#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using EDSDKLib;
using System;

namespace LensAF.Util
{
    public class CameraInfo
    {
        public string LensName;
        public string CameraName;
        public string CanonFirmware;
        public CameraInfo(IntPtr camera)
        {
            EDSDK.EdsGetPropertyData(camera, 0x0000040d, 0, out LensName);
            EDSDK.EdsGetPropertyData(camera, 0x00000002, 0, out CameraName);
            EDSDK.EdsGetPropertyData(camera, 0x00000007, 0, out CanonFirmware);
        }
    }
}
