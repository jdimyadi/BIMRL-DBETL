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
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL;
using BIMRL.Common;

namespace BIMRL.BIMRLGraph
{
    public class GraphData
    {
        public static BIMRLCommon refBimrlCommon;
        List<int> nodeIdList = new List<int>();
        List<string> nodeNameList;
        List<string> nodeTypeList;
        List<string> activeList;
        List<int> hierarchyLevelList;
        List<int> parentIdList;

        List<int> linkIdList;
        List<string> linkNameList;
        List<int> startNodeList;
        List<int> endNodeList;
        List<string> linkTypeList;
        List<string> linkActive;
        List<int> linkParentID;
        List<OracleParameterStatus> linkParentStatus;

        public GraphData()
        {
            refBimrlCommon = new BIMRLCommon();
            DBOperation.Connect();

            nodeIdList = new List<int>();
            nodeNameList = new List<string>();
            nodeTypeList = new List<string>();
            activeList = new List<string>();
            hierarchyLevelList = new List<int>();
            parentIdList = new List<int>();

            linkIdList = new List<int>();
            linkNameList = new List<string>();
            startNodeList = new List<int>();
            endNodeList = new List<int>();
            linkTypeList = new List<string>();
            linkActive = new List<string>();
        }

        void resetLists()
        {
            nodeIdList.Clear();
            nodeNameList.Clear();
            nodeTypeList.Clear();
            activeList.Clear();
            hierarchyLevelList.Clear();
            parentIdList.Clear();

            linkIdList.Clear();
            linkNameList.Clear();
            startNodeList.Clear();
            endNodeList.Clear();
            linkTypeList.Clear();
            linkActive.Clear();
            if (linkParentID != null)
                linkParentID.Clear();
            if (linkParentStatus != null)
                linkParentStatus.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FedID"></param>
        /// <param name="maxHeightLimit">Default set to 4m</param>
        /// <returns></returns>
        public bool createCirculationGraph(int FedID, double maxHeightLimit=4)
        {
            bool status = true;
            string sqlStmt = null;
            int nodeID = 1;
            int linkID = 1;
            Dictionary<string, int> nodeProcessed = new Dictionary<string, int>();
            Dictionary<string, int> parentNodeIdDict = new Dictionary<string, int>();
            Dictionary<string, Tuple<string, string>> dependencyDict = new Dictionary<string, Tuple<string, string>>();
            OracleCommand command = new OracleCommand("", DBOperation.DBConn);
            OracleCommand command2 = new OracleCommand("", DBOperation.DBConn);
            OracleCommand commandPlSql = new OracleCommand("", DBOperation.DBConn);
            string networkName = "CIRCULATION_" + FedID.ToString("X4");
            try
            {
                // Create network tables
                dropNetwork(networkName);   // Drop it first if already existing

                sqlStmt = "SDO_NET.CREATE_LOGICAL_NETWORK";
                commandPlSql.CommandText = sqlStmt;
                commandPlSql.CommandType = CommandType.StoredProcedure;
                commandPlSql.BindByName = true;
                int noHierarchy = 2;
                bool isDirected = false;
                commandPlSql.Parameters.Add("network", OracleDbType.Varchar2, networkName, ParameterDirection.Input);
                commandPlSql.Parameters.Add("no_of_hierarchy_levels", OracleDbType.Int32, noHierarchy, ParameterDirection.Input);
                commandPlSql.Parameters.Add("is_directed", OracleDbType.Boolean, isDirected, ParameterDirection.Input);
                commandPlSql.ExecuteNonQuery();
                DBOperation.commitTransaction();
                commandPlSql.Dispose();

               // Create the Circulation view
               var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
               string exePath = new FileInfo(location.AbsolutePath).Directory.FullName;

               // (Re)-Create the spatial indexes
               DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_graphview.sql"), FedID);

                // 
                sqlStmt = "SELECT ELEMENTID,CONTAINER FROM BIMRL_ELEMENT_" + FedID.ToString("X4") + " WHERE ELEMENTTYPE IN ('IFCSPACE','IFCDOOR','IFCDOORSTANDARDCASE','IFCOPENINGELEMENT','IFCOPENINGELEMENTSTANDARDCASE','IFCSTAIR','IFCSTAIRFLIGHT','IFCRAMP','IFCRAMPFLIGHT','IFCTRANSPORTELEMENT')"
                          + " OR (ELEMENTTYPE='IFCBUILDINGELEMENTPROXY' AND (UPPER(NAME) LIKE '%LIFT%' OR UPPER(NAME) LIKE '%ELEVATOR%'))";
                command.CommandText = sqlStmt;
                OracleDataReader reader = command.ExecuteReader();
                OracleDataReader reader2 = null;
                while (reader.Read())
                {
                    string elemID = reader.GetString(0);
                    string container = string.Empty;
                    if (reader.IsDBNull(1))
                    {
                        // If container is null, most likely the object has parts as aggregates or dependencies
                        //command2.CommandText = "select container from bimrl_element_" + FedID.ToString("X4")
                        //                        + " where elementid in (select aggregateelementid  from bimrl_relaggregation_" + FedID.ToString("X4")
                        //                        + " where masterelementid='" + elemID + "' union all select dependentelementid from  bimrl_elementdependency_" + FedID.ToString("X4")
                        //                        + " where elementid='" + elemID + "')";
                        //reader2 = command2.ExecuteReader();
                        //while (reader2.Read())
                        //{
                        //    if (reader2.IsDBNull(0))
                        //        continue;
                        //    container = reader2.GetString(0);
                        //}
                        //reader2.Close();

                        container = containerFromDetail(FedID, elemID);

                        if (string.IsNullOrEmpty(container))
                        {
                            container = containerFromHost(FedID, elemID);
                            if (string.IsNullOrEmpty(container))
                            {
                                // If still cannot identify the container even from the host, skip
                                refBimrlCommon.StackPushError("%% Warning: Can't find container for ElementID: " + elemID);
                                continue;               // Can't seem to find the appropriate container
                            }
                        }
                    }
                    else
                    {
                        container = reader.GetString(1);
                    }
                    string storeyID = storeyContainer(FedID, container);

                    if (string.IsNullOrEmpty(storeyID))
                    {
                        refBimrlCommon.StackPushError("%% Warning: Can't find the appropriate Storey for ElementID: " + elemID);
                        continue;               // Can't seem to find the appropriate container
                    }

                    if (!nodeProcessed.ContainsKey(storeyID))
                    {
                        string insStorey = "Insert into " + networkName + "_NODE$ (NODE_ID, NODE_NAME, NODE_TYPE, ACTIVE, HIERARCHY_LEVEL) "
                                                + "VALUES (" + nodeID.ToString() + ",'" + storeyID + "','IFCBUILDINGSTOREY','Y',1)";
                        command2.CommandText = insStorey;
                        command2.ExecuteNonQuery();

                        nodeProcessed.Add(storeyID, nodeID);
                        parentNodeIdDict.Add(elemID, nodeID);
                        nodeID++;
                    }
                    else
                    {
                        parentNodeIdDict.Add(elemID, nodeProcessed[storeyID]);
                    }
                }
                reader.Dispose();

                sqlStmt = "SELECT SPACEELEMENTID, BOUNDARYELEMENTID, BOUNDARYELEMENTTYPE, BFACENORMAL FROM BIMRL_SPACEBOUNDARYV_" + FedID.ToString("X4")
                            + " WHERE BOUNDARYELEMENTTYPE IN ('IFCSPACE','IFCDOOR','IFCOPENINGELEMENT','IFCDOORSTANDARDCASE','IFCOPENINGELEMENTSTANDARDCASE') order by spaceelementid";
                command.CommandText = sqlStmt;
                reader2 = command.ExecuteReader();
                while (reader2.Read())
                {
                    bool vertBound = false;
                    string spaceID = reader2.GetString(0);
                    int spaceParentNodeId = parentNodeIdDict[spaceID];
                    string boundID = reader2.GetString(1);
                    string boundType = reader2.GetString(2);
                    SdoGeometry bfaceN = reader2.GetValue(3) as SdoGeometry;
                    if (bfaceN != null)
                        if (MathUtils.equalTol(bfaceN.SdoPoint.ZD.Value, 1.0, 0.1))
                            vertBound = true;               // It is a vertical boundary (probably opening), we will skip it for Horizontal circulation

                    if (boundType.Length > 24)
                        boundType = boundType.Substring(0, 24);         // Oracle nodetype only accept 24 chars long!
                    int spaceNodeID = 0;
                    int boundNodeID = 0;

                    if (!nodeProcessed.ContainsKey(spaceID))
                    {

                        spaceNodeID = nodeID++;
                        nodeIdList.Add(spaceNodeID);
                        nodeNameList.Add(spaceID);
                        nodeTypeList.Add("IFCSPACE");
                        activeList.Add("Y");
                        hierarchyLevelList.Add(2);
                        parentIdList.Add(spaceParentNodeId);
                        nodeProcessed.Add(spaceID, spaceNodeID);
                    }
                    else
                    {
                        spaceNodeID = nodeProcessed[spaceID];
                    }

                    if (string.Compare(boundType, "IFCOPENINGELEMENT") == 0 || string.Compare(boundType, "IFCOPENINGELEMENTSTANDARDCASE") == 0)
                    {
                        command2.CommandText = "SELECT ELEMENTID,ELEMENTTYPE FROM BIMRL_ELEMENTDEPENDENCY_" + FedID.ToString("X4") + " WHERE DEPENDENTELEMENTID='" + boundID
                                                + "' AND DEPENDENTELEMENTTYPE IN ('IFCOPENINGELEMENT','IFCOPENINGELEMENTSTANDARDCASE')";
                        OracleDataReader depMaster = command2.ExecuteReader();
                        if (depMaster.HasRows)
                        {
                            depMaster.Read();
                            string masterID = depMaster.GetString(0);
                            string masterType = depMaster.GetString(1);
                            if (string.Compare(masterType, 0, "IFCSLAB", 0, 7, true) == 0)
                                vertBound = true;
                        }
                        depMaster.Dispose();

                        if (!vertBound)
                        {
                            if (!dependencyDict.ContainsKey(boundID))
                            {
                                // If opening element, need to check whether there is any infill, if yes, use the Door as the node
                                command2.CommandText = "SELECT DEPENDENTELEMENTID,DEPENDENTELEMENTTYPE FROM BIMRL_ELEMENTDEPENDENCY_" + FedID.ToString("X4") + " WHERE ELEMENTID='" + boundID
                                                        + "' AND DEPENDENTELEMENTTYPE IN ('IFCDOOR','IFCDOORSTANDARDCASE')";
                                OracleDataReader depReader = command2.ExecuteReader();
                                if (depReader.HasRows)
                                {
                                    depReader.Read();
                                    // If the query returns value, replace the boundary information with the infill
                                    string origBoundID = boundID;

                                    boundID = depReader.GetString(0);
                                    boundType = depReader.GetString(1);
                                    if (boundType.Length > 24)
                                        boundType = boundType.Substring(0, 24);         // Oracle nodetype only accept 24 chars long!

                                    dependencyDict.Add(origBoundID, new Tuple<string, string>(boundID, boundType));
                                }
                                else
                                {
                                    // when there is no dependency, insert itself into the Dict to avoid the query to be invoked again in future
                                    dependencyDict.Add(boundID, new Tuple<string, string>(boundID, boundType));
                                }
                                depReader.Dispose();
                            }
                            else
                            {
                                Tuple<string, string> boundTuple = dependencyDict[boundID];
                                boundID = boundTuple.Item1;
                                boundType = boundTuple.Item2;
                            }
                        }
                    }

                    if (!vertBound)     // Skip Horizontal link if it is a vertical boundary
                    {
                        if (!nodeProcessed.ContainsKey(boundID))
                        {
                            // for the node
                            int parentNodeId = 0;
                            if (parentNodeIdDict.ContainsKey(boundID))
                                parentNodeId = parentNodeIdDict[boundID];
                            else
                            {
                                refBimrlCommon.StackPushError("%%Warning: can't find the parent node id for " + boundType + "'" + boundID + "'");
                                continue;     // missing information, skip
                            }
                            boundNodeID = nodeID++;
                            nodeIdList.Add(boundNodeID);
                            nodeNameList.Add(boundID);
                            nodeTypeList.Add(boundType);
                            activeList.Add("Y");
                            hierarchyLevelList.Add(2);
                            parentIdList.Add(parentNodeId);
                            nodeProcessed.Add(boundID, boundNodeID);

                            // Space and the boundary must be on the same level
                            if (spaceParentNodeId == parentNodeId)
                            {
                                // for the link
                                string linkName = spaceID + " - " + boundID;
                                string linkType = "HORIZONTAL CIRCULATION BY " + boundType;
                                linkIdList.Add(linkID++);
                                linkNameList.Add(linkName);
                                startNodeList.Add(spaceNodeID);
                                endNodeList.Add(boundNodeID);
                                linkTypeList.Add(linkType);
                                linkActive.Add("Y");
                            }
                        }
                        else
                        {
                            int parentNodeId = 0;
                            if (parentNodeIdDict.ContainsKey(boundID))
                                parentNodeId = parentNodeIdDict[boundID];

                            // Space and the boundary must be on the same level
                            if (spaceParentNodeId == parentNodeId)
                            { 
                                boundNodeID = nodeProcessed[boundID];
                                string linkName = spaceID + " - " + boundID;
                                string linkType = "HORIZONTAL CIRCULATION BY " + boundType;
                                linkIdList.Add(linkID++);
                                linkNameList.Add(linkName);
                                startNodeList.Add(spaceNodeID);
                                endNodeList.Add(boundNodeID);
                                linkTypeList.Add(linkType);
                                linkActive.Add("Y");
                            }
                        }
                    }
                }
                reader2.Dispose();

                //sqlStmt = "Insert into " + networkName + "_NODE$ (NODE_ID, NODE_NAME, NODE_TYPE, ACTIVE, HIERARCHY_LEVEL, PARENT_NODE_ID) "
                //                        + "VALUES (:1, :2, :3, :4, :5, :6)";
                //command.CommandText = sqlStmt;
                //OracleParameter[] pars = new OracleParameter[6];
                //pars[0] = command.Parameters.Add("1", OracleDbType.Int32);
                //pars[1] = command.Parameters.Add("2", OracleDbType.Varchar2);
                //pars[2] = command.Parameters.Add("3", OracleDbType.Varchar2);
                //pars[3] = command.Parameters.Add("4", OracleDbType.Varchar2);
                //pars[4] = command.Parameters.Add("5", OracleDbType.Int32);
                //pars[5] = command.Parameters.Add("6", OracleDbType.Int32);
                //for (int i = 0; i < 6; i++)
                //{
                //    pars[i].Direction = ParameterDirection.Input;
                //}
                //if (nodeIdList.Count > 0)
                //{
                //    pars[0].Value = nodeIdList.ToArray();
                //    pars[1].Value = nodeNameList.ToArray();
                //    pars[2].Value = nodeTypeList.ToArray();
                //    pars[3].Value = activeList.ToArray();
                //    pars[4].Value = hierarchyLevelList.ToArray();
                //    pars[5].Value = parentIdList.ToArray();
                //    command.ArrayBindCount = nodeIdList.Count;

                //    command.ExecuteNonQuery();
                //    DBOperation.commitTransaction();
                //}

                insertNode(networkName, nodeIdList, nodeNameList, nodeTypeList, activeList, hierarchyLevelList, parentIdList);

                //sqlStmt = "Insert into " + networkName + "_LINK$ (LINK_ID, LINK_NAME, START_NODE_ID, END_NODE_ID, LINK_TYPE, ACTIVE) VALUES (:1, :2, :3, :4, :5, :6)";
                //command.CommandText = sqlStmt;
                //OracleParameter[] parsLink = new OracleParameter[6];
                //command.Parameters.Clear();
                //parsLink[0] = command.Parameters.Add("1", OracleDbType.Int32);
                //parsLink[1] = command.Parameters.Add("2", OracleDbType.Varchar2);
                //parsLink[2] = command.Parameters.Add("3", OracleDbType.Int32);
                //parsLink[3] = command.Parameters.Add("4", OracleDbType.Int32);
                //parsLink[4] = command.Parameters.Add("5", OracleDbType.Varchar2);
                //parsLink[5] = command.Parameters.Add("6", OracleDbType.Varchar2);
                //for (int i = 0; i < 6; i++)
                //{
                //    parsLink[i].Direction = ParameterDirection.Input;
                //}
                //if (linkIdList.Count > 0)
                //{
                //    parsLink[0].Value = linkIdList.ToArray();
                //    parsLink[1].Value = linkNameList.ToArray();
                //    parsLink[2].Value = startNodeList.ToArray();
                //    parsLink[3].Value = endNodeList.ToArray();
                //    parsLink[4].Value = linkTypeList.ToArray();
                //    parsLink[5].Value = linkActive.ToArray();
                //    command.ArrayBindCount = linkIdList.Count;

                //    command.ExecuteNonQuery();
                //    DBOperation.commitTransaction();
                //}
                //command.Dispose();
                //command2.Dispose();

                insertLink(networkName, linkIdList, linkNameList, startNodeList, endNodeList, linkTypeList, linkActive);
                resetLists();

                HashSet<Tuple<string, string>> processedVertPair = new HashSet<Tuple<string, string>>();    // To track the unique pair of the elemID - SpaceAbove

                // Connect between stories: collect means to connect via Stairs or Ramp
                linkParentID = new List<int>();
                linkParentStatus = new List<OracleParameterStatus>();
                sqlStmt = "SELECT ELEMENTID, ELEMENTTYPE, CONTAINER, NAME FROM BIMRL_ELEMENT_" + FedID.ToString("X4") + " WHERE ELEMENTTYPE IN ('IFCSTAIR','IFCSTAIRFLIGHT','IFCRAMP','IFCRAMPFLIGHT') order by elementid";
                command.CommandText = sqlStmt;
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string elemID = reader.GetString(0);
                    string elemType = reader.GetString(1);
                    string containerId = string.Empty;
                    if (!reader.IsDBNull(2))
                        containerId = reader.GetString(2);
                    else
                    {
                        containerId = containerFromDetail(FedID, elemID);
                        if (string.IsNullOrEmpty(containerId))
                        {
                            containerId = spaceContainer(FedID, elemID);
                            if (string.IsNullOrEmpty(containerId))
                            {
                                // If even after using geometry to get the space container it cannot get any, give it up
                                refBimrlCommon.StackPushError("%%Warning: Can't find container for " + elemType + " '" + elemID + "'");
                                continue;
                            }
                        }
                    }
                    string elemName = string.Empty;
                    if (!reader.IsDBNull(3))
                        elemName = reader.GetString(3);

                    string storeyID = storeyContainer(FedID, containerId);

                    sqlStmt = "select aggregateelementid,aggregateelementtype from bimrl_relaggregation_" + FedID.ToString("X4") + " where masterelementid='" + elemID + "'";
                    command2.CommandText = sqlStmt;
                    reader2 = command2.ExecuteReader();
                    string elemIDList = "'" + elemID + "'";
                    List<string> aggrList = new List<string>(){elemID};
                    while (reader2.Read())
                    {
                        string aggrId = reader2.GetString(0);
                        string aggrType = reader2.GetString(1);
                        BIMRLCommon.appendToString("'" + aggrId + "'", ",", ref elemIDList);
                        aggrList.Add(aggrId);
                    }
                    reader2.Close();

                    // Getting objects (space or the same object) that intersect cells that are at the top of the object
                    sqlStmt = "select a.elementid, a.elementtype, a.name, a.container, count(b.cellid) cellCount from bimrl_element_" + FedID.ToString("X4") + " a, bimrl_spatialindex_" + FedID.ToString("X4")
                               + " b where a.elementid=b.elementid and b.cellid  in (select cellid from bimrl_spatialindex_" + FedID.ToString("X4")
                               + " where elementid in(" + elemIDList + ") and zmaxbound = (select max(zmaxbound) from bimrl_spatialindex_" + FedID.ToString("X4")
                               + " where elementid in (" + elemIDList + "))) and elementtype='IFCSPACE' GROUP BY a.elementid, a.elementtype, a.name, a.container order by cellCount desc";
                    command2.CommandText = sqlStmt;
                    reader2 = command2.ExecuteReader();
                    if (reader2.HasRows)
                    {
                        while (reader2.Read())
                        {
                            string spaceAbove = reader2.GetString(0);
                            string container = string.Empty;
                            string spaceType = reader2.GetString(1);
                            string spaceName = string.Empty;
                            if (!reader2.IsDBNull(2))
                                spaceName = reader2.GetString(2);
                            if (!reader2.IsDBNull(3))
                                container = reader2.GetString(3);
                            else
                            {
                                refBimrlCommon.StackPushError("%%Warning: Can't find container for space '" + spaceAbove + "'");
                                continue;
                            }

                            int cellCount = reader2.GetInt32(4);
                            string nextStoreyId = storeyContainer(FedID, container);
                            if (string.Compare(storeyID, nextStoreyId) == 0 || string.IsNullOrEmpty(nextStoreyId))
                            {
                                continue;           // The space is at the same storey, skip
                            }

                            // add now a link between the storey
                            int storeyNode = 0;
                            int storeyAbove = 0;
                            if (nodeProcessed.ContainsKey(storeyID))
                                storeyNode = nodeProcessed[storeyID];
                            if (nodeProcessed.ContainsKey(nextStoreyId))
                                storeyAbove = nodeProcessed[nextStoreyId];
                            if (storeyNode == 0 || storeyAbove == 0)
                            {
                                refBimrlCommon.StackPushError("%%Warning: can't find the corresponding storey node ids (current or above) for '" + elemID + "'");
                                continue;     // missing information, skip
                            }

                            string linkName = storeyNode + " - " + storeyAbove;
                            string linkType = "INTERSTOREY LINK";
                            int parentLinkID = linkID++;
                            linkIdList.Add(parentLinkID);
                            linkNameList.Add(linkName);
                            startNodeList.Add(storeyNode);
                            endNodeList.Add(storeyAbove);
                            linkTypeList.Add(linkType);
                            linkActive.Add("Y");
                            linkParentID.Add(0);
                            linkParentStatus.Add(OracleParameterStatus.NullInsert);

                            if (!nodeProcessed.ContainsKey(elemID))
                            {
                                int parentNodeId = 0;
                                // for the node (this object)
                                if (parentNodeIdDict.ContainsKey(elemID))
                                    parentNodeId = parentNodeIdDict[elemID];
                                else
                                {
                                    // Check whether the information is available through the dependent element
                                    foreach (string aggrId in aggrList)
                                    {
                                        if (parentNodeIdDict.ContainsKey(aggrId))
                                        {
                                            parentNodeId = parentNodeIdDict[aggrId];
                                            break;
                                        }
                                    }

                                    // if still cannot find the information, continue with 0
                                    if (parentNodeId == 0)
                                    {
                                        refBimrlCommon.StackPushError("%%Warning: can't find the corresponding parent node id for '" + elemID + "'");
                                        //continue;     // missing information, skip
                                    }
                                }

                                int boundNodeID = nodeID++;
                                nodeIdList.Add(boundNodeID);
                                nodeNameList.Add(elemID);
                                nodeTypeList.Add(elemType);
                                activeList.Add("Y");
                                hierarchyLevelList.Add(2);
                                parentIdList.Add(parentNodeId);
                                nodeProcessed.Add(elemID, boundNodeID);

                                // for the link between the object and the space that contains it
                                // string spaceID = spaceContainer(FedID, elemID); 
                                string spaceID = spaceContainer(FedID, aggrList);
                                if (!string.IsNullOrEmpty(spaceID))
                                {
                                    linkName = spaceID + " - " + elemID;
                                    linkType = "HORIZONTAL CIRCULATION BY " + elemType;
                                    linkIdList.Add(linkID++);
                                    linkNameList.Add(linkName);
                                    int spaceNodeID = nodeProcessed[spaceID];
                                    startNodeList.Add(spaceNodeID);
                                    endNodeList.Add(boundNodeID);
                                    linkTypeList.Add(linkType);
                                    linkActive.Add("Y");
                                    linkParentID.Add(parentLinkID);
                                    linkParentStatus.Add(OracleParameterStatus.Success);

                                    processedVertPair.Add(new Tuple<string, string>(spaceID, elemID));
                                }
                            }

                            // Here, we only consider the main element (elemID) for pairing and not the details therefore we need to filter them by the processed pair
                            Tuple<string, string> vPair = new Tuple<string, string>(elemID, spaceAbove);
                            if (!processedVertPair.Contains(vPair))
                            {
                                // Element has been inserted to the Node table before, now we just need to link this element to the space above
                                linkName = spaceAbove + " - " + elemID;
                                linkType = "VERTICAL CIRCULATION BY " + elemType;
                                linkIdList.Add(linkID++);
                                linkNameList.Add(linkName);
                                int spaceAboveNode = nodeProcessed[spaceAbove];
                                startNodeList.Add(spaceAboveNode);
                                int elemIDNode = nodeProcessed[elemID];
                                endNodeList.Add(elemIDNode);
                                linkTypeList.Add(linkType);
                                linkActive.Add("Y");
                                linkParentID.Add(parentLinkID);
                                linkParentStatus.Add(OracleParameterStatus.Success);

                                processedVertPair.Add(new Tuple<string, string>(elemID, spaceAbove));
                            }
                        }
                    }
                    else
                    {
                        // Missing space above, cannot determine the vertical circulation connectivity
                        refBimrlCommon.StackPushError("%%Warning: Elementid '" + elemID + "' does not have a space above that it can connect to!");
                        continue;
                    }
                }

                projectUnit projUnit = DBOperation.getProjectUnitLength(FedID);
                double maxHeight = maxHeightLimit; // default in Meter
                if (projUnit == projectUnit.SIUnit_Length_MilliMeter)
                    maxHeight = maxHeight * 1000;
                else if (projUnit == projectUnit.Imperial_Length_Foot)
                    maxHeight = maxHeight * 3.28084;
                else if (projUnit == projectUnit.Imperial_Length_Inch)
                    maxHeight = maxHeight * 39.37008;

                // To set MaxOctreeLevel correctly, do this first
                Point3D llb, urt;
                DBOperation.getWorldBB(FedID, out llb, out urt);

                // Now look for connection through elevator or elevator space
                sqlStmt = "SELECT ELEMENTID, ELEMENTTYPE, CONTAINER, BODY_MAJOR_AXIS_CENTROID FROM BIMRL_ELEMENT_" + FedID.ToString("X4")
                            + " WHERE ELEMENTID IN (select elementid from bimrl_classifassignment_" + FedID.ToString("X4") 
                            + " where classificationitemname='Elevators and Lifts' or classificationitemcode='D1010')";
                command.CommandText = sqlStmt;
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string elemID = reader.GetString(0);
                    string elemType = reader.GetString(1);
                    string containerId = reader.GetString(2);
                    SdoGeometry geom = reader.GetValue(3) as SdoGeometry;
                    double geomZValue = geom.SdoPoint.ZD.Value;

                    string storeyID = storeyContainer(FedID, containerId);

                    sqlStmt = "SELECT MIN(XMINBOUND), MAX(XMAXBOUND), MIN(YMINBOUND), MAX(YMAXBOUND), MIN(ZMINBOUND), MAX(ZMAXBOUND) FROM BIMRL_SPATIALINDEX_" + FedID.ToString("X4")
                                + " WHERE ELEMENTID='" + elemID + "'";
                    command2.CommandText = sqlStmt;
                    reader2 = command2.ExecuteReader();
                    reader2.Read();
                    int xmin = reader2.GetInt32(0);
                    int xmax = reader2.GetInt32(1);
                    int ymin = reader2.GetInt32(2);
                    int ymax = reader2.GetInt32(3);
                    int zmin = reader2.GetInt32(4);
                    int zmax = reader2.GetInt32(5);
                    reader2.Close();

                    Point3D maxHeightLoc = new Point3D(geom.SdoPoint.XD.Value, geom.SdoPoint.YD.Value, (geomZValue + maxHeight));
                    CellID64 enclCellAtMaxHeight = CellID64.cellAtMaxDepth(maxHeightLoc);
                    int xmin2, xmax2, ymin2, ymax2, zmin2, zmax2;
                    CellID64.getCellIDComponents(enclCellAtMaxHeight, out xmin2, out ymin2, out zmin2, out xmax2, out ymax2, out zmax2);
                    
                    // Swap if some how zmax2 is lower than zmax
                    if (zmax2 < zmax)
                    {
                        int tmpMax = zmax;
                        zmax = zmax2;
                        zmax2 = tmpMax;
                    }

                    HashSet<Tuple<string, string>> processedEPair = new HashSet<Tuple<string, string>>();

                    // Need to limit the max height to avoid getting other relevant object on the higher storeys
                    // Getting objects (space or the same object) that have cells that are above of the object
                    sqlStmt = "select a.elementid, a.elementtype, a.name, a.container, b.zminbound, count(b.cellid) cellCount from bimrl_element_" + FedID.ToString("X4") + " a, bimrl_spatialindex_" + FedID.ToString("X4")
                                + " b where a.elementid=b.elementid and b.cellid  in (select cellid from bimrl_spatialindex_" + FedID.ToString("X4")
                                + " where (zminbound >= " + zmax.ToString() + " and zminbound < " + zmax2.ToString() + ") "
                                + " and xminbound between " + xmin.ToString() + " and " + xmax.ToString() 
                                + " and yminbound between " + ymin.ToString() + " and " + ymax.ToString() + ") and (a.elementtype in ('IFCSPACE','IFCTRANSPORTELEMENT') or (a.elementtype='IFCBUILDINGELEMENTPROXY' and (upper(a.name) like '%LIFT%' OR upper(a.name) like '%ELEVATOR%')))"
                                + " GROUP BY a.elementid, a.elementtype, a.name, a.container, b.zminbound order by b.zminbound asc, cellCount desc";
                    command2.CommandText = sqlStmt;
                    reader2 = command2.ExecuteReader();
                    if (reader2.HasRows)
                    {
                        bool objAboveFound = false;
                        while (reader2.Read())
                        {
                            string objectAbove = reader2.GetString(0);
                            string objectType = reader2.GetString(1);
                            string container = string.Empty;
                            if (!reader2.IsDBNull(3))
                            {
                                container = reader2.GetString(3);
                            }
                            else
                            {
                                container = containerFromDetail(FedID, objectAbove);
                                if (string.IsNullOrEmpty(container))
                                    container = containerFromHost(FedID, objectAbove);
                                if (string.IsNullOrEmpty(container))
                                {
                                    refBimrlCommon.StackPushError("%%Warning: can't find the container of '" + objectAbove + "' even from its host or details");
                                    continue;     // missing information, skip
                                }
                            }

                            double zminbound = reader2.GetDouble(4);
                            int cellCount = reader2.GetInt32(5);
                            string nextStoreyId = storeyContainer(FedID, container);
                            if (string.Compare(storeyID, nextStoreyId) == 0)
                                continue;           // The space is at the same storey, skip

                            // add now a link between the storey
                            int storeyNode = 0;
                            int storeyAbove = 0;
                            if (nodeProcessed.ContainsKey(storeyID))
                                storeyNode = nodeProcessed[storeyID];
                            if (nodeProcessed.ContainsKey(nextStoreyId))
                                storeyAbove = nodeProcessed[nextStoreyId];
                            if (storeyNode == 0 || storeyAbove == 0)
                            {
                                refBimrlCommon.StackPushError("%%Warning: can't find the corresponding storey node ids (current or above) for '" + elemID + "'");
                                continue;     // missing information, skip
                            }

                            string linkName = storeyNode + " - " + storeyAbove;
                            string linkType = "INTERSTOREY LINK";
                            int parentLinkID = linkID++;
                            linkIdList.Add(parentLinkID);
                            linkNameList.Add(linkName);
                            startNodeList.Add(storeyNode);
                            endNodeList.Add(storeyAbove);
                            linkTypeList.Add(linkType);
                            linkActive.Add("Y");
                            linkParentID.Add(0);
                            linkParentStatus.Add(OracleParameterStatus.NullInsert);

                            if (!nodeProcessed.ContainsKey(elemID))
                            {
                                // for the node (this object)
                                int parentNodeId = 0;
                                if (parentNodeIdDict.ContainsKey(elemID))
                                    parentNodeId = parentNodeIdDict[elemID];

                                int boundNodeID = nodeID++;
                                nodeIdList.Add(boundNodeID);
                                nodeNameList.Add(elemID);
                                nodeTypeList.Add(elemType);
                                activeList.Add("Y");
                                hierarchyLevelList.Add(2);
                                parentIdList.Add(parentNodeId);
                                nodeProcessed.Add(elemID, boundNodeID);

                                // for the link between the object and the space that contains it
                                string spaceID = spaceContainer(FedID, elemID);
                                if (!string.IsNullOrEmpty(spaceID))
                                {
                                    linkName = spaceID + " - " + elemID;
                                    linkType = "HORIZONTAL CIRCULATION BY " + elemType;
                                    linkIdList.Add(linkID++);
                                    linkNameList.Add(linkName);
                                    int spaceNodeID = nodeProcessed[spaceID];
                                    startNodeList.Add(spaceNodeID);
                                    endNodeList.Add(boundNodeID);
                                    linkTypeList.Add(linkType);
                                    linkActive.Add("Y");
                                    linkParentID.Add(parentLinkID);
                                    linkParentStatus.Add(OracleParameterStatus.Success);

                                    processedVertPair.Add(new Tuple<string, string>(spaceID, elemID));
                                }

                            }
                            // for the object above
                            if (!nodeProcessed.ContainsKey(objectAbove))
                            {
                                // for the node (object above)
                                int parentNodeId = 0;
                                if (parentNodeIdDict.ContainsKey(objectAbove))
                                    parentNodeId = parentNodeIdDict[objectAbove];

                                int boundNodeID = nodeID++;
                                nodeIdList.Add(boundNodeID);
                                nodeNameList.Add(objectAbove);
                                nodeTypeList.Add(objectType);
                                activeList.Add("Y");
                                hierarchyLevelList.Add(2);
                                parentIdList.Add(storeyAbove);
                                nodeProcessed.Add(objectAbove, boundNodeID);
                            }

                            Tuple<string,string> ePair = new Tuple<string,string>(objectAbove, elemID);
                            if (!processedVertPair.Contains(ePair))
                            {
                                // for the link between the object and the object above
                                linkName = objectAbove + " - " + elemID;
                                linkType = "VERTICAL CIRCULATION BY ELEVATOR";
                                linkIdList.Add(linkID++);
                                linkNameList.Add(linkName);
                                int elemNode = nodeProcessed[elemID];
                                int boundNodeID = nodeProcessed[objectAbove];
                                startNodeList.Add(boundNodeID);
                                endNodeList.Add(elemNode);
                                linkTypeList.Add(linkType);
                                linkActive.Add("Y");
                                linkParentID.Add(parentLinkID);
                                linkParentStatus.Add(OracleParameterStatus.Success);

                                processedVertPair.Add(ePair);
                                objAboveFound = true;
                            }
                            if (objAboveFound)
                                break;      // break from the while loop if one object above is already processed
                        }
                    }
                }

                insertNode(networkName, nodeIdList, nodeNameList, nodeTypeList, activeList, hierarchyLevelList, parentIdList);
                insertLink(networkName, linkIdList, linkNameList, startNodeList, endNodeList, linkTypeList, linkActive, linkParentID, linkParentStatus);
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                throw;
            }

