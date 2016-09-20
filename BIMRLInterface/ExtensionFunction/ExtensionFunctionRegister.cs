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
