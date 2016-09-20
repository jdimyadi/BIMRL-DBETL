using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Xbim.Common.Geometry
{
    public struct XbimPoint3D 
    {
        public float X;
        public float Y;
        public float Z;

        public readonly static XbimPoint3D Zero;

        static XbimPoint3D()
        {
            Zero = new XbimPoint3D(0, 0, 0);
        }
        public XbimPoint3D(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
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

        /// <summary>
        /// Rounds the coordinates from double to float, some accuracy may be lost
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        public XbimPoint3D(double x, double y, double z)
        {
            X = (float)x; Y = (float)y; Z = (float)z;
        }

        

        public override string ToString()
        {
            return string.Format("[{0}, {1}, {2}]", X, Y, Z);
        }



        public override bool Equals(object ob)
        {
            if (ob is XbimPoint3D)
            {
                XbimPoint3D v = (XbimPoint3D)ob;
                return (X == v.X && Y == v.Y && Z == v.Z);
            }
            else
                return false;
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
        }


        public static XbimPoint3D operator +(XbimPoint3D p, XbimVector3D v)
        {
            return XbimPoint3D.Add(p, v);
        }
        /// <summary>
        /// Adds a XbimPoint3D structure to a XbimVector3D and returns the result as a XbimPoint3D structure.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static XbimPoint3D Add(XbimPoint3D p, XbimVector3D v)
        {
            return new XbimPoint3D (p.X + v.X,
                                    p.Y + v.Y,
                                    p.Z + v.Z
                                    );
        }

        public static XbimPoint3D operator *(XbimPoint3D p, XbimMatrix3D m)
        {
            return XbimPoint3D.Multiply(p, m);
        }

        public static XbimPoint3D Multiply(XbimPoint3D p, XbimMatrix3D m)
        {
            var x = p.X;
            var y = p.Y; 
            var z = p.Z;
            return new XbimPoint3D(m.M11 * x + m.M21 * y + m.M31 * z + m.OffsetX,
                                   m.M12 * x + m.M22 * y + m.M32 * z + m.OffsetY,
                                   m.M13 * x + m.M23 * y + m.M33 * z + m.OffsetZ
                                  );
        }
        public static XbimVector3D operator -(XbimPoint3D a, XbimPoint3D b)
        {
            return XbimPoint3D.Subtract(a, b);
        }
        public static XbimPoint3D operator -(XbimPoint3D a, XbimVector3D b)
        {
            return new XbimPoint3D(a.X - b.X,
                                    a.Y - b.Y,
                                    a.Z - b.Z);
        }
        public static XbimVector3D Subtract(XbimPoint3D a, XbimPoint3D b)
        {
            return new XbimVector3D(a.X - b.X,
                                    a.Y - b.Y,
                                    a.Z - b.Z);
        }




        
    }
}