            command.Dispose();
            command2.Dispose();

            return status;
        }

        public bool createSpaceAdjacencyGraph(int FedID)
        {
            bool status = true;
            string sqlStmt = "";
            OracleCommand command = new OracleCommand("", DBOperation.DBConn);

            try
            {

            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                throw;
            }
            return status;
        }

        public bool dropNetwork(string networkName)
        {
            bool status = true;
            string sqlStmt = "";
            OracleCommand commandPlSql = new OracleCommand("", DBOperation.DBConn);
            try
            {
                sqlStmt = "SELECT network FROM USER_SDO_NETWORK_METADATA WHERE NETWORK='" + networkName.ToUpper() + "'";
                commandPlSql.CommandText = sqlStmt;
                object netw = commandPlSql.ExecuteScalar();
                if (netw != null)
                {
                    // Create network tables
                    sqlStmt = "SDO_NET.DROP_NETWORK";
                    commandPlSql.CommandText = sqlStmt;
                    commandPlSql.CommandType = CommandType.StoredProcedure;
                    commandPlSql.BindByName = true;
                    commandPlSql.Parameters.Add("network", OracleDbType.Varchar2, networkName, ParameterDirection.Input);
                    commandPlSql.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                }
                commandPlSql.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                commandPlSql.Dispose();
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                throw;
            }
            return status;
        }

