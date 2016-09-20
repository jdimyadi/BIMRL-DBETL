using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BIMRL.OctreeLib
{
    // Matrix3D is actually matrix 4x4 that is common in geometry that includes translation
    // Note that this Matrix is ROW MAJOR, i.e. 
    /*      M[0,0] M [0,1] M[0,2] M[0,3]                X1 Y1 Z1 W1
     *      M[1,0] M [1,1] M[1,2] M[1,3]                X2 Y2 Z2 W2
     *      M[2,0] M [2,1] M[2,2] M[2,3]       =        X3 Y3 Z3 W3
     *      M[3,0] M [3,1] M[3,2] M[3,3]                X4 Y4 Z4 W4
     *      
     * But in some cases, e.g. PricipalComponentAnalysis (PCA), it expects a column major matrix
     *      X1 X2 X3 X4
     *      Y1 Y2 Y3 Y4
     *      Z1 Z2 Z3 Z4
     *      W1 W2 W3 W4
     */

    public class Matrix3D
    {
        public double[,] M = new double[4, 4];

        public Matrix3D()
        {
            Identity3();
        }

        public Matrix3D(float m00, float m01, float m02, float m03,
                       float m10, float m11, float m12, float m13,
                       float m20, float m21, float m22, float m23,
                       float m30, float m31, float m32, float m33)
        {
            M[0, 0] = (double)m00;
            M[0, 1] = (double)m01;
            M[0, 2] = (double)m02;
            M[0, 3] = (double)m03;
            M[1, 0] = (double)m10;
            M[1, 1] = (double)m11;
            M[1, 2] = (double)m12;
            M[1, 3] = (double)m13;
            M[2, 0] = (double)m20;
            M[2, 1] = (double)m21;
            M[2, 2] = (double)m22;
            M[2, 3] = (double)m23;
            M[3, 0] = (double)m30;
            M[3, 1] = (double)m31;
            M[3, 2] = (double)m32;
            M[3, 3] = (double)m33;
        }

        public Matrix3D(double m00, double m01, double m02, double m03,
                       double m10, double m11, double m12, double m13,
                       double m20, double m21, double m22, double m23,
                       double m30, double m31, double m32, double m33)
        {
            M[0, 0] = m00;
            M[0, 1] = m01;
            M[0, 2] = m02;
            M[0, 3] = m03;
            M[1, 0] = m10;
            M[1, 1] = m11;
            M[1, 2] = m12;
            M[1, 3] = m13;
            M[2, 0] = m20;
            M[2, 1] = m21;
            M[2, 2] = m22;
            M[2, 3] = m23;
            M[3, 0] = m30;
            M[3, 1] = m31;
            M[3, 2] = m32;
            M[3, 3] = m33;
        }

        public Matrix3D(Matrix3D mInput)
        {
            M = mInput.M;
        }

/// <summary>
///  Define a Identity matrix:
/// </summary>
        public void Identity3()
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    if (i == j)
                    {
                        M[i, j] = 1;
                    }
                    else
                    {
                        M[i, j] = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Multiply two matrices together
        /// </summary>
        /// <param name="m1"></param>
        /// <param name="m2"></param>
        /// <returns></returns>
        public static Matrix3D operator *(Matrix3D m1, Matrix3D m2)
        {
            Matrix3D result = new Matrix3D();
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    double element = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        element += m1.M[i, k] * m2.M[k, j];
                    }
                    result.M[i, j] = element;
                }
            }
            return result;
        }

        /// <summary>
        /// Apply a transformation to a vector (point)
        /// </summary>
        /// <param name="vector"></param>
        /// <returns></returns>
        public double[] VectorMultiply(double[] vector)
        {
            double[] result = new double[4];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[i] += M[i, j] * vector[j];
                }
            }
            return result;
        }

        /// <summary>
        /// Transform a point with a Matrix
        /// </summary>
        /// <param name="p">input point</param>
        /// <returns>transformed point</returns>
        public Point3D Transform(Point3D p)
        {
            double[] result = new double[4];
            double[] input = new double[4];
            input[0] = p.X;
            input[1] = p.Y;
            input[2] = p.Z;
            input[3] = 1;

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[i] += M[i, j] * input[j];
                }
            }

            Point3D resP = new Point3D(result[0], result[1], result[2]);
            return resP;
        }

        /// <summary>
        /// Create a scaling matrix
        /// </summary>
        /// <param name="sx"></param>
        /// <param name="sy"></param>
        /// <param name="sz"></param>
        /// <returns></returns>
        public static Matrix3D Scale3(double sx, double sy, double sz)
        {
            Matrix3D result = new Matrix3D();
            result.M[0, 0] = sx;
            result.M[1, 1] = sy;
            result.M[2, 2] = sz;
            return result;
        }

        /// <summary>
        /// Create a translation matrix
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        /// <param name="dz"></param>
        /// <returns></returns>
        public static Matrix3D Translate3(double dx, double dy, double dz)
        {
            Matrix3D result = new Matrix3D();
            result.M[0, 3] = dx;
            result.M[1, 3] = dy;
            result.M[2, 3] = dz;
            return result;
        }

        /// <summary>
        /// Create a rotation matrix around the x axis
        /// </summary>
        /// <param name="theta"></param>
        /// <returns></returns>
        public static Matrix3D Rotate3X(double theta)
        {
            theta = theta * (double)Math.PI / 180.0f;
            double sn = (double)Math.Sin(theta);
            double cn = (double)Math.Cos(theta);
            Matrix3D result = new Matrix3D();
            result.M[1, 1] = cn;
            result.M[1, 2] = -sn;
            result.M[2, 1] = sn;
            result.M[2, 2] = cn;
            return result;
        }

        /// <summary>
        /// Create a rotation matrix around the y axis
        /// </summary>
        /// <param name="theta"></param>
        /// <returns></returns>
        public static Matrix3D Rotate3Y(double theta)
        {
            theta = theta * (double)Math.PI / 180.0f;
            double sn = (double)Math.Sin(theta);
            double cn = (double)Math.Cos(theta);
            Matrix3D result = new Matrix3D();
            result.M[0, 0] = cn;
            result.M[0, 2] = sn;
            result.M[2, 0] = -sn;
            result.M[2, 2] = cn;
            return result;
        }

        /// <summary>
        /// Create a rotation matrix around the z axis
        /// </summary>
        /// <param name="theta"></param>
        /// <returns></returns>
        public static Matrix3D Rotate3Z(double theta)
        {
            theta = theta * (double)Math.PI / 180.0f;
            double sn = (double)Math.Sin(theta);
            double cn = (double)Math.Cos(theta);
            Matrix3D result = new Matrix3D();
            result.M[0, 0] = cn;
            result.M[0, 1] = -sn;
            result.M[1, 0] = sn;
            result.M[1, 1] = cn;
            return result;
        }

        /// <summary>
        /// Front view projection matrix
        /// </summary>
        /// <returns></returns>
        public static Matrix3D FrontView()
        {
            Matrix3D result = new Matrix3D();
            result.M[2, 2] = 0;
            return result;
        }

        /// <summary>
        /// Side view projection matrix
        /// </summary>
        /// <returns></returns>
        public static Matrix3D SideView()
        {
            Matrix3D result = new Matrix3D();
            result.M[0, 0] = 0;
            result.M[2, 2] = 0;
            result.M[0, 2] = -1;
            return result;
        }

        /// <summary>
        /// Top view projection matrix
        /// </summary>
        /// <returns></returns>
        public static Matrix3D TopView()
        {
            Matrix3D result = new Matrix3D();
            result.M[1, 1] = 0;
            result.M[2, 2] = 0;
            result.M[1, 2] = -1;
            return result;
        }

        /// <summary>
        /// Axonometric projection matrix
        /// </summary>
        /// <param name="alpha"></param>
        /// <param name="beta"></param>
        /// <returns></returns>
        public static Matrix3D Axonometric(double alpha, double beta)
        {
            Matrix3D result = new Matrix3D();
            double sna = (double)Math.Sin(alpha * Math.PI / 180);
            double cna = (double)Math.Cos(alpha * Math.PI / 180);
            double snb = (double)Math.Sin(beta * Math.PI / 180);
            double cnb = (double)Math.Cos(beta * Math.PI / 180);
            result.M[0, 0] = cnb;
            result.M[0, 2] = snb;
            result.M[1, 0] = sna * snb;
            result.M[1, 1] = cna;
            result.M[1, 2] = -sna * cnb;
            result.M[2, 2] = 0;
            return result;
        }

        /// <summary>
        /// Oblique projection matrix
        /// </summary>
        /// <param name="alpha"></param>
        /// <param name="theta"></param>
        /// <returns></returns>
        public static Matrix3D Oblique(double alpha, double theta)
        {
            Matrix3D result = new Matrix3D();
            double ta = (double)Math.Tan(alpha * Math.PI / 180);
            double snt = (double)Math.Sin(theta * Math.PI / 180);
            double cnt = (double)Math.Cos(theta * Math.PI / 180);
            result.M[0, 2] = -cnt / ta;
            result.M[1, 2] = -snt / ta;
            result.M[2, 2] = 0;
            return result;
        }

        /// <summary>
        /// Perspective projection matrix
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public static Matrix3D Perspective(double d)
        {
            Matrix3D result = new Matrix3D();
            result.M[3, 2] = -1 / d;
            return result;
        }

        public Point3D Cylindrical(double r, double theta, double y)
        {
            Point3D pt = new Point3D();
            double sn = (double)Math.Sin(theta * Math.PI / 180);
            double cn = (double)Math.Cos(theta * Math.PI / 180);
            pt.X = r * cn;
            pt.Y = y;
            pt.Z = -r * sn;
            pt.W = 1;
            return pt;
        }

        public Point3D Spherical(double r, double theta, double phi)
        {
            Point3D pt = new Point3D();
            double snt = (double)Math.Sin(theta * Math.PI / 180);
            double cnt = (double)Math.Cos(theta * Math.PI / 180);
            double snp = (double)Math.Sin(phi * Math.PI / 180);
            double cnp = (double)Math.Cos(phi * Math.PI / 180);
            pt.X = r * snt * cnp;
            pt.Y = r * cnt;
            pt.Z = -r * snt * snp;
            pt.W = 1;
            return pt;
        }

        public static Matrix3D Euler(double alpha, double beta, double gamma)
        {
            Matrix3D result = new Matrix3D();
            alpha = alpha * (double)Math.PI / 180.0f;
            double sna = (double)Math.Sin(alpha);
            double cna = (double)Math.Cos(alpha);
            beta = beta * (double)Math.PI / 180.0f;
            double snb = (double)Math.Sin(beta);
            double cnb = (double)Math.Cos(beta);
            gamma = gamma * (double)Math.PI / 180.0f;
            double sng = (double)Math.Sin(gamma);
            double cng = (double)Math.Cos(gamma);
            result.M[0, 0] = cna * cng - sna * snb * sng;
            result.M[0, 1] = -snb * sng;
            result.M[0, 2] = sna * cng - cna * cnb * sng;
            result.M[1, 0] = -sna * snb;
            result.M[1, 1] = cnb;
            result.M[1, 2] = cna * snb;
            result.M[2, 0] = -cna * sng - sna * cnb * cng;
            result.M[2, 1] = -snb * cng;
            result.M[2, 2] = cna * cnb * cng - sna * snb;
            return result;
        }

        public static Matrix3D AzimuthElevation(double elevation, double azimuth, double oneOverd)
        {
            Matrix3D result = new Matrix3D();
            Matrix3D rotate = new Matrix3D();

            // make sure elevation in the range of [-90, 90]:
            if (elevation > 90)
                elevation = 90;
            else if (elevation < -90)
                elevation = -90;
            // Make sure azimuth in the range of [-180, 180]:
            if (azimuth > 180)
                azimuth = 180;
            else if (azimuth < -180)
                azimuth = -180;

            elevation = elevation * (double)Math.PI / 180.0f;
            double sne = (double)Math.Sin(elevation);
            double cne = (double)Math.Cos(elevation);
            azimuth = azimuth * (double)Math.PI / 180.0f;
            double sna = (double)Math.Sin(azimuth);
            double cna = (double)Math.Cos(azimuth);
            rotate.M[0, 0] = cna;
            rotate.M[0, 1] = sna;
            rotate.M[0, 2] = 0;
            rotate.M[1, 0] = -sne * sna;
            rotate.M[1, 1] = sne * cna;
            rotate.M[1, 2] = cne;
            rotate.M[2, 0] = cne * sna;
            rotate.M[2, 1] = -cne * cna;
            rotate.M[2, 2] = sne;

            if (oneOverd <= 0)
                result = rotate;
            else if (oneOverd > 0)
            {
                Matrix3D perspective = Matrix3D.Perspective(1 / oneOverd);
                result = perspective * rotate;
            }
            return result;
        }

        public Vector3D Up
        {
            get { return new Vector3D(M[1, 0], M[1, 1], M[1, 2]); }
        }

        public Vector3D Down
        {
            get { return new Vector3D(-M[1, 0], -M[1, 1], -M[1, 2]); }
        }

        public Vector3D Right
        {
            get { return new Vector3D(M[0, 0], M[0, 1], M[0, 2]); }
        }

        public Vector3D Left
        {
            get { return new Vector3D(-M[0, 0], -M[0, 1], -M[0, 2]); }
        }

        public Vector3D Forward
        {
            get { return new Vector3D(-M[2, 0], -M[2, 1], -M[2, 2]); }
        }
        public Vector3D Backward
        {
            get { return new Vector3D(M[2, 0], M[2, 1], M[2, 2]); }
        }

        public Vector3D Translation
        {
            get { return new Vector3D(M[3, 0], M[3, 1], M[3, 2]); }
        }

        public override string ToString()
        {
            string outStr = "";
            for (int i = 0; i < 4; i++)
            {
                outStr += String.Format("{0:n4} {1:n4} {2:n4} {3:n4}\n", this.M[i, 0], this.M[i, 1], this.M[i, 2], this.M[i, 3]);
            }
            return outStr;
        }

        public Matrix3D inverse()
        {
            Matrix3x3 m3 = new Matrix3x3(M[0, 0], M[0, 1], M[0, 2],
                                           M[1, 0], M[1, 1], M[1, 2],
                                            M[2, 0], M[2, 1], M[2, 2]);
            Matrix3x3 inv = m3.inverse;
            Matrix3D inverseM = new Matrix3D(inv.M[0, 0], inv.M[0, 1], inv.M[0, 2], -M[0, 3],
                                            inv.M[1, 0], inv.M[1, 1], inv.M[1, 2], -M[1, 3],
                                            inv.M[2, 0], inv.M[2, 1], inv.M[2, 2], -M[2, 3],
                                            0, 0, 0, 1);
            return inverseM;
        }
    }
}
