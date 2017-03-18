using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.OctreeLib;

namespace BIMRL
{
    public class BIMRLGeometryPostProcess
    {
        static Vector3D _trueNorth = new Vector3D();
        Polyhedron _geom;
        string _elementid;
        int _currFedID;
        HashSet<int> _mergedFaceList = new HashSet<int>();
        BIMRLCommon _refBIMRLCommon;
        public SdoGeometry OBB;
        string _faceCategory;
        Vector3D[] majorAxes;
        int _fIDOffset;

        Dictionary<int, Face3D> facesColl = new Dictionary<int, Face3D>();
        int lastFaceID = 0;

        Dictionary<Point3D, HashSet<int>> sortedFVert = new Dictionary<Point3D, HashSet<int>>();

      public BIMRLGeometryPostProcess(string elementid, Polyhedron geometry, BIMRLCommon bimrlCommon, int federatedID, string faceCategory)
        {
            _geom = geometry;
            _elementid = elementid;
            _refBIMRLCommon = bimrlCommon;
            _currFedID = federatedID;
            // For optimization purpose, the offset for face id is fixed to 10000 for OBB and 10100 for PROJOBB. If there is any other category in future, this needs to be updated !!
            if (!string.IsNullOrEmpty(faceCategory))
            {
                _faceCategory = faceCategory;
                if (string.Compare(faceCategory, "OBB") == 0)
                    _fIDOffset = 10000;
                else if (string.Compare(faceCategory, "PROJOBB") == 0)
                    _fIDOffset = 10100;
                else
                    _fIDOffset = 10200;
            }
            else
            {
                _faceCategory = "BODY"; // default value
                _fIDOffset = 0;
            }

            string sqlStmt = null;
            try
            {
                //// Set the current maxid number to offset the new face ids. Cannot involve row of type HOLE because it is not in a number format
                //sqlStmt = "SELECT MAX(TO_NUMBER(ID))+1 FROM BIMRL_TOPO_FACE_" + _currFedID.ToString("X4") + " WHERE ELEMENTID='" + elementid + "' AND TYPE != 'HOLE'";
                //OracleCommand cmdFID = new OracleCommand(sqlStmt, DBOperation.DBConn);
                //object retFID = cmdFID.ExecuteScalar();
                //_fIDOffset = 0;
                //int.TryParse(retFID.ToString(), out _fIDOffset);

                Vector3D nullVector = new Vector3D();
                if (_trueNorth == nullVector)
                {
                    sqlStmt = "Select PROPERTYVALUE FROM BIMRL_PROPERTIES_" + _currFedID.ToString("X4") + " WHERE PROPERTYGROUPNAME='IFCATTRIBUTES' AND PROPERTYNAME='TRUENORTH'";
                    OracleCommand cmd = new OracleCommand(sqlStmt, DBOperation.DBConn);
                    object ret = cmd.ExecuteScalar();
                    if (ret != null)
                    {
                        string trueNorthStr = ret as string;
                        string tmpStr = trueNorthStr.Replace('[', ' ');
                        tmpStr = tmpStr.Replace(']', ' ');

                        string[] tokens = tmpStr.Trim().Split(',');
                        if (tokens.Length < 2)
                        {
                            // not a valid value, use default
                            _trueNorth = new Vector3D(0.0, 1.0, 0.0);
                        }
                        else
                        {
                            double x = Convert.ToDouble(tokens[0]);
                            double y = Convert.ToDouble(tokens[1]);
                            double z = 0.0;     // ignore Z for true north
                            //if (tokens.Length >= 3)
                            //    z = Convert.ToDouble(tokens[2]); 
                            _trueNorth = new Vector3D(x, y, z);
                        }
                    }
                    else
                    {
                        _trueNorth = new Vector3D(0.0, 1.0, 0.0);   // if not defined, default is the project North = +Y of the coordinate system
                    }
                    cmd.Dispose();
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + sqlStmt;
                _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                // Ignore any error
            }
            catch (SystemException e)
            {
                string excStr = "%%Insert Error - " + e.Message + "\n\t" + sqlStmt;
                _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                throw;
            }
        }

        public List<Face3D> MergedFaceList
        {
            get 
            {
                List<Face3D> mergedFaceList = new List<Face3D>();
                foreach (int Idx in _mergedFaceList)
                    mergedFaceList.Add(facesColl[Idx]);
                return mergedFaceList;
            }
        }

        public void simplifyAndMergeFaces()
        {
            // Set an arbitrary max faces to avoid complex geometry and only work on relatively simpler geometry such as Spaces, Walls
            //if (_geom.Faces.Count > 1000)
            //    return;
            double origTol = MathUtils.tol;
            int origDPrec = MathUtils._doubleDecimalPrecision;
            int origFPrec = MathUtils._floatDecimalPrecision;

            // Use better precision for the merging of faces because it deals with smaller numbers generally (e.g. Millimeter default tol we use 0.1mm. For this we will use 0.001)
            MathUtils.tol = origTol/100;
            MathUtils._doubleDecimalPrecision = origDPrec + 2;
            MathUtils._floatDecimalPrecision = origFPrec + 2;

            foreach (Face3D f in _geom.Faces)
            {
                facesColl.Add(lastFaceID, f);         // Keep faces in a dictionary and assigns ID

                foreach (Point3D vert in f.outerAndInnerVertices)
                {
                    HashSet<int> facesOfVert;
                    if (!sortedFVert.TryGetValue(vert, out facesOfVert))
                    {
                        facesOfVert = new HashSet<int>();
                        facesOfVert.Add(lastFaceID);
                        sortedFVert.Add(vert, facesOfVert);
                    }
                    else
                    {
                        // Dict already contains the point, update the HashSet with this new face
                        facesOfVert.Add(lastFaceID);
                    }
                }
                lastFaceID++;
            }
            // After the above, we have a sorted polyhedron vertices that contains hashset of faces it belongs to
            // Loop through the dictionary to merge faces that have the same normal (on the same plane)
            foreach (KeyValuePair<Point3D, HashSet<int>> dictItem in sortedFVert)
            {
               IEqualityComparer<Vector3D> normalComparer = new vectorCompare(MathUtils.tol, MathUtils._doubleDecimalPrecision);
               Dictionary<Vector3D, List<int>> faceSortedByNormal = new Dictionary<Vector3D, List<int>>(normalComparer);
                List<int> fIDList;
                List<int> badFIDList = new List<int>();

                foreach (int fID in dictItem.Value)
                {
                    Face3D f = facesColl[fID];
                    if ((double.IsNaN(f.basePlane.normalVector.X) && double.IsNaN(f.basePlane.normalVector.Y) && double.IsNaN(f.basePlane.normalVector.Z))
                        || (f.basePlane.normalVector.X == 0.0 && f.basePlane.normalVector.Y == 0.0 && f.basePlane.normalVector.Z == 0.0))
                    {
                        badFIDList.Add(fID);
                        continue;
                    }

                    if (!faceSortedByNormal.TryGetValue(f.basePlane.normalVector, out fIDList))
                    {
                        fIDList = new List<int>();
                        fIDList.Add(fID);
                        faceSortedByNormal.Add(f.basePlane.normalVector, fIDList);
                    }
                    else
                    {
                        if (!fIDList.Contains(fID))
                        {
                            fIDList.Add(fID);
                        }
                    }
                }

                foreach(KeyValuePair<Vector3D, List<int>> fListDict in faceSortedByNormal)
                {
                    // Add bad face IDs into each list as candidate that may be needed to complete the merge
                    fListDict.Value.AddRange(badFIDList);
                    List<int> mergedFaceList = null;
                    if (fListDict.Value.Count > 1)
                    {
                        tryMergeFaces(fListDict.Value, out mergedFaceList);
                        if (mergedFaceList != null && mergedFaceList.Count > 0)
                        {
                            // insert only new face indexes as the mergedlist from different vertices can be duplicated
                            foreach (int fIdx in mergedFaceList)
                                if (!_mergedFaceList.Contains(fIdx))
                                    _mergedFaceList.Add(fIdx);
                        }
                    }
                    else
                        if (!_mergedFaceList.Contains(fListDict.Value[0]))
                            _mergedFaceList.Add(fListDict.Value[0]);    // No pair face, add it into the mergedList
                }
                badFIDList.Clear();
            }

            MathUtils.tol = origTol;
            MathUtils._doubleDecimalPrecision = origDPrec;
            MathUtils._floatDecimalPrecision = origFPrec;
        }

        bool tryMergeFaces(List<int> inputFaceList, out List<int> outputFaceList)
        {
            outputFaceList = new List<int>();
            Face3D firstF = facesColl[inputFaceList[0]];
            int prevFirstFIdx = 0;
            HashSet<int> mergedFacesIdxList = new HashSet<int>();
            mergedFacesIdxList.Add(inputFaceList[0]);

            inputFaceList.RemoveAt(0);  // remove the first face from the list
            int currEdgeIdx = 0;
            bool merged = false;

            while (currEdgeIdx < firstF.outerAndInnerBoundaries.Count && inputFaceList.Count > 0)
            {
                LineSegment3D reversedEdge = new LineSegment3D(firstF.outerAndInnerBoundaries[currEdgeIdx].endPoint, firstF.outerAndInnerBoundaries[currEdgeIdx].startPoint);
                int currFaceIdx = 0;
                while (currFaceIdx < inputFaceList.Count && currEdgeIdx < firstF.outerAndInnerBoundaries.Count)
                {

                    int idx = -1;
                    Face3D currFace = facesColl[inputFaceList[currFaceIdx]];
                    idx = currFace.outerAndInnerBoundaries.IndexOf(reversedEdge);       // Test reversedEdge first as it is the most likely one in our data
                    if (idx < 0)
                    {
                        idx = currFace.outerAndInnerBoundaries.IndexOf(firstF.outerAndInnerBoundaries[currEdgeIdx]);
                        if (idx >= 0)
                        {
                            // Found match, we need to reversed the order of the data in this face
                            currFace.Reverse();
                            idx = currFace.outerAndInnerBoundaries.IndexOf(reversedEdge);
                        }
                    }
                    if (idx < 0)
                    {
                        currFaceIdx++;
                        merged = false;
                        continue;   // not found
                    }

                    // Now we need to check other edges of this face whether there is other coincide edge (this is in the case of hole(s))
                    List<int> fFaceIdxList = new List<int>(); 
                    List<int> currFaceIdxList = new List<int>();
                    for (int ci = 0; ci < currFace.outerAndInnerBoundaries.Count; ci++)
                    {
                        if (ci == idx)
                            continue;   // skip already known coincide edge
                        int ffIdx = -1;
                        LineSegment3D reL = new LineSegment3D(currFace.outerAndInnerBoundaries[ci].endPoint, currFace.outerAndInnerBoundaries[ci].startPoint);
                        ffIdx = firstF.outerAndInnerBoundaries.IndexOf(reL);
                        if (ffIdx > 0)
                        {
                            fFaceIdxList.Add(ffIdx);        // List of edges to skip when merging
                            currFaceIdxList.Add(ci);        // List of edges to skip when merging
                        }
                    }

                    // Now we will remove the paired edges and merge the faces
                    List<LineSegment3D> newFaceEdges = new List<LineSegment3D>();
                    for (int i = 0; i < currEdgeIdx; i++)
                    {
                        bool toSkip = false;
                        if (fFaceIdxList.Count > 0)
                            toSkip = fFaceIdxList.Contains(i);
                        if (!toSkip)
                            newFaceEdges.Add(firstF.outerAndInnerBoundaries[i]);     // add the previous edges from the firstF faces first. This will skip the currEdge
                    }

                    // Add the next-in-sequence edges from the second face
                    for (int i = idx + 1; i < currFace.outerAndInnerBoundaries.Count; i++)
                    {
                        bool toSkip = false;
                        if (currFaceIdxList.Count > 0)
                            toSkip = currFaceIdxList.Contains(i);
                        if (!toSkip)
                            newFaceEdges.Add(currFace.outerAndInnerBoundaries[i]);
                    }
                    for (int i = 0; i < idx; i++)
                    {
                        bool toSkip = false;
                        if (currFaceIdxList.Count > 0)
                            toSkip = currFaceIdxList.Contains(i);
                        if (!toSkip)
                            newFaceEdges.Add(currFace.outerAndInnerBoundaries[i]);
                    }

                    for (int i = currEdgeIdx + 1; i < firstF.outerAndInnerBoundaries.Count; i++)
                    {
                        bool toSkip = false;
                        if (fFaceIdxList.Count > 0)
                            toSkip = fFaceIdxList.Contains(i);
                        if (!toSkip)
                            newFaceEdges.Add(firstF.outerAndInnerBoundaries[i]);
                    }

                    // Build a new face
                    // Important to note that the list of edges may not be continuous if there is a whole. We need to go through the list here to identify whether there is any such
                    //   discontinuity and collect the edges into their respective loops
                    List<List<LineSegment3D>> loops = new List<List<LineSegment3D>>();

                    List<LineSegment3D> loopEdges = new List<LineSegment3D>();
                    loops.Add(loopEdges);
                    for (int i = 0; i < newFaceEdges.Count; i++)
                    {
                        if (i == 0)
                        {
                            loopEdges.Add(newFaceEdges[i]);
                        }
                        else
                        {
                            if (newFaceEdges[i].startPoint == newFaceEdges[i - 1].endPoint)
                                loopEdges.Add(newFaceEdges[i]);
                            else
                            {
                                // Discontinuity detected
                                loopEdges = new List<LineSegment3D>();   // start new loop
                                loops.Add(loopEdges); 
                                loopEdges.Add(newFaceEdges[i]);
                            }
                        }
                    }

                    List<List<LineSegment3D>> finalLoops = new List<List<LineSegment3D>>();
                    //if (loops.Count <= 2)
                    //    finalLoops.AddRange(loops);
                    //else
                    {
                        while (loops.Count > 1)
                        {
                            // There are more than 1 loops, need to consolidate if there are fragments to combine due to their continuity between the fragments
                            int toDelIdx = -1;
                            for (int i = 1; i < loops.Count; i++)
                            {
                                if (loops[0][loops[0].Count - 1].endPoint == loops[i][0].startPoint)
                                {
                                    // found continuity, merge the loops
                                    List<LineSegment3D> newLoop = new List<LineSegment3D>(loops[0]);
                                    newLoop.AddRange(loops[i]);
                                    finalLoops.Add(newLoop);
                                    toDelIdx = i;
                                    break;
                                }
                            }
                            if (toDelIdx > 0)
                            {
                                loops.RemoveAt(toDelIdx);   // !!!! Important to remove the later member first before removing the first one 
                                loops.RemoveAt(0);
                            }
                            else
                            {
                                // No continuity found, copy the firs loop to the final loop
                                List<LineSegment3D> newLoop = new List<LineSegment3D>(loops[0]);
                                finalLoops.Add(newLoop);
                                loops.RemoveAt(0);
                            }
                        }
                        if (loops.Count > 0)
                        {
                            // Add remaining list into the final loops
                            finalLoops.AddRange(loops);
                        }
                    }

                    if (finalLoops.Count > 1)
                    {
                        // Find the largest loop and put it in the first position signifying the outer loop and the rest are the inner loops
                        int largestPerimeterIdx = 0;
                        double largestPerimeter = 0.0;
                        for (int i = 0; i < finalLoops.Count; i++)
                        {
                            double loopPerimeter = 0.0;
                            foreach (LineSegment3D line in finalLoops[i])
                                loopPerimeter += line.extent;
                            if (loopPerimeter > largestPerimeter)
                            {
                                largestPerimeter = loopPerimeter;
                                largestPerimeterIdx = i;
                            }
                        }
                        // We need to move the largest loop into the head if it is not
                        if (largestPerimeterIdx > 0)
                        {
                            List<LineSegment3D> largestLoop = new List<LineSegment3D>(finalLoops[largestPerimeterIdx]);
                            finalLoops.RemoveAt(largestPerimeterIdx);
                            finalLoops.Insert(0, largestLoop);
                        }
                    }

                    // Collect the vertices from the list of Edges into list of list of vertices starting with the outer loop (largest loop) following the finalLoop
                    List<List<Point3D>> newFaceVertsLoops = new List<List<Point3D>>();
                    foreach (List<LineSegment3D> loop in finalLoops)
                    {
                        List<Point3D> newFaceVerts = new List<Point3D>();
                        for (int i = 0; i < loop.Count; i++)
                        {
                            if (i == 0)
                            {
                                newFaceVerts.Add(loop[i].startPoint);
                                newFaceVerts.Add(loop[i].endPoint);
                            }
                            else if (i == loop.Count - 1)   // Last
                            {
                                // Add nothing as the last segment ends at the first vertex
                            }
                            else
                            {
                                newFaceVerts.Add(loop[i].endPoint);
                            }
                        }
                        // close the loop with end point from the starting point (it is important to mark the end of loop and if there is any other vertex follow, they start the inner loop)
                        if (newFaceVerts.Count > 0)
                        {
                            if (newFaceVerts[0] != newFaceVerts[newFaceVerts.Count-1])
                            {
                                // If the end vertex is not the same as the start vertex, add the first vertex to the end vertex
                                newFaceVerts.Add(newFaceVerts[0]);
                            }
                            newFaceVertsLoops.Add(newFaceVerts);
                        }
                    }

                    // Validate the resulting face, skip if not valid
                    if (!Face3D.validateFace(newFaceVertsLoops))
                    {
                        inputFaceList.RemoveAt(0);  // remove the first face from the list to advance to the next face
                        currEdgeIdx = 0;
                        merged = false;
                        break;
                    }

                    // This new merged face will override/replace the original firstF for the next round
                    //Vector3D origNormal = firstF.basePlane.normalVector;
                    firstF = new Face3D(newFaceVertsLoops);
                    //Vector3D newFNormal = new Vector3D(firstF.basePlane.normalVector.X, firstF.basePlane.normalVector.Y, firstF.basePlane.normalVector.Z);
                    //// Need to make sure the new face maintains the same normal. If somehow it turns (possibly because of hole), reverse it
                    //if (origNormal != newFNormal)
                    //{
                    //    newFNormal.Negate();
                    //    if (origNormal == newFNormal)
                    //        firstF.Reverse();
                    //}
                    // Reset currEdgeIdx since the first face has been replaced
                    currEdgeIdx = 0;
                    reversedEdge = new LineSegment3D(firstF.outerAndInnerBoundaries[currEdgeIdx].endPoint, firstF.outerAndInnerBoundaries[currEdgeIdx].startPoint);

                    mergedFacesIdxList.Add(inputFaceList[currFaceIdx]);
                    inputFaceList.RemoveAt(currFaceIdx);
                    currFaceIdx = 0;
                    merged = true;
                }

                if (!merged)
                {
                    currEdgeIdx++;
                }
                if (merged || currEdgeIdx == firstF.outerAndInnerBoundaries.Count)
                {
                    facesColl.Add(lastFaceID, firstF);
                    prevFirstFIdx = lastFaceID;
                    outputFaceList.Add(lastFaceID);

                    // Now loop through all the dictionary of the sortedVert and replace all merged face indexes with the new one
                    foreach (KeyValuePair<Point3D, HashSet<int>> v in sortedFVert)
                    {
                        HashSet<int> fIndexes = v.Value;
                        bool replaced = false;
                        foreach(int Idx in mergedFacesIdxList)
                        {
                            replaced |= fIndexes.Remove(Idx);
                            _mergedFaceList.Remove(Idx);        // Remove the idx face also from _mergeFaceList as some faces might be left unmerged in the previous step(s)
                            // remove also prev firstF
                            //fIndexes.Remove(prevFirstFIdx);
                            //_mergedFaceList.Remove(prevFirstFIdx);
                            //outputFaceList.Remove(prevFirstFIdx);
                        }
                        if (replaced)
                            fIndexes.Add(lastFaceID);   // replace the merged face indexes with the new merged face index
                    }

                    lastFaceID++;
                    if (inputFaceList.Count > 0)
                    {
                        firstF = facesColl[inputFaceList[0]];
                        mergedFacesIdxList.Clear();
                        mergedFacesIdxList.Add(inputFaceList[0]);
                        inputFaceList.RemoveAt(0);  // remove the first face from the list
                        currEdgeIdx = 0;
                        merged = false;
                    }
                }
            }

            return merged;
        }

      int findMatechedIndexSegment(Dictionary<LineSegment3D, int> segDict, LineSegment3D inpSeg)
      {
         int idx;
         if (segDict.TryGetValue(inpSeg, out idx))
            return idx;
         else
            return -1;
      }

      bool tryMergeFacesUsingDict(List<int> inputFaceList, out List<int> outputFaceList)
      {
         outputFaceList = new List<int>();
         Face3D firstF = facesColl[inputFaceList[0]];
         int prevFirstFIdx = 0;
         HashSet<int> mergedFacesIdxList = new HashSet<int>();
         mergedFacesIdxList.Add(inputFaceList[0]);

         inputFaceList.RemoveAt(0);  // remove the first face from the list
         int currEdgeIdx = 0;
         bool merged = false;

         while (currEdgeIdx < firstF.outerAndInnerBoundaries.Count && inputFaceList.Count > 0)
         {
            LineSegment3D reversedEdge = new LineSegment3D(firstF.outerAndInnerBoundaries[currEdgeIdx].endPoint, firstF.outerAndInnerBoundaries[currEdgeIdx].startPoint);
            int currFaceIdx = 0;
            while (currFaceIdx < inputFaceList.Count && currEdgeIdx < firstF.outerAndInnerBoundaries.Count)
            {

               //int idx = -1;
               Face3D currFace = facesColl[inputFaceList[currFaceIdx]];
               //idx = currFace.outerAndInnerBoundaries.IndexOf(reversedEdge);       // Test reversedEdge first as it is the most likely one in our data
               int idx = findMatechedIndexSegment(currFace.outerAndInnerBoundariesWithDict, reversedEdge);
               if (idx < 0)
               {
                  //idx = currFace.outerAndInnerBoundaries.IndexOf(firstF.outerAndInnerBoundaries[currEdgeIdx]);
                  idx = findMatechedIndexSegment(currFace.outerAndInnerBoundariesWithDict, firstF.outerAndInnerBoundaries[currEdgeIdx]);
                  if (idx >= 0)
                  {
                     // Found match, we need to reversed the order of the data in this face
                     currFace.Reverse();
                     //idx = currFace.outerAndInnerBoundaries.IndexOf(reversedEdge);
                     idx = findMatechedIndexSegment(currFace.outerAndInnerBoundariesWithDict, reversedEdge);
                  }
               }
               if (idx < 0)
               {
                  currFaceIdx++;
                  merged = false;
                  continue;   // not found
               }

               // Now we need to check other edges of this face whether there is other coincide edge (this is in the case of hole(s))
               List<int> fFaceIdxList = new List<int>();
               List<int> currFaceIdxList = new List<int>();
               for (int ci = 0; ci < currFace.outerAndInnerBoundaries.Count; ci++)
               {
                  if (ci == idx)
                     continue;   // skip already known coincide edge
                  int ffIdx = -1;
                  LineSegment3D reL = new LineSegment3D(currFace.outerAndInnerBoundaries[ci].endPoint, currFace.outerAndInnerBoundaries[ci].startPoint);
                  //ffIdx = firstF.outerAndInnerBoundaries.IndexOf(reL);
                  ffIdx = findMatechedIndexSegment(firstF.outerAndInnerBoundariesWithDict, reL);
                  if (ffIdx > 0)
                  {
                     fFaceIdxList.Add(ffIdx);        // List of edges to skip when merging
                     currFaceIdxList.Add(ci);        // List of edges to skip when merging
                  }
               }

               // Now we will remove the paired edges and merge the faces
               List<LineSegment3D> newFaceEdges = new List<LineSegment3D>();
               for (int i = 0; i < currEdgeIdx; i++)
               {
                  bool toSkip = false;
                  if (fFaceIdxList.Count > 0)
                     toSkip = fFaceIdxList.Contains(i);
                  if (!toSkip)
                     newFaceEdges.Add(firstF.outerAndInnerBoundaries[i]);     // add the previous edges from the firstF faces first. This will skip the currEdge
               }

               // Add the next-in-sequence edges from the second face
               for (int i = idx + 1; i < currFace.outerAndInnerBoundaries.Count; i++)
               {
                  bool toSkip = false;
                  if (currFaceIdxList.Count > 0)
                     toSkip = currFaceIdxList.Contains(i);
                  if (!toSkip)
                     newFaceEdges.Add(currFace.outerAndInnerBoundaries[i]);
               }
               for (int i = 0; i < idx; i++)
               {
                  bool toSkip = false;
                  if (currFaceIdxList.Count > 0)
                     toSkip = currFaceIdxList.Contains(i);
                  if (!toSkip)
                     newFaceEdges.Add(currFace.outerAndInnerBoundaries[i]);
               }

               for (int i = currEdgeIdx + 1; i < firstF.outerAndInnerBoundaries.Count; i++)
               {
                  bool toSkip = false;
                  if (fFaceIdxList.Count > 0)
                     toSkip = fFaceIdxList.Contains(i);
                  if (!toSkip)
                     newFaceEdges.Add(firstF.outerAndInnerBoundaries[i]);
               }

               // Build a new face
               // Important to note that the list of edges may not be continuous if there is a whole. We need to go through the list here to identify whether there is any such
               //   discontinuity and collect the edges into their respective loops
               List<List<LineSegment3D>> loops = new List<List<LineSegment3D>>();

               List<LineSegment3D> loopEdges = new List<LineSegment3D>();
               loops.Add(loopEdges);
               for (int i = 0; i < newFaceEdges.Count; i++)
               {
                  if (i == 0)
                  {
                     loopEdges.Add(newFaceEdges[i]);
                  }
                  else
                  {
                     if (newFaceEdges[i].startPoint == newFaceEdges[i - 1].endPoint)
                        loopEdges.Add(newFaceEdges[i]);
                     else
                     {
                        // Discontinuity detected
                        loopEdges = new List<LineSegment3D>();   // start new loop
                        loops.Add(loopEdges);
                        loopEdges.Add(newFaceEdges[i]);
                     }
                  }
               }

               List<List<LineSegment3D>> finalLoops = new List<List<LineSegment3D>>();
               //if (loops.Count <= 2)
               //    finalLoops.AddRange(loops);
               //else
               {
                  while (loops.Count > 1)
                  {
                     // There are more than 1 loops, need to consolidate if there are fragments to combine due to their continuity between the fragments
                     int toDelIdx = -1;
                     for (int i = 1; i < loops.Count; i++)
                     {
                        if (loops[0][loops[0].Count - 1].endPoint == loops[i][0].startPoint)
                        {
                           // found continuity, merge the loops
                           List<LineSegment3D> newLoop = new List<LineSegment3D>(loops[0]);
                           newLoop.AddRange(loops[i]);
                           finalLoops.Add(newLoop);
                           toDelIdx = i;
                           break;
                        }
                     }
                     if (toDelIdx > 0)
                     {
                        loops.RemoveAt(toDelIdx);   // !!!! Important to remove the later member first before removing the first one 
                        loops.RemoveAt(0);
                     }
                     else
                     {
                        // No continuity found, copy the firs loop to the final loop
                        List<LineSegment3D> newLoop = new List<LineSegment3D>(loops[0]);
                        finalLoops.Add(newLoop);
                        loops.RemoveAt(0);
                     }
                  }
                  if (loops.Count > 0)
                  {
                     // Add remaining list into the final loops
                     finalLoops.AddRange(loops);
                  }
               }

               if (finalLoops.Count > 1)
               {
                  // Find the largest loop and put it in the first position signifying the outer loop and the rest are the inner loops
                  int largestPerimeterIdx = 0;
                  double largestPerimeter = 0.0;
                  for (int i = 0; i < finalLoops.Count; i++)
                  {
                     double loopPerimeter = 0.0;
                     foreach (LineSegment3D line in finalLoops[i])
                        loopPerimeter += line.extent;
                     if (loopPerimeter > largestPerimeter)
                     {
                        largestPerimeter = loopPerimeter;
                        largestPerimeterIdx = i;
                     }
                  }
                  // We need to move the largest loop into the head if it is not
                  if (largestPerimeterIdx > 0)
                  {
                     List<LineSegment3D> largestLoop = new List<LineSegment3D>(finalLoops[largestPerimeterIdx]);
                     finalLoops.RemoveAt(largestPerimeterIdx);
                     finalLoops.Insert(0, largestLoop);
                  }
               }

               // Collect the vertices from the list of Edges into list of list of vertices starting with the outer loop (largest loop) following the finalLoop
               List<List<Point3D>> newFaceVertsLoops = new List<List<Point3D>>();
               foreach (List<LineSegment3D> loop in finalLoops)
               {
                  List<Point3D> newFaceVerts = new List<Point3D>();
                  for (int i = 0; i < loop.Count; i++)
                  {
                     if (i == 0)
                     {
                        newFaceVerts.Add(loop[i].startPoint);
                        newFaceVerts.Add(loop[i].endPoint);
                     }
                     else if (i == loop.Count - 1)   // Last
                     {
                        // Add nothing as the last segment ends at the first vertex
                     }
                     else
                     {
                        newFaceVerts.Add(loop[i].endPoint);
                     }
                  }
                  // close the loop with end point from the starting point (it is important to mark the end of loop and if there is any other vertex follow, they start the inner loop)
                  if (newFaceVerts.Count > 0)
                  {
                     if (newFaceVerts[0] != newFaceVerts[newFaceVerts.Count - 1])
                     {
                        // If the end vertex is not the same as the start vertex, add the first vertex to the end vertex
                        newFaceVerts.Add(newFaceVerts[0]);
                     }
                     newFaceVertsLoops.Add(newFaceVerts);
                  }
               }

               // Validate the resulting face, skip if not valid
               if (!Face3D.validateFace(newFaceVertsLoops))
               {
                  inputFaceList.RemoveAt(0);  // remove the first face from the list to advance to the next face
                  currEdgeIdx = 0;
                  merged = false;
                  break;
               }

               // This new merged face will override/replace the original firstF for the next round
               //Vector3D origNormal = firstF.basePlane.normalVector;
               firstF = new Face3D(newFaceVertsLoops);
               //Vector3D newFNormal = new Vector3D(firstF.basePlane.normalVector.X, firstF.basePlane.normalVector.Y, firstF.basePlane.normalVector.Z);
               //// Need to make sure the new face maintains the same normal. If somehow it turns (possibly because of hole), reverse it
               //if (origNormal != newFNormal)
               //{
               //    newFNormal.Negate();
               //    if (origNormal == newFNormal)
               //        firstF.Reverse();
               //}
               // Reset currEdgeIdx since the first face has been replaced
               currEdgeIdx = 0;
               reversedEdge = new LineSegment3D(firstF.outerAndInnerBoundaries[currEdgeIdx].endPoint, firstF.outerAndInnerBoundaries[currEdgeIdx].startPoint);

               mergedFacesIdxList.Add(inputFaceList[currFaceIdx]);
               inputFaceList.RemoveAt(currFaceIdx);
               currFaceIdx = 0;
               merged = true;
            }

            if (!merged)
            {
               currEdgeIdx++;
            }
            if (merged || currEdgeIdx == firstF.outerAndInnerBoundaries.Count)
            {
               facesColl.Add(lastFaceID, firstF);
               prevFirstFIdx = lastFaceID;
               outputFaceList.Add(lastFaceID);

               // Now loop through all the dictionary of the sortedVert and replace all merged face indexes with the new one
               foreach (KeyValuePair<Point3D, HashSet<int>> v in sortedFVert)
               {
                  HashSet<int> fIndexes = v.Value;
                  bool replaced = false;
                  foreach (int Idx in mergedFacesIdxList)
                  {
                     replaced |= fIndexes.Remove(Idx);
                     _mergedFaceList.Remove(Idx);        // Remove the idx face also from _mergeFaceList as some faces might be left unmerged in the previous step(s)
                                                         // remove also prev firstF
                                                         //fIndexes.Remove(prevFirstFIdx);
                                                         //_mergedFaceList.Remove(prevFirstFIdx);
                                                         //outputFaceList.Remove(prevFirstFIdx);
                  }
                  if (replaced)
                     fIndexes.Add(lastFaceID);   // replace the merged face indexes with the new merged face index
               }

               lastFaceID++;
               if (inputFaceList.Count > 0)
               {
                  firstF = facesColl[inputFaceList[0]];
                  mergedFacesIdxList.Clear();
                  mergedFacesIdxList.Add(inputFaceList[0]);
                  inputFaceList.RemoveAt(0);  // remove the first face from the list
                  currEdgeIdx = 0;
                  merged = false;
               }
            }
         }

         return merged;
      }
      public bool insertIntoDB(bool forUserDict)
        {
            List<string> arrElementID = new List<string>();
            List<string> arrFaceID = new List<string>();
            List<string> arrType = new List<string>();
            List<SdoGeometry> arrFaceGeom = new List<SdoGeometry>();
            List<SdoGeometry> arrNormal = new List<SdoGeometry>();
            List<double> arrAngle = new List<double>();
            List<SdoGeometry> arrCentroid = new List<SdoGeometry>();

            string sqlStmt;
            if (forUserDict)
                sqlStmt = "INSERT INTO USERGEOM_TOPO_FACE (ELEMENTID, ID, TYPE, POLYGON, NORMAL, ANGLEFROMNORTH, CENTROID) "
                            + "VALUES (:1, :2, :3, :4, :5, :6, : 7)";
            else
                sqlStmt = "INSERT INTO BIMRL_TOPO_FACE_" + _currFedID.ToString("X4") + "(ELEMENTID, ID, TYPE, POLYGON, NORMAL, ANGLEFROMNORTH, CENTROID) "
                            + "VALUES (:1, :2, :3, :4, :5, :6, : 7)";
            OracleCommand cmd = new OracleCommand(sqlStmt, DBOperation.DBConn);
            OracleParameter[] Params = new OracleParameter[7];
            
            Params[0] = cmd.Parameters.Add("1", OracleDbType.Varchar2);
            Params[1] = cmd.Parameters.Add("2", OracleDbType.Varchar2);
            Params[2] = cmd.Parameters.Add("3", OracleDbType.Varchar2);
            Params[3] = cmd.Parameters.Add("4", OracleDbType.Object);
            Params[3].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            Params[4] = cmd.Parameters.Add("5", OracleDbType.Object);
            Params[4].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            Params[5] = cmd.Parameters.Add("1", OracleDbType.Double);
            Params[6] = cmd.Parameters.Add("7", OracleDbType.Object);
            Params[6].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            for (int i = 0; i < 7; i++)
                Params[i].Direction = ParameterDirection.Input;

            //foreach (KeyValuePair<int,Face3D> fCol in facesColl)
            foreach(int fIdx in _mergedFaceList)
            {
                Face3D face = facesColl[fIdx];
                if (!Face3D.validateFace(face.verticesWithHoles) || !Face3D.validateFace(face))
                {
                    // In some cases for unknown reason, we may get a face with all vertices aligned in a straight line, resulting in invalid normal vector
                    // for this case, we will simply skip the face
                    _refBIMRLCommon.BIMRlErrorStack.Push("%Warning (ElementID: '" + _elementid + "'): Skipping face#  " + fIdx.ToString() + " because it is an invalid face!");
                    continue;
                }

                SdoGeometry normal = new SdoGeometry();
                normal.Dimensionality = 3;
                normal.LRS = 0;
                normal.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                int gType = normal.PropertiesToGTYPE();

                SdoPoint normalP = new SdoPoint();
                normalP.XD = face.basePlane.normalVector.X;
                normalP.YD = face.basePlane.normalVector.Y;
                normalP.ZD = face.basePlane.normalVector.Z;
                normal.SdoPoint = normalP;

                arrNormal.Add(normal);
                arrElementID.Add(_elementid);

                // For the main table, we want to ensure that the face ID is not-overlapped
                int faceID = fIdx;
                if (!forUserDict)
                    faceID = fIdx + _fIDOffset;

                arrFaceID.Add(faceID.ToString());
                arrType.Add(_faceCategory);

                Vector3D normal2D = new Vector3D(normalP.XD.Value, normalP.YD.Value, 0.0);
                double angleRad = Math.Atan2(normal2D.Y, normal2D.X) - Math.Atan2(_trueNorth.Y, _trueNorth.X);
                arrAngle.Add(angleRad);

                SdoGeometry centroid = new SdoGeometry();
                centroid.Dimensionality = 3;
                centroid.LRS = 0;
                centroid.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                gType = centroid.PropertiesToGTYPE();

                SdoPoint centroidP = new SdoPoint();
                centroidP.XD = face.boundingBox.Center.X;
                centroidP.YD = face.boundingBox.Center.Y;
                centroidP.ZD = face.boundingBox.Center.Z;
                centroid.SdoPoint = centroidP;

                arrCentroid.Add(centroid);

                List<double> arrCoord = new List<double>();
                List<int> elemInfoArr = new List<int>();

                SdoGeometry sdoGeomData = new SdoGeometry();
                sdoGeomData.Dimensionality = 3;
                sdoGeomData.LRS = 0;
                sdoGeomData.GeometryType = (int)SdoGeometryTypes.GTYPE.POLYGON;
                gType = sdoGeomData.PropertiesToGTYPE();

                int noVerts = face.vertices.Count;
                foreach (Point3D v in face.vertices)
                {
                    arrCoord.Add(v.X);
                    arrCoord.Add(v.Y);
                    arrCoord.Add(v.Z);
                }
                if (face.vertices[0] != face.vertices[noVerts - 1])
                {
                    // If the end vertex is not the same as the first, add it to close for SDO
                    arrCoord.Add(face.vertices[0].X);
                    arrCoord.Add(face.vertices[0].Y);
                    arrCoord.Add(face.vertices[0].Z);
                    noVerts++;
                }
                elemInfoArr.Add(1);  // starting position
                elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_EXTERIOR);
                elemInfoArr.Add(1);

                // Add holes
                // For every hole, we will also create an independent face for the hole in addition of being a hole to the main face
                List<SdoGeometry> holeFaces = new List<SdoGeometry>();
                List<Point3D> holeCentroids = new List<Point3D>();
                if (face.verticesWithHoles.Count > 1)
                {
                    for (int i = 1; i < face.verticesWithHoles.Count; i++)
                    {
                        SdoGeometry hole = new SdoGeometry();
                        hole.Dimensionality = 3;
                        hole.LRS = 0;
                        hole.GeometryType = (int)SdoGeometryTypes.GTYPE.POLYGON;
                        int holeGType = hole.PropertiesToGTYPE();
                        List<double> arrHoleCoord = new List<double>();
                        List<int> holeInfoArr = new List<int>();
                        holeInfoArr.Add(1);
                        holeInfoArr.Add((int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_EXTERIOR);
                        holeInfoArr.Add(1);
                        BoundingBox3D holeBB = new BoundingBox3D(face.verticesWithHoles[i]);
                        holeCentroids.Add(holeBB.Center);

                        elemInfoArr.Add(noVerts * 3 + 1);  // starting position
                        elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_INTERIOR);
                        elemInfoArr.Add(1);
                        foreach (Point3D v in face.verticesWithHoles[i])
                        {
                            arrCoord.Add(v.X);
                            arrCoord.Add(v.Y);
                            arrCoord.Add(v.Z);

                            arrHoleCoord.Add(v.X);
                            arrHoleCoord.Add(v.Y);
                            arrHoleCoord.Add(v.Z);
                            noVerts++;
                        }
                        if (face.verticesWithHoles[i][0] != face.verticesWithHoles[i][face.verticesWithHoles[i].Count - 1])
                        {
                            // If the end vertex is not the same as the first, add it to close for SDO
                            arrCoord.Add(face.verticesWithHoles[i][0].X);
                            arrCoord.Add(face.verticesWithHoles[i][0].Y);
                            arrCoord.Add(face.verticesWithHoles[i][0].Z);
                            arrHoleCoord.Add(face.verticesWithHoles[i][0].X);
                            arrHoleCoord.Add(face.verticesWithHoles[i][0].Y);
                            arrHoleCoord.Add(face.verticesWithHoles[i][0].Z);
                            noVerts++;
                        }
                        hole.ElemArrayOfInts = holeInfoArr.ToArray();
                        hole.OrdinatesArrayOfDoubles = arrHoleCoord.ToArray();
                        holeFaces.Add(hole);
                    }
                }

                sdoGeomData.ElemArrayOfInts = elemInfoArr.ToArray();
                sdoGeomData.OrdinatesArrayOfDoubles = arrCoord.ToArray();
                arrFaceGeom.Add(sdoGeomData);

                // add entry(ies) for holes
                int noHole = 0;
                foreach(SdoGeometry geom in holeFaces)
                {
                    arrElementID.Add(_elementid);
                    arrFaceID.Add(fIdx.ToString() + "-" + noHole.ToString());

                    arrType.Add("HOLE");    // special category for HOLE
                    arrNormal.Add(normal);  // follow the normal of the main face
                    arrAngle.Add(angleRad);

                    SdoGeometry holeCentroid = new SdoGeometry();
                    holeCentroid.Dimensionality = 3;
                    holeCentroid.LRS = 0;
                    holeCentroid.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                    gType = holeCentroid.PropertiesToGTYPE();

                    SdoPoint holeCentroidP = new SdoPoint();
                    holeCentroidP.XD = holeCentroids[noHole].X;
                    holeCentroidP.YD = holeCentroids[noHole].Y;
                    holeCentroidP.ZD = holeCentroids[noHole].Z;
                    holeCentroid.SdoPoint = holeCentroidP;
                    arrCentroid.Add(holeCentroid);

                    arrFaceGeom.Add(holeFaces[noHole]);
                    noHole++;
                }

                if (arrElementID.Count >= DBOperation.commitInterval)
                {
                    Params[0].Value = arrElementID.ToArray();
                    Params[0].Size = arrElementID.Count;
                    Params[1].Value = arrFaceID.ToArray();
                    Params[1].Size = arrFaceID.Count;
                    Params[2].Value = arrType.ToArray();
                    Params[2].Size = arrType.Count;
                    Params[3].Value = arrFaceGeom.ToArray();
                    Params[3].Size = arrFaceGeom.Count;
                    Params[4].Value = arrNormal.ToArray();
                    Params[4].Size = arrNormal.Count;
                    Params[5].Value = arrAngle.ToArray();
                    Params[5].Size = arrAngle.Count;
                    Params[6].Value = arrCentroid.ToArray();
                    Params[6].Size = arrCentroid.Count;

                    try
                    {
                        cmd.ArrayBindCount = arrElementID.Count;    // No of values in the array to be inserted
                        int commandStatus = cmd.ExecuteNonQuery();
                        DBOperation.commitTransaction();
                        arrElementID.Clear();
                        arrFaceID.Clear();
                        arrType.Clear();
                        arrFaceGeom.Clear();
                        arrNormal.Clear();
                        arrAngle.Clear();
                        arrCentroid.Clear();
                    }
                    catch (OracleException e)
                    {
                        string excStr = "%%Insert Error - " + e.Message + "\n\t" + sqlStmt;
                        _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                        arrElementID.Clear();
                        arrFaceID.Clear();
                        arrType.Clear();
                        arrFaceGeom.Clear();
                        arrNormal.Clear();
                        arrAngle.Clear();
                        arrCentroid.Clear();
                        continue;
                    }
                    catch (SystemException e)
                    {
                        string excStr = "%%Insert Error - " + e.Message + "\n\t" + sqlStmt;
                        _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                        throw;
                    }
                }
            }

            if (arrElementID.Count > 0)
            {

                Params[0].Value = arrElementID.ToArray();
                Params[0].Size = arrElementID.Count;
                Params[1].Value = arrFaceID.ToArray();
                Params[1].Size = arrFaceID.Count;
                Params[2].Value = arrType.ToArray();
                Params[2].Size = arrType.Count;
                Params[3].Value = arrFaceGeom.ToArray();
                Params[3].Size = arrFaceGeom.Count;
                Params[4].Value = arrNormal.ToArray();
                Params[4].Size = arrNormal.Count;
                Params[5].Value = arrAngle.ToArray();
                Params[5].Size = arrAngle.Count;
                Params[6].Value = arrCentroid.ToArray();
                Params[6].Size = arrCentroid.Count;

                try
                {
                    cmd.ArrayBindCount = arrElementID.Count;    // No of values in the array to be inserted
                    int commandStatus = cmd.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                }
                catch (OracleException e)
                {
                    string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + sqlStmt;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    // Ignore any error
                }
                catch (SystemException e)
                {
                    string excStr = "%%Insert Error - " + e.Message + "\n\t" + sqlStmt;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    throw;
                }
            }

            DBOperation.commitTransaction();
            cmd.Dispose();
            return true;
        }

