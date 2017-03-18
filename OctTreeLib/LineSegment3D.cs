using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BIMRL.OctreeLib
{

   /// <summary>
   /// Custom IEqualityComparer for a lighweight line segment
   /// </summary>
   public class SegmentCompare : IEqualityComparer<LineSegment3D>
   {
      public bool Equals(LineSegment3D o1, LineSegment3D o2)
      {
         if (o1.Equals(o2))
            return true;
         else
            return false;
      }

      public int GetHashCode(LineSegment3D obj)
      {
         int hash = 23;
         hash = hash * 31 + VertexHashCode(obj.startPoint);
         hash = hash * 31 + VertexHashCode(obj.endPoint);
         return hash;
      }

      int VertexHashCode(Point3D obj)
      {
         double X = Math.Round(obj.X, MathUtils._doubleDecimalPrecision);
         double Y = Math.Round(obj.Y, MathUtils._doubleDecimalPrecision);
         double Z = Math.Round(obj.Z, MathUtils._doubleDecimalPrecision);

         return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
      }
   }

   public class LineSegment3DLW
    {
        public Point3DLW startpoint { get; set; }
        public Point3DLW endpoint { get; set; }
        public LineSegment3DLW(LineSegment3D l3d)
        {
            startpoint = new Point3DLW(l3d.startPoint);
            endpoint = new Point3DLW(l3d.endPoint);
        }
    }

    public class LineSegment3D
    {
        private Point3D _pStart, _pEnd;
        private Line3D _baseLine;
        private Vector3D _unNormalizedV;

        /// <summary>
        /// Line segment is bounded by a start point and an end point
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        public LineSegment3D(Point3D p1, Point3D p2)
        {
            _pStart = p1;
            _pEnd = p2;

            _baseLine = new Line3D();
            _baseLine.point = _pStart;
            _baseLine.direction = new Vector3D((p2.X - p1.X), (p2.Y - p1.Y), (p2.Z - p1.Z));
            _baseLine.direction.Normalize();
        }

        /// <summary>
        /// The vector between start and end without normalizing it
        /// </summary>
        public Vector3D unNormalizedVector
        {
            get { return new Vector3D((_pEnd.X - _pStart.X), (_pEnd.Y - _pStart.Y), (_pEnd.Z - _pStart.Z)); }
        }

        public Point3D startPoint
        {
            get { return _pStart; }
            set 
            { 
                _pStart = value;

                // setting the start point needs to recalculate the new Line3D
                _baseLine.point = _pStart;
                _baseLine.direction = new Vector3D((_pEnd.X - _pStart.X), (_pEnd.Y - _pStart.Y), (_pEnd.Z - _pStart.Z));
                _baseLine.direction.Normalize();
            }
        }

        public Point3D endPoint
        {
            get { return _pEnd; }
            set { 
                _pEnd = value;

                // setting the end point needs to recalculate the new Line3D
                _baseLine.direction = new Vector3D((_pEnd.X - _pStart.X), (_pEnd.Y - _pStart.Y), (_pEnd.Z - _pStart.Z));
                _baseLine.direction.Normalize();
            
            }
        }

        public Line3D baseLine
        {
            get { return _baseLine; }
        }

        public void Reverse()
        {
            // Reverse the point sequence, then regenerate the rest
            Point3D tmp = _pStart;
            _pStart = _pEnd;
            _pEnd = tmp;

            _baseLine = new Line3D();
            _baseLine.point = _pStart;
            _baseLine.direction = new Vector3D((_pEnd.X - _pStart.X), (_pEnd.Y - _pStart.Y), (_pEnd.Z - _pStart.Z));
            _baseLine.direction.Normalize();
        }

        public List<Point3D> toPointSet(double resolution)
        {
            List<Point3D> pointSet = new List<Point3D>();
            int noStep = (int) Math.Floor(this.extent / resolution);
            for (int i = 0; i <= noStep; i++ )
            {
                pointSet.Add(_pStart + _baseLine.direction*resolution*i);
            }
            pointSet.Add(_pEnd);

            return pointSet;
        }

        /// <summary>
        /// Return the extent of the lie segment, essentially the distance between the start and end point in 3D
        /// </summary>
        public double extent
        {
            get { return Point3D.distance(_pEnd, _pStart); }
        }

        public void extendAtEnd(double extension)
        {
            Vector3D ext = extension * _baseLine.direction;
            _pEnd.X = _pEnd.X + ext.X;
            _pEnd.Y = _pEnd.Y + ext.Y;
            _pEnd.Z = _pEnd.Z + ext.Z;
        }

        public void extendAtStart(double extension)
        {
            Vector3D ext = extension * _baseLine.direction;
            _pStart.X = _pStart.X - ext.X;
            _pStart.Y = _pStart.Y - ext.Y;
            _pStart.Z = _pStart.Z - ext.Z;
        }

        /// <summary>
        /// Testing whether a point lies on the segment
        /// </summary>
        /// <param name="lineSegment">Line segment</param>
        /// <param name="thePoint">Point</param>
        /// <returns></returns>
        public static bool isInSegment(LineSegment3D lineSegment, Point3D thePoint)
        {
            LineSegmentOnSegmentEnum stat;
            return isInSegment(lineSegment, thePoint, out stat);
        }

        /// <summary>
        /// Testing whether a point lies on the segment
        /// </summary>
        /// <param name="lineSegment">Line segment</param>
        /// <param name="thePoint">Point</param>
        /// <param name="status">Returnign an enumeration to identify where the point is relative to the line segment</param>
        /// <returns></returns>
        public static bool isInSegment(LineSegment3D lineSegment, Point3D thePoint, out LineSegmentOnSegmentEnum status )
        {
            status = LineSegmentOnSegmentEnum.Undefined;

            double tx = (thePoint.X - lineSegment.startPoint.X) / (lineSegment.unNormalizedVector.X);
            double ty = (thePoint.Y - lineSegment.startPoint.Y) / (lineSegment.unNormalizedVector.Y);
            double tz = (thePoint.Z - lineSegment.startPoint.Z) / (lineSegment.unNormalizedVector.Z);

            // Handle in case the vector is parallel to an axis (hence the value is 0), in this case any value is acceptable and therefore should be set the same as at least one other 
            // that has a valid value
            if (MathUtils.equalTol(lineSegment.unNormalizedVector.X,0.0))
            {
                if (MathUtils.equalTol(lineSegment.unNormalizedVector.Y, 0.0))
                    if (MathUtils.equalTol(lineSegment.unNormalizedVector.Z, 0.0))
                        tx = 0.0;
                    else
                        tx = tz;
                else
                    tx = ty;
            }

            if (MathUtils.equalTol(lineSegment.unNormalizedVector.Y, 0.0))
            {
                if (MathUtils.equalTol(lineSegment.unNormalizedVector.X, 0.0))
                    if (MathUtils.equalTol(lineSegment.unNormalizedVector.Z, 0.0))
                        ty = 0.0;
                    else
                        ty = tz;
                else
                    ty = tx;
            }

            if (MathUtils.equalTol(lineSegment.unNormalizedVector.Z, 0.0))
            {
                if (MathUtils.equalTol(lineSegment.unNormalizedVector.Y, 0.0))
                    if (MathUtils.equalTol(lineSegment.unNormalizedVector.X, 0.0))
                        tz = 0.0;
                    else
                        tz = tx;
                else
                    tz = ty;
            }


            if ((MathUtils.equalTol(tx, 0.0, MathUtils.defaultTol) && MathUtils.equalTol(ty, 0.0, MathUtils.defaultTol) && MathUtils.equalTol(tz, 0.0, MathUtils.defaultTol)) ||
                (MathUtils.equalTol(tx, 1.0, MathUtils.defaultTol) && MathUtils.equalTol(ty, 1.0, MathUtils.defaultTol) && MathUtils.equalTol(tz, 1.0, MathUtils.defaultTol)))
            {
                status = LineSegmentOnSegmentEnum.CoincideEndSegment;
                return true;
            }

            if (MathUtils.equalTolSign(tx, ty, MathUtils.defaultTol) && MathUtils.equalTolSign(ty, tz, MathUtils.defaultTol) && MathUtils.equalTolSign(tx, tz, MathUtils.defaultTol))
            {
                if (tx > 1.0 || tx < 0.0)
                {
                    status = LineSegmentOnSegmentEnum.OutsideSegment;
                    return false;
                }
                else
                {
                    status = LineSegmentOnSegmentEnum.InsideSegment;
                    return true;
                }
            }

            return false;
                
        }

        /// <summary>
        /// Testing whether 2 line segments overlapped
        /// </summary>
        /// <param name="lineSegment1">First line segment</param>
        /// <param name="lineSegment2">Second line segment</param>
        /// <param name="overlappedSegment">The resulting overlapped line segment</param>
        /// <param name="mode">The enumeration of the result</param>
        /// <returns></returns>
        public static bool overlap(LineSegment3D lineSegment1, LineSegment3D lineSegment2, out LineSegment3D overlappedSegment, out LineSegmentOverlapEnum mode)
        {
            mode = LineSegmentOverlapEnum.Undefined;
            overlappedSegment = lineSegment1;

            // First check whether they have the same lineDirection (absolute: -v = +v for overlap test)
            if (!Line3D.parallel(lineSegment1.baseLine, lineSegment2.baseLine))
                return false;

            // The lines are parallel, now check whether there is at least one point falls in the segment
            // Test points in line 2 on line 1
            LineSegmentOnSegmentEnum p1l1_s, p2l1_s;
            LineSegmentOnSegmentEnum p1l2_s, p2l2_s;

            bool p1l1 = isInSegment(lineSegment1, lineSegment2.startPoint, out p1l1_s);
            bool p2l1 = isInSegment(lineSegment1, lineSegment2.endPoint, out p2l1_s);
            if (p1l1 && p2l1)
            {
                if ((p1l1_s == p2l1_s && p2l1_s == LineSegmentOnSegmentEnum.InsideSegment)
                    || (p1l1_s == LineSegmentOnSegmentEnum.CoincideEndSegment && p2l1_s == LineSegmentOnSegmentEnum.InsideSegment)
                    || (p1l1_s == LineSegmentOnSegmentEnum.InsideSegment && p2l1_s == LineSegmentOnSegmentEnum.CoincideEndSegment))
                    mode = LineSegmentOverlapEnum.SubSegment;
                if (p1l1_s == p2l1_s && p1l1_s == LineSegmentOnSegmentEnum.CoincideEndSegment)
                    mode = LineSegmentOverlapEnum.ExactlyOverlap;
                overlappedSegment = lineSegment2;
                return true;
            }
            else if (p1l1 || p2l1)
            {
                if (p1l1_s == LineSegmentOnSegmentEnum.CoincideEndSegment || p2l1_s == LineSegmentOnSegmentEnum.CoincideEndSegment)
                {
                    mode = LineSegmentOverlapEnum.Touch;
                    // Zero lenth line segments = a point, a valid data
                    if (p1l1) overlappedSegment = new LineSegment3D(lineSegment2.startPoint, lineSegment2.startPoint);
                    if (p2l1) overlappedSegment = new LineSegment3D(lineSegment2.endPoint, lineSegment2.endPoint);
                }
                if (p1l1_s == LineSegmentOnSegmentEnum.InsideSegment || p2l1_s == LineSegmentOnSegmentEnum.InsideSegment)
                {
                    bool p1l2 = isInSegment(lineSegment2, lineSegment1.startPoint, out p1l2_s);
                    bool p2l2 = isInSegment(lineSegment2, lineSegment1.endPoint, out p2l2_s);
                    if (p1l1_s == LineSegmentOnSegmentEnum.InsideSegment && p1l2_s == LineSegmentOnSegmentEnum.InsideSegment)
                        overlappedSegment = new LineSegment3D(lineSegment2.startPoint, lineSegment1.startPoint);
                    if (p2l1_s == LineSegmentOnSegmentEnum.InsideSegment && p1l2_s == LineSegmentOnSegmentEnum.InsideSegment)
                        overlappedSegment = new LineSegment3D(lineSegment2.endPoint, lineSegment1.startPoint);
                    if (p1l1_s == LineSegmentOnSegmentEnum.InsideSegment && p2l2_s == LineSegmentOnSegmentEnum.InsideSegment)
                        overlappedSegment = new LineSegment3D(lineSegment2.startPoint, lineSegment1.endPoint);
                    if (p2l1_s == LineSegmentOnSegmentEnum.InsideSegment && p2l2_s == LineSegmentOnSegmentEnum.InsideSegment)
                        overlappedSegment = new LineSegment3D(lineSegment2.endPoint, lineSegment1.endPoint);

                    mode = LineSegmentOverlapEnum.PartiallyOverlap;
                }
                return true;
            }
            else
            {
                // if both p1l1 and p2l1 are false, there is probability that the l1 are inside the l2 instead. Only this case needs to be checked since other
                //  cases should be covered by the previous conditional checks
                bool p1l2 = isInSegment(lineSegment2, lineSegment1.startPoint, out p1l2_s);
                bool p2l2 = isInSegment(lineSegment2, lineSegment1.endPoint, out p2l2_s);
                if ((p1l2 && p2l2) &&
                    (p1l2_s == p2l2_s && p1l2_s == LineSegmentOnSegmentEnum.InsideSegment))
                {
                    mode = LineSegmentOverlapEnum.SuperSegment;
                    overlappedSegment = lineSegment1;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Testing whether 2 line segments intersect
        /// </summary>
        /// <param name="L1">Line 1</param>
        /// <param name="L2">Line 2</param>
        /// <param name="intersectionPoint">Intersection point</param>
        /// <param name="mode">enumeration that identifies how the two lines intersect</param>
        /// <returns></returns>
        public static bool intersect(LineSegment3D L1, LineSegment3D L2, out Point3D intersectionPoint, out LineSegmentIntersectEnum mode)
        {
            mode = LineSegmentIntersectEnum.Undefined;
            Point3D ip = new Point3D();
            intersectionPoint = ip;

            if (Line3D.parallel(L1.baseLine, L2.baseLine)) return false;   // Lines are parallel, no intersection

            // With segments AB and CD, we get Pab = (1-s)A + sB, and Qab = (1-t)C + D
            // For intersection Pab = Qcd and therefore (1-s)A + sB = (1-t)C + tD. We get s(B-A) - t(D-C) = C-A for some s,t
            // using matrix operation we can get s and t using X and Y, and also using X and Z. If they intersect s and t have to be equal for both
            double a = L1.endPoint.X - L1.startPoint.X;
            double b = L2.startPoint.X - L2.endPoint.X;
            double c = L1.endPoint.Y - L1.startPoint.Y;
            double d = L2.startPoint.Y - L2.endPoint.Y;
            double e = L1.endPoint.Z - L1.startPoint.Z;
            double f = L2.startPoint.Z - L2.endPoint.Z;
            double g = L2.startPoint.X - L1.startPoint.X;
            double h = L2.startPoint.Y - L1.startPoint.Y;
            double i = L2.startPoint.Z - L1.startPoint.Z;

            // Work on special case when the lines are 2D
            if ((MathUtils.equalTol(a, 0.0) && MathUtils.equalTol(b, 0.0) && MathUtils.equalTol(g, 0.0))
                    || (MathUtils.equalTol(e, 0.0) && MathUtils.equalTol(f, 0.0) && MathUtils.equalTol(i, 0.0))
                    || (MathUtils.equalTol(c, 0.0) && MathUtils.equalTol(d, 0.0) && MathUtils.equalTol(h, 0.0)))
            {
                double s = -1.0;
                double t = -1.0;
                // 2D line segments on Y-Z plane (X = 0)
                if (MathUtils.equalTol(a, 0.0) && MathUtils.equalTol(b, 0.0) && MathUtils.equalTol(g, 0.0))
                {
                    s = (d * i - h * f) / (d * e - c * f);
                    t = (e * h - i * c) / (d * e - c * f);
                }
                    // 2D line segments on X-Y plane (Z = 0)
                else if (MathUtils.equalTol(e, 0.0) && MathUtils.equalTol(f, 0.0) && MathUtils.equalTol(i, 0.0))
                {
                    s = (b*h - g*d) / (b*c - a*d);
                    t = (g*c - h*a) / (b*c - a*d);
                }
                    // 2D line segment on X-Z plane (Y = 0)
                else if (MathUtils.equalTol(c, 0.0) && MathUtils.equalTol(d, 0.0) && MathUtils.equalTol(h, 0.0))
                {
                    s = (b * i - g * f) / (b * e - a * f);
                    t = (g * e - i * a) / (b * e - a * f);
                }

                // calculate intersection point
                ip.X = (1 - s) * L1.startPoint.X + s * L1.endPoint.X;
                ip.Y = (1 - s) * L1.startPoint.Y + s * L1.endPoint.Y;
                ip.Z = (1 - s) * L1.startPoint.Z + s * L1.endPoint.Z;
                intersectionPoint = ip;

                if ((0 <= s && s <= 1) && (0 <= t && t <= 1))
                {
                    // If the segments intersect s and t have to be between 0 and 1
                    mode = LineSegmentIntersectEnum.IntersectedWithinSegments;
                    return true;
                }
                else
                {
                    // Lines intersect but intersection occurs outside of the segments
                    mode = LineSegmentIntersectEnum.IntersectedOutsideSegments;
                    return false;
                }
            }

            // Line segments are real 3D lines
            {
                // on X-Y
                double s1 = (b*h - g*d) / (b*c - a*d);
                double t1 = (g*c - h*a) / (b*c - a*d);

                // on X-Z
                double s2 = (b * i - g * f) / (b * e - a * f);
                double t2 = (g * e - i * a) / (b * e - a * f);
            
                // on Y-Z
                double s3 = (d * i - h * f) / (d * e - c * f);
                double t3 = (e * h - i * c) / (d * e - c * f);

                // When the result of calculation gives infinity or NaN, the line is somewhat aligned, so it can hav any value. Set it here to be the same with one other
                if (double.IsInfinity(s1) || double.IsNaN(s1))
                {
                    if (double.IsInfinity(s2) || double.IsNaN(s2))
                        if (double.IsInfinity(s3) || double.IsNaN(s3))
                            s1 = 0.0;
                        else
                            s1 = s3;
                    else
                        s1 = s2;
                }

                if (double.IsInfinity(s2) || double.IsNaN(s2))
                {
                    if (double.IsInfinity(s1) || double.IsNaN(s1))
                        if (double.IsInfinity(s3) || double.IsNaN(s3))
                            s2 = 0.0;
                        else
                            s2 = s3;
                    else
                        s2 = s1;
                }

                if (double.IsInfinity(s3) || double.IsNaN(s3))
                {
                    if (double.IsInfinity(s1) || double.IsNaN(s1))
                        if (double.IsInfinity(s2) || double.IsNaN(s2))
                            s3 = 0.0;
                        else
                            s3 = s2;
                    else
                        s3 = s1;
                }

                // When the result of calculation gives infinity or NaN, the line is somewhat aligned, so it can hav any value. Set it here to be the same with one other
                if (double.IsInfinity(t1) || double.IsNaN(t1))
                {
                    if (double.IsInfinity(t2) || double.IsNaN(t2))
                        if (double.IsInfinity(t3) || double.IsNaN(t3))
                            t1 = 0.0;
                        else
                            t1 = t3;
                    else
                        t1 = t2;
                }

                if (double.IsInfinity(t2) || double.IsNaN(t2))
                {
                    if (double.IsInfinity(t1) || double.IsNaN(t1))
                        if (double.IsInfinity(t3) || double.IsNaN(t3))
                            t2 = 0.0;
                        else
                            t2 = t3;
                    else
                        t2 = t1;
                }

                if (double.IsInfinity(t3) || double.IsNaN(t3))
                {
                    if (double.IsInfinity(t1) || double.IsNaN(t1))
                        if (double.IsInfinity(t2) || double.IsNaN(t2))
                            t3 = 0.0;
                        else
                            t3 = t2;
                    else
                        t3 = t1;
                }

                // Lines intersect when s1=s2 and t1=t2 and als s1=s3 and t1=t3
                if (MathUtils.equalTol(s1, s2, MathUtils.defaultTol) && MathUtils.equalTol(t1, t2, MathUtils.defaultTol) && MathUtils.equalTol(s1, s3, MathUtils.defaultTol) && MathUtils.equalTol(t1, t3, MathUtils.defaultTol))
                {
                    // calculate intersection point
                    ip.X = (1 - s1) * L1.startPoint.X + s1 * L1.endPoint.X;
                    ip.Y = (1 - s1) * L1.startPoint.Y + s1 * L1.endPoint.Y;
                    ip.Z = (1 - s1) * L1.startPoint.Z + s1 * L1.endPoint.Z;
                    intersectionPoint = ip;

                    if ((0 <= s1 && s1 <= 1) && (0 <= t1 && t1 <= 1))
                    {
                        // If the segments intersect s and t have to be between 0 and 1
                        mode = LineSegmentIntersectEnum.IntersectedWithinSegments;
                        return true;
                    }
                    else
                    {
                        // Lines intersect but intersection occurs outside of the segments
                        mode = LineSegmentIntersectEnum.IntersectedOutsideSegments;
                        return false;
                    }
                }
            }
            return false;
        }

        public override string ToString()
        {
            string pr = "Baseline:\n" + this.baseLine.ToString() + "\nStartpoint" + this.startPoint.ToString() + "\nEndpoint" + this.endPoint.ToString();
            return pr;
        }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(new LineSegment3DLW(this));
        }

        #region Operators

        public override int GetHashCode()
        {
            // Uses the precision set in MathUtils to round the values so that the HashCode will be consistent with the Equals method
            double sX = Math.Round(this._pStart.X, MathUtils._doubleDecimalPrecision);
            double sY = Math.Round(this._pStart.Y, MathUtils._doubleDecimalPrecision);
            double sZ = Math.Round(this._pStart.Z, MathUtils._doubleDecimalPrecision);
            Point3D start = new Point3D(sX, sY, sZ);

            double eX = Math.Round(this._pEnd.X, MathUtils._doubleDecimalPrecision);
            double eY = Math.Round(this._pEnd.Y, MathUtils._doubleDecimalPrecision);
            double eZ = Math.Round(this._pEnd.Z, MathUtils._doubleDecimalPrecision);
            Point3D end = new Point3D(eX, eY, eZ);

            return start.GetHashCode() ^ end.GetHashCode();
            // return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        }

        /// <summary>
        /// Equality test. Use EqualTol instead of the exact comparison
        /// </summary>
        /// <param name="ob"></param>
        /// <returns></returns>
        public override bool Equals(object ob)
        {
            if (ob is LineSegment3D)
            {
                LineSegment3D li = ob as LineSegment3D;
                return (MathUtils.equalTol(_pStart.X, li._pStart.X) && MathUtils.equalTol(_pStart.Y, li._pStart.Y) && MathUtils.equalTol(_pStart.Z, li._pStart.Z)
                    && MathUtils.equalTol(_pEnd.X, li._pEnd.X) && MathUtils.equalTol(_pEnd.Y, li._pEnd.Y) && MathUtils.equalTol(_pEnd.Z, li._pEnd.Z));
            }
            else
                return false;
        }
        #endregion
    }
}