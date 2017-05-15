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
using System.Threading.Tasks;

namespace BIMRL.OctreeLib
{
    /// <summary>
    /// Class Plane3D is internally represented by a point and a normal vector of the plane. Plane3D is an unbounded plane (the bounded plane is defined in Face3D)
    /// </summary>
    public class Plane3D
    {
        private Point3D _point;
        private Vector3D _normal;
        private double _parConstant ;

        public Plane3D(Point3D pointOnPlane, Vector3D normalV)
        {
            _point = pointOnPlane;
            _normal = normalV;
            _normal.Normalize();
            _parConstant = -(_normal.X * _point.X + _normal.Y * _point.Y + _normal.Z * _point.Z);
        }

        public Plane3D(Point3D pointOnPlane, Vector3D v1OnPlane, Vector3D v2OnPlane)
        {
            _point = pointOnPlane;
            _normal = Vector3D.CrossProduct (v1OnPlane, v2OnPlane);
            _normal.Normalize();
            _parConstant = -(_normal.X * _point.X + _normal.Y * _point.Y + _normal.Z * _point.Z);
        }
        
        public Plane3D(Point3D p1OnPlane, Point3D p2OnPlane, Vector3D parallelVector)
        {
            _point = p1OnPlane;

            double dX = p2OnPlane.X - p1OnPlane.X;
            double dY = p2OnPlane.Y - p1OnPlane.Y;
            double dZ = p2OnPlane.Z - p1OnPlane.Z;
            Vector3D v2 = new Vector3D( dX, dY, dZ);

            _normal = Vector3D.CrossProduct(v2, parallelVector);
            _normal.Normalize();
            _parConstant = -(_normal.X * _point.X + _normal.Y * _point.Y + _normal.Z * _point.Z);
        }

        public Plane3D(Point3D P1, Point3D P2, Point3D P3)
        {
            _point = P1;

            double d1X = P2.X - P1.X;
            double d1Y = P2.Y - P1.Y;
            double d1Z = P2.Z - P1.Z;
            Vector3D v1 = new Vector3D(d1X, d1Y, d1Z);

            double d2X = P3.X - P1.X;
            double d2Y = P3.Y - P1.Y;
            double d2Z = P3.Z - P1.Z;
            Vector3D v2 = new Vector3D(d2X, d2Y, d2Z);

            _normal = Vector3D.CrossProduct(v1, v2);
            _normal.Normalize();
            _parConstant = -(_normal.X * _point.X + _normal.Y * _point.Y + _normal.Z * _point.Z);
        }

        public Vector3D normalVector
        {
            get { return this._normal; }
            set 
            { 
                this._normal = value;
                this._normal.Normalize();
                _parConstant = -(_normal.X * _point.X + _normal.Y * _point.Y + _normal.Z * _point.Z);
            }
        }

        public Point3D point
        {
            get { return this._point; }
            set { 
                this._point = value;
                _parConstant = -(_normal.X * _point.X + _normal.Y * _point.Y + _normal.Z * _point.Z);
            }
        }

        public double parConstant
        {
            get { return this._parConstant; }
        }

        public static bool Parallels(Plane3D P1, Plane3D P2)
        {
            return Vector3D.Parallels(P1.normalVector, P2.normalVector);
        }

        public static bool Parallels(Plane3D P1, Face3D F1)
        {
            return Vector3D.Parallels(P1.normalVector, F1.basePlane.normalVector);
        }

        public static bool Parallels(Plane3D P1, Line3D L1)
        {
            double res = Vector3D.DotProduct(P1.normalVector, L1.direction);
            if (MathUtils.equalTol(res, 0.0, MathUtils.defaultTol))
                return true;
            return false;
        }

        public static bool Parallels(Plane3D P1, LineSegment3D LS)
        {
            double res = Vector3D.DotProduct(P1.normalVector, LS.baseLine.direction);
            if (MathUtils.equalTol(res, 0.0, MathUtils.defaultTol))
                return true;
            return false;
        }

