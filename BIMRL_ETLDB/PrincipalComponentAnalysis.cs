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
using System.Numerics;
using BIMRL.Common;
using MathNet.Numerics.Providers.LinearAlgebra;

namespace BIMRL
{
    public class PrincipalComponentAnalysis
    {
        // Polyhedron _geom;
        Point3D _centroid;
        List<Point3D> pointSet = new List<Point3D>();
        Matrix3D _transformMatrix;
        List<Point3D> OBB = new List<Point3D>();

        public PrincipalComponentAnalysis (Polyhedron geom)
        {
            //double resolution = 20;
            //projectUnit unit = DBOperation.currModelProjectUnitLength;
            //switch (unit)
            //{
            //    case projectUnit.SIUnit_Length_MilliMeter:
            //        resolution = 20;
            //        break;
            //    case projectUnit.SIUnit_Length_Meter:
            //        resolution = 0.02;
            //        break;
            //    case projectUnit.Imperial_Length_Inch:
            //        resolution = 0.8;
            //        break;
            //    case projectUnit.Imperial_Length_Foot:
            //        resolution = 0.06;
            //        break;
            //    default:
            //        break;
            //}
            //_geom = geom;
            // Use more elaborate data by sampling all edges into segmented points
            //foreach(Face3D f in _geom.Faces)
            //{
            //    foreach(LineSegment3D l in f.outerAndInnerBoundaries)
            //    {
            //        List<Point3D> ptSet = l.toPointSet(resolution);
            //        pointSet.AddRange(ptSet);
            //    }
            //}
            // Use simple data, i.e. just the list of vertices
            //pointSet.AddRange(_geom.Vertices);
            pointSet.AddRange(geom.Vertices);
        }

        public PrincipalComponentAnalysis (Face3D geom)
        {
            pointSet.AddRange(geom.vertices);
        }

        public PrincipalComponentAnalysis(List<Point3D> pointList)
        {
            pointSet.AddRange(pointList);
        }

        public Point3D Centroid
        {
            get { return _centroid; }
        }

        public Matrix3D transformMatrix
        {
            get { return _transformMatrix; }
            set { _transformMatrix = value; }
        }

        double[] componentMean()
        {
            double[] means = new double[3];

            double[] sums = new double[3];
            foreach (Point3D p in pointSet)
            {
                sums[0] += p.X;
                sums[1] += p.Y;
                sums[2] += p.Z;
            }
            means[0] = sums[0] / pointSet.Count;
            means[1] = sums[1] / pointSet.Count;
            means[2] = sums[2] / pointSet.Count;
            
            _centroid = new Point3D(means[0], means[1], means[2]);

            return means;
        }

        double[] covariance()
        {
            double[] means = componentMean();
            //double meanXY = means[0] * means[1];
            //double meanXZ = means[0] * means[2];
            //double meanYZ = means[1] * means[2];

            double covXX = 0;
            double covYY = 0;
            double covZZ = 0;
            double covXY = 0;
            double covXZ = 0;
            double covYZ = 0;
            //foreach (Point3D p in pointSet)
            //{
            //    covXY += p.X * p.Y;
            //    covXZ += p.X * p.Z;
            //    covYZ += p.Y * p.Z;
            //}
            //covXY = covXY / pointSet.Count - meanXY;
            //covXZ = covXZ / pointSet.Count - meanXZ;
            //covYZ = covYZ / pointSet.Count - meanYZ;

            foreach (Point3D p in pointSet)
            {
                covXX += (p.X - means[0]) * (p.X - means[0]);
                covYY += (p.Y - means[1]) * (p.Y - means[1]);
                covZZ += (p.Z - means[2]) * (p.Z - means[2]);
                covXY += (p.X - means[0]) * (p.Y - means[1]);
                covXZ += (p.X - means[0]) * (p.Z - means[2]);
                covYZ += (p.Y - means[1]) * (p.Z - means[2]);
            }
            covXX = covXX / (pointSet.Count - 1);
            covYY = covYY / (pointSet.Count - 1);
            covZZ = covZZ / (pointSet.Count - 1);
            covXY = covXY / (pointSet.Count - 1);
            covXZ = covXZ / (pointSet.Count - 1);
            covYZ = covYZ / (pointSet.Count - 1);

            double[] covariance = new double[9];

            covariance[0] = covXX;
            covariance[1] = covXY;
            covariance[2] = covXZ;
            covariance[3] = covXY;
            covariance[4] = covYY;
            covariance[5] = covYZ;
            covariance[6] = covXZ;
            covariance[7] = covYZ;
            covariance[8] = covZZ;

            return covariance;
        }

