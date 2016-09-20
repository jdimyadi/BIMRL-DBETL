using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Algorithms.ShortestPath;
using QuickGraph.Algorithms.RankedShortestPath;
using QuickGraph.Algorithms.Observers;
using QuickGraph.Data;
using QuickGraph.Graphviz;
using System.Data;
using Oracle.DataAccess;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using BIMRL.OctreeLib;
using BIMRL.BIMRLGraph;
using NetSdoGeometry;

namespace BIMRLInterface
{
    public class BuildGraph
    {
        BIMRLCommon refBimrlCommon;
        Dictionary<int, Tuple<string, string, string, Point3D>> NodeDict;
        Dictionary<string, int> ElemIDToNodeIDDict;
        UndirectedGraph<int, TaggedEdge<int, int>> graph;
        BidirectionalGraph<int, TaggedEdge<int, int>> biDirGraph;
        Dictionary<TaggedEdge<int,int>,double> EdgeCostDict;

        private class tagEdgeComparer : IEqualityComparer<TaggedEdge<int,int>>
        {
            public bool Equals(TaggedEdge<int,int> x, TaggedEdge<int,int> y)
            {
                return (x.Source == y.Source && x.Target == y.Target && x.Tag == y.Tag);
            }
            public int GetHashCode(TaggedEdge<int,int> obj)
            {
                //Simple hashcode by adding the 3 numbers after shifting each number by multiplier so that they do not overlap (max 100,000 nodes?)
                int hash = 17;
                hash = hash * 31 + obj.Source.GetHashCode();
                hash = hash * 31 + obj.Target.GetHashCode();
                hash = hash * 31 + obj.Tag.GetHashCode();
                return hash;
            }
        }

        public BuildGraph(BIMRLCommon bimrlCommon)
        {
            if (bimrlCommon == null)
            {
                GraphData.refBimrlCommon = new BIMRLCommon();
                refBimrlCommon = GraphData.refBimrlCommon;
            }
            else
                refBimrlCommon = bimrlCommon;
            NodeDict = new Dictionary<int, Tuple<string, string, string, Point3D>>();
            ElemIDToNodeIDDict = new Dictionary<string,int>();
            graph = new UndirectedGraph<int, TaggedEdge<int, int>>();
            biDirGraph = new BidirectionalGraph<int, TaggedEdge<int,int>>();
            EdgeCostDict = new Dictionary<TaggedEdge<int,int>,double>(new tagEdgeComparer());
        }

        public void generateUndirectedGraph(int FedID, string networkName)
        {
            string sqlStmt = "SELECT a.node_id, b.elementid, b.elementtype, b.name, b.body_major_axis_centroid from " + networkName + "_node$ a, "
                            + "bimrl_element_" + FedID.ToString("X4") + " b where a.node_name = b.elementid";
            OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);

            try
            {
                // Populate Dict with node information (with basic information from BIMRL_ELEMENT)
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int nodeID = reader.GetInt32(0);
                    string elemID = reader.GetString(1);
                    string elemType = reader.GetString(2);
                    string name = string.Empty;
                    if (!reader.IsDBNull(3))
                        name = reader.GetString(3);
                    Point3D centroid = null;
                    if (!reader.IsDBNull(4))
                    {
                        SdoGeometry sdogeom = reader.GetValue(4) as SdoGeometry;
                        centroid = new Point3D(sdogeom.SdoPoint.XD.Value, sdogeom.SdoPoint.YD.Value, sdogeom.SdoPoint.ZD.Value);
                    }
                    else
                    {
                        // If centroid is null, need to get the centroid from one of its details
                        DBQueryManager dbQ = new DBQueryManager();
                        string sqlStmt_det = "SELECT body_major_axis_centroid from bimrl_element where elementid in " +
                                            "(select aggregateelementid from bimrl_relaggregation where masterelementid='" + elemID + "') and body_major_axis_centroid is not null";
                        DataTable res = dbQ.querySingleRow(sqlStmt_det);
                        if (res != null)
                        {
                            SdoGeometry sdogeom = res.Rows[0][0] as SdoGeometry;
                            centroid = new Point3D(sdogeom.SdoPoint.XD.Value, sdogeom.SdoPoint.YD.Value, sdogeom.SdoPoint.ZD.Value);
                        }
                    }
                    NodeDict.Add(nodeID, new Tuple<string, string, string, Point3D>(elemID, elemType, name, centroid));
                    ElemIDToNodeIDDict.Add(elemID, nodeID);
                }
                reader.Dispose();

