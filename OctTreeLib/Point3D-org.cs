using System;
using System.Collections.Generic;
using System.Text;

namespace com.invicara.empire.EmpireBIMDataTile.OctTreeLib
{
    public class Point3D
    {
        public float X;
        public float Y;
        public float Z;
        public float W = 1f;

        public readonly static Point3D Zero = new Point3D(0, 0, 0);

        public Point3D()
        {

        }

        public Point3D(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Point3D(double x, double y, double z)
        {
            X = (float) x;
            Y = (float) y;
            Z = (float) z;
        }

        public float Length()
        {
            return (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        // Apply a transformation to a point:
        public void Transform(Matrix3D m)
        {
            float[] result = m.VectorMultiply(new float[4] { X, Y, Z, W });
            X = result[0];
            Y = result[1];
            Z = result[2];
            W = result[3];
        }

        public void TransformNormalize(Matrix3D m)
        {
            float[] result = m.VectorMultiply(new float[4] { X, Y, Z, W });
            X = result[0] / result[3];
            Y = result[1] / result[3];
            Z = result[2];
            W = 1;
        }
    }
}