        public void deriveMajorAxes()
        {
            PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis(_geom);
            majorAxes = pca.identifyMajorAxes();
            Point3D centroid = pca.Centroid;
            List<Point3D> OBBVerts = pca.OBBVertices;

            // Update BIMRL_ELEMENT table
            string sqlStmt = "UPDATE BIMRL_ELEMENT_" + _currFedID.ToString("X4") + " SET BODY_MAJOR_AXIS_CENTROID=:1, BODY_MAJOR_AXIS1=:2, BODY_MAJOR_AXIS2=:3, BODY_MAJOR_AXIS3=:4, OBB=:5 WHERE ELEMENTID='" + _elementid + "'";
            OracleCommand cmd = new OracleCommand(sqlStmt, DBOperation.DBConn);
            OracleParameter[] Params = new OracleParameter[5];

            Params[0] = cmd.Parameters.Add("1", OracleDbType.Object);
            Params[0].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            Params[1] = cmd.Parameters.Add("2", OracleDbType.Object);
            Params[1].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            Params[2] = cmd.Parameters.Add("3", OracleDbType.Object);
            Params[2].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            Params[3] = cmd.Parameters.Add("4", OracleDbType.Object);
            Params[3].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            Params[4] = cmd.Parameters.Add("5", OracleDbType.Object);
            Params[4].UdtTypeName = "MDSYS.SDO_GEOMETRY";
            for (int i = 0; i < 5; i++)
                Params[i].Direction = ParameterDirection.Input;

            SdoGeometry cent = new SdoGeometry();
            cent.Dimensionality = 3;
            cent.LRS = 0;
            cent.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
            int gType = cent.PropertiesToGTYPE();
            SdoPoint centP = new SdoPoint();
            centP.XD = centroid.X;
            centP.YD = centroid.Y;
            centP.ZD = centroid.Z;
            cent.SdoPoint = centP;
            Params[0].Value = cent;
            Params[0].Size = 1;

            SdoGeometry ax1 = new SdoGeometry();
            ax1.Dimensionality = 3;
            ax1.LRS = 0;
            ax1.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
            gType = ax1.PropertiesToGTYPE();
            SdoPoint ax1P = new SdoPoint();
            ax1P.XD = majorAxes[0].X;
            ax1P.YD = majorAxes[0].Y;
            ax1P.ZD = majorAxes[0].Z;
            ax1.SdoPoint = ax1P;
            Params[1].Value = ax1;
            Params[1].Size = 1;

            SdoGeometry ax2 = new SdoGeometry();
            ax2.Dimensionality = 3;
            ax2.LRS = 0;
            ax2.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
            gType = ax2.PropertiesToGTYPE();
            SdoPoint ax2P = new SdoPoint();
            ax2P.XD = majorAxes[1].X;
            ax2P.YD = majorAxes[1].Y;
            ax2P.ZD = majorAxes[1].Z;
            ax2.SdoPoint = ax2P;
            Params[2].Value = ax2;
            Params[2].Size = 1;

            SdoGeometry ax3 = new SdoGeometry();
            ax3.Dimensionality = 3;
            ax3.LRS = 0;
            ax3.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
            gType = ax3.PropertiesToGTYPE();
            SdoPoint ax3P = new SdoPoint();
            ax3P.XD = majorAxes[2].X;
            ax3P.YD = majorAxes[2].Y;
            ax3P.ZD = majorAxes[2].Z;
            ax3.SdoPoint = ax3P;
            Params[3].Value = ax3;
            Params[3].Size = 1;

            OBB = createSDOGeomOBB(OBBVerts);
            Params[4].Value = OBB;
            Params[4].Size = 1;

            try
            {
                int commandStatus = cmd.ExecuteNonQuery();
                DBOperation.commitTransaction();
            }
            catch (OracleException e)
            {
                string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + sqlStmt;
                _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                // Ignore any error
            }
            catch (SystemException e)
            {
                string excStr = "%%Insert Error - " + e.Message + "\n\t" + sqlStmt;
                _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                throw;
            }

            DBOperation.commitTransaction();
            cmd.Dispose();
        }

