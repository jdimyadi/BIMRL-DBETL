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

   /// <summary>
   /// Custom IEqualityComparer for a vector with tolerance for use with Dictionary
   /// </summary>
   public class vectorCompare : IEqualityComparer<Vector3D>
   {
      double _tol;
      int _tolNoDecPrecision;

      public vectorCompare(double tol, int doubleDecimalPrecision)
      {
         _tol = tol;
         _tolNoDecPrecision = doubleDecimalPrecision;
      }

      public bool Equals(Vector3D o1, Vector3D o2)
      {
         bool xdiff = Math.Abs(o1.X - o2.X) < _tol;
         bool ydiff = Math.Abs(o1.Y - o2.Y) < _tol;
         bool zdiff = Math.Abs(o1.Z - o2.Z) < _tol;
         if (xdiff && ydiff && zdiff)
            return true;
         else
            return false;
      }

      public int GetHashCode(Vector3D obj)
      {
         // Uses the precision set in MathUtils to round the values so that the HashCode will be consistent with the Equals method
         double X = Math.Round(obj.X, _tolNoDecPrecision);
         double Y = Math.Round(obj.Y, _tolNoDecPrecision);
         double Z = Math.Round(obj.Z, _tolNoDecPrecision);

         return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
      }
   }

   public class Vector3D
    {
        private double _X;
        private double _Y;
        private double _Z;

        /// <summary>
        /// Defining a Vector in 3D
        /// </summary>
        public Vector3D()
        {
            _X = 0.0;
            _Y = 0.0;
            _Z = 0.0;
        }

        public Vector3D(Vector3D vectorInput)
        {
            _X = vectorInput.X;
            _Y = vectorInput.Y;
            _Z = vectorInput.Z;
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

        /// <summary>
        /// Length of the vector
        /// </summary>
        public double Length 
        {
            get
            {
                return length();
            }
           
        }

        /// <summary>
        /// Calculation of the vector length
        /// </summary>
        /// <returns></returns>
        private double length()
        {
           return (Single)Math.Sqrt(_X * _X + _Y * _Y + _Z * _Z);
        }

        /// <summary>
        /// Vector3D uses double internally, but accepts float as an input too
        /// </summary>
        /// <param name="vx"></param>
        /// <param name="vy"></param>
        /// <param name="vz"></param>
        public Vector3D(float vx, float vy, float vz)
        {
            _X = (double)vx;
            _Y = (double)vy;
            _Z = (double)vz;
        }

        public Vector3D(double vx, double vy, double vz)
        {
            _X = vx;
            _Y = vy;
            _Z = vz;
        }

        public Vector3D(double v)
        {
            _X = v;
            _Y = v;
            _Z = v;
        }

        /// <summary>
        /// Returning the minimum of the combination of two vectors
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        static public Vector3D Min(Vector3D a, Vector3D b)
        {
            return new Vector3D(
                (a._X < b._X) ? a._X : b._X,
                (a._Y < b._Y) ? a._Y : b._Y,
                (a._Z < b._Z) ? a._Z : b._Z);
        }

        /// <summary>
        /// Returning the maximum of the combination of two vectors
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        static public Vector3D Max(Vector3D a, Vector3D b)
        {
            return new Vector3D(
                (a._X > b._X) ? a._X : b._X,
                (a._Y > b._Y) ? a._Y : b._Y,
                (a._Z > b._Z) ? a._Z : b._Z);
        }

        #region Operators

        public override int GetHashCode()
        {
            // Uses the precision set in MathUtils to round the values so that the HashCode will be consistent with the Equals method
            double X = Math.Round(_X, MathUtils._doubleDecimalPrecision);
            double Y = Math.Round(_Y, MathUtils._doubleDecimalPrecision);
            double Z = Math.Round(_Z, MathUtils._doubleDecimalPrecision);

            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        }

        /// <summary>
        /// Equality test. Use EqualTol instead of the exact comparison
        /// </summary>
        /// <param name="ob"></param>
        /// <returns></returns>
        public override bool Equals(object ob)
        {
            if (ob is Vector3D)
            {
                Vector3D v = (Vector3D)ob;
                return (MathUtils.equalTol(_X, v._X, MathUtils.defaultTol) && MathUtils.equalTol(_Y, v._Y, MathUtils.defaultTol) && MathUtils.equalTol(_Z, v._Z, MathUtils.defaultTol));
            }
            else
                return false;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ob"></param>
        /// <returns></returns>
        public static bool Parallels(Vector3D vec, object ob)
        {
            if (ob is Vector3D)
            {
                Vector3D v = (Vector3D)ob;
                Vector3D negV = new Vector3D(v.X, v.Y, v.Z);
                negV.Negate();
                return ((MathUtils.equalTol(vec.X, v._X, MathUtils.defaultTol) && MathUtils.equalTol(vec.Y, v._Y, MathUtils.defaultTol) && MathUtils.equalTol(vec.Z, v._Z, MathUtils.defaultTol))
                        || (MathUtils.equalTol(vec.X, negV._X, MathUtils.defaultTol) && MathUtils.equalTol(vec.Y, negV._Y, MathUtils.defaultTol) && MathUtils.equalTol(vec.Z, negV._Z, MathUtils.defaultTol)));
            }
            else
                return false;
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}, {2}]", _X, _Y, _Z);
        }

        public static bool operator !=(Vector3D v1, Vector3D v2)
        {
            return !v1.Equals(v2);
        }

        public static bool operator ==(Vector3D v1, Vector3D v2)
        {
            return v1.Equals(v2);
        }

        public static Vector3D operator +(Vector3D vector1, Vector3D vector2)
        {
            return new Vector3D(vector1._X + vector2._X, vector1._Y + vector2._Y, vector1._Z + vector2._Z);
        }

        public static Vector3D Add(Vector3D vector1, Vector3D vector2)
        {
            return new Vector3D(vector1._X + vector2._X, vector1._Y + vector2._Y, vector1._Z + vector2._Z);
        }

        public static Vector3D operator -(Vector3D vector1, Vector3D vector2)
        {
            return new Vector3D(vector1._X - vector2._X, vector1._Y - vector2._Y, vector1._Z - vector2._Z);
        }

        public static Vector3D Subtract(Vector3D vector1, Vector3D vector2)
        {
            return new Vector3D(vector1._X - vector2._X, vector1._Y - vector2._Y, vector1._Z - vector2._Z);
        }

        public static Vector3D operator *(double l, Vector3D v1)
        {
            return Vector3D.Multiply(l, v1);
        }

        public static Vector3D operator *(Vector3D v1, float l)
        {
            return Vector3D.Multiply((double)l, v1);
        }
        
        public static Vector3D operator *(Vector3D v1, double l)
        {
            return Vector3D.Multiply(l, v1);
        }

        public static Vector3D operator *(Vector3D v1, Matrix3D m)
        {
            return Vector3D.Multiply(v1, m);
        }

        public static Vector3D Multiply(double val, Vector3D vec)
        {
            return new Vector3D( vec._X * val, vec._Y * val, vec._Z * val);
        }
        
        public static Vector3D Multiply(Vector3D vec, Matrix3D m)
        {
            
            var x = vec._X;
            var y = vec._Y;
            var z = vec._Z;
            return new Vector3D (m.M[0,0] * x + m.M[1,0] * y + m.M[2,0] * z ,
                                     m.M[0,1] * x + m.M[1,1] * y + m.M[2,1] * z ,
                                     m.M[0,2] * x + m.M[1,2] * y + m.M[2,2] * z 
                                    );
        }


        public void Normalize()
        {
            var x = _X;
            var y = _Y;
            var z = _Z;
            var len = (Single)Math.Sqrt(x * x + y * y + z * z);

            if (len == 0)
            {
                _X = 0; _Y = 0; _Z = 0;
            }
            else if (len == 1 || len == 0)
                return; //do nothing

            len = 1 / len;
            _X = x * len;
            _Y = y * len;
            _Z = z * len;
        }

        public Vector3D CrossProduct(Vector3D v2)
        {
            return Vector3D.CrossProduct(this, v2);
        }

        public static Vector3D CrossProduct(Vector3D v1, Vector3D v2)
        {
            var x = v1._X;
            var y = v1._Y;
            var z = v1._Z;
            var x2 = v2._X;
            var y2 = v2._Y;
            var z2 = v2._Z;
            return new Vector3D(y * z2 - z * y2,
                                    z * x2 - x * z2,
                                    x * y2 - y * x2);
        }

        public void Negate()
        {
            _X = -_X;
            _Y = -_Y;
            _Z = -_Z;
        }

        public static double DotProduct(Vector3D v1, Vector3D v2)
        {
            return v1._X * v2._X + v1._Y * v2._Y + v1._Z * v2._Z;
        }

        #endregion

    }
}
