using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.invicara.empire.EmpireBIMDataTile.OctreeLib
{
    public class Sphere:Ihas3DLocation
    {
        Point3D origin;
        float radius;

        public Point3D Origin { get { return origin; } }
        public float Radius { get { return radius; } }

        public Sphere(Point3D originPoint, float sphereRadius)
        {
            origin = originPoint;
            radius = sphereRadius;
        }

        public bool Contains(float x, float y, float z)
        {
            float or = origin.Length();
            float dis = (float)Math.Abs((Math.Sqrt(x * x + y * y + z * z)));
            if (dis - or < radius)
                return true;

            return false;
        }

        public bool Contains(Cuboid cube)
        {
            if (Contains(cube.Left, cube.Top, cube.Rear)   &&
                Contains(cube.Right, cube.Top, cube.Rear)  &&
                Contains(cube.Left, cube.Down, cube.Rear)  &&
                Contains(cube.Right, cube.Down, cube.Rear) &&
                Contains(cube.Left, cube.Top, cube.Front)  &&
                Contains(cube.Right, cube.Top, cube.Front) &&
                Contains(cube.Left, cube.Down, cube.Front) &&
                Contains(cube.Right, cube.Down, cube.Front))
                return true;

            return false;
        }

        public bool isOverlapWith(Cuboid cube)
        {
            if (isInside(cube) || Contains(cube) ||
                Contains(cube.Left, cube.Top, cube.Rear)   ||
                Contains(cube.Right, cube.Top, cube.Rear)  ||
                Contains(cube.Left, cube.Down, cube.Rear)  ||
                Contains(cube.Right, cube.Down, cube.Rear) ||
                Contains(cube.Left, cube.Top, cube.Front)  ||
                Contains(cube.Right, cube.Top, cube.Front) ||
                Contains(cube.Left, cube.Down, cube.Front) ||
                Contains(cube.Right, cube.Down, cube.Front))
                return true;

            return false;
        }

        public bool isOverlapWith(Sphere sphere)
        {
            double distance = Math.Sqrt(
                Math.Pow((origin.X - sphere.origin.X),2) +
                Math.Pow((origin.Y - sphere.origin.Y),2) +
                Math.Pow((origin.Z - sphere.origin.Z),2)
                );

            if (distance < (radius + sphere.radius))
                return true;


            return false;
        }


        public bool isInside(Cuboid cube)
        {
            if (origin.X - radius < cube.Left  ||
                origin.X + radius > cube.Right  ||
                origin.Y - radius < cube.Down  ||
                origin.Y + radius > cube.Top    ||
                origin.Z - radius < cube.Rear  ||
                origin.Z + radius > cube.Front)
                return false;

            return true;
        }
    }
}