        public void writeToX3D()
        {
            BIMRLCommon BIMRLCommonRef = new BIMRLCommon();
            BIMRLExportSDOToX3D x3dExp = new BIMRLExportSDOToX3D(BIMRLCommonRef, "c:\\temp\\drawFaces.x3d");
            if (MergedFaceList != null)
                x3dExp.exportFacesToX3D(MergedFaceList);
            x3dExp.endExportToX3D();
        }

        /// <summary>
        /// Create SDOGeometry data for Solid that comes from OBB vertices in the following order
        ///               7 --- 6
        ///              /|    /|
        ///             4 --- 5 |
        ///             | 3 --| 2 
        ///             |/    |/
        ///             0 --- 1
        /// </summary>
        /// <param name="OBBVerts">OBB vertices: must be in order</param>
        /// <returns></returns>
        SdoGeometry createSDOGeomOBB(List<Point3D> OBBVerts)
        {
            SdoGeometry geom = new SdoGeometry();
            geom.Dimensionality = 3;
            geom.LRS = 0;
            geom.GeometryType = (int)SdoGeometryTypes.GTYPE.SOLID;
            int gType = geom.PropertiesToGTYPE();

            // expand the list into denormalized face coordinates for SDO Geometry
            List<Point3D> expVertList = new List<Point3D>();
            expVertList.Add(OBBVerts[0]);
            expVertList.Add(OBBVerts[3]);
            expVertList.Add(OBBVerts[2]);
            expVertList.Add(OBBVerts[1]);
            expVertList.Add(OBBVerts[0]);

            expVertList.Add(OBBVerts[0]);
            expVertList.Add(OBBVerts[1]);
            expVertList.Add(OBBVerts[5]);
            expVertList.Add(OBBVerts[4]);
            expVertList.Add(OBBVerts[0]);

            expVertList.Add(OBBVerts[1]);
            expVertList.Add(OBBVerts[2]);
            expVertList.Add(OBBVerts[6]);
            expVertList.Add(OBBVerts[5]);
            expVertList.Add(OBBVerts[1]);

            expVertList.Add(OBBVerts[2]);
            expVertList.Add(OBBVerts[3]);
            expVertList.Add(OBBVerts[7]);
            expVertList.Add(OBBVerts[6]);
            expVertList.Add(OBBVerts[2]);

            expVertList.Add(OBBVerts[3]);
            expVertList.Add(OBBVerts[0]);
            expVertList.Add(OBBVerts[4]);
            expVertList.Add(OBBVerts[7]);
            expVertList.Add(OBBVerts[3]);

            expVertList.Add(OBBVerts[4]);
            expVertList.Add(OBBVerts[5]);
            expVertList.Add(OBBVerts[6]);
            expVertList.Add(OBBVerts[7]);
            expVertList.Add(OBBVerts[4]);

            int geomType = (int)SdoGeometryTypes.ETYPE_COMPOUND.SOLID;
            List<int> elemInfoArr = new List<int>() { 1, geomType, 1 };
            elemInfoArr.Add(1);
            elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_COMPOUND.SURFACE_EXTERIOR);
            elemInfoArr.Add(6);     // 6 faces of the bounding box

