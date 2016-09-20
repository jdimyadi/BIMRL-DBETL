using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Xbim.Common.Geometry
{
    public struct XbimVector3D
    {
        public readonly static XbimVector3D Zero;

        static XbimVector3D()
        {
            Zero = new XbimVector3D(0, 0, 0);
        }
        public float X;
        public float Y;
        public float Z;
        
        public float Length 
        {
            get
            {
             return length();
            }
           
        }

        private float length()
        {
           return (Single)Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        public XbimVector3D(float vx, float vy, float vz)
        { 
            X = vx;
            Y = vy;
            Z = vz;
        }

        public XbimVector3D(double vx, double vy, double vz)
        {
            X = (float)vx;
            Y = (float)vy;
            Z = (float)vz;
        }

        public XbimVector3D(float v)
        {
            X = v;
            Y = v;
            Z = v;
        }

        public double Xd
        {
            get { return (double)X; }
            set { X = (float)value; }
        }

        public double Yd
        {
            get { return (double)Y; }
            set { Y = (float)value; }
        }

        public double Zd
        {
            get { return (double)Z; }
            set { Z = (float)value; }
        }
        
        static public XbimVector3D Min(XbimVector3D a, XbimVector3D b)
        {
            return new XbimVector3D(
                (a.X < b.X) ? a.X : b.X,
                (a.Y < b.Y) ? a.Y : b.Y,
                (a.Z < b.Z) ? a.Z : b.Z);
        }
        static public XbimVector3D Max(XbimVector3D a, XbimVector3D b)
        {
            return new XbimVector3D(
                (a.X > b.X) ? a.X : b.X,
                (a.Y > b.Y) ? a.Y : b.Y,
                (a.Z > b.Z) ? a.Z : b.Z);
        }

        #region Operators

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        }

        public override bool Equals(object ob)
        {
            if (ob is XbimVector3D)
            {
                XbimVector3D v = (XbimVector3D)ob;
                return (X == v.X && Y == v.Y && Z == v.Z);
            }
            else
                return false;
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}, {2}]", X, Y, Z);
        }

        public static bool operator !=(XbimVector3D v1, XbimVector3D v2)
        {
            return !v1.Equals(v2);
        }

        public static bool operator ==(XbimVector3D v1, XbimVector3D v2)
        {
            return v1.Equals(v2);
        }

        public static XbimVector3D operator +(XbimVector3D vector1, XbimVector3D vector2)
        {
            return new XbimVector3D(vector1.X + vector2.X, vector1.Y + vector2.Y, vector1.Z + vector2.Z);
        }

        public static XbimVector3D Add(XbimVector3D vector1, XbimVector3D vector2)
        {
            return new XbimVector3D(vector1.X + vector2.X, vector1.Y + vector2.Y, vector1.Z + vector2.Z);
        }

        public static XbimVector3D operator -(XbimVector3D vector1, XbimVector3D vector2)
        {
            return new XbimVector3D(vector1.X - vector2.X, vector1.Y - vector2.Y, vector1.Z - vector2.Z);
        }

        public static XbimVector3D Subtract(XbimVector3D vector1, XbimVector3D vector2)
        {
            return new XbimVector3D(vector1.X - vector2.X, vector1.Y - vector2.Y, vector1.Z - vector2.Z);
        }

        public static XbimVector3D operator *(float l, XbimVector3D v1)
        {
            return XbimVector3D.Multiply(l, v1);
        }

        
        public static XbimVector3D operator *(XbimVector3D v1, double l)
        {
            return XbimVector3D.Multiply((float)l, v1);
        }
        

        public static XbimVector3D operator *(XbimVector3D v1, float l)
        {
            return XbimVector3D.Multiply(l, v1);
        }
        public static XbimVector3D operator *(XbimVector3D v1, XbimMatrix3D m)
        {
            return XbimVector3D.Multiply(v1, m);
        }

        public static XbimVector3D Multiply(float val, XbimVector3D vec)
        {
            return new XbimVector3D( vec.X * val, vec.Y * val, vec.Z * val);
        }
        
        public static XbimVector3D Multiply(XbimVector3D vec, XbimMatrix3D m)
        {
            
            var x = vec.X;
            var y = vec.Y;
            var z = vec.Z;
            return new XbimVector3D (m.M11 * x + m.M21 * y + m.M31 * z ,
                                     m.M12 * x + m.M22 * y + m.M32 * z ,
                                     m.M13 * x + m.M23 * y + m.M33 * z 
                                    );
        }


        public void Normalize()
        {
            var x = X;
            var y = Y;
            var z = Z;
            var len = (Single)Math.Sqrt(x * x + y * y + z * z);

            if (len == 0)
            {
                X = 0; Y = 0; Z = 0;
            }
            else if (len == 1)
                return; //do nothing

            len = 1 / len;
            X = x * len;
            Y = y * len;
            Z = z * len;
        }
        public XbimVector3D CrossProduct(XbimVector3D v2)
        {
            return XbimVector3D.CrossProduct(this, v2);
        }
        public static XbimVector3D CrossProduct(XbimVector3D v1, XbimVector3D v2)
        {
            var x = v1.X;
            var y = v1.Y;
            var z = v1.Z;
            var x2 = v2.X;
            var y2 = v2.Y;
            var z2 = v2.Z;
            return new XbimVector3D(y * z2 - z * y2,
                                    z * x2 - x * z2,
                                    x * y2 - y * x2);
        }

        public void Negate()
        {
            X = -X;
            Y = -Y;
            Z = -Z;
        }

        public static float DotProduct(XbimVector3D v1, XbimVector3D v2)
        {
            return v1.X * v2.X + v1.Y * v2.Y + v1.Z * v2.Z;
        }

        #endregion



       
        
       
    }
}
