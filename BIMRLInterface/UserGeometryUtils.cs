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
using BIMRL;

// Currently we are not supporting face with hole(s), only the outer boundary will be used

namespace BIMRLInterface
{
    public class UserGeometryUtils
    {
        // Set of arrays for USERGEOM_GEOMETRY
        static List<string> geometryIDList = new List<string>();
        static List<SdoGeometry> geometryList = new List<SdoGeometry>();
        static List<SdoGeometry> mjAxis1List = new List<SdoGeometry>();
        static List<OracleParameterStatus> mjAxis1Status = new List<OracleParameterStatus>();
        static List<SdoGeometry> mjAxis2List = new List<SdoGeometry>();
        static List<OracleParameterStatus> mjAxis2Status = new List<OracleParameterStatus>();
        static List<SdoGeometry> mjAxis3List = new List<SdoGeometry>();
        static List<OracleParameterStatus> mjAxis3Status = new List<OracleParameterStatus>();
        static List<SdoGeometry> mjAxisCentroidList = new List<SdoGeometry>();
        static List<OracleParameterStatus> mjAxisCentroidStatus = new List<OracleParameterStatus>();

        //static string sqlSpIdx = "INSERT INTO USERGEOM_SPATIALINDEX (ELEMENTID, CELLID, CELLTYPE, XMINBOUND, YMINBOUND, ZMINBOUND, XMAXBOUND, YMAXBOUND, ZMAXBOUND, DEPTH)"
        //                        + " VALUES (:1, :2, :3, :4, :5, :6, :7, :8, :9, :10)";
        //static string sqlTopoF = "INSERT INTO USERGEOM_TOPO_FACE (ELEMENTID, FACEID, GEOMETRY, NORMAL, CENTROID, ORIENTATION) VALUES (:1, :2, :3, :4, :5, :6)";
        static OracleCommand cmdNewGeomID = new OracleCommand("SELECT SEQ_GEOMID.NEXTVAL FROM DUAL", DBOperation.DBConn);

        static void geomCollReset()
        {
            geometryIDList.Clear();
            geometryList.Clear();
            mjAxis1List.Clear();
            mjAxis1Status.Clear();
            mjAxis2List.Clear();
            mjAxis2Status.Clear();
            mjAxis3List.Clear();
            mjAxis3Status.Clear();
            mjAxisCentroidList.Clear();
            mjAxisCentroidStatus.Clear();
        }

        public static void addUserGeometry(string geomID, SdoGeometry geom, 
                                            SdoGeometry mjAxis1, SdoGeometry mjAxis2, SdoGeometry mjAxis3, SdoGeometry mjAxisCentroid)
        {
            geometryIDList.Add(geomID);

            geometryList.Add(geom);

            SdoGeometry nullGeom = new SdoGeometry();
            if (mjAxis1 != null)
            {
                mjAxis1List.Add(mjAxis1);
                mjAxis1Status.Add(OracleParameterStatus.Success);
            }
            else
            {
                mjAxis1List.Add(nullGeom);
                mjAxis1Status.Add(OracleParameterStatus.NullInsert);
            }

            if (mjAxis2 != null)
            {
                mjAxis2List.Add(mjAxis2);
                mjAxis2Status.Add(OracleParameterStatus.Success);
            }
            else
            {
                mjAxis2List.Add(nullGeom);
                mjAxis2Status.Add(OracleParameterStatus.NullInsert);
            }

            if (mjAxis3 != null)
            {
                mjAxis3List.Add(mjAxis3);
                mjAxis3Status.Add(OracleParameterStatus.Success);
            }
            else
            {
                mjAxis3List.Add(nullGeom);
                mjAxis3Status.Add(OracleParameterStatus.NullInsert);
            }

            if (mjAxisCentroid != null)
            {
                mjAxisCentroidList.Add(mjAxisCentroid);
                mjAxisCentroidStatus.Add(OracleParameterStatus.Success);
            }
            else
            {
                mjAxisCentroidList.Add(nullGeom);
                mjAxisCentroidStatus.Add(OracleParameterStatus.NullInsert);
            }

            string sqlGeom = "INSERT INTO USERGEOM_GEOMETRY (ELEMENTID, GEOMETRY, BODY_MAJOR_AXIS1, BODY_MAJOR_AXIS2, BODY_MAJOR_AXIS3, BODY_MAJOR_AXIS_CENTROID)"
                    + " VALUES (:1, :6, :7, :8, :9, :10)";
            OracleCommand cmd = new OracleCommand(sqlGeom, DBOperation.DBConn);
            OracleParameter[] pars = new OracleParameter[6];

            pars[0] = cmd.Parameters.Add("1", OracleDbType.Varchar2);

            pars[1] = cmd.Parameters.Add("6", OracleDbType.Object);
            pars[1].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            pars[2] = cmd.Parameters.Add("7", OracleDbType.Object);
            pars[2].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            pars[3] = cmd.Parameters.Add("8", OracleDbType.Object);
            pars[3].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            pars[4] = cmd.Parameters.Add("9", OracleDbType.Object);
            pars[4].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            pars[5] = cmd.Parameters.Add("10", OracleDbType.Object);
            pars[5].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            for (int i = 0; i < pars.Count(); ++i)
            {
                pars[i].Direction = ParameterDirection.Input;
            }

            pars[0].Value = geometryIDList.ToArray();
            pars[0].Size = geometryIDList.Count;

            pars[1].Value = geometryList.ToArray();
            pars[1].Size = geometryList.Count;

            pars[2].Value = mjAxis1List.ToArray();
            pars[2].Size = mjAxis1List.Count;
            pars[2].ArrayBindStatus = mjAxis1Status.ToArray();

            pars[3].Value = mjAxis2List.ToArray();
            pars[3].Size = mjAxis2List.Count;
            pars[3].ArrayBindStatus = mjAxis2Status.ToArray();

            pars[4].Value = mjAxis3List.ToArray();
            pars[4].Size = mjAxis3List.Count;
            pars[4].ArrayBindStatus = mjAxis3Status.ToArray();

            pars[5].Value = mjAxisCentroidList.ToArray();
            pars[5].Size = mjAxisCentroidList.Count;
            pars[5].ArrayBindStatus = mjAxisCentroidStatus.ToArray();

            cmd.ArrayBindCount = geometryIDList.Count;
            try
            {
                int status = cmd.ExecuteNonQuery();
                DBOperation.commitTransaction();
                geomCollReset();
                cmd.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%SQL Error - " + e.Message + "\n\t" + sqlGeom;
                DBOperation.refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%SQL Error - " + e.Message + "\n\t" + sqlGeom;
                DBOperation.refBIMRLCommon.StackPushError(excStr);
                throw;
            }
            cmd.Dispose();
        }

        public static SdoGeometry createSDOGeomPoint(Point3D point)
        {
            SdoGeometry geom = new SdoGeometry();
            geom.Dimensionality = 3;
            geom.LRS = 0;
            geom.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
            int gType = geom.PropertiesToGTYPE();

            SdoPoint pointGeom = new SdoPoint();
            pointGeom.XD = point.X;
            pointGeom.YD = point.Y;
            pointGeom.ZD = point.Z;
            geom.SdoPoint = pointGeom;

            return geom;
        }

        public static SdoGeometry createSDOGeomPoint(Vector3D vector)
        {
            SdoGeometry geom = new SdoGeometry();
            geom.Dimensionality = 3;
            geom.LRS = 0;
            geom.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
            int gType = geom.PropertiesToGTYPE();

            SdoPoint pointGeom = new SdoPoint();
            pointGeom.XD = vector.X;
            pointGeom.YD = vector.Y;
            pointGeom.ZD = vector.Z;
            geom.SdoPoint = pointGeom;

            return geom;
        }