        /// <summary>
        /// Test whether 2 planes overlapped
        /// </summary>
        /// <param name="P1"></param>
        /// <param name="P2"></param>
        /// <returns></returns>
        public static bool Overlaps(Plane3D P1, Plane3D P2)
        {
            bool parallel = Vector3D.Parallels(P1.normalVector, P2.normalVector);
            bool pOnPlane = pointOnPlane(P1, P2.point);
            // if the normals are the same and point in P2 is also point in P1, the two planes are overlapped
            return (parallel && pOnPlane);
        }

        public static bool Overlaps(Plane3D P1, Face3D F1)
        {
            bool parallel = Vector3D.Parallels(P1.normalVector, F1.basePlane.normalVector);
            bool pOnPlane = pointOnPlane(P1, F1.vertices[0]);
            // if the normals are the same and point in P2 is also point in P1, the two planes are overlapped
            return (parallel && pOnPlane);
        }

        public static bool Overlaps(Plane3D P1, Line3D L1)
        {
            double res = Vector3D.DotProduct(P1.normalVector, L1.direction);
            bool pOnPlane = pointOnPlane(P1, L1.point);
            // if the normals are the same and point in P2 is also point in P1, the two planes are overlapped
            return (MathUtils.equalTol(res, 0.0, MathUtils.defaultTol) && pOnPlane);
        }

        public static bool Overlaps(Plane3D P1, LineSegment3D LS)
        {
            double res = Vector3D.DotProduct(P1.normalVector, LS.baseLine.direction);
            bool pOnPlane = pointOnPlane(P1, LS.startPoint);
            // if the normals are the same and point in P2 is also point in P1, the two planes are overlapped
            return (MathUtils.equalTol(res, 0.0, MathUtils.defaultTol) && pOnPlane);
        }

        /// <summary>
        /// Test whether two planes intersect
        /// </summary>
        /// <param name="P1">Plane 1</param>
        /// <param name="P2">Plane 2</param>
        /// <param name="intersectingLine">Resulting intersection line</param>
        /// <returns></returns>
        public static bool PPintersect(Plane3D P1, Plane3D P2, out Line3D intersectingLine)
        {
            intersectingLine = new Line3D();
            if (Vector3D.Parallels(P1.normalVector, P2.normalVector))
                return false;   // Planes are parallel to each other

            intersectingLine.direction = Vector3D.CrossProduct(P1.normalVector, P2.normalVector);

            // To find the point on the line, we can use 3 plane intersection with the 3rd plane with the normal as a cross product of P1 and P2 to guarantee 1 point intersection. It passes through the origin
            Vector3D n3 = Vector3D.CrossProduct(P1.normalVector, P2.normalVector);
            double da = 1 / (Vector3D.DotProduct(P1.normalVector, Vector3D.CrossProduct(P2.normalVector, n3)));
            Vector3D vp = (-P1.parConstant * (Vector3D.CrossProduct(P2.normalVector, n3)) - P2.parConstant * (Vector3D.CrossProduct(n3, P1.normalVector))) * da;
            intersectingLine.point.X = vp.X;
            intersectingLine.point.Y = vp.Y;
            intersectingLine.point.Z = vp.Z;

            //if ((Math.Abs(intersectingLine.direction.Z) > Math.Abs(intersectingLine.direction.X))  && (Math.Abs(intersectingLine.direction.Z) > Math.Abs(intersectingLine.direction.Y)))
            //{// Z=0
            //    intersectingLine.point.Y = (P1.normalVector.X * P2.parConstant - P2.normalVector.X * P1.parConstant)
            //                                    / (P1.normalVector.Y * P2.normalVector.X - P1.normalVector.X * P2.normalVector.Y);
            //    if (!MathUtils.equalTol(P1.normalVector.X, 0.0, MathUtils.defaultTol))
            //        intersectingLine.point.X = (-P1.parConstant - P1.normalVector.Y * intersectingLine.point.Y) / P1.normalVector.X;
            //    else
            //        intersectingLine.point.X = (-P2.parConstant - P2.normalVector.Y * intersectingLine.point.Y) / P2.normalVector.X;
            //    intersectingLine.point.Z = 0.0;
            //}
            //else if ((Math.Abs(intersectingLine.direction.Y) > Math.Abs(intersectingLine.direction.X)) && (Math.Abs(intersectingLine.direction.Y) > Math.Abs(intersectingLine.direction.Z)))
            //{// Y=0
            //    intersectingLine.point.Z = (P1.normalVector.X * P2.parConstant - P2.normalVector.X * P1.parConstant)
            //                                    / (P1.normalVector.Z * P2.normalVector.X - P1.normalVector.X * P2.normalVector.Z);
            //    if (!MathUtils.equalTol(P1.normalVector.X, 0.0, MathUtils.defaultTol))
            //        intersectingLine.point.X = (-P1.parConstant - P1.normalVector.Z * intersectingLine.point.Z) / P1.normalVector.X;
            //    else
            //        intersectingLine.point.X = (-P2.parConstant - P2.normalVector.Z * intersectingLine.point.Z) / P2.normalVector.X;
            //    intersectingLine.point.Y = 0.0;
            //}
            //else
            //{// X=0
            //    intersectingLine.point.Y = (P1.normalVector.Z * P2.parConstant - P2.normalVector.Z * P1.parConstant)
            //                                    / (P1.normalVector.Y * P2.normalVector.Z - P1.normalVector.Z * P2.normalVector.Y);
            //    if (!MathUtils.equalTol(P1.normalVector.Z, 0.0, MathUtils.defaultTol))
            //        intersectingLine.point.Z = (-P1.parConstant - P1.normalVector.Y * intersectingLine.point.Y) / P1.normalVector.Z;
            //    else
            //        intersectingLine.point.Z = (-P2.parConstant - P2.normalVector.Y * intersectingLine.point.Y) / P2.normalVector.Z;
            //    intersectingLine.point.X = 0.0;
            //}
            return true;
        }

