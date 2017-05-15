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
using System.Text;

namespace BIMRL.Common
{
    /// <summary>
    /// because we want to check Overlap object in quad Tree we need pack All Object  in a cube
    /// you can add any object with every shape to this octa tree. but first you should inheriting from this
    /// if you want to check is object overlap or not.octa tree get you the cube overlap and then you can check More accurate your self
    /// </summary>
    public interface Ihas3DLocation
    {
        //float Left { get; }
        //float Top { get; }
        //float Right { get; }
        //float Down { get; }

         bool Contains(double x, double y, double z);
         bool Contains(Cuboid cube);
         bool isOverlapWith(Cuboid cube);

        /// <summary>
        /// determine that this object is complatly inside the cube parameter
        /// </summary>
        /// <param name="cube">a cube that we want check is this item complatly inside that</param>
        /// <returns></returns>
         bool isInside(Cuboid cube);


    }
}
