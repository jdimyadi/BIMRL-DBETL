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
using System.Data;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.OctreeLib;

namespace BIMRL
{
    public class SDOGeomUtils
    {
        public static SdoGeometryTypes.GTYPE generateGeometryFromSDO(SdoGeometry sdoGeomData, out object geomOut)
        {
            SdoGeometryTypes.GTYPE sdoGtype;
            int gtype = sdoGeomData.PropertiesFromGTYPE();
            int geomtyp = sdoGeomData.GeometryType;
            if (geomtyp == (int)SdoGeometryTypes.GTYPE.SOLID || geomtyp == (int)SdoGeometryTypes.GTYPE.MULTISOLID)
            {
                Polyhedron geomOutPH;
                generate_Polyhedron(sdoGeomData, out geomOutPH);
                geomOut = geomOutPH;
                sdoGtype = SdoGeometryTypes.GTYPE.SOLID;
            }
            else if (geomtyp == (int)SdoGeometryTypes.GTYPE.POLYGON || geomtyp == (int)SdoGeometryTypes.GTYPE.MULTIPOLYGON)
            {
                Face3D geomOutF3D;
                generate_Face3D(sdoGeomData, out geomOutF3D);
                geomOut = geomOutF3D;
                sdoGtype = SdoGeometryTypes.GTYPE.POLYGON;
            }
            else if (geomtyp == (int)SdoGeometryTypes.GTYPE.LINE || geomtyp == (int)SdoGeometryTypes.GTYPE.MULTILINE)
            {
                List<LineSegment3D> geomOutLS;
                generate_Line(sdoGeomData, out geomOutLS);
                geomOut = geomOutLS;
                sdoGtype = SdoGeometryTypes.GTYPE.LINE;
            }
            else if (geomtyp == (int)SdoGeometryTypes.GTYPE.POINT || geomtyp == (int)SdoGeometryTypes.GTYPE.MULTIPOINT)
            {
                List<Point3D> geomOutP;
                generate_Point(sdoGeomData, out geomOutP);
                geomOut = geomOutP;
                sdoGtype = SdoGeometryTypes.GTYPE.POINT;
            }
            else
            {
                throw new Exception("Geometry not supported!");
            }

            return sdoGtype;
        }

        public static bool generate_Polyhedron(SdoGeometry geom, out Polyhedron pH)
        {
            List<double> pointCoordList = new List<double>();
            List<int> coordIndexList = new List<int>();

            int[] elInfo = geom.ElemArrayOfInts; 
            double[] ordArray = geom.OrdinatesArrayOfDoubles;

            if (ordArray.Length == 0)
            {
                // An empty data (should not come here, but in some cases it may happen when there is bad data)
                pH = null;
                return false;
            }

            int eInfoIdx = 0;   // Index on Element Info array
            int vertIdx = 0;     // new Index for the index to the vertex coordinate lists
            Point3D v = new Point3D();
            Point3D v1 = new Point3D();
            List<int> noFaceVertList = new List<int>();

            // First loop: loop for lump
            while (eInfoIdx < elInfo.Length)
            {
                // advance to the 6th array in the element info to get the number of faces
                eInfoIdx += 5;
                int noFace = elInfo[eInfoIdx];

                eInfoIdx += 1;      // Advance to the first face offset

                // second loop for the number of faces inside a lump
                for (int f = 0; f < noFace; f++)
                {
                    bool vert = true;
                    int vcount = 0;
                    int fIdx = elInfo[eInfoIdx];

                    while (vert)
                    {
                        if (vcount == 0)
                        {
                            v1.X = ordArray[fIdx - 1];     // -1 because the index starts at no 1
                            v1.Y = ordArray[fIdx];
                            v1.Z = ordArray[fIdx + 1];

                            pointCoordList.Add(v1.X);
                            pointCoordList.Add(v1.Y);
                            pointCoordList.Add(v1.Z);
                            coordIndexList.Add(vertIdx * 3);
                            vertIdx++;
                            vcount++;
                            fIdx += 3;
                            continue;
                        }
                        v.X = ordArray[fIdx - 1];
                        v.Y = ordArray[fIdx];
                        v.Z = ordArray[fIdx + 1];

                        if (Point3D.Equals(v, v1))
                        {
                            // We are at the end of the vertex list. Oracle SDO repeat the last point as the first point, we can skip this for X3D
                            vert = false;
                            eInfoIdx += 3;
                            noFaceVertList.Add(vcount);     // List of no of vertices in each individual face
                            continue;
                        }

                        pointCoordList.Add(v.X);
                        pointCoordList.Add(v.Y);
                        pointCoordList.Add(v.Z);
                        coordIndexList.Add(vertIdx * 3);
                        fIdx += 3;
                        vertIdx++;
                        vcount++;
                    }
                }
            }

//            pH = new Polyhedron(PolyhedronFaceTypeEnum.TriangleFaces, true, pointCoordList, coordIndexList, null);
            pH = new Polyhedron(PolyhedronFaceTypeEnum.ArbitraryFaces, true, pointCoordList, coordIndexList, noFaceVertList);
            return true;
        }

