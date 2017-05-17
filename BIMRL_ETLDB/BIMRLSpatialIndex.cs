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
using System.Data;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.Common;

namespace BIMRL
{
    public class BIMRLSpatialIndex
    {
        public static List<List<int>> polyHFaceVertIdxList = new List<List<int>>();
        public static List<List<double>> polyHCoordListList = new List<List<double>>();
        public static List<string> elemIDList = new List<string>();
        DataTable IdxList = new DataTable();

        BIMRLCommon _refBIMRLCommon;

        public BIMRLSpatialIndex(BIMRLCommon bimrlCommon)
        {
            _refBIMRLCommon = bimrlCommon;
            polyHFaceVertIdxList.Clear();
            polyHCoordListList.Clear();
            elemIDList.Clear();
        }

        public void createSpatialIndexFromBIMRLElement(int federatedId, string whereCond, bool createFaces=true, bool createSpIdx=true)
        {
            DBOperation.beginTransaction();
            string currStep = string.Empty;
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            OracleDataReader reader;
            bool selectedRegen = false;

            Point3D llb;
            Point3D urt;
            DBOperation.getWorldBB(federatedId, out llb, out urt);
            Octree.WorldBB = new BoundingBox3D(llb, urt);
            Octree.MaxDepth = DBOperation.OctreeSubdivLevel;

            try
            {
                command.CommandText = "SELECT COUNT(*) FROM " + DBOperation.formatTabName("BIMRL_ELEMENT", federatedId) + " where geometrybody is not null ";
                object rC = command.ExecuteScalar();
                int totalRowCount = Convert.ToInt32(rC.ToString()) * (int) Math.Pow(8,2);

                string sqlStmt = "select elementid, elementtype, geometrybody from " + DBOperation.formatTabName("BIMRL_ELEMENT", federatedId) + " where geometrybody is not null ";
                if (!string.IsNullOrEmpty(whereCond))
                {
                    sqlStmt += " and " + whereCond;
                    selectedRegen = true;
                }
                currStep = sqlStmt;

                command.CommandText = sqlStmt;
                command.FetchSize = 20;

                // The following is needed to update the element table with Bbox information
                string sqlStmt3 = "UPDATE " + DBOperation.formatTabName("BIMRL_ELEMENT", federatedId) + " SET GeometryBody_BBOX = :bbox, "
                                    + "GeometryBody_BBOX_CENTROID = :cent WHERE ELEMENTID = :eid";
                OracleCommand commandUpdBbox = new OracleCommand(" ", DBOperation.DBConn);
                commandUpdBbox.CommandText = sqlStmt3;
                commandUpdBbox.Parameters.Clear();

                OracleParameter[] Bbox = new OracleParameter[3];
                Bbox[0] = commandUpdBbox.Parameters.Add("bbox", OracleDbType.Object);
                Bbox[0].Direction = ParameterDirection.Input;
                Bbox[0].UdtTypeName = "MDSYS.SDO_GEOMETRY";
                List<List<SdoGeometry>> bboxListList = new List<List<SdoGeometry>>();
                List<SdoGeometry> bboxList = new List<SdoGeometry>();

                Bbox[1] = commandUpdBbox.Parameters.Add("cent", OracleDbType.Object);
                Bbox[1].Direction = ParameterDirection.Input;
                Bbox[1].UdtTypeName = "MDSYS.SDO_GEOMETRY";
                List<List<SdoGeometry>> centListList = new List<List<SdoGeometry>>();
                List<SdoGeometry> centList = new List<SdoGeometry>();

                Bbox[2] = commandUpdBbox.Parameters.Add("eid", OracleDbType.Varchar2);
                Bbox[2].Direction = ParameterDirection.Input;
                List<List<string>> eidUpdListList = new List<List<string>>();
                List<string> eidUpdList = new List<string>();
                int sublistCnt = 0;

                // end for Bbox

                Octree octreeInstance = null;
                if (selectedRegen)
                    octreeInstance = new Octree(federatedId, totalRowCount, DBOperation.OctreeSubdivLevel);
                else
                    octreeInstance = new Octree(federatedId, totalRowCount, DBOperation.OctreeSubdivLevel, false, true);      // Since it is not selectedRegen, we will rebuild the entire tree, skip Dict regen for this case

                reader = command.ExecuteReader();

                while (reader.Read())
                {

                    string elemID = reader.GetString(0);
                    string elemTyp = reader.GetString(1);

                    SdoGeometry sdoGeomData = reader.GetValue(2) as SdoGeometry;

                    Polyhedron geom;
                    if (!SDOGeomUtils.generate_Polyhedron(sdoGeomData, out geom))
                        continue;                                       // if there is something not right, skip the geometry

                    // - Update geometry info with BBox information
                    SdoGeometry bbox = new SdoGeometry();
                    bbox.Dimensionality = 3;
                    bbox.LRS = 0;
                    bbox.GeometryType = (int)SdoGeometryTypes.GTYPE.POLYGON;
                    int gType = bbox.PropertiesToGTYPE();

                    double[] arrCoord = new double[6];
                    int[] elemInfoArr = { 1, (int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_EXTERIOR, 1 };
                    arrCoord[0] = geom.boundingBox.LLB.X;
                    arrCoord[1] = geom.boundingBox.LLB.Y;
                    arrCoord[2] = geom.boundingBox.LLB.Z;
                    arrCoord[3] = geom.boundingBox.URT.X;
                    arrCoord[4] = geom.boundingBox.URT.Y;
                    arrCoord[5] = geom.boundingBox.URT.Z;

                    bbox.ElemArrayOfInts = elemInfoArr;
                    bbox.OrdinatesArrayOfDoubles = arrCoord;
                    bboxList.Add(bbox);

                    SdoGeometry centroid = new SdoGeometry();
                    centroid.Dimensionality = 3;
                    centroid.LRS = 0;
                    centroid.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                    gType = centroid.PropertiesToGTYPE();
                    SdoPoint cent = new SdoPoint();
                    cent.XD = geom.boundingBox.Center.X;
                    cent.YD = geom.boundingBox.Center.Y;
                    cent.ZD = geom.boundingBox.Center.Z;
                    centroid.SdoPoint = cent;
                    centList.Add(centroid);

                    eidUpdList.Add(elemID);

                    sublistCnt++;

                    // Set 1000 records as a threshold for interval commit later on
                    if (sublistCnt >= 500)
                    {
                        bboxListList.Add(bboxList);
                        centListList.Add(centList);
                        eidUpdListList.Add(eidUpdList);

                        sublistCnt = 0;
                        bboxList = new List<SdoGeometry>();
                        centList = new List<SdoGeometry>();
                        eidUpdList = new List<string>();
                    }

                    // We will skip large buildinglementproxy that has more than 5000 vertices
                    bool largeMesh = (string.Compare(elemTyp, "IFCBUILDINGELEMENTPROXY", true) == 0) && geom.Vertices.Count > 5000;
                    if ((createFaces && !largeMesh) || (createFaces && selectedRegen))
                    {
                        // - Process face information and create consolidated faces and store them into BIMRL_TOPO_FACE table
                        BIMRLGeometryPostProcess processFaces = new BIMRLGeometryPostProcess(elemID, geom, _refBIMRLCommon, federatedId, null);
                        processFaces.simplifyAndMergeFaces();
                        processFaces.insertIntoDB(false);
                    }

                    if (createSpIdx)
                       octreeInstance.ComputeOctree(elemID, geom);
                }

                reader.Dispose();


                  if (createSpIdx)
                  {
                     // Truncate the table first before reinserting the records
                     FederatedModelInfo fedModel = DBOperation.getFederatedModelByID(federatedId);
                     if (DBOperation.DBUserID.Equals(fedModel.FederatedID))
                        DBOperation.executeSingleStmt("TRUNCATE TABLE " + DBOperation.formatTabName("BIMRL_SPATIALINDEX", federatedId));
                     else
                        DBOperation.executeSingleStmt("DELETE FROM " + DBOperation.formatTabName("BIMRL_SPATIALINDEX"));

                     collectSpatialIndexAndInsert(octreeInstance, federatedId);
                  }

                  if (sublistCnt > 0)
                  {
                     bboxListList.Add(bboxList);
                     centListList.Add(centList);
                     eidUpdListList.Add(eidUpdList);
                  }

                for (int i = 0; i < eidUpdListList.Count; i++)
                {
                    Bbox[0].Value = bboxListList[i].ToArray();
                    Bbox[0].Size = bboxListList[i].Count;
                    Bbox[1].Value = centListList[i].ToArray();
                    Bbox[1].Size = centListList[i].Count;
                    Bbox[2].Value = eidUpdListList[i].ToArray();
                    Bbox[2].Size = eidUpdListList[i].Count;

                    commandUpdBbox.ArrayBindCount = eidUpdListList[i].Count;    // No of values in the array to be inserted
                    int commandStatus = commandUpdBbox.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                }

                if (!string.IsNullOrEmpty(whereCond) && createSpIdx)
                {
                    command.CommandText = "UPDATE BIMRL_FEDERATEDMODEL SET MAXOCTREELEVEL=" + Octree.MaxDepth.ToString() + " WHERE FEDERATEDID=" + federatedId.ToString();
                    command.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }

            command.Dispose();
        }

        public void createFacesFromBIMRLElement(int federatedId, string whereCond)
        {
            DBOperation.beginTransaction();
            string currStep = string.Empty;
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            OracleDataReader reader;

            try
            {
                SdoGeometry sdoGeomData = new SdoGeometry();

                string sqlStmt = "select elementid, geometrybody from " + DBOperation.formatTabName("BIMRL_ELEMENT", federatedId) + " where geometrybody is not null ";
                if (!string.IsNullOrEmpty(whereCond))
                    sqlStmt += " and " + whereCond;
                currStep = sqlStmt;

                command.CommandText = sqlStmt;
                command.FetchSize = 20;

                reader = command.ExecuteReader();

                while (reader.Read())
                {

                    string elemID = reader.GetString(0);
                    sdoGeomData = reader.GetValue(1) as SdoGeometry;

                    Polyhedron geom;
                    if (!SDOGeomUtils.generate_Polyhedron(sdoGeomData, out geom))
                        continue;                                       // if there is something not right, skip the geometry

                    // - Process face information and create consolidated faces and store them into BIMRL_TOPO_FACE table
                    BIMRLGeometryPostProcess processFaces = new BIMRLGeometryPostProcess(elemID, geom, _refBIMRLCommon, federatedId, null);
                    processFaces.simplifyAndMergeFaces();
                    processFaces.insertIntoDB(false);
                }
                reader.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }

            command.Dispose();
        }

        public void computeMajorAxes(int federatedId, string whereCond)
        {
            DBOperation.beginTransaction();
            string currStep = string.Empty;
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            OracleDataReader reader;

            try
            {
                SdoGeometry sdoGeomData = new SdoGeometry();

                string sqlStmt = "select elementid, geometrybody from " + DBOperation.formatTabName("BIMRL_ELEMENT", federatedId) + " where geometrybody is not null ";
                if (!string.IsNullOrEmpty(whereCond))
                    sqlStmt += " and " + whereCond;
                currStep = sqlStmt;

                command.CommandText = sqlStmt;
                command.FetchSize = 20;

                reader = command.ExecuteReader();

                while (reader.Read())
                {

                    string elemID = reader.GetString(0);
                    sdoGeomData = reader.GetValue(1) as SdoGeometry;

                    Polyhedron geom;
                    if (!SDOGeomUtils.generate_Polyhedron(sdoGeomData, out geom))
                        continue;                                       // if there is something not right, skip the geometry

                    // - Process face information and create consolidated faces and store them into BIMRL_TOPO_FACE table
                    BIMRLGeometryPostProcess majorAxes = new BIMRLGeometryPostProcess(elemID, geom, _refBIMRLCommon, federatedId, null);
                    majorAxes.deriveMajorAxes();
                }
                reader.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }

            command.Dispose();
        }

        public void collectSpatialIndexAndInsert(Octree octreeInstance, int federatedId)
        {
            string sqlStmt = "INSERT INTO " + DBOperation.formatTabName("BIMRL_SPATIALINDEX", federatedId) + " (ELEMENTID, CELLID, XMinBound, YMinBound, ZMinBound, XMaxBound, YMaxBound, ZMaxBound, Depth) "
                                + "VALUES (:1, :2, :3, :4, :5, :6, :7, :8, :9)";
            OracleCommand commandIns = new OracleCommand(" ", DBOperation.DBConn);
            commandIns.CommandText = sqlStmt;

            OracleParameter[] spatialIdx = new OracleParameter[9]; 
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

            int initArraySize = 1000;
            List<string> elementIDList = new List<string>(initArraySize);
            List<string> cellIDStrList = new List<string>(initArraySize);

            List<int> XMinB = new List<int>(initArraySize);
            List<int> YMinB = new List<int>(initArraySize);
            List<int> ZMinB = new List<int>(initArraySize);
            List<int> XMaxB = new List<int>(initArraySize);
            List<int> YMaxB = new List<int>(initArraySize);
            List<int> ZMaxB = new List<int>(initArraySize);
            List<int> depthList = new List<int>(initArraySize);

            int XMin;
            int YMin;
            int ZMin;
            int XMax;
            int YMax;
            int ZMax;

            int recCount = 0;

            foreach (KeyValuePair<UInt64, Octree.CellData> dictEntry in octreeInstance.MasterDict)
            {
                CellID64 cellID = new CellID64(dictEntry.Key);
                string cellIDstr = cellID.ToString();
                CellID64.getCellIDComponents(cellID, out XMin, out YMin, out ZMin, out XMax, out YMax, out ZMax);
                int cellLevel = CellID64.getLevel(cellID);

                if (dictEntry.Value.data != null && dictEntry.Value.nodeType != 0)
                {
                    foreach (int tupEID in dictEntry.Value.data)
                    {
                        List<int> cBound = new List<int>();

                        ElementID eID = new ElementID(Octree.getElementIDByIndex(tupEID));
                        elementIDList.Add(eID.ElementIDString);
                        //cellIDStrList.Add(cellID.ToString());
                        cellIDStrList.Add(cellIDstr);

                        //CellID64.getCellIDComponents(cellID, out XMin, out YMin, out ZMin, out XMax, out YMax, out ZMax);
                        XMinB.Add(XMin);
                        YMinB.Add(YMin);
                        ZMinB.Add(ZMin);
                        XMaxB.Add(XMax);
                        YMaxB.Add(YMax);
                        ZMaxB.Add(ZMax);
                        //depthList.Add(CellID64.getLevel(cellID));
                        depthList.Add(cellLevel);
                    }
                }

                try 
                {
                    recCount = elementIDList.Count;
                    if (recCount >= initArraySize)
                    {
                        spatialIdx[0].Value = elementIDList.ToArray();
                        spatialIdx[0].Size = recCount;
                        spatialIdx[1].Value = cellIDStrList.ToArray();
                        spatialIdx[1].Size = recCount;
                        spatialIdx[2].Value = XMinB.ToArray();
                        spatialIdx[2].Size = recCount;

                        spatialIdx[3].Value = YMinB.ToArray();
                        spatialIdx[3].Size = recCount;

                        spatialIdx[4].Value = ZMinB.ToArray();
                        spatialIdx[4].Size = recCount;

                        spatialIdx[5].Value = XMaxB.ToArray();
                        spatialIdx[5].Size = recCount;

                        spatialIdx[6].Value = YMaxB.ToArray();
                        spatialIdx[6].Size = recCount;

                        spatialIdx[7].Value = ZMaxB.ToArray();
                        spatialIdx[7].Size = recCount;

                        spatialIdx[8].Value = depthList.ToArray();
                        spatialIdx[8].Size = recCount;

                        commandIns.ArrayBindCount = recCount;

                        int commandStatus = commandIns.ExecuteNonQuery();
                        DBOperation.commitTransaction();

                        elementIDList.Clear();
                        cellIDStrList.Clear();
                        XMinB.Clear();
                        YMinB.Clear();
                        ZMinB.Clear();
                        XMaxB.Clear();
                        YMaxB.Clear();
                        ZMaxB.Clear();
                        depthList.Clear();
                    }
                }
                catch (OracleException e)
                {
                    string excStr = "%%Insert Spatial Index Error - " + e.Message + "\n\t" ;
                    _refBIMRLCommon.StackPushError(excStr);
                }
                catch (SystemException e)
                {
                    string excStr = "%%Insert Spatial Index Error - " + e.Message + "\n\t";
                    _refBIMRLCommon.StackPushError(excStr);
                    throw;
                }
            }

            // At last if there are entries in the list, insert them
            try
            {
                recCount = elementIDList.Count;
                if (recCount > 0)
                {
                    spatialIdx[0].Value = elementIDList.ToArray();
                    spatialIdx[0].Size = recCount;
                    spatialIdx[1].Value = cellIDStrList.ToArray();
                    spatialIdx[1].Size = recCount;
                    spatialIdx[2].Value = XMinB.ToArray();
                    spatialIdx[2].Size = recCount;

                    spatialIdx[3].Value = YMinB.ToArray();
                    spatialIdx[3].Size = recCount;

                    spatialIdx[4].Value = ZMinB.ToArray();
                    spatialIdx[4].Size = recCount;

                    spatialIdx[5].Value = XMaxB.ToArray();
                    spatialIdx[5].Size = recCount;

                    spatialIdx[6].Value = YMaxB.ToArray();
                    spatialIdx[6].Size = recCount;

                    spatialIdx[7].Value = ZMaxB.ToArray();
                    spatialIdx[7].Size = recCount;

                    spatialIdx[8].Value = depthList.ToArray();
                    spatialIdx[8].Size = recCount;

                    commandIns.ArrayBindCount = recCount;

                    int commandStatus = commandIns.ExecuteNonQuery();
                    DBOperation.commitTransaction();

                    elementIDList.Clear();
                    cellIDStrList.Clear();
                    XMinB.Clear();
                    YMinB.Clear();
                    ZMinB.Clear();
                    XMaxB.Clear();
                    YMaxB.Clear();
                    ZMaxB.Clear();
                    depthList.Clear();
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Insert Spatial Index Error - " + e.Message + "\n\t";
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Insert Spatial Index Error - " + e.Message + "\n\t";
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }
            commandIns.Dispose();
        }
    }
}
