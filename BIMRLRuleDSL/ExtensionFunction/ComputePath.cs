//
// BIMRL (BIM Rule Language) library: this library performs DSL for rule checking using BIM Rule Language that works on BIMRL Simplified Schema on RDBMS. 
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
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL;
using BIMRLInterface;
using BIMRL.Common;
using QuickGraph;
using QuickGraph.Data;

namespace BIMRLInterface.ExtensionFunction
{
    public class ComputePath : ExtensionFunctionBase, IBIMRLExtensionFunction
    {
        public ComputePath()
        {

        }

        /// <summary>
        /// VolumeIntersection function expects the following properties to be set: KEYFIELDS
        /// </summary>
        /// <param name="inputDT"></param>
        /// <param name="inputParams"></param>
        public override void InvokeRule(DataTable inputDT, params string[] inputParams)
        {
            base.InvokeRule(inputDT, inputParams);

            string sourceNode = inputParams[0];
            string targetNode = inputParams[1];
            if (!inputDT.Columns.Contains(sourceNode) || !inputDT.Columns.Contains(targetNode))
                throw new BIMRLInterfaceRuntimeException("%Error, Source ('" + sourceNode + "') or Target ('" + targetNode + "') column is not found!");

            DataColumn column;

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Int32");
            column.ColumnName = "OUTPUT";
            column.ReadOnly = false;
            column.Unique = false;
            try
            {
                inputDT.Columns.Add(column);    // Add column result
            }
            catch (System.Data.DuplicateNameException)
            {
                // ignore error for duplicate column and continue
            }

            // ELEMENTID and OUTPUTDETAILS are the mandatory columns !!
            SdoGeometry linePath = new SdoGeometry();
            DBQueryManager dbQ = new DBQueryManager();
            dbQ.runNonQuery("TRUNCATE TABLE USERGEOM_OUTPUTDETAILS", true);
            dbQ.runNonQuery("DROP TABLE USERGEOM_OUTPUTDETAILS", true);

            DataTable outputDetails = new DataTable();
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "ELEMENTID";
            column.ReadOnly = false;
            column.Unique = false;
            outputDetails.Columns.Add(column);    // Add result details (elementids that intersects) column            column = new DataColumn();

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "OUTPUTDETAILS";
            column.ReadOnly = false;
            column.Unique = false;
            outputDetails.Columns.Add(column);    // Add result details (elementids that intersects) column

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Int32");
            column.ColumnName = "SEQUENCENO";
            column.ReadOnly = false;
            column.Unique = false;
            outputDetails.Columns.Add(column);    // Add result details (elementids that intersects) column

            BuildGraph graph = new BuildGraph(DBOperation.refBIMRLCommon);
            
            string exclCmd = string.Empty;
            string avoidCmd = string.Empty;
            int multiPath = 0;
            if (inputParams.Count() > 2)
            {
                for (int i = 2; i < inputParams.Count(); ++i)
                {
                    if (string.Compare(inputParams[i], 0, "EXCLUDE ", 0, 8, true) == 0)
                    {
                        exclCmd = "UPDATE CIRCULATION_" + DBQueryManager.FedModelID.ToString("X4") + " SET ACTIVE='N' WHERE " + inputParams[i].Substring(8);
                    }
                    else if (string.Compare(inputParams[i], 0, "AVOID ", 0, 6, true) == 0)
                    {
                        avoidCmd = "SELECT LINK_ID, START_NODE_ID, END_NODE_ID FROM CIRCULATION_" + DBQueryManager.FedModelID.ToString("X4") + " WHERE " + inputParams[i].Substring(6);
                        OracleCommand cmd = new OracleCommand(avoidCmd, DBOperation.DBConn);
                        try
                        {
                            OracleDataReader reader = cmd.ExecuteReader();
                            while (reader.Read())
                            {
                                int linkID = reader.GetInt32(0);
                                int startID = reader.GetInt32(1);
                                int endID = reader.GetInt32(2);
                                TaggedEdge<int, int> tagEdge = new TaggedEdge<int, int>(startID, endID, linkID);
                                graph.setCost(tagEdge, 100.0, overWrite:true);
                                // for Bidirectional graph, cost has to be set on the other direction as well
                                tagEdge = new TaggedEdge<int, int>(endID, startID, linkID);
                                graph.setCost(tagEdge, 100.0, overWrite:true);
                            }
                            reader.Dispose();
                            cmd.Dispose();
                        }
                        catch (OracleException e)
                        {
                            string excStr = "%%Error - " + e.Message + "\n\t" + avoidCmd;
                            DBOperation.refBIMRLCommon.StackPushError(excStr);
                            cmd.Dispose();
                        }
                        catch (SystemException e)
                        {
                            string excStr = "%%Error - " + e.Message + "\n\t" + avoidCmd;
                            DBOperation.refBIMRLCommon.StackPushError(excStr);
                            throw;
                        }
                    }
                    else if (string.Compare(inputParams[i], 0, "k=", 0, 2, true) == 0 || string.Compare(inputParams[i], 0, "k =", 0, 3, true) == 0)
                    {
                        string[] tokens = inputParams[i].Split('=');
                        if (tokens.Count() >= 2)
                        {
                            int kCount;
                            if (int.TryParse(tokens[1], out kCount))
                            {
                                multiPath = kCount;

                                // For k-shortest path add the alternative paths into OUTPUTDETAILS for the time being, and add the geometry also there (no default visualization for now, but can be done with a little tweak in future)
                                column = new DataColumn();
                                column.DataType = System.Type.GetType("System.Int32");
                                column.ColumnName = "GROUPNO";
                                column.ReadOnly = false;
                                column.Unique = false;
                                try
                                {
                                    outputDetails.Columns.Add(column);
                                }
                                catch (System.Data.DuplicateNameException)
                                {
                                    // ignore error for duplicate column and continue
                                }

                                column = new DataColumn();
                                column.DataType = linePath.GetType();
                                column.ColumnName = "GEOMETRY";
                                column.ReadOnly = false;
                                column.Unique = false;
                                try
                                {
                                    outputDetails.Columns.Add(column);
                                }
                                catch (System.Data.DuplicateNameException)
                                {
                                    // ignore error for duplicate column and continue
                                }
                            }
                        }
                    }
                }
            }

            bool rollbackTmp = false;
            if (!string.IsNullOrEmpty(exclCmd))
            {
                DBOperation.executeSingleStmt("SAVEPOINT computePathTemp", commit:false);
                DBOperation.executeSingleStmt(exclCmd, commit: false);
                rollbackTmp = true;
            }

            graph.generateBiDirectionalGraph(DBQueryManager.FedModelID, "CIRCULATION_" + DBQueryManager.FedModelID.ToString("X4"), this.WhereCondition);
            
            if (rollbackTmp)
            {
                DBOperation.executeSingleStmt("ROLLBACK TO computePathTemp", commit: false);
            }

            foreach (DataRow row in inputDT.Rows)
            {
                string sourceEID = row[sourceNode].ToString();
                string targetEID = row[targetNode].ToString();
                string elemID = row["ELEMENTID"].ToString();

                List<string> vPath = new List<string>();
                List<int> nodePath = new List<int>();
                List<string> elemIDPath = new List<string>();
                List<Point3D> centroidPath = new List<Point3D>();

                if (multiPath <= 1)
                {
                    if (graph.getShortestPath(sourceEID, targetEID, out vPath, out nodePath, out elemIDPath, out centroidPath))
                    {
                        string geomid = UserGeometryUtils.createLine(elemID, centroidPath, true);
                        row["OUTPUT"] = 1;

                        int seqNo = 0;
                        foreach (string eID in elemIDPath)
                        {
                            DataRow insRow = outputDetails.NewRow();
                            insRow["ELEMENTID"] = elemID;
                            insRow["SEQUENCENO"] = seqNo++;
                            insRow["OUTPUTDETAILS"] = eID;
                            outputDetails.Rows.Add(insRow);
                        }
                    }
                    else
                    {
                        row["OUTPUT"] = 0;
                    }
                }
                else
                {


                    List<List<string>> vPaths;
                    List<List<int>> nodePaths;
                    List<List<string>> elemIDPaths;
                    List<List<Point3D>> centroidPaths;
                    if (graph.getkShortestPath(sourceEID, targetEID, multiPath, out vPaths, out nodePaths, out elemIDPaths, out centroidPaths))
                    {
                        // STILL NEED WORK!! ELEMENTID is a key in user geometry table and cannot have multiple entries!!!!!
                        //foreach (List<Point3D> onePath in centroidPaths)
                        //{
                        //    string geomid = UserGeometryUtils.createLine(elemID, onePath, true);
                        //}
                        // For now, create only the first one
                        string geomid = UserGeometryUtils.createLine(elemID, centroidPaths[0], true);

                        row["OUTPUT"] = multiPath;

                        for (int k = 0; k < elemIDPaths.Count; ++k)
                        {
                            int seqNo = 0;
                            foreach (string eID in elemIDPaths[k])
                            {
                                DataRow insRow = outputDetails.NewRow();
                                insRow["ELEMENTID"] = elemID;
                                insRow["GROUPNO"] = k;
                                insRow["SEQUENCENO"] = seqNo++;
                                insRow["OUTPUTDETAILS"] = eID;
                                // Do not repeat the geom, just add it to the first member
                                if (eID == elemIDPaths[k].First())
                                {
                                    linePath = UserGeometryUtils.createSdoGeomLine(centroidPaths[k]);
                                    insRow["GEOMETRY"] = linePath;
                                }
                                outputDetails.Rows.Add(insRow);
                            }
                        }
                    }
                    else
                    {
                        row["OUTPUT"] = 0;
                    }
                }
            }

            m_Result = inputDT;
            if (outputDetails.Rows.Count > 0)
            {
                dbQ.createTableFromDataTable("USERGEOM_OUTPUTDETAILS", outputDetails, false, true);
            }

        }
    }
}