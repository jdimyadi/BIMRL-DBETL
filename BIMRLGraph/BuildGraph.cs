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
using BIMRLGraph;
using NetSdoGeometry;

namespace BIMRLGraph
{
    public class BuildGraph
    {
        BIMRLCommon refBimrlCommon;
        Dictionary<int, Tuple<string, string, string, Point3D>> NodeDict;
        Dictionary<string, int> ElemIDToNodeIDDict;
        UndirectedGraph<int, TaggedEdge<int, int>> graph;
        BidirectionalGraph<int, TaggedEdge<int, int>> biDirGraph;

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

        public void generateBiDirectionalGraph(int FedID, string networkName)
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

                    TaggedEdge<int, int> outEdge = new TaggedEdge<int, int>(startNode, endNode, linkID);
                    TaggedEdge<int,int> revEdge = new TaggedEdge<int, int>(endNode, startNode, linkID);
                    biDirGraph.AddVerticesAndEdge(outEdge);
                    biDirGraph.AddVerticesAndEdge(revEdge);
                }
                reader.Dispose();
                command.Dispose();
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

        public List<string> getShortestPath(string startElem, string endElem)
        {
            List<string> vPath = new List<string>();
            IEnumerable<TaggedEdge<int, int>> computedPath = new List<TaggedEdge<int, int>>();
            try
            {
                Func<TaggedEdge<int, int>, double> edgeCost = e => 1; // constant cost
                if (ElemIDToNodeIDDict.ContainsKey(startElem))
                {
                    int startNode = ElemIDToNodeIDDict[startElem];
                    TryFunc<int, IEnumerable<TaggedEdge<int, int>>> tryGetPaths = biDirGraph.ShortestPathsDijkstra(edgeCost, startNode);

                    if (ElemIDToNodeIDDict.ContainsKey(endElem))
                    {
                        int endNode = ElemIDToNodeIDDict[endElem];
                        if (tryGetPaths(endNode, out computedPath))
                        {
                            foreach (var edge in computedPath)
                            {
                                vPath.Add(edge.Source + " -> " + edge.Target + " : " + edge.Tag);
                            }
                        }
                    }
                    else
                    {
                        // can't find end node entry
                        vPath.Add("Can't find the end node entry in the graph!");
                    }
                }
                else
                {
                    // can't find start node entry
                    vPath.Add("Can't find the start node entry in the graph!");
                }
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t";
                refBimrlCommon.StackPushError(excStr);
                throw;
            }

            return vPath;
        }

        public List<List<string>> getkShortestPath(string startElem, string endElem, int k)
        {
            List<List<string>> vPaths = new List<List<string>>();
            IEnumerable<IEnumerable<TaggedEdge<int, int>>> computedPaths = new List<List<TaggedEdge<int, int>>>();
            try
            {
                Func<TaggedEdge<int, int>, double> edgeCost = e => 1; // constant cost
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
                            foreach (var edge in path)
                            {
                                vPath.Add(edge.Source + " -> " + edge.Target + " : " + edge.Tag);
                            }
                            vPaths.Add(vPath);
                        }
                    }
                    else
                    {
                        // can't find end node entry
                        vPaths = null;
                    }
                }
                else
                {
                    // can't find start node entry
                    vPaths = null;
                }
            }
            catch (SystemException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t";
                refBimrlCommon.StackPushError(excStr);
                throw;
            }

            return vPaths;
        }

        public void exportToGraphViz()
        {
            var graphviz = new GraphvizAlgorithm<int, TaggedEdge<int, int>>(graph);
            //string output = graphviz.Generate(new FileDotEngine(), "graph");
        }
    }
}
