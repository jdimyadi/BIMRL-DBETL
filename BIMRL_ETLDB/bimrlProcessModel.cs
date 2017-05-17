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
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using Xbim.Common;
using Xbim.Common.Federation;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.Common;

namespace BIMRL
{
   public class BIMRLProcessModel
   {
      //static int DBOperation.currFedModel.FederatedID = 0;

      static int _ModelID = 0;
      BIMRLCommon _bimrlCommon = new BIMRLCommon();

      //public static int currFedID
      //{ 
      //   get { return DBOperation.currFedModel.FederatedID; } 
      //}

      public static int currModelID
      {
         get { return _ModelID; }
      }

      public BIMRLProcessModel(IModel model, bool update)
      {
         //IfcProject proj = model.IfcProject;
         IfcStore modelStore = model as IfcStore;
         string currStep = String.Empty;
         _bimrlCommon.resetAll();

         // Connect to Oracle DB
         DBOperation.refBIMRLCommon = _bimrlCommon;      // important to ensure DBoperation has reference to this object!!

         try
         {
            DBOperation.ExistingOrDefaultConnection();
         }
         catch
         {
            if (DBOperation.UIMode)
            {
               BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(_bimrlCommon);
               erroDlg.ShowDialog();
            }
            else
               Console.Write(_bimrlCommon.ErrorMessages);
            return;
         }

         DBOperation.commitInterval = 5000;

         // Initial Spatial index for later use
         BIMRLSpatialIndex spIdx = new BIMRLSpatialIndex(_bimrlCommon);

         try
         { 
            DBOperation.beginTransaction();
            IIfcProject firstProject;
            if (modelStore.IsFederation)
            {
               IfcStore firstModel = modelStore.ReferencedModels.FirstOrDefault().Model as IfcStore;
               firstProject = firstModel.Instances.OfType<IIfcProject>().FirstOrDefault();
            }
            else
            {
               firstProject = modelStore.Instances.OfType<IIfcProject>().FirstOrDefault();
            }
            string projLName;

            // Check whether Model has been defined before
            if (string.IsNullOrEmpty(firstProject.LongName))
               projLName = firstProject.Name + " - Federated";
            else
               projLName = firstProject.LongName;

            string modelNameFromFile;
            if (!string.IsNullOrEmpty(modelStore.FileName))
               modelNameFromFile = Path.GetFileNameWithoutExtension(modelStore.FileName);
            else
               modelNameFromFile = firstProject.Name + " - " + firstProject.LongName;

            currStep = "Getting Federated ID from BIMRL_FEDERATEDMODEL - Model name, Project name and longname: " + modelNameFromFile + "; " + firstProject.Name + "; " + firstProject.LongName;
            FederatedModelInfo fedModel;
            FedIDStatus stat = DBOperation.getFederatedModel(modelNameFromFile, projLName, firstProject.Name, out fedModel);
            if (stat == FedIDStatus.FedIDNew)
            {
               DBOperation.currFedModel = fedModel;
               // Create new set of tables using the fedID as suffix
               int retStat = DBOperation.createModelTables(DBOperation.currFedModel.FederatedID);
            }
            else
            {
               DBOperation.currFedModel = fedModel;
               if (!fedModel.Owner.Equals(DBOperation.DBUserID))
               {
                  _bimrlCommon.StackPushError("%Error: Only the Owner (" + fedModel.Owner + ") can delete or override existing model (!" + fedModel.ModelName + ")");
                  throw new Exception("%Error: Unable to overwrite exisitng model");
               }

               // Drop and recreate tables
               currStep = "Dropping existing model tables (ID: " + DBOperation.currFedModel.FederatedID.ToString("X4") + ")";
               int retStat = DBOperation.dropModelTables(DBOperation.currFedModel.FederatedID);
               currStep = "Creating model tables (ID: " + DBOperation.currFedModel.FederatedID.ToString("X4") + ")";
               retStat = DBOperation.createModelTables(DBOperation.currFedModel.FederatedID);
            }

            DBOperation.currSelFedID = DBOperation.currFedModel.FederatedID;        // set the static variable keeping the selected Fed Id

            if (modelStore.IsFederation)
            {
               // get all models

               foreach (IReferencedModel refModel in modelStore.ReferencedModels)
               {
                  IfcStore m = refModel.Model as IfcStore;
                  currStep = "Getting Model ID for Federated model ID:" + DBOperation.currFedModel.FederatedID.ToString("X4");
                  _bimrlCommon.ClearDicts();

                  _ModelID = DBOperation.getModelID(DBOperation.currFedModel.FederatedID);
                  doModel(m);
                  BIMRLUtils.ResetIfcUnitDicts();
               }

            }
            else
            {
               currStep = "Getting Model ID for Federated model ID:" + DBOperation.currFedModel.FederatedID.ToString("X4");
               _ModelID = DBOperation.getModelID(DBOperation.currFedModel.FederatedID);
               doModel(modelStore);
               BIMRLUtils.ResetIfcUnitDicts();
            }
         }
         catch (Exception e)
         {
            string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
            _bimrlCommon.StackPushError(excStr);
            DBOperation.endTransaction(false);  // rollback
         }

         DBOperation.endTransaction(true);     // Commit = true

         try
         {
            DBOperation.beginTransaction();
            OracleCommand cmd = new OracleCommand("", DBOperation.DBConn);

            // Define the spatial index metadata
            double marginX = (_bimrlCommon.URT_X - _bimrlCommon.LLB_X) * 0.2; // 20% margin
            double marginY = (_bimrlCommon.URT_Y - _bimrlCommon.LLB_Y) * 0.2; // 20% margin
            double marginZ = (_bimrlCommon.URT_Z - _bimrlCommon.LLB_Z) * 0.2; // 20% margin
            double lowerX = _bimrlCommon.LLB_X - marginX;
            double upperX = _bimrlCommon.URT_X + marginX;
            double lowerY = _bimrlCommon.LLB_Y - marginY;
            double upperY = _bimrlCommon.URT_Y + marginY;
            double lowerZ = _bimrlCommon.LLB_Z - marginZ;
            double upperZ = _bimrlCommon.URT_Z + marginZ;

            string sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_ELEMENT_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','GEOMETRYBODY',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', " + lowerX.ToString() + ", " + upperX.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', " + lowerY.ToString() + ", " + upperY.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', " + lowerZ.ToString() + ", " + upperZ.ToString() + ", 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_ELEMENT_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','GEOMETRYBODY_BBOX',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', " + lowerX.ToString() + ", " + upperX.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', " + lowerY.ToString() + ", " + upperY.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', " + lowerZ.ToString() + ", " + upperZ.ToString() + ", 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_ELEMENT_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','GEOMETRYBODY_BBOX_CENTROID',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', " + lowerX.ToString() + ", " + upperX.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', " + lowerY.ToString() + ", " + upperY.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', " + lowerZ.ToString() + ", " + upperZ.ToString() + ", 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_ELEMENT_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','GEOMETRYFOOTPRINT',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', " + lowerX.ToString() + ", " + upperX.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', " + lowerY.ToString() + ", " + upperY.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', " + lowerZ.ToString() + ", " + upperZ.ToString() + ", 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_ELEMENT_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','GEOMETRYAXIS',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', " + lowerX.ToString() + ", " + upperX.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', " + lowerY.ToString() + ", " + upperY.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', " + lowerZ.ToString() + ", " + upperZ.ToString() + ", 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_ELEMENT_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','BODY_MAJOR_AXIS1',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', -1.01, 1.01, 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', -1.01, 1.01, 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', -1.01, 1.01, 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_ELEMENT_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','BODY_MAJOR_AXIS2',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', -1.01, 1.01, 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', -1.01, 1.01, 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', -1.01, 1.01, 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_ELEMENT_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','BODY_MAJOR_AXIS3',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', -1.01, 1.01, 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', -1.01, 1.01, 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', -1.01, 1.01, 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_ELEMENT_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','BODY_MAJOR_AXIS_CENTROID',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', " + lowerX.ToString() + ", " + upperX.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', " + lowerY.ToString() + ", " + upperY.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', " + lowerZ.ToString() + ", " + upperZ.ToString() + ", 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_TOPO_FACE_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','POLYGON',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', " + lowerX.ToString() + ", " + upperX.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', " + lowerY.ToString() + ", " + upperY.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', " + lowerZ.ToString() + ", " + upperZ.ToString() + ", 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_TOPO_FACE_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','NORMAL',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', -1.01, 1.01, 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', -1.01, 1.01, 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', -1.01, 1.01, 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            sqlStmt = "insert into USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES "
                           + "('BIMRL_TOPO_FACE_" + DBOperation.currFedModel.FederatedID.ToString("X4") + "','CENTROID',"
                           + "SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', " + lowerX.ToString() + ", " + upperX.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Y', " + lowerY.ToString() + ", " + upperY.ToString() + ", 0.000001),"
                           + "SDO_DIM_ELEMENT('Z', " + lowerZ.ToString() + ", " + upperZ.ToString() + ", 0.000001)),"
                           + "NULL)";
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();


            sqlStmt = "Update BIMRL_FEDERATEDMODEL SET LastUpdateDate=sysdate WHERE FederatedID=" + DBOperation.currFedModel.FederatedID.ToString();
            currStep = sqlStmt;
            cmd.CommandText = sqlStmt;
            cmd.ExecuteNonQuery();

            // get the world BBox coordinates and update the BIMRL_FEDERATEDMODEL Table
            SdoGeometry Bbox = new SdoGeometry();
            Bbox.Dimensionality = 3;
            Bbox.LRS = 0;
            Bbox.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
            int gType = Bbox.PropertiesToGTYPE();

            int[] elemInfoArr = { 1, (int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_EXTERIOR, 1 };
            Bbox.ElemArrayOfInts = elemInfoArr;

            double[] coordArr = new double[6];
            coordArr[0] = _bimrlCommon.LLB_X;
            coordArr[1] = _bimrlCommon.LLB_Y;
            coordArr[2] = _bimrlCommon.LLB_Z;
            coordArr[3] = _bimrlCommon.URT_X;
            coordArr[4] = _bimrlCommon.URT_Y;
            coordArr[5] = _bimrlCommon.URT_Z;
            Bbox.OrdinatesArrayOfDoubles = coordArr;

            // Create spatial index from the new model (list of triangles are accummulated during processing of geometries)
            sqlStmt = "update BIMRL_FEDERATEDMODEL SET WORLDBBOX=:1 , MAXOCTREELEVEL=:2 WHERE FEDERATEDID=" + DBOperation.currFedModel.FederatedID.ToString();
            currStep = sqlStmt;
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            command.CommandText = sqlStmt;

            OracleParameter[] sdoGeom2 = new OracleParameter[3];
            sdoGeom2[0] = command.Parameters.Add("1", OracleDbType.Object);
            sdoGeom2[0].Direction = ParameterDirection.Input;
            sdoGeom2[0].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            sdoGeom2[0].Value = Bbox;
            sdoGeom2[0].Size = 1;
            sdoGeom2[1] = command.Parameters.Add("2", OracleDbType.Int16);
            sdoGeom2[1].Direction = ParameterDirection.Input;
            sdoGeom2[1].Value = DBOperation.OctreeSubdivLevel;
            sdoGeom2[1].Size = 1;

            int commandStatus = command.ExecuteNonQuery();

            if (DBOperation.OnepushETL)
            {
               DBOperation.commitInterval = 10000;
               int octreeLevel = DBOperation.computeRecomOctreeLevel(DBOperation.currFedModel.FederatedID);

               // 1. Create Octree spatial indexes and the Brep Topology Faces
               spIdx.createSpatialIndexFromBIMRLElement(DBOperation.currFedModel.FederatedID, null, true, true);

               // 2. Update major Axes and OBB
               BIMRLUtils.updateMajorAxesAndOBB(DBOperation.currFedModel.FederatedID, null);

               // 3. Enhance Space Boundary
               EnhanceBRep eBrep = new EnhanceBRep();
               eBrep.enhanceSpaceBoundary(null);

               // 4. Process Face orientations. We will procees the normal face first and then after that the spacial ones (OBB, PROJOBB)
               string whereCond2 = "";
               BIMRLCommon.appendToString(" TYPE NOT IN ('OBB','PROJOBB')", " AND ", ref whereCond2);
               eBrep.ProcessOrientation(whereCond2);
               whereCond2 = "";
               BIMRLCommon.appendToString(" TYPE='OBB'", " AND ", ref whereCond2);
               eBrep.ProcessOrientation(whereCond2);
               whereCond2 = "";
               BIMRLCommon.appendToString(" TYPE='PROJOBB'", " AND ", ref whereCond2);
               eBrep.ProcessOrientation(whereCond2);

               // 5. Create Graph Data
               BIMRLGraph.GraphData graphData = new BIMRLGraph.GraphData();
               graphData.createCirculationGraph(DBOperation.currFedModel.FederatedID);
               graphData.createSpaceAdjacencyGraph(DBOperation.currFedModel.FederatedID);

               sqlStmt = "UPDATE BIMRL_FEDERATEDMODEL SET LASTUPDATEDATE=sysdate";
               BIMRLCommon.appendToString("MAXOCTREELEVEL=" + octreeLevel.ToString(), ", ", ref sqlStmt);
               BIMRLCommon.appendToString("WHERE FEDERATEDID=" + DBOperation.currFedModel.FederatedID.ToString(), " ", ref sqlStmt);
               DBOperation.executeSingleStmt(sqlStmt);
            }
            else
            {
               // Minimum (without One-push ETL, create the bounding boxes (AABB)
               spIdx.createSpatialIndexFromBIMRLElement(DBOperation.currFedModel.FederatedID, null, false, false);
            }

            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            string exePath = new FileInfo(location.AbsolutePath).Directory.FullName;

            // (Re)-Create the spatial indexes
            DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_SpatialIndexes.sql"), DBOperation.currFedModel.FederatedID);
            DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_TopoFace.sql"), DBOperation.currFedModel.FederatedID);
            DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_MajorAxes.sql"), DBOperation.currFedModel.FederatedID);

            //sqlStmt = "Create Index IDX_BIMRLELEM_GEOM_" + currFedID.ToString("X4") + " on BIMRL_ELEMENT_" + currFedID.ToString("X4")
            //            + " (GEOMETRYBODY) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_BIMRLELEM_GEOMBB_" + currFedID.ToString("X4") + " on BIMRL_ELEMENT_" + currFedID.ToString("X4")
            //            + " (GeometryBody_BBOX) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_BIMRLELEM_GEOMBBC_" + currFedID.ToString("X4") + " on BIMRL_ELEMENT_" + currFedID.ToString("X4")
            //            + " (GeometryBody_BBOX_CENTROID) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_BIMRLELEM_GEOMFP_" + currFedID.ToString("X4") + " on BIMRL_ELEMENT_" + currFedID.ToString("X4")
            //            + " (GeometryFootprint) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_BIMRLELEM_GEOMAX_" + currFedID.ToString("X4") + " on BIMRL_ELEMENT_" + currFedID.ToString("X4")
            //            + " (GeometryAxis) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_BIMRLELEM_GEOMMJ1_" + currFedID.ToString("X4") + " on BIMRL_ELEMENT_" + currFedID.ToString("X4")
            //            + " (BODY_MAJOR_AXIS1) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_BIMRLELEM_GEOMMJ2_" + currFedID.ToString("X4") + " on BIMRL_ELEMENT_" + currFedID.ToString("X4")
            //            + " (BODY_MAJOR_AXIS2) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_BIMRLELEM_GEOMMJ3_" + currFedID.ToString("X4") + " on BIMRL_ELEMENT_" + currFedID.ToString("X4")
            //            + " (BODY_MAJOR_AXIS3) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_BIMRLELEM_GEOMMJC_" + currFedID.ToString("X4") + " on BIMRL_ELEMENT_" + currFedID.ToString("X4")
            //            + " (BODY_MAJOR_AXIS_CENTROID) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_TOPOF_POLYGON_" + currFedID.ToString("X4") + " on BIMRL_TOPO_FACE_" + currFedID.ToString("X4")
            //            + " (POLYGON) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_TOPOF_NORMAL_" + currFedID.ToString("X4") + " on BIMRL_TOPO_FACE_" + currFedID.ToString("X4")
            //            + " (NORMAL) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            //sqlStmt = "Create Index IDX_TOPOF_CENTROID_" + currFedID.ToString("X4") + " on BIMRL_TOPO_FACE_" + currFedID.ToString("X4")
            //            + " (CENTROID) INDEXTYPE is MDSYS.SPATIAL_INDEX PARAMETERS ('sdo_indx_dims=3')";
            //currStep = sqlStmt;
            //cmd.CommandText = sqlStmt;
            //cmd.ExecuteNonQuery();

            DBOperation.commitTransaction();

         }
         catch (Exception e)
         {
            string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
            _bimrlCommon.StackPushError(excStr);
            if (DBOperation.UIMode)
            {
               BIMRLErrorDialog errorDlg = new BIMRLErrorDialog(_bimrlCommon);
               errorDlg.ShowDialog();
            }
            else
               Console.Write(_bimrlCommon.ErrorMessages);
         }

         // There are entries in the error stack, show them at the end
         if (_bimrlCommon.BIMRLErrorStackCount > 0)
         {
            if (DBOperation.UIMode)
            {
               BIMRLErrorDialog errorDlg = new BIMRLErrorDialog(_bimrlCommon);
               errorDlg.ShowDialog();
            }
            else
               Console.Write(_bimrlCommon.ErrorMessages);
         }
      }

      private void doModel(IfcStore m)
      {
         string projName = m.Instances.FirstOrDefault<IIfcProject>().Name;
         string SqlStmt;
         string currStep = string.Empty;
         m.BeginCaching();

         try
         {
            // Insert model info
            SqlStmt = "Insert into " + DBOperation.formatTabName("BIMRL_ModelInfo") + " (ModelID, ModelName, Source) Values ("
                        + _ModelID + ",'" + projName + "','" + m.FileName + "')";
            currStep = SqlStmt;
            int status = DBOperation.insertRow(SqlStmt);

            // Process the owner history
            currStep = "Processing Owner History.";
            BIMRLOwnerHistory procOwnH = new BIMRLOwnerHistory(m, _bimrlCommon);
            procOwnH.processOwnerHistory();

            // Process all Ifc Type definitions
            currStep = "Processing IFC Type Objects.";
            BIMRLTypeObject bimrlTyp = new BIMRLTypeObject(m, _bimrlCommon);
            bimrlTyp.processTypeObject();
                
            // Process structure elements
            // processStructureElementData(m);
            currStep = "Processing Spatial Structure Elements.";
            BIMRLSpatialStructure spaStruc = new BIMRLSpatialStructure(m, _bimrlCommon);
            spaStruc.processElementStructure();

            // Process all IfcProduct for BIMRL_ELEMENT
            currStep = "Processing IFC Elements.";
            BIMRLElement procElem = new BIMRLElement(m, _bimrlCommon);
            procElem.processElements();

            currStep = "Processing All relationships.";
            // Process All relationships
            BIMRLRelationship procRel = new BIMRLRelationship(m, _bimrlCommon);
            procRel.processRelationships();

            currStep = "Processing Classifications.";
            // Process All Classifications
            BIMRLClassification procClassif = new BIMRLClassification(m, _bimrlCommon);
            procClassif.processClassificationItems();

            currStep = "Processing Materials.";
            // Process All Materials
            BIMRLMaterial procMat = new BIMRLMaterial(m, _bimrlCommon);
            procMat.processMaterials();
         }
         catch (SystemException e)
         {
            string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
            _bimrlCommon.StackPushError(excStr);
            throw;
         }
         m.StopCaching();
      }

      public string ErrorsInStack()
      {
          return _bimrlCommon.ErrorMessages;
      }
   }
}
