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

namespace BIMRL.Common
{
    public class Matrix3x3
    {
        public double[,] M = new double[3, 3];

        public Matrix3x3()
        {
            identity();
        }

        public Matrix3x3(float m00, float m01, float m02,
                       float m10, float m11, float m12,
                       float m20, float m21, float m22)
        {
            M[0, 0] = (double)m00;
            M[0, 1] = (double)m01;
            M[0, 2] = (double)m02;
            M[1, 0] = (double)m10;
            M[1, 1] = (double)m11;
            M[1, 2] = (double)m12;
            M[2, 0] = (double)m20;
            M[2, 1] = (double)m21;
            M[2, 2] = (double)m22;
        }

        public Matrix3x3(double m00, double m01, double m02,
                       double m10, double m11, double m12,
                       double m20, double m21, double m22)
        {
            M[0, 0] = m00;
            M[0, 1] = m01;
            M[0, 2] = m02;
            M[1, 0] = m10;
            M[1, 1] = m11;
            M[1, 2] = m12;
            M[2, 0] = m20;
            M[2, 1] = m21;
            M[2, 2] = m22;
        }

        // Define a Identity matrix:
        public void identity()
        {
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
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

        // Multiply two matrices together:
        public static Matrix3x3 operator *(Matrix3x3 m1, Matrix3x3 m2)
        {
            Matrix3x3 result = new Matrix3x3();
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double element = 0;
                    for (int k = 0; k < 3; k++)
                    {
                        element += m1.M[i, k] * m2.M[k, j];
                    }
                    result.M[i, j] = element;
                }
            }
            return result;
        }

        public double determinant
        {
            get
            {
                return (M[0,0] * (M[1,1]*M[2,2] - M[1,2]*M[2,1]) 
                       - M[0,1] * (M[1,0]*M[2,2] - M[1,2]*M[2,0])
                        + M[0,2] * (M[1,0]*M[2,1] - M[1,1]*M[2,0]));
            }
        }

        public Matrix3x3 inverse
        {
            get
            {
                Matrix3x3 inv = new Matrix3x3();

                // calculate the minor matrix determinant and transpose them
                inv.M[0,0] = (M[1,1]*M[2,2]-M[1,2]*M[2,1])/determinant;
                inv.M[1,0] = -(M[1,0]*M[2,2]-M[1,2]*M[2,0])/determinant;
                inv.M[2,0] = (M[1,0]*M[2,1]-M[1,1]*M[2,0])/determinant;
                inv.M[0,1] = -(M[0,1]*M[2,2]-M[0,2]*M[2,1])/determinant;
                inv.M[1,1] = (M[0,0]*M[2,2]-M[0,2]*M[2,0])/determinant;
                inv.M[2,1] = -(M[0,0]*M[2,1]-M[0,1]*M[2,0])/determinant;
                inv.M[0,2] = (M[0,1]*M[1,2]-M[0,2]*M[1,1])/determinant;
                inv.M[1,2] = -(M[0,0]*M[1,2]-M[0,2]*M[1,0])/determinant;
                inv.M[2,2] = (M[0,0]*M[1,1]-M[0,1]*M[1,0])/determinant;

                return inv;
            }
        }
        public override string ToString()
        {
            string outStr = "";
            for (int i = 0; i < 3; i++)
            {
                outStr += String.Format("{0:n4} {1:n4} {2:n4}\n", this.M[i, 0], this.M[i, 1], this.M[i, 2]);
            }
            return outStr;
        }

    }
}