            List<double> arrCoord = new List<double>();

            for (int i = 0; i < 30; i+=5)
            {
                elemInfoArr.Add(i*3 +1);     // ElemInfoArray counts the entry of double value and not the vertex
                elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_EXTERIOR);
                elemInfoArr.Add(1);

                for (int j = i; j < i + 5; ++j)
                {
                    arrCoord.Add(expVertList[j].X);
                    arrCoord.Add(expVertList[j].Y);
                    arrCoord.Add(expVertList[j].Z);
                }
            }

            geom.ElemArrayOfInts = elemInfoArr.ToArray();
            geom.OrdinatesArrayOfDoubles = arrCoord.ToArray();
            return geom;
        }

        public void trueOBBFaces()
        {
            if (OBB != null)
            {
                Polyhedron obbGeom;
                if (SDOGeomUtils.generate_Polyhedron(OBB, out obbGeom))
                {
                    BIMRLGeometryPostProcess processFaces = new BIMRLGeometryPostProcess(_elementid, obbGeom, _refBIMRLCommon, _currFedID, "OBB");
                    processFaces.simplifyAndMergeFaces();
                    processFaces.insertIntoDB(false);
                }
            }
        }

        //public List<Point3D> projectedPointList()
        //{
        //    PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis(_geom);
        //    Vector3D[] majorAxes = pca.identifyMajorAxes();
        //    Point3D centroid = pca.Centroid;
        //    // Modify Axes to be aligned to XY plane, the longest X will be first axis (X), the longest Y will be Y
        //    Vector3D[] modAxes = new Vector3D[3];
        //    // set as default first to be the same

        //    if (Math.Abs(majorAxes[0].X) > Math.Abs(majorAxes[1].X) && Math.Abs(majorAxes[0].X) > Math.Abs(majorAxes[2].X))
        //    {
        //        modAxes[0] = majorAxes[0];
        //        if (Math.Abs(majorAxes[1].Y) > Math.Abs(majorAxes[2].Y))
        //        {
        //            modAxes[1] = majorAxes[1];
        //            modAxes[2] = majorAxes[2];
        //        }
        //        else
        //        {
        //            modAxes[1] = majorAxes[2];
        //            modAxes[2] = majorAxes[1];
        //        }
        //    }
        //    else if (Math.Abs(majorAxes[1].X) > Math.Abs(majorAxes[0].X) && Math.Abs(majorAxes[1].X) > Math.Abs(majorAxes[2].X))
        //    {
        //        modAxes[0] = majorAxes[1];
        //        if (Math.Abs(majorAxes[0].Y) > Math.Abs(majorAxes[2].Y))
        //        {
        //            modAxes[1] = majorAxes[0];
        //            modAxes[2] = majorAxes[2];
        //        }
        //        else
        //        {
        //            modAxes[1] = majorAxes[2];
        //            modAxes[2] = majorAxes[0];
        //        }
        //    }
        //    else if (Math.Abs(majorAxes[2].X) > Math.Abs(majorAxes[0].X) && Math.Abs(majorAxes[2].X) > Math.Abs(majorAxes[1].X))
        //    {
        //        modAxes[0] = majorAxes[2];
        //        if (Math.Abs(majorAxes[1].Y) > Math.Abs(majorAxes[0].Y))
        //        {
        //            modAxes[1] = majorAxes[1];
        //            modAxes[2] = majorAxes[0];
        //        }
        //        else
        //        {
        //            modAxes[1] = majorAxes[0];
        //            modAxes[2] = majorAxes[1];
        //        }
        //    }
        //    else
        //    {
        //        modAxes[0] = majorAxes[0];
        //        if (Math.Abs(majorAxes[1].Y) > Math.Abs(majorAxes[2].Y))
        //        {
        //            modAxes[1] = majorAxes[1];
        //            modAxes[2] = majorAxes[2];
        //        }
        //        else
        //        {
        //            modAxes[1] = majorAxes[2];
        //            modAxes[2] = majorAxes[1];
        //        }
        //    }

        //    // Force axes 1 and 2 (X and Y) to be on the XY plane
        //    modAxes[0].Z = 0;
        //    modAxes[1].Z = 0;
        //    // the third Axis will be +Z
        //    modAxes[2].X = 0;
        //    modAxes[2].Y = 0;
        //    if (modAxes[2].Z == 0)
        //        modAxes[2].Z = 1;

        //    pca.transformMatrix = new Matrix3D(modAxes[0].X, modAxes[1].X, modAxes[2].X, 0,
        //                        modAxes[0].Y, modAxes[1].Y, modAxes[2].Y, 0,
        //                        modAxes[0].Z, modAxes[1].Z, modAxes[2].Z, 0,
        //                        -centroid.X, -centroid.Y, -centroid.Z, 1);
        //    List<Point3D> transfPoints = pca.transformPointSet();
        //    BoundingBox3D trOBB = new BoundingBox3D(transfPoints);
        //    List<Point3D> modOBB = pca.transformBackPointSet(trOBB.BBVertices);
        //    return modOBB;
        //}

        public void projectedFaces()
        {
            PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis(_geom);
            List<Point3D> transfPoints = pca.projectedPointList();
            BoundingBox3D trOBB = new BoundingBox3D(transfPoints);
            List<Point3D> modOBB = pca.transformBackPointSet(trOBB.BBVertices);
            SdoGeometry sdomOBB = createSDOGeomOBB(modOBB);
            Polyhedron modOBBPolyH;
            SDOGeomUtils.generate_Polyhedron(sdomOBB, out modOBBPolyH);
            BIMRLGeometryPostProcess processFaces = new BIMRLGeometryPostProcess(_elementid, modOBBPolyH, _refBIMRLCommon, _currFedID, "PROJOBB");
            processFaces.simplifyAndMergeFaces();
            processFaces.insertIntoDB(false);
        }
    }
}