        public static SdoGeometry createSdoGeomLine(List<Point3D> pointSet)
        {
            SdoGeometry geom = new SdoGeometry();
            geom.Dimensionality = 3;
            geom.LRS = 0;
            if (pointSet.Count == 2)
                geom.GeometryType = (int)SdoGeometryTypes.GTYPE.LINE;
            else
                geom.GeometryType = (int)SdoGeometryTypes.GTYPE.MULTILINE;
            int gType = geom.PropertiesToGTYPE();

            List<int> elemInfoArr = new List<int>(){1,2,1};
            List<double> arrCoord = new List<double>();

            foreach (Point3D p in pointSet)
            {
                arrCoord.Add(p.X);
                arrCoord.Add(p.Y);
                arrCoord.Add(p.Z);
            }

            // Add Elementinfo for Linestring
            for (int i=2; i<pointSet.Count; ++i)
            {
                elemInfoArr.Add(i * 3 + 1);
                elemInfoArr.Add(2);
                elemInfoArr.Add(1);
            }

            geom.ElemArrayOfInts = elemInfoArr.ToArray();
            geom.OrdinatesArrayOfDoubles = arrCoord.ToArray();

            return geom;
        }

        public static SdoGeometry createSdoGeomFace(Face3D face)
        {
            SdoGeometry geom = new SdoGeometry();
            geom.Dimensionality = 3;
            geom.LRS = 0;
            geom.GeometryType = (int)SdoGeometryTypes.GTYPE.POLYGON;
            int gType = geom.PropertiesToGTYPE();

            int geomType = (int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_EXTERIOR;
            List<int> elemInfoArr = new List<int>() { 1, geomType, 1 };
            List<double> arrCoord = new List<double>();
            if (face.verticesWithHoles.Count > 0)
            {
                foreach (Point3D p in face.verticesWithHoles[0])
                {
                    arrCoord.Add(p.X);
                    arrCoord.Add(p.Y);
                    arrCoord.Add(p.Z);
                }

                int offset = 1;
                for (int i=1; i < face.verticesWithHoles.Count; ++i)
                {
                    offset += face.verticesWithHoles[i - 1].Count;
                    elemInfoArr.Add(offset);   // offset to the start of the inner loop (no of double val of the prev list + 1)
                    elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_INTERIOR);
                    elemInfoArr.Add(1);

                    foreach (Point3D p in face.verticesWithHoles[i])
                    {
                        arrCoord.Add(p.X);
                        arrCoord.Add(p.Y);
                        arrCoord.Add(p.Z);
                    }
                }
            }

            geom.ElemArrayOfInts = elemInfoArr.ToArray();
            geom.OrdinatesArrayOfDoubles = arrCoord.ToArray();

            return geom;
        }

        public static SdoGeometry createSdoGeomBrep(Point3D LLB, Point3D URT)
        {
            SdoGeometry geom = new SdoGeometry();
            geom.Dimensionality = 3;
            geom.LRS = 0;
            geom.GeometryType = (int)SdoGeometryTypes.GTYPE.SOLID;
            int gType = geom.PropertiesToGTYPE();

            int geomType = (int)SdoGeometryTypes.ETYPE_COMPOUND.SOLID;
            List<int> elemInfoArr = new List<int>() { 1, geomType, 3 };

            List<double> arrCoord = new List<double>();

            arrCoord.Add(LLB.X);
            arrCoord.Add(LLB.Y);
            arrCoord.Add(LLB.Z);
            arrCoord.Add(URT.X);
            arrCoord.Add(URT.Y);
            arrCoord.Add(URT.Z);

            geom.ElemArrayOfInts = elemInfoArr.ToArray();
            geom.OrdinatesArrayOfDoubles = arrCoord.ToArray();

            return geom;
        }

        /// <summary>
        /// For this function, only fixed face shapes accepted, mostly triangle or quadrangle as deteminef by the faceType
        /// </summary>
        /// <param name="vertexList"></param>
        /// <param name="faceIdxList"></param>
        /// <param name="faceType"></param>
        /// <returns></returns>
        public static SdoGeometry createSdoGeomBrep(List<Point3D> vertexList, List<int> faceIdxList, PolyhedronFaceTypeEnum faceType)
        {
            int noFaces = faceIdxList.Count / ((int)faceType + 1);

            SdoGeometry geom = new SdoGeometry();
            geom.Dimensionality = 3;
            geom.LRS = 0;
            geom.GeometryType = (int)SdoGeometryTypes.GTYPE.SOLID;
            int gType = geom.PropertiesToGTYPE();

            int geomType = (int)SdoGeometryTypes.ETYPE_COMPOUND.SOLID;
            List<int> elemInfoArr = new List<int>() { 1, geomType, 1 };
            elemInfoArr.Add(1);
            elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_COMPOUND.SURFACE_EXTERIOR);
            elemInfoArr.Add(noFaces);

            List<double> arrCoord = new List<double>();

            int offset = 1;
            for (int i = 0; i < noFaces; ++i )
            {
                elemInfoArr.Add(offset);
                elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_EXTERIOR);
                elemInfoArr.Add(1);
                for (int j=0; j<((int)faceType+1); ++j)
                {
                    int faceIdxOffset = offset + i*((int)faceType+1);
                    arrCoord.Add(vertexList[faceIdxOffset + j].X);
                    arrCoord.Add(vertexList[faceIdxOffset + j].Y);
                    arrCoord.Add(vertexList[faceIdxOffset + j].Z);
                }
                offset += ((int)faceType + 1) * 3;              // offset counts the double values in the ORDINATE_ARRAY
            }

            geom.ElemArrayOfInts = elemInfoArr.ToArray();
            geom.OrdinatesArrayOfDoubles = arrCoord.ToArray();

