#region "copyright"

/*
    Copyright © 2022 Christian Palm (christian@palm-family.de)
    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LensAF.Util
{
    public class AutoFocusProfile
    {
        public int StepsBig;
        public int StepsSmall;
        public AutoFocusProfile(int stepsBig, int stepsSmall)
        {
            StepsBig = stepsBig;
            StepsSmall = stepsSmall;
        }
    }
}