        /// <summary>
        /// Test whether a Face (bounded plane) intersects with a plane
        /// </summary>
        /// <param name="P1">Plane</param>
        /// <param name="F1">Face</param>
        /// <param name="intersectingLine">Resulting intercting linesegment (bounded)</param>
        /// <returns></returns>
        public static bool PPintersect(Plane3D P1, Face3D F1, out LineSegment3D intersectingLine)
        {
            Line3D intLine = new Line3D();
            LineSegment3D ls = new LineSegment3D(new Point3D(0.0, 0.0, 0.0), new Point3D(1.0, 1.0, 1.0));
            intersectingLine = ls;

            // Intersect operation: get the intersection (unbounded) line
            if (!PPintersect(P1,F1.basePlane, out intLine))
                return false;

            // Now needs to check get the segment by getting intersecting points between the line and the Face boundaries
            List<Point3D> intPts = new List<Point3D>();
            // Use line segment that is large enough to ensure it covers the extent of the face
            //double extent = Point3D.distance(F1.boundingBox.LLB, F1.boundingBox.URT) * 10;        // Bug: NOT big enough for some cases!, use worldBB extent
            double extent ;
            if (Octree.WorldBB == null)
                extent = 1000000000;
            else
                extent = Octree.WorldBB.extent * 10;
            LineSegment3D intLS = new LineSegment3D(new Point3D(intLine.point - (intLine.direction * extent)), new Point3D(intLine.point + (intLine.direction * extent)));

            bool res = Face3D.intersect(F1, intLS, out intPts);
            if (res)
            {
                intersectingLine.startPoint = intPts[0];
                intersectingLine.endPoint = intPts[intPts.Count - 1];
                return true;
            }
            return false;
        }


        /// <summary>
        /// Plane and ray intersection. The ray is defined as a line starting from its point.
        /// </summary>
        /// <param name="P1">Plane</param>
        /// <param name="L1">Line</param>
        /// <param name="intersectingPoint">Point of intersection</param>
        /// <returns></returns>
        public static bool PRayintersect(Plane3D P1, Line3D L1, out Point3D intersectingPoint)
        {
            intersectingPoint = new Point3D();
            double denom = Vector3D.DotProduct(P1.normalVector, L1.direction);
            if (MathUtils.equalTol(denom, 0.0, MathUtils.defaultTol))
                return false;   // Normal and the lines are perpendicular to each other: line is parallel to the plane, no intersection

            double r = Vector3D.DotProduct(P1.normalVector, new Vector3D(P1.point.X - L1.point.X, P1.point.Y - L1.point.Y, P1.point.Z - L1.point.Z))
                        /denom;
            if (r <= 0)
                return false;   // No intersection because intersection occurs on the other direction of the Ray

            intersectingPoint.X = L1.point.X + r * L1.direction.X;
            intersectingPoint.Y = L1.point.Y + r * L1.direction.Y;
            intersectingPoint.Z = L1.point.Z + r * L1.direction.Z;
            return true;
        }

