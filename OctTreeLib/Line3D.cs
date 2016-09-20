using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRL.OctreeLib
{
    // Line3D is an unbounded line defined by a point (on the line) and a vector defining the direction. The bounded line is defined in LineSegment3D
    public class Line3D
    {
        Point3D _point;
        Vector3D _vectorDirection;

        public Line3D()
        {
            // default value for empty initialization
            _point = new Point3D(0.0, 0.0, 0.0);
            _vectorDirection = new Vector3D(1.0, 1.0, 1.0);
        }

        /// <summary>
        /// Define Line3 using the point and vector
        /// </summary>
        /// <param name="point">Point on the line</param>
        /// <param name="direction">vector direction</param>
        public Line3D(Point3D point, Vector3D direction)
        {
            _point = point;
            _vectorDirection = direction;
            _vectorDirection.Normalize();
        }

        /// <summary>
        /// Define a line using 2 points on the line
        /// </summary>
        /// <param name="P1"></param>
        /// <param name="P2"></param>
        public Line3D(Point3D P1, Point3D P2)
        {
            _point = P1;
            Vector3D v = new Vector3D(P2.X - P1.X, P2.Y - P1.Y, P2.Z - P1.Z);
            v.Normalize();
            _vectorDirection = v;
        }

        public Vector3D direction
        {
            get { return this._vectorDirection; }
            set 
            { 
                this._vectorDirection = value;
                this._vectorDirection.Normalize();
            }
        }

        public Point3D point
        {
            get { return this._point; }
            set { this._point = value; }
        }

        /// <summary>
        /// Test the intersection beween 2 lines
        /// </summary>
        /// <param name="L1">Line 1</param>
        /// <param name="L2">Line 2</param>
        /// <returns></returns>
        public static bool intersect (Line3D L1, Line3D L2)
        {
            Point3D p = new Point3D();
            return Line3D.intersect(L1, L2, out p);
        }

        /// <summary>
        /// Calculate the intersection point of 2 lines
        /// </summary>
        /// <param name="L1">Line 1</param>
        /// <param name="L2">Line 2</param>
        /// <param name="intersectionPoint">Intersection point</param>
        /// <returns></returns>
        public static bool intersect(Line3D L1, Line3D L2, out Point3D intersectionPoint)
        {
            intersectionPoint = new Point3D();
            // Intersection operation
            // Not needed for the time being
            return false;
        }

        /// <summary>
        /// Test whether 2 lines are parallel
        /// </summary>
        /// <param name="L1"></param>
        /// <param name="L2"></param>
        /// <returns></returns>
        public static bool parallel(Line3D L1, Line3D L2)
        {
            // Parallel test must allow vectors with opposite direction as parallel too
            return
                Vector3D.Parallels(L1.direction, L2.direction);
        }

        public override string ToString()
        {
            string pr = "P" + this.point.ToString() + "\nv" + this.direction.ToString();
            return pr;
        }

    }
}