                // generate the graph
                sqlStmt = "Select link_id, start_node_id, end_node_id from " + networkName + "_link$ where active='Y'";
                command.CommandText = sqlStmt;
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int linkID = reader.GetInt32(0);
                    int startNode = reader.GetInt32(1);
                    int endNode = reader.GetInt32(2);

                    var edge = new TaggedEdge<int, int>(startNode, endNode, linkID);
                    graph.AddVerticesAndEdge(edge);
                }
                reader.Dispose();
                command.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.BIMRlErrorStack.Push(excStr);
                command.Dispose();
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.BIMRlErrorStack.Push(excStr);
                throw;
            }
        }

        public void generateBiDirectionalGraph(int FedID, string networkName, string nodeConstraints=null, string elemConstraints=null)
        {
            string elemCondition = "";
            if (!string.IsNullOrEmpty(elemConstraints))
                BIMRLInterfaceCommon.appendToString(elemCondition, " and ", ref elemCondition);

            string sqlStmt = "SELECT a.node_id, b.elementid, b.elementtype, b.name, b.body_major_axis_centroid from " + networkName + "_node$ a, "
                            + "bimrl_element_" + FedID.ToString("X4") + " b where a.node_name = b.elementid";
            OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);

            try
            {
                // Populate Dict with node information (with basic information from BIMRL_ELEMENT)
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int nodeID = reader.GetInt32(0);
                    string elemID = reader.GetString(1);
                    string elemType = reader.GetString(2);
                    string name = string.Empty;
                    if (!reader.IsDBNull(3))
                        name = reader.GetString(3);
                    Point3D centroid = null;
                    if (!reader.IsDBNull(4))
                    {
                        SdoGeometry sdogeom = reader.GetValue(4) as SdoGeometry;
                        centroid = new Point3D(sdogeom.SdoPoint.XD.Value, sdogeom.SdoPoint.YD.Value, sdogeom.SdoPoint.ZD.Value);
                    }
                    else
                    {
                        // If centroid is null, need to get the centroid from one of its details
                        DBQueryManager dbQ = new DBQueryManager();
                        string sqlStmt_det = "SELECT body_major_axis_centroid from bimrl_element where elementid in " +
                                            "(select aggregateelementid from bimrl_relaggregation where masterelementid='" + elemID + "') and body_major_axis_centroid is not null";
                        DataTable res = dbQ.querySingleRow(sqlStmt_det);
                        if (res.Rows.Count > 0)
                        {
                            SdoGeometry sdogeom = res.Rows[0][0] as SdoGeometry;
                            centroid = new Point3D(sdogeom.SdoPoint.XD.Value, sdogeom.SdoPoint.YD.Value, sdogeom.SdoPoint.ZD.Value);
                        }
                    }
                    NodeDict.Add(nodeID, new Tuple<string, string, string, Point3D>(elemID, elemType, name, centroid));
                    ElemIDToNodeIDDict.Add(elemID, nodeID);
                }
                reader.Dispose();

                // generate the graph
                string nodeCondition = "";
                if (!string.IsNullOrEmpty(nodeConstraints))
                    BIMRLInterfaceCommon.appendToString(nodeConstraints, " and ", ref nodeCondition);

                sqlStmt = "Select link_id, start_node_id, end_node_id from " + networkName + "_link$ where active='Y'" + nodeCondition;
                command.CommandText = sqlStmt;
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int linkID = reader.GetInt32(0);
                    int startNode = reader.GetInt32(1);
                    int endNode = reader.GetInt32(2);

                    TaggedEdge<int, int> outEdge = new TaggedEdge<int, int>(startNode, endNode, linkID);
                    TaggedEdge<int,int> revEdge = new TaggedEdge<int, int>(endNode, startNode, linkID);
                    biDirGraph.AddVerticesAndEdge(outEdge);
                    //setCost(outEdge, 1);
                    biDirGraph.AddVerticesAndEdge(revEdge);
                    //setCost(revEdge, 1);
                }
                reader.Dispose();
                command.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.BIMRlErrorStack.Push(excStr);
                command.Dispose();
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBimrlCommon.BIMRlErrorStack.Push(excStr);
                throw;
            }
        }

        public bool getShortestPath(string startElem, string endElem, out List<string> vPath, out List<int> nodePath, out List<string> elemIDPath, out List<Point3D> centroidPath)
        {
            bool retStatus = false;
            vPath = new List<string>();
            nodePath = new List<int>();
            elemIDPath = new List<string>();
            centroidPath = new List<Point3D>();
            IEnumerable<TaggedEdge<int, int>> computedPath = new List<TaggedEdge<int, int>>();
            try
            {
                //Func<TaggedEdge<int, int>, double> edgeCost = e => 1; // constant cost
                Func<TaggedEdge<int, int>, double> edgeCost = edgeCostFunc;
                //Func<TaggedEdge<int, int>, double> edgeCost = AlgorithmExtensions.GetIndexer(EdgeCostDict);
                if (ElemIDToNodeIDDict.ContainsKey(startElem))
                {
                    int startNode = ElemIDToNodeIDDict[startElem];
                    //TryFunc<int, IEnumerable<TaggedEdge<int, int>>> tryGetPaths = graph.ShortestPathsDijkstra(edgeCost, startNode);
                    TryFunc<int, IEnumerable<TaggedEdge<int, int>>> tryGetPaths = biDirGraph.ShortestPathsDijkstra(edgeCost, startNode);

                    if (ElemIDToNodeIDDict.ContainsKey(endElem))
                    {
                        int endNode = ElemIDToNodeIDDict[endElem];
                        if (tryGetPaths(endNode, out computedPath))
                        {
                            foreach (var edge in computedPath)
                            {
                                Tuple<string, string, string, Point3D> nodeDetail = NodeDict[edge.Source];
                                string elemIDNode = nodeDetail.Item1;
                                string elemTypeNode = nodeDetail.Item2;
                                string elemNameNode = nodeDetail.Item3;
                                Point3D centroidNode = nodeDetail.Item4;
                                nodePath.Add(edge.Source);
                                elemIDPath.Add(elemIDNode);
                                centroidPath.Add(centroidNode);

                                vPath.Add(edge.Source + " ( " + elemIDNode + ", " + elemNameNode + ") -> " + edge.Target + " : " + edge.Tag);
                                
                                // Add the last node target into the list
                                if (edge == computedPath.Last())
                                {
                                    Tuple<string, string, string, Point3D> endNodeDetail = NodeDict[edge.Target];
                                    elemIDNode = endNodeDetail.Item1;
                                    elemTypeNode = endNodeDetail.Item2;
                                    elemNameNode = endNodeDetail.Item3;
                                    centroidNode = endNodeDetail.Item4;
                                    nodePath.Add(edge.Source);
                                    elemIDPath.Add(elemIDNode);
                                    centroidPath.Add(centroidNode);
                                }
                            }
                        }
                        if (computedPath != null) 
                            retStatus = true;
                    }
                    else
                    {
                        // can't find end node entry
                        vPath.Add("Can't find the end node entry in the graph!");
                        retStatus = false;
                    }
                }
                else
                {
                    // can't find start node entry
                    vPath.Add("Can't find the start node entry in the graph!");
                    retStatus = false;
                }
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t";
                refBimrlCommon.BIMRlErrorStack.Push(excStr);
                throw;
            }

            return retStatus;
        }

        public bool getkShortestPath(string startElem, string endElem, int k, out List<List<string>> vPaths, out List<List<int>> nodePaths, out List<List<string>> elemIDPaths, 
                                        out List<List<Point3D>> centroidPaths)
        {
            bool retStatus = false;
            vPaths = new List<List<string>>();
            nodePaths = new List<List<int>>();
            elemIDPaths = new List<List<string>>();
            centroidPaths = new List<List<Point3D>>();
            IEnumerable<IEnumerable<TaggedEdge<int, int>>> computedPaths = new List<List<TaggedEdge<int, int>>>();
            try
            {
                // Func<TaggedEdge<int, int>, double> edgeCost = e => 1; // constant cost
                Func<TaggedEdge<int, int>, double> edgeCost = edgeCostFunc;
                //Func<TaggedEdge<int, int>, double> edgeCost = AlgorithmExtensions.GetIndexer(EdgeCostDict);
                if (ElemIDToNodeIDDict.ContainsKey(startElem))
                {
                    int startNode = ElemIDToNodeIDDict[startElem];
                    int endNode = ElemIDToNodeIDDict[endElem];
                    computedPaths = biDirGraph.RankedShortestPathHoffmanPavley(edgeCost, startNode, endNode, k);
                    if (computedPaths.Count() > 0)
                    {
                        foreach (var path in computedPaths)
                        {
                            List<string> vPath = new List<string>();
                            List<int> nodePath = new List<int>();
                            List<string> elemIDPath = new List<string>();
                            List<Point3D> centroidPath = new List<Point3D>();
                            foreach (var edge in path)
                            {
                                Tuple<string, string, string, Point3D> nodeDetail = NodeDict[edge.Source];
                                string elemIDNode = nodeDetail.Item1;
                                string elemTypeNode = nodeDetail.Item2;
                                string elemNameNode = nodeDetail.Item3;
                                Point3D centroidNode = nodeDetail.Item4;
                                nodePath.Add(edge.Source);
                                elemIDPath.Add(elemIDNode);
                                centroidPath.Add(centroidNode);

                                vPath.Add(edge.Source + " -> " + edge.Target + " : " + edge.Tag);
                                
                                // Add the last node target into the list
                                if (edge == path.Last())
                                {
                                    Tuple<string, string, string, Point3D> endNodeDetail = NodeDict[edge.Target];
                                    elemIDNode = endNodeDetail.Item1;
                                    elemTypeNode = endNodeDetail.Item2;
                                    elemNameNode = endNodeDetail.Item3;
                                    centroidNode = endNodeDetail.Item4;
                                    nodePath.Add(edge.Source);
                                    elemIDPath.Add(elemIDNode);
                                    centroidPath.Add(centroidNode);
                                }
                            }

                            vPaths.Add(vPath);
                            nodePaths.Add(nodePath);
                            elemIDPaths.Add(elemIDPath);
                            centroidPaths.Add(centroidPath);
                        }
                        if (computedPaths != null)
                            if (computedPaths.Count() > 0)
                                retStatus = true;
                    }
                    else
                    {
                        // can't find end node entry
                        vPaths = null;
                        retStatus = false;
                    }
                }
                else
                {
                    // can't find start node entry
                    vPaths = null;
                    retStatus = false;
                }
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t";
                refBimrlCommon.BIMRlErrorStack.Push(excStr);
                throw;
            }

            return retStatus;
        }

        double edgeCostFunc(TaggedEdge<int,int> tagEdge)
        {
            double cost = 1;
            if (EdgeCostDict.ContainsKey(tagEdge))
                cost = EdgeCostDict[tagEdge];

            return cost;
        }

        public void setCost(TaggedEdge<int,int> key, double cost, bool overWrite=false)
        {
            if (EdgeCostDict.ContainsKey(key))
            {
                // overwrite only if the flag is set
                if (overWrite)
                    EdgeCostDict[key] = cost;   // Update cost if the key already in the Dict
            }
            else
            {
                EdgeCostDict.Add(key, cost);
            }
        }
    }
}