            return geom;
        }

        public static SdoGeometry createSdoGeomBrep(List<Face3D> faceList)
        {
            SdoGeometry geom = new SdoGeometry();
            geom.Dimensionality = 3;
            geom.LRS = 0;
            geom.GeometryType = (int)SdoGeometryTypes.GTYPE.SOLID;
            int gType = geom.PropertiesToGTYPE();

            int geomType = (int)SdoGeometryTypes.ETYPE_COMPOUND.SOLID;
            List<int> elemInfoArr = new List<int>() { 1, geomType, 1 };
            elemInfoArr.Add(1);
            elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_COMPOUND.SURFACE_EXTERIOR);
            elemInfoArr.Add(faceList.Count);

            List<double> arrCoord = new List<double>();

            int offset = 1;
            foreach (Face3D face in faceList)
            {
                elemInfoArr.Add(offset);
                elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_EXTERIOR);
                elemInfoArr.Add(1);

                foreach (Point3D p in face.verticesWithHoles[0])
                {
                    arrCoord.Add(p.X);
                    arrCoord.Add(p.Y);
                    arrCoord.Add(p.Z);
                    offset += 3;
                }

                for (int i = 1; i < face.verticesWithHoles.Count; ++i)
                {
                    elemInfoArr.Add(offset);
                    elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_INTERIOR);
                    elemInfoArr.Add(1);

                    foreach (Point3D p in face.verticesWithHoles[i])
                    {
                        arrCoord.Add(p.X);
                        arrCoord.Add(p.Y);
                        arrCoord.Add(p.Z);
                        offset += 3;
                    }
                }
            }

            geom.ElemArrayOfInts = elemInfoArr.ToArray();
            geom.OrdinatesArrayOfDoubles = arrCoord.ToArray();

            return geom;
        }

        /// <summary>
        /// We will do the following:
        /// 1. transform the base face to and end location using extrusionDir and extent
        /// 2. Connect the edges to form new faces (use the same order as it is a simple extrusion
        /// 3. (can include the hole as well using the opposite direction loop)
        /// </summary>
        /// <param name="fBase"></param>
        /// <param name="extrustionDir"></param>
        /// <param name="extent"></param>
        /// <returns></returns>
        public static SdoGeometry createSdoGeomExtrusion(Face3D fBase, Vector3D extrustionDir, double extent)
        {
            extrustionDir.Normalize();
            Vector3D extr = extent * extrustionDir;

            List<Face3D> extrusionFaces = new List<Face3D>();
            extrusionFaces.Add(fBase);

            List<List<Point3D>> vertLists = new List<List<Point3D>>();
            List<Point3D> outerB = new List<Point3D>();
            foreach (Point3D p in fBase.verticesWithHoles[0])
            {
                Point3D newP = p + extr;
                outerB.Add(newP);
            }
            vertLists.Add(outerB);
            Face3D eFace = new Face3D(vertLists);
            extrusionFaces.Add(eFace);

            for (int i = 1; i < fBase.verticesWithHoles.Count; ++i )
            {
                List<Point3D> innerB = new List<Point3D>();
                foreach (Point3D p in fBase.verticesWithHoles[i])
                {
                    Point3D newP = p + extr;
                    innerB.Add(newP);
                }
                vertLists.Add(innerB);
            }

            for (int i = 0; i < fBase.verticesWithHoles[0].Count; ++i)
            {
                List<Point3D> fVerts = new List<Point3D>();
                if (i == fBase.verticesWithHoles[0].Count - 1)
                {
                    Point3D p1 = fBase.verticesWithHoles[0][i];
                    Point3D p2 = fBase.verticesWithHoles[0][0];
                    Point3D p3 = vertLists[0][0];
                    Point3D p4 = vertLists[0][i];
                    fVerts.Add(p1);
                    fVerts.Add(p2);
                    fVerts.Add(p3);
                    fVerts.Add(p4);
                }
                else
                {
                    Point3D p1 = fBase.verticesWithHoles[0][i];
                    Point3D p2 = fBase.verticesWithHoles[0][i + 1];
                    Point3D p3 = vertLists[0][i + 1];
                    Point3D p4 = vertLists[0][i];
                    fVerts.Add(p1);
                    fVerts.Add(p2);
                    fVerts.Add(p3);
                    fVerts.Add(p4);
                }
                Face3D newF = new Face3D(fVerts);
                extrusionFaces.Add(newF);
            }

            // Process inner loops
            for (int j = 1; j < fBase.verticesWithHoles.Count; ++j )
            {
                for (int i = 0; i < fBase.verticesWithHoles[j].Count; ++i)
                {
                    List<Point3D> fVerts = new List<Point3D>();
                    if (i == fBase.verticesWithHoles[0].Count - 1)
                    {
                        Point3D p4 = fBase.verticesWithHoles[0][i];
                        Point3D p3 = fBase.verticesWithHoles[0][0];
                        Point3D p2 = vertLists[0][0];
                        Point3D p1 = vertLists[0][i];
                        fVerts.Add(p1);
                        fVerts.Add(p2);
                        fVerts.Add(p3);
                        fVerts.Add(p4);
                    }
                    else
                    {
                        Point3D p4 = fBase.verticesWithHoles[0][i];
                        Point3D p3 = fBase.verticesWithHoles[0][i + 1];
                        Point3D p2 = vertLists[0][i + 1];
                        Point3D p1 = vertLists[0][i];
                        fVerts.Add(p1);
                        fVerts.Add(p2);
                        fVerts.Add(p3);
                        fVerts.Add(p4);
                    }
                    Face3D newF = new Face3D(fVerts);
                    extrusionFaces.Add(newF);
                }                
            }

            SdoGeometry geom = createSdoGeomBrep(extrusionFaces);

            return geom;
        }

        /// <summary>
        /// Spacial extrusion functionality to form a shape from two faces that may not be identical but must have the same number of vertices. We will not handle hole
        /// The main usage is probably a point to a face
        /// </summary>
        /// <param name="fBase"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static SdoGeometry createSdoGeomExtrusion(Point3D point, Face3D endFace, double? offset)
        {
            List<Point3D> startFVerts = new List<Point3D>();
            for (int i = 0; i < endFace.vertices.Count; ++i)
            {
                startFVerts.Add(point);
            }
            Face3D startFace = new Face3D(startFVerts);

            if (offset.HasValue)
            {
                double ext = offset.Value;
                if (offset.Value > 0)
                {
                    List<Point3D> newEndFVerts = new List<Point3D>();
                    for(int i=0; i<endFace.vertices.Count; ++i)
                    {
                        Vector3D vDir = endFace.vertices[i] - startFace.vertices[i];
                        vDir.Normalize();
                        Vector3D extend = ext*vDir;
                        newEndFVerts.Add(endFace.vertices[i] + extend);
                    }
                    endFace = new Face3D(newEndFVerts);
                }
            }

            SdoGeometry geom = createSdoGeomExtrusion(startFace, endFace);

            return geom;
        }

        public static SdoGeometry createSdoGeomExtrusion(Face3D startFace, Face3D endFace)
        {
            List<Face3D> extrFaces = new List<Face3D>();
            extrFaces.Add(startFace);
            extrFaces.Add(endFace);

            for (int i = 0; i < startFace.vertices.Count; ++i)
            {
                List<Point3D> fVerts = new List<Point3D>();
                if (i == startFace.vertices.Count - 1)
                {
                    Point3D p1 = startFace.vertices[i];
                    Point3D p2 = startFace.vertices[0];
                    Point3D p3 = endFace.vertices[0];
                    Point3D p4 = endFace.vertices[i];
                    fVerts.Add(p1);
                    fVerts.Add(p2);
                    fVerts.Add(p3);
                    fVerts.Add(p4);
                }
                else
                {
                    Point3D p1 = startFace.vertices[i];
                    Point3D p2 = startFace.vertices[i + 1];
                    Point3D p3 = endFace.vertices[i + 1];
                    Point3D p4 = endFace.vertices[i];
                    fVerts.Add(p1);
                    fVerts.Add(p2);
                    fVerts.Add(p3);
                    fVerts.Add(p4);
                }
                Face3D newF = new Face3D(fVerts);
                extrFaces.Add(newF);
            }

            SdoGeometry geom = createSdoGeomBrep(extrFaces);
            return geom;
        }

        #region creation_functions

        static string getNextGeomID()
        {
            try
            {
                object gidret = cmdNewGeomID.ExecuteScalar();

                return gidret.ToString();
            }
            catch (OracleException e)
            {
                string excStr = "%%SQL Error Getting the next Geometry ID from Sequence SEQ_GEOMID - " + e.Message + "\n\t";
                DBOperation.refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%SQL Error Getting the next Geometry ID from Sequence SEQ_GEOMID - " + e.Message + "\n\t";
                DBOperation.refBIMRLCommon.StackPushError(excStr);
                throw;
            }
            return null;
        }

        public static string createLine(string elemID, List<Point3D> line, bool skipSpatialIndex)
        {
            string geomID;
            if (string.IsNullOrEmpty(elemID))
                geomID = getNextGeomID();
            else
                geomID = elemID;

            SdoGeometry sdogeomLine = createSdoGeomLine(line);
            addUserGeometry(geomID, sdogeomLine, null, null, null, null);
            if (!skipSpatialIndex)
                createSpatialIndexAndFaceInfo(DBQueryManager.FedModelID, geomID, sdogeomLine);
            return geomID;
        }
        
        public static string createFace3D(string elemID, List<Point3D> vertexList, bool skipSpatialIndex)
        {
            string geomID;
            if (string.IsNullOrEmpty(elemID))
                geomID = getNextGeomID();
            else
                geomID = elemID;

            Face3D face = new Face3D(vertexList);
            SdoGeometry sdogeomFace = createSdoGeomFace(face);
            PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis(face);
            Vector3D[] mjAxes = pca.identifyMajorAxes();
            SdoGeometry mjAxis1 = createSDOGeomPoint(mjAxes[0]);
            SdoGeometry mjAxis2 = createSDOGeomPoint(mjAxes[1]);
            SdoGeometry mjAxis3 = createSDOGeomPoint(mjAxes[2]);
            SdoGeometry mjAxisCentroid = createSDOGeomPoint(pca.Centroid);

            addUserGeometry(geomID, sdogeomFace, mjAxis1, mjAxis2, mjAxis3, mjAxisCentroid);
            if (!skipSpatialIndex)
                createSpatialIndexAndFaceInfo(DBQueryManager.FedModelID, geomID, sdogeomFace);
            return geomID;
        }

        public static string createBox3D(string elemID, Point3D LLB, Point3D URT, bool skipSpatialIndex)
        {
            string geomID;
            if (string.IsNullOrEmpty(elemID))
                geomID = getNextGeomID();
            else
                geomID = elemID;

            SdoGeometry bbox = createSdoGeomBrep(LLB, URT);
            SdoGeometry mjAxis1 = createSDOGeomPoint(new Point3D(1, 0, 0));
            SdoGeometry mjAxis2 = createSDOGeomPoint(new Point3D(0, 1, 0));
            SdoGeometry mjAxis3 = createSDOGeomPoint(new Point3D(0, 0, 1));
            BoundingBox3D bbox3D = new BoundingBox3D(LLB, URT);
            SdoGeometry mjAxisCentroid = createSDOGeomPoint(bbox3D.Center);

            addUserGeometry(geomID, bbox, mjAxis1, mjAxis2, mjAxis3, mjAxisCentroid);
            if (!skipSpatialIndex)
                createSpatialIndexAndFaceInfo(DBQueryManager.FedModelID, geomID, bbox); 
            return geomID;
        }

        public static string createExtrusion(string elemID, Face3D baseFace, Vector3D extrusionDir, double extent, bool skipSpatialIndex)
        {
            string geomID;
            if (string.IsNullOrEmpty(elemID))
                geomID = getNextGeomID();
            else
                geomID = elemID;

            List<Point3D> endFaceVerts = new List<Point3D>();
            foreach (Point3D p in baseFace.vertices)
            {
                Point3D newP = new Point3D(p);
                newP = newP + extent * extrusionDir;
                endFaceVerts.Add(newP);
            }
            List<Face3D> brep;
            List<Point3D> pointList;

            double[] noExtend = new double[2];
            noExtend[0] = 0.0;
            noExtend[1] = 0.0;

            genBrepFaces(baseFace.vertices, endFaceVerts, noExtend, out brep, out pointList);

            SdoGeometry sdogeom = createSdoGeomBrep(brep);
            PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis(pointList);
            Vector3D[] mjAxes = pca.identifyMajorAxes();
            SdoGeometry mjAxis1 = createSDOGeomPoint(mjAxes[0]);
            SdoGeometry mjAxis2 = createSDOGeomPoint(mjAxes[1]);
            SdoGeometry mjAxis3 = createSDOGeomPoint(baseFace.basePlane.normalVector);
            SdoGeometry mjAxisCentroid = createSDOGeomPoint(pca.Centroid);

            addUserGeometry(geomID, sdogeom, mjAxis1, mjAxis2, mjAxis3, mjAxisCentroid);
            if (!skipSpatialIndex)
                createSpatialIndexAndFaceInfo(DBQueryManager.FedModelID, geomID, sdogeom);
            return geomID;
        }

        /// <summary>
        /// Create Brep given a triangulated face set or rectangular face set
        /// </summary>
        /// <param name="coordList"></param>
        /// <param name="faceIndexList"></param>
        /// <param name="assocEID1"></param>
        /// <param name="assocFID1"></param>
        /// <param name="assocEID2"></param>
        /// <param name="assocFID2"></param>
        /// <returns></returns>
        public static string createBrep(string elemID, List<Point3D> coordList, List<List<int>> faceIndexList, int noVertInFace, bool skipSpatialIndex)
        {
            string geomID;
            if (string.IsNullOrEmpty(elemID))
                geomID = getNextGeomID();
            else
                geomID = elemID;

            List<double> coordDblList = new List<double>();
            foreach (Point3D p in coordList)
            {
                coordDblList.Add(p.X);
                coordDblList.Add(p.Y);
                coordDblList.Add(p.Z);
            }
            List<int> faceCoordIdxList = new List<int>();

            PolyhedronFaceTypeEnum fType = PolyhedronFaceTypeEnum.TriangleFaces;
            if (noVertInFace == 4)
                fType = PolyhedronFaceTypeEnum.RectangularFaces;

            foreach(List<int> fIdx in faceIndexList)
            {
                for (int i = 0; i < noVertInFace; ++i )
                    faceCoordIdxList.Add(fIdx[0] * 3 + i);
            }
            Polyhedron polyH = new Polyhedron(fType, true, coordDblList, faceCoordIdxList, null);
            SdoGeometry sdogeom = createSdoGeomBrep(polyH.Faces);
            PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis(polyH);
            Vector3D[] mjAxes = pca.identifyMajorAxes();
            SdoGeometry mjAxis1 = createSDOGeomPoint(mjAxes[0]);
            SdoGeometry mjAxis2 = createSDOGeomPoint(mjAxes[1]);
            SdoGeometry mjAxis3 = createSDOGeomPoint(mjAxes[2]);
            SdoGeometry mjAxisCentroid = createSDOGeomPoint(pca.Centroid);

            addUserGeometry(geomID, sdogeom, mjAxis1, mjAxis2, mjAxis3, mjAxisCentroid);
            if (!skipSpatialIndex)
                createSpatialIndexAndFaceInfo(DBQueryManager.FedModelID, geomID, sdogeom);
            return geomID;
        }

        public static string createBrep(string elemID, Face3D startFace, Face3D endFace, double[] extend, int[] extendMod, bool skipSpatialIndex)
        {
            if (startFace.vertices.Count != endFace.vertices.Count)
            {
                throw new BIMRLInterfaceRuntimeException("No. of vertices of the start and end Faces are not equal!");
            }

            List<Face3D> brep;

            SdoGeometry mjAxisCentroid = null;
            SdoGeometry mjAxis1 = null;
            SdoGeometry mjAxis2 = null;
            SdoGeometry mjAxis3 = null;

            List<Point3D> pointList;

            Face3D newStartFace = startFace;
            Face3D newEndFace = endFace;

            string geomID;
            if (string.IsNullOrEmpty(elemID))
                geomID = getNextGeomID();
            else
                geomID = elemID;

            if (extend.Count() >= 2)
            {
                if (extend[0] > 0.0)
                {
                    PrincipalComponentAnalysis pca = identifyMajorAxes(startFace.outerAndInnerVertices, out mjAxisCentroid, out mjAxis1, out mjAxis2, out mjAxis3);
                    pointList = extendGeometry(pca, extend[0], extendMod[0]);
                    newStartFace = new Face3D(pointList);
                }

                if (extend[1] > 0.0)
                {
                    PrincipalComponentAnalysis pca = identifyMajorAxes(endFace.outerAndInnerVertices, out mjAxisCentroid, out mjAxis1, out mjAxis2, out mjAxis3);
                    pointList = extendGeometry(pca, extend[1], extendMod[1]);
                    newEndFace = new Face3D(pointList);
                }
            }

            double[] noExtend = new double[2];
            noExtend[0] = 0.0;
            noExtend[1] = 0.0;

            genBrepFaces(newStartFace.vertices, newEndFace.vertices, noExtend, out brep, out pointList);
            SdoGeometry sdogeom = createSdoGeomBrep(brep);
            addUserGeometry(geomID, sdogeom, mjAxis1, mjAxis2, mjAxis3, mjAxisCentroid);
            if (!skipSpatialIndex)
                createSpatialIndexAndFaceInfo(DBQueryManager.FedModelID, geomID, sdogeom);
            return geomID;
        }

        static void genBrepFaces(List<Point3D> startFaceVerts, List<Point3D> endFaceVerts, double[] extend, out List<Face3D> brepFaces, out List<Point3D> pointList)
        {
            pointList = new List<Point3D>();

            brepFaces = new List<Face3D>();

            Vector3D dirSE = new Vector3D(endFaceVerts[0] - startFaceVerts[0]);
            Vector3D dirES = new Vector3D(startFaceVerts[0] - endFaceVerts[0]);

            Face3D startFace = new Face3D(startFaceVerts);
            Face3D endFace = new Face3D(endFaceVerts);

            bool startFaceNull = Face3D.isNullFace(startFace);
            bool endFaceNull = Face3D.isNullFace(endFace);

            Face3D tempStartFace = new Face3D(startFace.verticesWithHoles);

            List<Point3D> newStartFaceVerts = new List<Point3D>();
            List<Point3D> newEndFaceVerts = new List<Point3D>();

            if (extend[0] != 0.0 || extend[1] != 0)
            {
                // Currently we will only support outer boundary/ no hole

                for (int i = 0; i < startFaceVerts.Count; ++i)
                {
                    LineSegment3D ls = new LineSegment3D(startFace.vertices[i], endFace.vertices[i]);
                    if (!startFaceNull)
                        ls.extendAtStart(-extend[0]);
                    if (!endFaceNull)
                        ls.extendAtEnd(extend[1]);
                    newStartFaceVerts.Add(ls.startPoint);
                    newEndFaceVerts.Add(ls.endPoint);
                }
            }
            else
            {
                newStartFaceVerts.AddRange(startFace.vertices);
                newEndFaceVerts.AddRange(endFace.vertices);
            }

            Face3D newStartFace = new Face3D(newStartFaceVerts);
            Face3D newEndFace = new Face3D(newEndFaceVerts);

            // To create the side faces, first make sure the start and end faces are following the same direction as the extrusion
            if (!startFaceNull)
            {
                // Reverse the order of vertices of the face if it is NOT the same direction as the extrusion
                if (Vector3D.DotProduct(dirSE, startFace.basePlane.normalVector) < 0)
                    newStartFace.Reverse();
            }
            if (!endFaceNull)
            {
                if (Vector3D.DotProduct(dirSE, endFace.basePlane.normalVector) < 0)
                    newEndFace.Reverse();
            }

            for (int i = 1; i < endFace.vertices.Count; ++i)
            {
                // BEWARE: Because the StartFace vertices can be all the same (representing a point actually), we should use the EndFace first, otherwise generate polyhedron may think it is
                // the end of the loop since the two points in the StartFace are actually the same!
                List<Point3D> verts = new List<Point3D>();
                verts.Add(newEndFaceVerts[i]);
                verts.Add(newEndFaceVerts[i - 1]);
                verts.Add(newStartFaceVerts[i - 1]);
                verts.Add(newStartFaceVerts[i]);
                verts.Add(newEndFaceVerts[i]);
                Face3D face = new Face3D(verts);
                brepFaces.Add(face);
            }

            // Reverse the start face since the vertices if the startface is is the same direction as the extrusion
            if (!startFaceNull)
            {
                // Reverse the order of vertices of the face if it is in the same direction as the extrusion
                if (Vector3D.DotProduct(dirSE, startFace.basePlane.normalVector) > 0)
                    newStartFace.Reverse();
            }

            // Reverse the end face since the vertices if the startface is is the opposite direction as the extrusion
            if (!endFaceNull)
            {
                if (Vector3D.DotProduct(dirES, endFace.basePlane.normalVector) > 0)
                    newEndFace.Reverse();
            }

            // Add the end faces folowed by the side (newly created) faces
            if (!startFaceNull)
                brepFaces.Add(newStartFace);
            if (!endFaceNull)
                brepFaces.Add(newEndFace);

            pointList.AddRange(newStartFace.vertices);
            pointList.AddRange(newEndFace.vertices);
        }

        public static string createBrep(string elemID, Point3D startPoint, Face3D endFace, double[] extend, int[] extendMod, bool skipSpatialIndex)
        {
            List<Point3D> pointList;
            List<Face3D> brep;
            List<Point3D> startVerts = new List<Point3D>();

            SdoGeometry mjAxis1 = null;
            SdoGeometry mjAxis2 = null;
            SdoGeometry mjAxis3 = null;
            SdoGeometry mjAxisCentroid = null;

            // The first argument: Point3D
            for (int i = 0; i < endFace.vertices.Count; ++i)
            {
                startVerts.Add(startPoint);     // Create a list of vertices of a 0 face to connect the point and the end face
            }
            
            // The second/end face
            Face3D newEndFace = endFace;
            extend[0] = 0.0;    // make sure it is set to 0 since the stat face is a point (no extension)
            if (extendMod[1] > 0.0)
            {
                PrincipalComponentAnalysis pca = identifyMajorAxes(endFace.outerAndInnerVertices, out mjAxisCentroid, out mjAxis1, out mjAxis2, out mjAxis3);
                pointList = extendGeometry(pca, extend[1], extendMod[1]);
                newEndFace = new Face3D(pointList);
                extend[1] = 0.0;   // if extendMod is > 0, it is 2D extension handled above. Reset to 0 here for the subsequent call to handle extension in 3D
            }
            genBrepFaces(startVerts, endFace.vertices, extend, out brep, out pointList);

            string geomID;
            if (string.IsNullOrEmpty(elemID))
                geomID = getNextGeomID();
            else
                geomID = elemID;

            SdoGeometry sdogeom = createSdoGeomBrep(brep);

            PrincipalComponentAnalysis pcaBrep = new PrincipalComponentAnalysis(pointList);
            Vector3D[] mjAxes = pcaBrep.identifyMajorAxes();
            SdoGeometry brepMjAxis1 = createSDOGeomPoint(mjAxes[0]);
            SdoGeometry brepMjAxis2 = createSDOGeomPoint(mjAxes[1]);
            SdoGeometry brepMjAxis3 = createSDOGeomPoint(mjAxes[2]);
            SdoGeometry brepMjCentroid = createSDOGeomPoint(pcaBrep.Centroid);

            addUserGeometry(geomID, sdogeom, mjAxis1, mjAxis2, mjAxis3, mjAxisCentroid);
            if (!skipSpatialIndex)
                createSpatialIndexAndFaceInfo(DBQueryManager.FedModelID, geomID, sdogeom);
            return geomID;
        }

        public static string createBrep(string elemID, List<Face3D> faceList, bool skipSpatialIndex)
        {
            string geomID;
            if (string.IsNullOrEmpty(elemID))
                geomID = getNextGeomID();
            else
                geomID = elemID;

            List<Point3D> pointList = new List<Point3D>();
            foreach(Face3D f in faceList)
            {
                pointList.AddRange(f.vertices);
            }

            SdoGeometry sdogeom = createSdoGeomBrep(faceList);
            PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis(pointList);
            Vector3D[] mjAxes = pca.identifyMajorAxes();
            SdoGeometry mjAxis1 = createSDOGeomPoint(mjAxes[0]);
            SdoGeometry mjAxis2 = createSDOGeomPoint(mjAxes[1]);
            SdoGeometry mjAxis3 = createSDOGeomPoint(mjAxes[2]);
            SdoGeometry mjAxisCentroid = createSDOGeomPoint(pca.Centroid);

            addUserGeometry(geomID, sdogeom, mjAxis1, mjAxis2, mjAxis3, mjAxisCentroid);
            if (!skipSpatialIndex)
                createSpatialIndexAndFaceInfo(DBQueryManager.FedModelID, geomID, sdogeom);
            return geomID;
        }
        /// <summary>
        /// This function creates a Brep along face edges that has an option to segmentize the edges
        /// </summary>
        /// <param name="elemID">elementID</param>
        /// <param name="baseFace">the Base face where the edges are to be used</param>
        /// <param name="depth">depth of the face to be created: + value means toward the interior, - value means away toward the exterior</param>
        /// <param name="extrusion">extrusion extent and direction: + value means it follows the face normal, - value means in the reverse direction</param>
        /// <param name="segmentize">value for the segment</param>
        /// <param name="skipIndex">if it is to skip the creation of the spatal index</param>
        /// <returns></returns>
        public static string createBrepFromEdge(string elemID, Face3D baseFace, double depth, double extrusion, double? segmentize, bool? skipIndex)
        {
            string geomID;
            if (string.IsNullOrEmpty(elemID))
                geomID = getNextGeomID();
            else
                geomID = elemID;
            bool skipSpatialIndex = false;
            if (skipIndex.HasValue)
                skipSpatialIndex = skipIndex.Value;

            Vector3D extrDir = new Vector3D(baseFace.basePlane.normalVector);
            if (extrusion < 0)
                extrDir.Negate();

            foreach (LineSegment3D edge in baseFace.boundaries)
            {
                Vector3D edgeDir = new Vector3D(edge.unNormalizedVector);
                edgeDir.Normalize();
                Vector3D dir = Vector3D.CrossProduct(baseFace.basePlane.normalVector, edgeDir);
                Point3D p1 = edge.startPoint;
                Point3D p2 = edge.endPoint;
                if (segmentize.HasValue)
                {
                    // Segmentize the edge
                    double segmentSize = segmentize.Value;
                    int noSeg = (int) Math.Ceiling(Point3D.distance(p1, p2) / segmentSize);
                    for (int i = 0; i < noSeg; i++)
                    {
                        Point3D p3 = p1 - depth * dir;
                        p2 = p1 + segmentSize * edgeDir;
                        Point3D p4 = p2 - depth * dir;
                        List<Point3D> pList = new List<Point3D>() { p1, p3, p4, p2, p1 };
                        Face3D makeFace = new Face3D(pList);
                        string geomIDSeg = geomID + "-" + i.ToString();
                        string ret = createExtrusion(geomIDSeg, makeFace, extrDir, Math.Abs(extrusion), skipSpatialIndex);
                        p1 = p2;
                    }
                }
                else
                {
                    // use single edge
                    Point3D p3 = p1 - depth * dir;
                    Point3D p4 = p2 - depth * dir;
                    List<Point3D> pList = new List<Point3D>() { p1, p3, p4, p2, p1 };
                    Face3D makeFace = new Face3D(pList);
                    string ret = createExtrusion(geomID, makeFace, extrDir, Math.Abs(extrusion), skipSpatialIndex);
                }
            }

            return geomID;
        }

        #endregion

        public static void createSpatialIndexAndFaceInfo(int federatedID, string geometryId, SdoGeometry sdogeom)
        {
            DBOperation.beginTransaction();
            string currStep = string.Empty;
            BIMRLCommon refBimrlCommon = new BIMRLCommon();
            OracleCommand commandIns = new OracleCommand(" ", DBOperation.DBConn);

            try
            {
                string sqlStmt = "INSERT INTO USERGEOM_SPATIALINDEX (ELEMENTID, CELLID, XMinBound, YMinBound, ZMinBound, XMaxBound, YMaxBound, ZMaxBound, Depth, CellType) "
                                    + "VALUES (:1, :2, :3, :4, :5, :6, :7, :8, :9, :10)";
                commandIns.CommandText = sqlStmt;
                currStep = sqlStmt;

                OracleParameter[] spatialIdx = new OracleParameter[10];
                spatialIdx[0] = commandIns.Parameters.Add("1", OracleDbType.Varchar2);
                spatialIdx[0].Direction = ParameterDirection.Input;
                spatialIdx[1] = commandIns.Parameters.Add("2", OracleDbType.Varchar2);
                spatialIdx[1].Direction = ParameterDirection.Input;
                spatialIdx[2] = commandIns.Parameters.Add("3", OracleDbType.Int32);
                spatialIdx[2].Direction = ParameterDirection.Input;
                spatialIdx[3] = commandIns.Parameters.Add("4", OracleDbType.Int32);
                spatialIdx[3].Direction = ParameterDirection.Input;
                spatialIdx[4] = commandIns.Parameters.Add("5", OracleDbType.Int32);
                spatialIdx[4].Direction = ParameterDirection.Input;
                spatialIdx[5] = commandIns.Parameters.Add("6", OracleDbType.Int32);
                spatialIdx[5].Direction = ParameterDirection.Input;
                spatialIdx[6] = commandIns.Parameters.Add("7", OracleDbType.Int32);
                spatialIdx[6].Direction = ParameterDirection.Input;
                spatialIdx[7] = commandIns.Parameters.Add("8", OracleDbType.Int32);
                spatialIdx[7].Direction = ParameterDirection.Input;
                spatialIdx[8] = commandIns.Parameters.Add("9", OracleDbType.Int32);
                spatialIdx[8].Direction = ParameterDirection.Input;
                spatialIdx[9] = commandIns.Parameters.Add("10", OracleDbType.Int32);
                spatialIdx[9].Direction = ParameterDirection.Input;

                Octree octreeInstance = new Octree(federatedID, null, DBOperation.OctreeSubdivLevel, true);      // true for the userDict

                // Use Defined Variable "KeepOrigUserGeomCell" to control whether we want the original cell to be kept or not (useful for documentation, not for runtime)
                bool keepCell = false;
                VarsInfo keepOrigCell = DefinedVarsManager.getDefinedVar("KeepOrigUserGeomCell");
                if (keepOrigCell.varValue != null)
                    bool.TryParse(keepOrigCell.varValue.ToString(), out keepCell);
                octreeInstance.userDictKeepOriginalCell = keepCell;

                Point3D llb;
                Point3D urt;
                DBOperation.getWorldBB(federatedID, out llb, out urt);

                int gtyp = sdogeom.PropertiesFromGTYPE();
                if (sdogeom.GeometryType == (int)SdoGeometryTypes.GTYPE.SOLID && sdogeom.OrdinatesArrayOfDoubles.Count() == 6)
                {
                    // This is an optimized Bbox with only LLB and URT. Derived all 6 faces
                    List<double> vertCoords = new List<double>();
                    // point #1
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[0]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[1]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[2]);
                    // point #2
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[3]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[1]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[2]);
                    // point #3
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[3]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[4]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[2]);
                    // point #4
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[0]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[4]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[2]);
                    // point #5
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[0]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[1]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[5]);
                    // point #6
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[3]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[1]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[5]);
                    // point #7
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[3]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[4]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[5]);
                    // point #8
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[0]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[4]);
                    vertCoords.Add(sdogeom.OrdinatesArrayOfDoubles[5]);

                    List<int> faceIdx = new List<int>();
                    // face #1
                    faceIdx.Add(0);
                    faceIdx.Add(3);
                    faceIdx.Add(15);
                    faceIdx.Add(12);
                    faceIdx.Add(0);
                    // face #2
                    faceIdx.Add(3);
                    faceIdx.Add(6);
                    faceIdx.Add(18);
                    faceIdx.Add(15);
                    faceIdx.Add(3);
                    // face #3
                    faceIdx.Add(6);
                    faceIdx.Add(9);
                    faceIdx.Add(21);
                    faceIdx.Add(18);
                    faceIdx.Add(6);
                    // face #4
                    faceIdx.Add(9);
                    faceIdx.Add(0);
                    faceIdx.Add(12);
                    faceIdx.Add(21);
                    faceIdx.Add(9);
                    // face #5
                    faceIdx.Add(0);
                    faceIdx.Add(3);
                    faceIdx.Add(6);
                    faceIdx.Add(9);
                    faceIdx.Add(0);
                    // face #6
                    faceIdx.Add(12);
                    faceIdx.Add(15);
                    faceIdx.Add(18);
                    faceIdx.Add(21);
                    faceIdx.Add(12);

                    Polyhedron geom = new Polyhedron(PolyhedronFaceTypeEnum.RectangularFaces, true, vertCoords, faceIdx, null);

                    // - Process face information and create consolidated faces and store them into USERGEOM_TOPO_FACE table
                    BIMRLGeometryPostProcess processFaces = new BIMRLGeometryPostProcess(geometryId, geom, refBimrlCommon, federatedID, null);
                    processFaces.simplifyAndMergeFaces();
                    processFaces.insertIntoDB(true);

                    octreeInstance.ComputeOctree(geometryId, geom, true);
                }
                else if (sdogeom.GeometryType == (int)SdoGeometryTypes.GTYPE.SOLID && sdogeom.OrdinatesArrayOfDoubles.Count() > 6)
                {
                    Polyhedron geom;
                    SDOGeomUtils.generate_Polyhedron(sdogeom, out geom);

                    // - Process face information and create consolidated faces and store them into USERGEOM_TOPO_FACE table
                    BIMRLGeometryPostProcess processFaces = new BIMRLGeometryPostProcess(geometryId, geom, refBimrlCommon, federatedID, null);
                    processFaces.simplifyAndMergeFaces();
                    processFaces.insertIntoDB(true);

                    octreeInstance.ComputeOctree(geometryId, geom, true);
                }
                else if (sdogeom.GeometryType == (int)SdoGeometryTypes.GTYPE.POLYGON)
                {
                    Face3D geom;
                    SDOGeomUtils.generate_Face3D(sdogeom, out geom);

                    octreeInstance.ComputeOctree(geometryId, geom, true);
                }
                else if (sdogeom.GeometryType == (int)SdoGeometryTypes.GTYPE.LINE || sdogeom.GeometryType == (int)SdoGeometryTypes.GTYPE.MULTILINE)
                {
                    for (int i = 0; i < sdogeom.OrdinatesArrayOfDoubles.Count() - 3; i+=3)
                    {
                        Point3D startP = new Point3D(sdogeom.OrdinatesArrayOfDoubles[i], sdogeom.OrdinatesArrayOfDoubles[i+1], sdogeom.OrdinatesArrayOfDoubles[i+2]);
                        Point3D endP = new Point3D(sdogeom.OrdinatesArrayOfDoubles[i+3], sdogeom.OrdinatesArrayOfDoubles[i+4], sdogeom.OrdinatesArrayOfDoubles[i+5]);
                        LineSegment3D geom = new LineSegment3D(startP, endP);

                        octreeInstance.ComputeOctree(geometryId, geom, true);
                    }
                }
                else
                {
                    throw new BIMRLInterfaceRuntimeException("Geometry type to be created is not supported!");
                }

                List<string> elemIDs;
                List<string> cellIDs;
                List<int> cellType;
                List<int> XMinBound;
                List<int> YMinBound;
                List<int> ZMinBound;
                List<int> XMaxBound;
                List<int> YMaxBound;
                List<int> ZMaxBound;
                List<int> depthList;

                octreeInstance.collectSpatialIndexUserDict(out elemIDs, out cellIDs, out XMinBound, out YMinBound, out ZMinBound, 
                                                            out XMaxBound, out YMaxBound, out ZMaxBound, out depthList, out cellType);
                if (cellIDs.Count > 0)
                {
                    //List<string> elemIDs = new List<string>();
                    //for (int ii = 0; ii < cellIDs.Count; ii++)
                    //    elemIDs.Add(elemID);

                    //int recCount = DBOperation.commitInterval;
                    int recCount = cellIDs.Count / 100;
                    if (recCount < 10000)
                        recCount = 10000;

                    while (elemIDs.Count > 0)
                    {
                        if (elemIDs.Count < recCount)
                            recCount = elemIDs.Count;

                        spatialIdx[0].Value = elemIDs.GetRange(0, recCount).ToArray();
                        elemIDs.RemoveRange(0, recCount);
                        spatialIdx[0].Size = recCount;
                        spatialIdx[1].Value = cellIDs.GetRange(0, recCount).ToArray();
                        cellIDs.RemoveRange(0, recCount);
                        spatialIdx[1].Size = recCount;
                        //spatialIdx[2].Value = borderFlagList.GetRange(0, recCount).ToArray();
                        //borderFlagList.RemoveRange(0, recCount);
                        //spatialIdx[2].Size = recCount;
                        spatialIdx[2].Value = XMinBound.GetRange(0, recCount).ToArray();
                        XMinBound.RemoveRange(0, recCount);
                        spatialIdx[2].Size = recCount;

                        spatialIdx[3].Value = YMinBound.GetRange(0, recCount).ToArray();
                        YMinBound.RemoveRange(0, recCount);
                        spatialIdx[3].Size = recCount;

                        spatialIdx[4].Value = ZMinBound.GetRange(0, recCount).ToArray();
                        ZMinBound.RemoveRange(0, recCount);
                        spatialIdx[4].Size = recCount;

                        spatialIdx[5].Value = XMaxBound.GetRange(0, recCount).ToArray();
                        XMaxBound.RemoveRange(0, recCount);
                        spatialIdx[5].Size = recCount;

                        spatialIdx[6].Value = YMaxBound.GetRange(0, recCount).ToArray();
                        YMaxBound.RemoveRange(0, recCount);
                        spatialIdx[6].Size = recCount;

                        spatialIdx[7].Value = ZMaxBound.GetRange(0, recCount).ToArray();
                        ZMaxBound.RemoveRange(0, recCount);
                        spatialIdx[7].Size = recCount;

                        spatialIdx[8].Value = depthList.GetRange(0, recCount).ToArray();
                        depthList.RemoveRange(0, recCount);
                        spatialIdx[8].Size = recCount;

                        spatialIdx[9].Value = cellType.GetRange(0, recCount).ToArray();
                        cellType.RemoveRange(0, recCount);
                        spatialIdx[9].Size = recCount;

                        commandIns.ArrayBindCount = recCount;

                        int commandStatus = commandIns.ExecuteNonQuery();
                        DBOperation.commitTransaction();
                    }
                }
                else
                {
                    // Something is not right that it does not return any Octree?
                    string msg = "%Warning: Octree master does not return anything";
                    refBimrlCommon.StackPushError(msg);
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                refBimrlCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                refBimrlCommon.StackPushError(excStr);
                throw;
            }

            commandIns.Dispose();
        }

        static PrincipalComponentAnalysis identifyMajorAxes(List<Point3D> pointList, out SdoGeometry mjAxisCentroid, out SdoGeometry mjAxis1, out SdoGeometry mjAxis2, out SdoGeometry mjAxis3)
        {
            PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis(pointList);
            Vector3D[] mjAxes = pca.identifyMajorAxes();

            mjAxis1 = null;
            mjAxis2 = null;
            mjAxis3 = null;
            Vector3D[] reorgMjAxes = new Vector3D[4];

            // Assign the longest axis to X
            if ((Math.Abs(mjAxes[0].X) >= Math.Abs(mjAxes[1].X)) && (Math.Abs(mjAxes[0].X) >= Math.Abs(mjAxes[2].X)))
            {
                mjAxis1 = createSDOGeomPoint(mjAxes[0]);
                reorgMjAxes[0] = new Vector3D(mjAxes[0]);
                if ((Math.Abs(mjAxes[1].Y) >= Math.Abs(mjAxes[2].Y)))
                {
                    mjAxis2 = createSDOGeomPoint(mjAxes[1]);
                    mjAxis3 = createSDOGeomPoint(mjAxes[2]);
                    reorgMjAxes[1] = new Vector3D(mjAxes[1]);
                    reorgMjAxes[2] = new Vector3D(mjAxes[2]);
                }
                else
                {
                    mjAxis2 = createSDOGeomPoint(mjAxes[2]);
                    mjAxis3 = createSDOGeomPoint(mjAxes[1]);
                    reorgMjAxes[1] = new Vector3D(mjAxes[2]);
                    reorgMjAxes[2] = new Vector3D(mjAxes[1]);
                }
            }
            else if ((Math.Abs(mjAxes[1].X) >= Math.Abs(mjAxes[0].X)) && (Math.Abs(mjAxes[1].X) >= Math.Abs(mjAxes[2].X)))
            {
                mjAxis1 = createSDOGeomPoint(mjAxes[1]);
                reorgMjAxes[0] = new Vector3D(mjAxes[1]);
                if ((Math.Abs(mjAxes[0].Y) >= Math.Abs(mjAxes[2].Y)))
                {
                    mjAxis2 = createSDOGeomPoint(mjAxes[0]);
                    mjAxis3 = createSDOGeomPoint(mjAxes[2]);
                    reorgMjAxes[1] = new Vector3D(mjAxes[0]);
                    reorgMjAxes[2] = new Vector3D(mjAxes[2]);
                }
                else
                {
                    mjAxis2 = createSDOGeomPoint(mjAxes[2]);
                    mjAxis3 = createSDOGeomPoint(mjAxes[0]);
                    reorgMjAxes[1] = new Vector3D(mjAxes[2]);
                    reorgMjAxes[2] = new Vector3D(mjAxes[0]);
                }
            }
            else if ((Math.Abs(mjAxes[2].X) >= Math.Abs(mjAxes[1].X)) && (Math.Abs(mjAxes[2].X) >= Math.Abs(mjAxes[0].X)))
            {
                mjAxis1 = createSDOGeomPoint(mjAxes[2]);
                reorgMjAxes[0] = new Vector3D(mjAxes[2]);
                if ((Math.Abs(mjAxes[0].Y) >= Math.Abs(mjAxes[1].Y)))
                {
                    mjAxis2 = createSDOGeomPoint(mjAxes[0]);
                    mjAxis3 = createSDOGeomPoint(mjAxes[1]);
                    reorgMjAxes[1] = new Vector3D(mjAxes[0]);
                    reorgMjAxes[2] = new Vector3D(mjAxes[1]);
                }
                else
                {
                    mjAxis2 = createSDOGeomPoint(mjAxes[1]);
                    mjAxis3 = createSDOGeomPoint(mjAxes[0]);
                    reorgMjAxes[1] = new Vector3D(mjAxes[1]);
                    reorgMjAxes[2] = new Vector3D(mjAxes[0]);
                }
            }

            mjAxisCentroid = createSDOGeomPoint(pca.Centroid);
            Matrix3D reorgMat = new Matrix3D(reorgMjAxes[0].X, reorgMjAxes[1].X, reorgMjAxes[2].X, 0,
                                            reorgMjAxes[0].Y, reorgMjAxes[1].Y, reorgMjAxes[2].Y, 0,
                                            reorgMjAxes[0].Z, reorgMjAxes[1].Z, reorgMjAxes[2].Z, 0,
                                           -pca.Centroid.X, -pca.Centroid.Y, -pca.Centroid.Z, 1);
            pca.transformMatrix = reorgMat;         // Update the transform materix following the reorganized axes
            return pca;
        }

        /// <summary>
        /// Only supported for 3D Face, i.e. in plannar plane (on X or Y axis direction or both)
        /// </summary>
        /// <param name="pca"></param>
        /// <param name="extend"></param>
        /// <param name="extendMod"></param>
        /// <returns></returns>
        static List<Point3D> extendGeometry(PrincipalComponentAnalysis pca, double extend, int extendMod)
        {
            List<Point3D> resList = new List<Point3D>();
            resList = pca.transformPointSet();
            if (extendMod == 1 || extendMod == 3)     // XEDGE, i.e. Y direction/mjAxis2
            {
                foreach (Point3D p in resList)
                {
                    if (p.Y > 0)
                        p.Y += extend;
                    else if (p.Y < 0)
                        p.Y -= extend;
                }
            }

            if (extendMod == 2 || extendMod == 3)     // XEDGE, i.e. Y direction/mjAxis2
            {
                foreach (Point3D p in resList)
                {
                    if (p.X > 0)
                        p.X += extend;
                    else if (p.X < 0)
                        p.X -= extend;
                }
            }
            resList = pca.transformBackPointSet(resList);
            return resList;
        }

        public static void truncateUserGeomTables()
        {
            DBQueryManager dbQ = new DBQueryManager();
            dbQ.runNonQuery("DROP TABLE USERGEOM_GEOMETRY_BCK", true);
            dbQ.runNonQuery("CREATE TABLE USERGEOM_GEOMETRY_BCK AS SELECT * FROM USERGEOM_GEOMETRY", true);
            dbQ.runNonQuery("TRUNCATE TABLE USERGEOM_GEOMETRY", true);
            dbQ.runNonQuery("DROP TABLE USERGEOM_TOPO_FACE_BCK");
            dbQ.runNonQuery("CREATE TABLE USERGEOM_TOPO_FACE_BCK AS SELECT * FROM USERGEOM_TOPO_FACE", true);
            dbQ.runNonQuery("TRUNCATE TABLE USERGEOM_TOPO_FACE", true);
            dbQ.runNonQuery("DROP TABLE USERGEOM_SPATIALINDEX_BCK", true);
            dbQ.runNonQuery("CREATE TABLE USERGEOM_SPATIALINDEX_BCK AS SELECT * FROM USERGEOM_SPATIALINDEX", true);
            dbQ.runNonQuery("TRUNCATE TABLE USERGEOM_SPATIALINDEX", true);
            dbQ.runNonQuery("DROP TABLE USERGEOM_OUTPUTDETAILS_BCK", true);
            dbQ.runNonQuery("CREATE TABLE USERGEOM_OUTPUTDETAILS_BCK AS SELECT * FROM USERGEOM_OUTPUTDETAILS", true);
            dbQ.runNonQuery("TRUNCATE TABLE USERGEOM_OUTPUTDETAILS", true);
        }

        public static void clearUserGeomTables()
        {
            DBQueryManager dbQ = new DBQueryManager();
            dbQ.runNonQuery("DELETE FROM USERGEOM_GEOMETRY", true);
            dbQ.runNonQuery("DELETE FROM USERGEOM_TOPO_FACE", true);
            dbQ.runNonQuery("DELETE FROM USERGEOM_SPATIALINDEX", true);
            dbQ.runNonQuery("DELETE FROM USERGEOM_OUTPUTDETAILS", true);
        }
    }
}
