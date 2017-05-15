//
// BIMRL (BIM Rule Language) library: this library performs DSL for rule checking using BIM Rule Language that works on BIMRL Simplified Schema on RDBMS. 
// This work is part of the original author's Ph.D. thesis work on the automated rule checking in Georgia Institute of Technology
// Copyright (C) 2013 Wawan Solihin (borobudurws@hotmail.com)
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 3 of the License, or any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRLInterface.ExtensionFunction
{
    public class ExtensionFunctionRegister
    {
        /// <summary>
        /// This function checks and returns extension functions. In future, this should include external registry and loading of the assembly
        /// </summary>
        /// <param name="extensionFunctionName"></param>
        /// <returns></returns>
        public static bool checkExtensionFunction(string extensionFunctionName)
        {
            if (string.Compare(extensionFunctionName, "ValidateSpaceBoundary", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "SpaceConnectivity", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "SystemConnecivity", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "ComputeIntersection", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "VolumeIntersection", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "ComputeRemoteLocation", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "ComputePath", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "GetCoordinates", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "DoorOpeningDirection", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "SmallestRectangularEdge", true) == 0)
                return true;
            else if (string.Compare(extensionFunctionName, "Nothing", true) == 0)
                return true;
            else
                return false;
        }
    }
}