        public Vector3D[] identifyMajorAxes()
        {
            Vector3D[] majorAxes = new Vector3D[3];
            double[] covarianceMatrix = covariance();
            
            double[] eigenVectors = new double[9];
            Complex[] eigenValues = new Complex[3];
            double[] eigenVectorsM = new double[9];

            ManagedLinearAlgebraProvider pca = new ManagedLinearAlgebraProvider();
            pca.EigenDecomp(true, 3, covarianceMatrix, eigenVectors, eigenValues, eigenVectorsM);
            
            // major axis
            majorAxes[0] = new Vector3D();
            majorAxes[0].X = eigenVectors[6];
            majorAxes[0].Y = eigenVectors[7];
            majorAxes[0].Z = eigenVectors[8];

            majorAxes[1] = new Vector3D();
            majorAxes[1].X = eigenVectors[3];
            majorAxes[1].Y = eigenVectors[4];
            majorAxes[1].Z = eigenVectors[5];

            majorAxes[2] = new Vector3D();
            majorAxes[2].X = eigenVectors[0];
            majorAxes[2].Y = eigenVectors[1];
            majorAxes[2].Z = eigenVectors[2];

            _transformMatrix = new Matrix3D(majorAxes[0].X, majorAxes[1].X, majorAxes[2].X, 0,
                                            majorAxes[0].Y, majorAxes[1].Y, majorAxes[2].Y, 0,
                                            majorAxes[0].Z, majorAxes[1].Z, majorAxes[2].Z, 0,
                                           -Centroid.X, -Centroid.Y, -Centroid.Z, 1);

            List<Point3D> trOBBV = transformPointSet();
            BoundingBox3D trOBB = new BoundingBox3D(trOBBV);
            OBB = transformBackPointSet(trOBB.BBVertices);
            return majorAxes;
        }

        public List<Point3D> OBBVertices
        {
            get { return OBB; }
        }

        public List<Point3D> transformPointSet()
        {
            List<Point3D> transfPointSet = new List<Point3D>();
            foreach (Point3D p in pointSet)
            {
                Point3D pTransf = _transformMatrix.Transform(p);
                transfPointSet.Add(pTransf);
            }
            return transfPointSet;
        }

        public List<Point3D> transformBackPointSet(List<Point3D> pList)
        {
            Matrix3D invM = new Matrix3D(_transformMatrix);
            invM = invM.inverse();
            List<Point3D> result = new List<Point3D>();

            foreach (Point3D p in pList)
            {
                Point3D pTransf = invM.Transform(p);
                result.Add(pTransf);
            }
            return result;
        }

        public List<Point3D> projectedPointList()
        {
            Vector3D[] majorAxes = identifyMajorAxes();
            Point3D centroid = Centroid;
            // Modify Axes to be aligned to XY plane, the longest X will be first axis (X), the longest Y will be Y
            Vector3D[] modAxes = new Vector3D[3];
            // set as default first to be the same

            if (Math.Abs(majorAxes[0].X) > Math.Abs(majorAxes[1].X) && Math.Abs(majorAxes[0].X) > Math.Abs(majorAxes[2].X))
            {
                modAxes[0] = majorAxes[0];
                if (Math.Abs(majorAxes[1].Y) > Math.Abs(majorAxes[2].Y))
                {
                    modAxes[1] = majorAxes[1];
                    modAxes[2] = majorAxes[2];
                }
                else
                {
                    modAxes[1] = majorAxes[2];
                    modAxes[2] = majorAxes[1];
                }
            }
            else if (Math.Abs(majorAxes[1].X) > Math.Abs(majorAxes[0].X) && Math.Abs(majorAxes[1].X) > Math.Abs(majorAxes[2].X))
            {
                modAxes[0] = majorAxes[1];
                if (Math.Abs(majorAxes[0].Y) > Math.Abs(majorAxes[2].Y))
                {
                    modAxes[1] = majorAxes[0];
                    modAxes[2] = majorAxes[2];
                }
                else
                {
                    modAxes[1] = majorAxes[2];
                    modAxes[2] = majorAxes[0];
                }
            }
            else if (Math.Abs(majorAxes[2].X) > Math.Abs(majorAxes[0].X) && Math.Abs(majorAxes[2].X) > Math.Abs(majorAxes[1].X))
            {
                modAxes[0] = majorAxes[2];
                if (Math.Abs(majorAxes[1].Y) > Math.Abs(majorAxes[0].Y))
                {
                    modAxes[1] = majorAxes[1];
                    modAxes[2] = majorAxes[0];
                }
                else
                {
                    modAxes[1] = majorAxes[0];
                    modAxes[2] = majorAxes[1];
                }
            }
            else
            {
                modAxes[0] = majorAxes[0];
                if (Math.Abs(majorAxes[1].Y) > Math.Abs(majorAxes[2].Y))
                {
                    modAxes[1] = majorAxes[1];
                    modAxes[2] = majorAxes[2];
                }
                else
                {
                    modAxes[1] = majorAxes[2];
                    modAxes[2] = majorAxes[1];
                }
            }

            // Force axes 1 and 2 (X and Y) to be on the XY plane
            modAxes[0].Z = 0;
            modAxes[1].Z = 0;
            // the third Axis will be +Z
            modAxes[2].X = 0;
            modAxes[2].Y = 0;
            if (modAxes[2].Z == 0)
                modAxes[2].Z = 1;

            transformMatrix = new Matrix3D(modAxes[0].X, modAxes[1].X, modAxes[2].X, 0,
                                modAxes[0].Y, modAxes[1].Y, modAxes[2].Y, 0,
                                modAxes[0].Z, modAxes[1].Z, modAxes[2].Z, 0,
                               -centroid.X, -centroid.Y, -centroid.Z, 1);
            List<Point3D> transfPoints = transformPointSet();
            //BoundingBox3D trOBB = new BoundingBox3D(transfPoints);
            //List<Point3D> modOBB = transformBackPointSet(trOBB.BBVertices);
            return transfPoints;
        }
    }
}
