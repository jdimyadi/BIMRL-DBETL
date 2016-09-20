using System;
using System.Collections.Generic;
using System.Text;

namespace BIMRL.OctreeLib
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
