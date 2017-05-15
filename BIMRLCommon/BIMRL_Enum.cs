//
// BIMRL (BIM Rule Language) Simplified Schema ETL (Extract, Transform, Load) library: this library transforms IFC data into BIMRL Simplified Schema for RDBMS. 
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

namespace BIMRL.Common
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
