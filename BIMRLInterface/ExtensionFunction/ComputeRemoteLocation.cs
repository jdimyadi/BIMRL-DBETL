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
using BIMRL.OctreeLib;

namespace BIMRLInterface.ExtensionFunction
{
    public class ComputeRemoteLocation : ExtensionFunctionBase, IBIMRLExtensionFunction
    {
        public ComputeRemoteLocation()
        {

        }

        /// <summary>
        /// InvokeRule method to compute the remote location logic. It expects the following arguments from the BIMRL command:
        ///     MainID of the main object to check, e.g. IfcBuildingStorey or IfcSpace
        ///     DetailedID is/are detailed objects where geometry details are supplied, e.g. for IfcBuildingStorey since itself does not have geometry in most cases, it can be approximated by a collection of Top Of Face from the Floor Slab.
        ///         For a specific space, it may be the same object as the main object
        ///     Face geometry(ies) (2D plane). This geometry is a face geometry of the DetailedID(s). These 3 arguments provides way to calculate the diagonal distance of the building convex footprint using Oriented Bounding Box (OBB)
        ///     Set 2 ObjectID(s) provide relevant objects for the second set to compute direct distance between objects in the same MainID, e.g. Exit Door/Opening or Space
        ///     Set 2 Location Geometry provides a point representation for the Set 2 objects for the actual direct distance computation
        /// Output of this rule will be the ratio between the (direct distance)/(diagonal footprint)
        /// </summary>
        /// <param name="inputDT"></param>
        /// <param name="inputParams"></param>
        public override void InvokeRule(DataTable inputDT, params string[] inputParams)
        {
            base.InvokeRule(inputDT, inputParams);

            DataTable workingDT;
            workingDT = m_Result.Copy();
            workingDT.Clear();      // we do not need to keep the oiginal data

            // Columns to be added
            DataColumn column;
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Double");
            column.ColumnName = "OUTPUT";
            column.ReadOnly = false;
            column.Unique = false;
            try
            {
                workingDT.Columns.Add(column);    // Add column result
            }
            catch (System.Data.DuplicateNameException)
            {
                // ignore error for duplicate column and continue
            }

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Double");
            column.ColumnName = "OBBDIAGONAL";
            column.ReadOnly = false;
            column.Unique = false;
            try
            {
                workingDT.Columns.Add(column);    // Add column result
            }
            catch (System.Data.DuplicateNameException)
            {
                // ignore error for duplicate column and continue
            }

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Double");
            column.ColumnName = "DIRECTDISTANCE";
            column.ReadOnly = false;
            column.Unique = false;
            try
            {
                workingDT.Columns.Add(column);    // Add column result
            }
            catch (System.Data.DuplicateNameException)
            {
                // ignore error for duplicate column and continue
            }

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "ELEMENTID";
            column.ReadOnly = false;
            column.Unique = false;
            try
            {
                workingDT.Columns.Add(column);    // Add column result
            }
            catch (System.Data.DuplicateNameException)
            {
                // ignore error for duplicate column and continue
            }

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "OBJECT1";
            column.ReadOnly = false;
            column.Unique = false;
            try
            {
                workingDT.Columns.Add(column);    // Add column result
            }
            catch (System.Data.DuplicateNameException)
            {
                // ignore error for duplicate column and continue
            }

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "OBJECT2";
            column.ReadOnly = false;
            column.Unique = false;
            try
            {
                workingDT.Columns.Add(column);    // Add column result
            }
            catch (System.Data.DuplicateNameException)
            {
                // ignore error for duplicate column and continue
            }
            // Loop from the "unique" values to work on the input table data
            foreach (DataRow dtR in m_Result.Rows)
            {
                string filterCond = "";
                foreach (string aggrCol in AggregateFields)
                {
                    BIMRLInterfaceCommon.appendToString(aggrCol + "='" + dtR[aggrCol] + "'", " AND ", ref filterCond);
                }

                DataRow[] res = inputDT.Select(filterCond);

                // process the Set 1 detailed objects first
                string prevSet1ObjID = "";
                List<Point3D> tosPointList = new List<Point3D>();
                foreach (DataRow resRow in res)
                {
                    string storeyID = resRow[inputParams[0]].ToString();
                    string Set1ObjID = resRow[inputParams[1]].ToString();
                    SdoGeometry tosFace = resRow[inputParams[2]] as SdoGeometry;

                    if (string.Compare(Set1ObjID, prevSet1ObjID) != 0)
                    {
                        prevSet1ObjID = Set1ObjID;
                        Face3D face;
                        if (SDOGeomUtils.generate_Face3D(tosFace, out face))
                            tosPointList.AddRange(face.vertices);
                    }
                }

                DataRow newRow = workingDT.NewRow();
                double diagDistance = -1.0;
                if (tosPointList.Count > 0)
                {
                    List<Point3D> obb;
                    if (Qualifiers.Contains(BIMRLEnum.functionQualifier.USE_OBB))
                    {
                        // This version uses OBB
                        PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis(tosPointList);
                        List<Point3D> obbOrig = pca.projectedPointList();
                        BoundingBox3D bbox = new BoundingBox3D(obbOrig);
                        diagDistance = Point3D.distance(bbox.LLB, bbox.URT);
                        List<Point3D> bboxBottomCorners = bbox.BBVertices.GetRange(0, 4);    // Get only the bottom 4 corners for this display of OBB purpose
                        bboxBottomCorners.Add(bbox.BBVertices[0]); // close the linestring
                        obb = pca.transformBackPointSet(bboxBottomCorners);
                    }
                    else
                    {
                        // This version uses AABB instead
                        BoundingBox3D bbox = new BoundingBox3D(tosPointList);
                        diagDistance = Point3D.distance(bbox.LLB, bbox.URT);
                        obb = bbox.BBVertices.GetRange(0, 4);
                        obb.Add(bbox.BBVertices[0]);
                    }
                    // We will draw 4 lines for the OBB
                     string geomID = UserGeometryUtils.createLine(null, obb, true);

                    foreach (string aggrCol in AggregateFields)
                        newRow[aggrCol] = res[0][aggrCol];
                    newRow["ELEMENTID"] = geomID;
                    newRow["OBBDIAGONAL"] = diagDistance;
                    newRow["OUTPUT"] = 0.0;
                    workingDT.Rows.Add(newRow);     // create first row as a standalone data for the OBB of the story

                    newRow = workingDT.NewRow();
                    foreach (string aggrCol in AggregateFields)
                        newRow[aggrCol] = res[0][aggrCol];
                    newRow["OBBDIAGONAL"] = diagDistance;
                }

                // process the openings to exit
                List<string> OpeningIDs = new List<string>();
                List<Point3D> OpeningCentroid = new List<Point3D>();
                foreach (DataRow resRow in res)
                {
                    string storeyID = resRow[inputParams[0]].ToString();
                    string openingID = resRow[inputParams[3]].ToString();
                    SdoGeometry openingCentroid = null;

                    // Allow multiple columns specified at the last parameters, but only the first one that has data will be used
                    for (int i = 4; i < inputParams.Count(); ++i)
                    {
                        openingCentroid = resRow[inputParams[i]] as SdoGeometry;
                        if (openingCentroid != null)
                            break;
                    }
                    if (openingCentroid == null)
                        continue;

                    Point3D centroid = new Point3D(openingCentroid.SdoPoint.XD.Value, openingCentroid.SdoPoint.YD.Value, openingCentroid.SdoPoint.ZD.Value);
                    OpeningCentroid.Add(centroid);
                    OpeningIDs.Add(openingID);
                }

                if (OpeningIDs.Count == 2)
                {
                    double directDistance = Point3D.distance(OpeningCentroid[0], OpeningCentroid[1]);
                    double ratio = -1.0;
                    if (diagDistance > 0)
                        ratio = directDistance / diagDistance;
                    newRow["DIRECTDISTANCE"] = directDistance;
                    newRow["OUTPUT"] = ratio;
                    newRow["OBJECT1"] = OpeningIDs[0];
                    newRow["OBJECT2"] = OpeningIDs[1];
                    List<Point3D> line = new List<Point3D>();
                    line.Add(OpeningCentroid[0]);
                    line.Add(OpeningCentroid[1]);
                    string geomID = UserGeometryUtils.createLine(null, line, true);
                    newRow["ELEMENTID"] = geomID;

                    workingDT.Rows.Add(newRow);
                }
                else if (OpeningIDs.Count > 2)
                {
                    HashSet<Tuple<string, string>> pairDone = new HashSet<Tuple<string, string>>();   // HashSet to keep pair that has been processed 
                    // More than 2 objects, we need to pair them
                    while (OpeningIDs.Count > 1)
                    {
                        string firstID = OpeningIDs[0];
                        Point3D firstCentroid = OpeningCentroid[0];

                        for (int i=1; i < OpeningIDs.Count; ++i)
                        {
                            // Skip pair of itself
                            if (string.Compare(firstID, OpeningIDs[i]) == 0)
                                continue;

                            // Skip pair that has been processed (due to potentially duplicate data from the Join
                            if (pairDone.Contains(new Tuple<string, string>(firstID, OpeningIDs[i])) 
                                || pairDone.Contains(new Tuple<string,string>(OpeningIDs[i],firstID)))
                                continue; 

                            pairDone.Add(new Tuple<string, string>(firstID, OpeningIDs[i]));

                            double directDistance = Point3D.distance(firstCentroid, OpeningCentroid[i]);
                            double ratio = -1.0;
                            if (diagDistance > 0)
                                ratio = directDistance / diagDistance;
                            newRow["DIRECTDISTANCE"] = directDistance;
                            newRow["OUTPUT"] = ratio;
                            newRow["OBJECT1"] = firstID;
                            newRow["OBJECT2"] = OpeningIDs[i];
                            List<Point3D> line = new List<Point3D>();
                            line.Add(firstCentroid);
                            line.Add(OpeningCentroid[i]);
                            string geomID = UserGeometryUtils.createLine(null, line, true);
                            newRow["ELEMENTID"] = geomID;

                            workingDT.Rows.Add(newRow);

                            newRow = workingDT.NewRow();
                            foreach (string aggrCol in AggregateFields)
                                newRow[aggrCol] = res[0][aggrCol];
                            newRow["OBBDIAGONAL"] = diagDistance;
                        }
                        // Done with iteration of the first element, remove it and continue with the rest
                        OpeningCentroid.RemoveAt(0);
                        OpeningIDs.RemoveAt(0);
                    }
                }
                else
                {
                    // Only one row or no Row, cannot compute remotely located!
                    newRow["OUTPUT"] = -1.0;
                    if (OpeningIDs.Count == 1)
                        newRow["OBJECT1"] = OpeningIDs[0];

                    workingDT.Rows.Add(newRow);
                }
            }

            m_Result = workingDT.Copy();       // replace the m_Result with the new DataTable populated here
        }
    }
}