        public static bool generate_Face3D(SdoGeometry geom, out Face3D face)
        {
            int[] elInfo = geom.ElemArrayOfInts;
            double[] ordArray = geom.OrdinatesArrayOfDoubles;

            int noLoop = elInfo.Length / 3;     // first loop is the outerloop and the rest will be innerloop
            int totalVerts = ordArray.Length / 3;
            List<int> vertsInLoop = new List<int>();
            for (int i = 0; i < noLoop; i++)
            {
                if (i == noLoop - 1)
                    vertsInLoop.Add((totalVerts - (elInfo[i * 3] - 1) / 3));
                else
                    vertsInLoop.Add((elInfo[(i + 1) * 3] - elInfo[i * 3]) / 3);
            }

            int initPos = 0;
            List<List<Point3D>> vertLists = new List<List<Point3D>>();
            for (int i = 0; i < noLoop; i++)
            {
                List<Point3D> vertList = new List<Point3D>();
                for (int v = 0; v < vertsInLoop[i]; v++)
                {
                    initPos = elInfo[i * 3] - 1;
                    int pos = initPos + v * 3;
                    Point3D vert = new Point3D(ordArray[pos], ordArray[pos + 1], ordArray[pos + 2]);
                    vertList.Add(vert);
                }
                vertLists.Add(vertList);
            }
            face = new Face3D(vertLists);
            return true;
        }

        /// <summary>
        /// Generate a single line only
        /// </summary>
        /// <param name="geom"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static bool generate_Line(SdoGeometry geom, out List<LineSegment3D> lineList)
        {
            int[] elInfo = geom.ElemArrayOfInts;
            double[] ordArray = geom.OrdinatesArrayOfDoubles;

            lineList = new List<LineSegment3D>();
            for (int oCount = 0; oCount < ordArray.Count()/6; oCount++)
            {
                Point3D startP = new Point3D(ordArray[(oCount * 6) + 0], ordArray[(oCount * 6) + 1], ordArray[(oCount * 6) + 2]);
                Point3D endP = new Point3D(ordArray[(oCount*6)+3], ordArray[(oCount*6)+4], ordArray[(oCount*6)+5]);
                LineSegment3D line = new LineSegment3D(startP, endP);
                lineList.Add(line);
            }
            return true;
        }

        public static bool generate_Point(SdoGeometry geom, out List<Point3D> pointList)
        {
            pointList = new List<Point3D>();
            int gtype = geom.PropertiesFromGTYPE();
            if (geom.GeometryType == (int)SdoGeometryTypes.GTYPE.MULTIPOINT)
            {
                int[] elInfo = geom.ElemArrayOfInts;
                double[] ordArray = geom.OrdinatesArrayOfDoubles;
                for (int oCount = 0; oCount < elInfo.Count() / 3; oCount++)
                {
                    Point3D point = new Point3D(ordArray[(oCount * 3) + 0], ordArray[(oCount * 3) + 1], ordArray[(oCount * 3) + 2]);
                    pointList.Add(point);
                }
            }
            else
            {
                Point3D point = sdopointToPoint3D(geom.SdoPoint);
                pointList.Add(point);
            }
            return true;
        }

        public static Point3D sdopointToPoint3D(SdoPoint sdoP)
        {
            return new Point3D(sdoP.XD.Value, sdoP.YD.Value, sdoP.ZD.Value);
        }
    }
}
