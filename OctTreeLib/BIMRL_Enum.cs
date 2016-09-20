using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BIMRL.OctreeLib
{
    public enum FedIDStatus
    {
        FedIDExisting = 0,
        FedIDNew = 1
    }

    public enum projectUnit
    {
        SIUnit_Length_Meter,
        SIUnit_Length_MilliMeter,
        Imperial_Length_Foot,
        Imperial_Length_Inch
    }

    public enum faceOrientation
    {
        TOP,                    // A face that points upward at the top of an object (vector on +Z direction and has largest Z-position
        BOTTOM,                 // A face that points upward at the bottom of an object (vector on -Z direction and has smallest Z-position
        SIDE,                   // A face that mostly lies at the X-Z or Y-X plane (vector Z-component nearly 0)
        UNDERSIDE,              // A special case of a face that is at the underside of a geometry such as stairflight
        TOPSIDE                 // A spacial case of a face that is at the overside of a geometry such as a roof
    }
}