        string containerFromDetail(int FedID, string elemID)
        {
            string container = null;
            string sqlStmt = "select container from bimrl_element_" + FedID.ToString("X4")
                        + " where elementid in (select aggregateelementid  from bimrl_relaggregation_" + FedID.ToString("X4")
                        + " where masterelementid='" + elemID + "' union all select dependentelementid from  bimrl_elementdependency_" + FedID.ToString("X4")
                        + " where elementid='" + elemID + "')";
            OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
            try
            {
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(0))
                        continue;
                    container = reader.GetString(0);
                }
                reader.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                command.Dispose();
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                throw;
            }

            command.Dispose();
            return container;
        }

        string containerFromHost(int FedID, string elemID)
        {
            string container = null;
            string sqlStmt = "select container from bimrl_element_" + FedID.ToString("X4")
                        + " where elementid in (select masterelementid  from bimrl_relaggregation_" + FedID.ToString("X4")
                        + " where aggregateelementid='" + elemID + "' union all select elementid from  bimrl_elementdependency_" + FedID.ToString("X4")
                        + " where dependentelementid='" + elemID + "')";
            OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
            try
            {
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (reader.IsDBNull(0))
                        continue;
                    container = reader.GetString(0);
                }
                reader.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                command.Dispose();
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                throw;
            }

            command.Dispose();
            return container;
        }



        string storeyContainer(int FedID, string containerID)
        {
            string storeyID = null;
            OracleCommand command = new OracleCommand("", DBOperation.DBConn);
            string sqlStmt = "select parentid from bimrl_spatialstructure_" + FedID.ToString("X4") + " where spatialelementid='" + containerID + "' "
                            + " and parenttype='IFCBUILDINGSTOREY' union select spatialelementid from bimrl_spatialstructure_" + FedID.ToString("X4")
                            + " where spatialelementid='" + containerID + "' and spatialelementtype='IFCBUILDINGSTOREY'";
            command.CommandText = sqlStmt;
            try
            {
                object retC = command.ExecuteScalar();
                if (retC != null)
                    storeyID = retC.ToString();
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                command.Dispose();
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                throw;
            }
            command.Dispose();
            return storeyID;
        }

        string spaceContainer(int FedID, string elementID)
        {
            List<string> elemIDList = new List<string>();
            elemIDList.Add(elementID);
            return spaceContainer(FedID, elemIDList);
        }

        string spaceContainer(int FedID, List<string> elementIDList)
        {
            string elemIDLis = "";
            foreach (string elem in elementIDList)
                BIMRLCommon.appendToString("'" + elem + "'", ",", ref elemIDLis);

            string container = null;
            string sqlStmt = "select a.elementid, count(b.cellid) cellCount from bimrl_element_" + FedID.ToString("X4") + " a, bimrl_spatialindex_" + FedID.ToString("X4") + " b, "
                        + "bimrl_element_" + FedID.ToString("X4") + " c, bimrl_spatialindex_" + FedID.ToString("X4") + " d "
                        + "where a.elementid=b.elementid and c.elementid=d.elementid and b.cellid=d.cellid and c.elementid in (" + elemIDLis + ") and a.elementtype='IFCSPACE' "
                        + "group by a.elementid order by cellCount desc";
            OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
            OracleDataReader reader;
            try
            {
                reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    // We will get the first one, which has the most cells in common
                    reader.Read();
                    container = reader.GetString(0);
                }
                reader.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                command.Dispose();
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.StackPushError(excStr);
                throw;
            }
            command.Dispose();
            return container;
        }

        void insertNode(string networkName, List<int> nodeIdList, List<string> nodeNameList, List<string> nodeTypeList, List<string> activeList, List<int> hierarchyLevelList, List<int> parentIdList)
        {
            string sqlStmt = "Insert into " + networkName + "_NODE$ (NODE_ID, NODE_NAME, NODE_TYPE, ACTIVE, HIERARCHY_LEVEL, PARENT_NODE_ID) "
                                    + "VALUES (:1, :2, :3, :4, :5, :6)";
            OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
            OracleParameter[] pars = new OracleParameter[6];
            pars[0] = command.Parameters.Add("1", OracleDbType.Int32);
            pars[1] = command.Parameters.Add("2", OracleDbType.Varchar2);
            pars[2] = command.Parameters.Add("3", OracleDbType.Varchar2);
            pars[3] = command.Parameters.Add("4", OracleDbType.Varchar2);
            pars[4] = command.Parameters.Add("5", OracleDbType.Int32);
            pars[5] = command.Parameters.Add("6", OracleDbType.Int32);
            for (int i = 0; i < 6; i++)
            {
                pars[i].Direction = ParameterDirection.Input;
            }
            if (nodeIdList.Count > 0)
            {
                pars[0].Value = nodeIdList.ToArray();
                pars[1].Value = nodeNameList.ToArray();
                pars[2].Value = nodeTypeList.ToArray();
                pars[3].Value = activeList.ToArray();
                pars[4].Value = hierarchyLevelList.ToArray();
                pars[5].Value = parentIdList.ToArray();
                command.ArrayBindCount = nodeIdList.Count;

                try
                {
                    command.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                }
                catch (OracleException e)
                {
                    string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                    refBimrlCommon.StackPushError(excStr);
                    command.Dispose();
                }
                catch (SystemException e)
                {
                    string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                    refBimrlCommon.StackPushError(excStr);
                    throw;
                }
            }

            command.Dispose();
        }

        void insertLink(string networkName, List<int> linkIdList, List<string> linkNameList, List<int> startNodeList, List<int> endNodeList, List<string> linkTypeList,
                        List<string> linkActive, List<int> parentLinkID = null, List<OracleParameterStatus> parentLinkStatus = null)
        {
            string sqlStmt = "Insert into " + networkName + "_LINK$ (LINK_ID, LINK_NAME, START_NODE_ID, END_NODE_ID, LINK_TYPE, ACTIVE, PARENT_LINK_ID) VALUES (:1, :2, :3, :4, :5, :6, :7)";
            OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
            OracleParameter[] parsLink = new OracleParameter[7];
            command.Parameters.Clear();
            parsLink[0] = command.Parameters.Add("1", OracleDbType.Int32);
            parsLink[1] = command.Parameters.Add("2", OracleDbType.Varchar2);
            parsLink[2] = command.Parameters.Add("3", OracleDbType.Int32);
            parsLink[3] = command.Parameters.Add("4", OracleDbType.Int32);
            parsLink[4] = command.Parameters.Add("5", OracleDbType.Varchar2);
            parsLink[5] = command.Parameters.Add("6", OracleDbType.Varchar2);
            parsLink[6] = command.Parameters.Add("7", OracleDbType.Int32);
            for (int i = 0; i < 7; i++)
            {
                parsLink[i].Direction = ParameterDirection.Input;
            }
            if (linkIdList.Count > 0)
            {
                parsLink[0].Value = linkIdList.ToArray();
                parsLink[1].Value = linkNameList.ToArray();
                parsLink[2].Value = startNodeList.ToArray();
                parsLink[3].Value = endNodeList.ToArray();
                parsLink[4].Value = linkTypeList.ToArray();
                parsLink[5].Value = linkActive.ToArray();
                if (parentLinkID == null)
                {
                    parentLinkID = new List<int>();
                    parentLinkStatus = new List<OracleParameterStatus>();
                    for (int i=0; i<linkIdList.Count; ++i)
                    {
                        parentLinkID.Add(0);
                        parentLinkStatus.Add(OracleParameterStatus.NullInsert);
                    }
                    parsLink[6].Value = parentLinkID.ToArray();
                    parsLink[6].ArrayBindStatus = parentLinkStatus.ToArray();
                }
                else
                {
                    parsLink[6].Value = parentLinkID.ToArray();
                    parsLink[6].ArrayBindStatus = parentLinkStatus.ToArray();
                }

                command.ArrayBindCount = linkIdList.Count;

                try
                {
                    command.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                }
                catch (OracleException e)
                {
                    string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                    refBimrlCommon.StackPushError(excStr);
                    command.Dispose();
                }
                catch (SystemException e)
                {
                    string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                    refBimrlCommon.StackPushError(excStr);
                    throw;
                }
            }
            command.Dispose();
        }
    }
}
