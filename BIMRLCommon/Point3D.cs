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
using Newtonsoft.Json;

namespace BIMRL.Common
{
    public class Point3DLW
    {
        public double x { get; set; }
        public double y { get; set; }
        public double z { get; set; }
        public Point3DLW(Point3D p)
        {
            x = p.X;
            y = p.Y;
            z = p.Z;
        }
    }

    public class Point3D 
    {
        private double _X;
        private double _Y;
        private double _Z;
        private double _W;

        public Point3D()
        {
            _X = 0.0;
            _Y = 0.0;
            _Z = 0.0;
            _W = 1.0;
        }

        public Point3D(float x, float y, float z)
        {
            _X = (double) x; _Y = (double) y; _Z = (double) z;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public Point3D(double x, double y, double z)
        {
            _X = x; _Y = y; _Z = z;
        }

        public Point3D(Point3D P)
        {
            _X = P.X;
            _Y = P.Y;
            _Z = P.Z;
            _W = P.W;
        }

        public double X
        {
            get { return _X; }
            set { _X = value; }
        }

        public double Y
        {
            get { return _Y; }
            set { _Y = value; }
        }

        public double Z
        {
            get { return _Z; }
            set { _Z = value; }
        }

        public double W
        {
            get { return _W; }
            set { _W = value; }
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}, {2}]", _X, _Y, _Z);
        }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(new Point3DLW(this));
        }

        public override bool Equals(object ob)
        {
            if (ob is Point3D)
            {
                Point3D v = (Point3D)ob;
                return (MathUtils.equalTol(_X, v._X) && MathUtils.equalTol(_Y, v._Y) && MathUtils.equalTol(_Z, v._Z));
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            // Uses the precision set in MathUtils to round the values so that the HashCode will be consistent with the Equals method
            double X = Math.Round(_X, MathUtils._doubleDecimalPrecision);
            double Y = Math.Round(_Y, MathUtils._doubleDecimalPrecision);
            double Z = Math.Round(_Z, MathUtils._doubleDecimalPrecision);

            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
            //return _X.GetHashCode() ^ _Y.GetHashCode() ^ _Z.GetHashCode();
        }

        public static bool operator != (Point3D p1, Point3D p2)
        {
            return !p1.Equals(p2);
        }

        public static bool operator ==(Point3D p1, Point3D p2)
        {
            return p1.Equals(p2);
        }

        public static Point3D operator +(Point3D p, Vector3D v)
        {
            return Point3D.Add(p, v);
        }
        /// <summary>
        /// Adds a Point3D structure to a Vector3D and returns the result as a Point3D structure.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static Point3D Add(Point3D p, Vector3D v)
        {
            return new Point3D (p.X + v.X,
                                    p.Y + v.Y,
                                    p.Z + v.Z
                                    );
        }

        public static Point3D operator *(Point3D p, Matrix3D m)
        {
            return Point3D.Multiply(p, m);
        }

        public static Point3D Multiply(Point3D p, Matrix3D m)
        {
            var x = p.X;
            var y = p.Y; 
            var z = p.Z;
            return new Point3D(m.M[0,0] * x + m.M[1,0] * y + m.M[2,0] * z + m.M[3,0],
                                   m.M[0,1] * x + m.M[1,1] * y + m.M[2,1] * z + m.M[3,1],
                                   m.M[0,2] * x + m.M[1,2] * y + m.M[2,2] * z + m.M[3,2]
                                  );
        }
        public static Vector3D operator -(Point3D a, Point3D b)
        {
            return Point3D.Subtract(a, b);
        }
        public static Point3D operator -(Point3D a, Vector3D b)
        {
            return new Point3D(a.X - b.X,
                                    a.Y - b.Y,
                                    a.Z - b.Z);
        }
        public static Vector3D Subtract(Point3D a, Point3D b)
        {
            return new Vector3D(a.X - b.X,
                                    a.Y - b.Y,
                                    a.Z - b.Z);
        }

        public double Length()
        {
            return (double)Math.Sqrt(_X * _X + _Y * _Y + _Z * _Z);
        }

        // Apply a transformation to a point:
        public void Transform(Matrix3D m)
        {
            double[] result = m.VectorMultiply(new double[4] { _X, _Y, _Z, _W });
            _X = result[0];
            _Y = result[1];
            _Z = result[2];
            _W = result[3];
        }

        public void TransformNormalize(Matrix3D m)
        {
            double[] result = m.VectorMultiply(new double[4] { _X, _Y, _Z, _W });
            _X = result[0] / result[3];
            _Y = result[1] / result[3];
            _Z = result[2];
            _W = 1;
        }

        /// <summary>
        /// Calculate distance between 2 points in 3D space
        /// </summary>
        /// <param name="P1"></param>
        /// <param name="P2"></param>
        /// <returns></returns>
        public static double distance(Point3D P1, Point3D P2)
        {
            return Math.Sqrt(Math.Pow((P2.X - P1.X), 2) + Math.Pow((P2.Y - P1.Y),2) + Math.Pow((P2.Z - P1.Z),2));
        }
        
        /// <summary>
        /// Calculate square distance between 2 points. This is an optimized version than distance since it does not do sqrt. Suitable if we need to make comparison and not getting the actual distance.
        /// </summary>
        /// <param name="P1"></param>
        /// <param name="P2"></param>
        /// <returns></returns>
        public static double sqDistance(Point3D P1, Point3D P2)
        {
            return (Math.Pow((P2.X - P1.X), 2) + Math.Pow((P2.Y - P1.Y), 2) + Math.Pow((P2.Z - P1.Z), 2));
        }
    }
}