        /// <summary>
        /// Plane and a line intersection
        /// </summary>
        /// <param name="P1">Plane</param>
        /// <param name="L1">Line</param>
        /// <param name="intersectingPoint">Point of intersection</param>
        /// <returns></returns>
        public static bool PLintersect(Plane3D P1, Line3D L1, out Point3D intersectingPoint)
        {
            intersectingPoint = new Point3D();
            double denom = Vector3D.DotProduct(P1.normalVector, L1.direction);
            if (MathUtils.equalTol(denom, 0.0, MathUtils.defaultTol))
                return false;   // Normal and the lines are perpendicular to each other: line is parallel to the plane, no intersection

            double r = Vector3D.DotProduct(P1.normalVector, new Vector3D(P1.point.X - L1.point.X, P1.point.Y - L1.point.Y, P1.point.Z - L1.point.Z))
                        / denom;

            intersectingPoint.X = L1.point.X + r * L1.direction.X;
            intersectingPoint.Y = L1.point.Y + r * L1.direction.Y;
            intersectingPoint.Z = L1.point.Z + r * L1.direction.Z;
            return true;
        }

        /// <summary>
        /// Plane and a linesegment intersection
        /// </summary>
        /// <param name="P1">Plane</param>
        /// <param name="L1">Linesegment</param>
        /// <param name="intersectingPoint">Intersection Point</param>
        /// <returns></returns>
        public static bool PLintersect(Plane3D P1, LineSegment3D L1, out Point3D intersectingPoint)
        {
            intersectingPoint = new Point3D();
            double denom = Vector3D.DotProduct(P1.normalVector, L1.unNormalizedVector);
            if (MathUtils.equalTol(denom, 0.0, MathUtils.defaultTol))
                return false;   // Normal and the lines are perpendicular to each other: line is parallel to the plane, no intersection

            double r = Vector3D.DotProduct(P1.normalVector, new Vector3D(P1.point.X - L1.startPoint.X, P1.point.Y - L1.startPoint.Y, P1.point.Z - L1.startPoint.Z))
                        /denom;
            
            if (r < 0.0 || r > 1.0)
                return false;   // intersection occurs outside of the segment
            intersectingPoint.X = L1.startPoint.X + r * L1.unNormalizedVector.X;
            intersectingPoint.Y = L1.startPoint.Y + r * L1.unNormalizedVector.Y;
            intersectingPoint.Z = L1.startPoint.Z + r * L1.unNormalizedVector.Z;
            return true;
        }


        /// <summary>
        /// Test whether a point P1 lies on the plane PL1
        /// </summary>
        /// <param name="PL1">Plane</param>
        /// <param name="P1">Point</param>
        /// <returns></returns>
        public static bool pointOnPlane(Plane3D PL1, Point3D P1)
        {
            return (MathUtils.equalTol(distPoint2Plane(PL1, P1), 0.0));
        }

        /// <summary>
        /// Test whether a point P1 lies on the plane PL1
        /// </summary>
        /// <param name="PL1">Plane</param>
        /// <param name="P1">Point</param>
        /// <param name="distance">return the distance. Needed to check whether the point is actually on the plabe: distance=0</param>
        /// <returns></returns>
        public static bool pointOnPlane(Plane3D PL1, Point3D P1, out double distance)
        {
            distance = distPoint2Plane(PL1, P1);
            return (MathUtils.equalTol(distance, 0.0));
        }

        /// <summary>
        /// Distance from a point to a plane
        /// </summary>
        /// <param name="PL1">Plane</param>
        /// <param name="P1">Point</param>
        /// <returns></returns>
        public static double distPoint2Plane(Plane3D PL1, Point3D P1)
        {
            return Vector3D.DotProduct(PL1.normalVector, new Vector3D(P1.X - PL1.point.X, P1.Y - PL1.point.Y, P1.Z - PL1.point.Z));

        }

        public override string ToString()
        {
            string pr = "P" + this.point.ToString() + "\n" + this.normalVector.ToString();
            return pr;
        }
    }
}
