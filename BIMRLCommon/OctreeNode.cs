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
using System.Threading;
using System.Threading.Tasks;


namespace BIMRL.Common
{
    public class OctreeNode
    {
        /// 
        /// The number of children in an octree.
        /// 
        public PolyhedronIntersectEnum _flag = PolyhedronIntersectEnum.Intersect;

        /// The octree's depth and max depth (as the stop criterion for subdivision)
        /// 
        public int _depth = 0;

        /// 
        /// The octree's child nodes.
        /// 
        public List<OctreeNode> _children;

        public OctreeNode _parent = null;

        public CellID64 nodeCellID { get; set; }

        public Cuboid nodeCellCuboid
        {
            get 
            {
                Point3D loc = CellID64.getCellIdxLoc(nodeCellID);
                Vector3D cellSize = CellID64.cellSize(CellID64.getLevel(nodeCellID));
                return new Cuboid(loc, cellSize.X, cellSize.Y, cellSize.Z);
            }
        }

        /// Creates a new octree.
        ///   
        public OctreeNode()
        {
            nodeCellID = new CellID64((UInt64)0);  // root/level-0 tile
            _children = new List<OctreeNode>();
            _flag = PolyhedronIntersectEnum.FullyContains;
        }

        /// 
        /// Creates a new octree.
        /// 
        public OctreeNode(OctreeNode parentID, int depth, int leafNo)
        {
            this._depth = depth;
            this._parent = parentID;
            nodeCellID = CellID64.newChildCellId(parentID.nodeCellID, leafNo);
            _children = new List<OctreeNode>();
            _flag = PolyhedronIntersectEnum.Intersect;
        }

    }

    class OctreeNodeProcess
    {
#if (DBG_OCTREE)
        public static int _dbgDepth = 6;
#endif
        /// Splits the octree into eight children.
        /// 
        public static void Split(OctreeNode node)
        {
            node._children.Add(new OctreeNode(node, (node._depth + 1), 0));
            node._children.Add(new OctreeNode(node, (node._depth + 1), 1));
            node._children.Add(new OctreeNode(node, (node._depth + 1), 2));
            node._children.Add(new OctreeNode(node, (node._depth + 1), 3));
            node._children.Add(new OctreeNode(node, (node._depth + 1), 4));
            node._children.Add(new OctreeNode(node, (node._depth + 1), 5));
            node._children.Add(new OctreeNode(node, (node._depth + 1), 6));
            node._children.Add(new OctreeNode(node, (node._depth + 1), 7));
        }

        public static CellID64 getSmallestContainingCell(Polyhedron polyH)
        {
            return getSmallestContainingCell(polyH.boundingBox);
        }

        public static CellID64 getSmallestContainingCell(Face3D face)
        {
            return getSmallestContainingCell(face.boundingBox);
        }

        public static CellID64 getSmallestContainingCell(LineSegment3D line)
        {
            CellID64 llbSmallestCell = CellID64.cellAtMaxDepth(line.startPoint);
            CellID64 urtSmallestCell = CellID64.cellAtMaxDepth(line.endPoint);
            CellID64 theCell = getCombinedSmallestContainingCell(llbSmallestCell, urtSmallestCell);

            return theCell;
        }

        public static CellID64 getSmallestContainingCell(BoundingBox3D BBox)
        {
            // 1st step. Get the minimum Octree Cell containing the polyH bounding box
            
            CellID64 llbSmallestCell = CellID64.cellAtMaxDepth(BBox.LLB);
            CellID64 urtSmallestCell = CellID64.cellAtMaxDepth(BBox.URT);
            CellID64 theCell = getCombinedSmallestContainingCell(llbSmallestCell, urtSmallestCell);

            return theCell;
        }

        static CellID64 getCombinedSmallestContainingCell(params CellID64[] inputCells)
        {
            UInt64 cellidMask = 0xFFFFFFFFFFFFFF80;
            int startLevel = 0;
            int level = 0;

            UInt64[] cellCode = new UInt64[inputCells.Count()];
            int inputCnt=0;
            foreach(CellID64 inp in inputCells)
            {
                if (startLevel < inp.Level)
                    startLevel = inp.Level;          // get the largest level
                cellCode[inputCnt] = inp.iCellID & cellidMask;     // remove the level
                inputCnt++;
            }

            bool same = false;
            UInt64 shiftMask = 0xffffffffffffffff << ((19 - startLevel) * 3 + 7);
            for (int i = startLevel; i >= 0; --i)
            {
                for (int ccount = 1; ccount < inputCells.Count(); ++ccount)
                {
                    cellCode[0] = cellCode[0] & shiftMask;
                    cellCode[ccount] = cellCode[ccount] & shiftMask;
                    shiftMask = shiftMask << 3;
                    if (cellCode[0] == cellCode[ccount])
                        same = true;
                    else
                        break;
                }
                if (same)
                {
                    level = i; 
                    break;
                }
            }
            UInt64 iSmallestCell = cellCode[0] | (uint) level<<1;
            CellID64 theSmallestContainingCell = new CellID64(iSmallestCell);
            return theSmallestContainingCell;
        }

        static bool isNodeOutsideBB (OctreeNode node, BoundingBox3D bBox)
        {
            Point3D nodeLLB = node.nodeCellCuboid.cuboidPolyhedron.boundingBox.LLB;
            Point3D nodeURT = node.nodeCellCuboid.cuboidPolyhedron.boundingBox.URT;
            if ((nodeLLB.X > bBox.URT.X || nodeURT.X < bBox.LLB.X)
                || nodeLLB.Y > bBox.URT.Y || nodeURT.Y < bBox.LLB.Y
                || nodeLLB.Z > bBox.URT.Z || nodeURT.Z < bBox.LLB.Z)
                return true;

            return false;
        }

        /// <summary>
        /// Peform very simple Disjoint filter by AABB against the node (that is also AABB)
        /// </summary>
        /// <param name="node"></param>
        /// <param name="boundingBox"></param>
        public static void ProcessBB (OctreeNode node, BoundingBox3D boundingBox)
        {
            // 2nd step. Subdivide now but only use AABB first. The result of the collections will be further divide using the actual polyhedron
            if (node._depth < Octree.MaxDepth)
            {
                int disjointCount = 0;

                Split(node);
                List<int> childToRemove = new List<int>();
                List<int> childToTraverse = new List<int>();

                for (int i = 0; i < node._children.Count; i++)
                {
                    OctreeNode childNode = node._children[i];
                    if (isNodeOutsideBB(childNode, boundingBox))
                    {
                        childNode._flag = PolyhedronIntersectEnum.Disjoint;
                        disjointCount++;
                        continue;
                    }
                    else
                    {
                        childNode._flag = PolyhedronIntersectEnum.IntersectOrInside;
                        childToTraverse.Add(i);
                    }
                }

                if (disjointCount == 8)
                {
                    // All children are disjoint. Remove all children and set the node to Disjoint
                    node._children.Clear();
                    node._flag = PolyhedronIntersectEnum.Disjoint;
                    return;
                }

                return;

                // We will do only one time split from the result of the fisrt step (min encl cell) because even BBox operation takes a lot of time during recursion

                //if (childToTraverse.Count == 1)
                //    OctreeNodeProcess.ProcessBB(node._children[childToTraverse[0]], boundingBox);
                //else if (childToTraverse.Count > 1)
                //{
                //    ParallelOptions po = new ParallelOptions();
                //    po.MaxDegreeOfParallelism = 8;

                //    Parallel.ForEach(childToTraverse, po, i => OctreeNodeProcess.ProcessBB(node._children[i], boundingBox));

                //    // Non-parallel option for easier debugging
                //    //foreach (int i in childToTraverse)
                //    //{
                //    //    OctreeNodeProcess.ProcessBB(node._children[i], boundingBox);
                //    //}
                //}

                //// If there is any disjoint, we need to keep this node as it is. This should be done after we processed all the children to be traversed!!
                //if (disjointCount > 0 && disjointCount < 8)
                //    return;

                //int countGrandChildren = 0;
                //// If there is no disjoint, we need to check whether all children are terminal (i.e. child._children.Count == 0)
                //foreach (OctreeNode child in node._children)
                //    countGrandChildren += child._children.Count;

                //// All children are terminal and no disjoint (by implication of previous steps). Remove children
                //if (countGrandChildren == 0)
                //{
                //    node._children.Clear();
                //    node._flag = PolyhedronIntersectEnum.IntersectOrInside;
                //    return;
                //}
            }
            else
            {
                // at _depth == Octree.MaxDepth there is nothing else to do since the test has been done at the parent level and when entering this stage, the test has determined
                // that the cell is intersected with the polyH
            }

            return;

        }

        public static void Process(OctreeNode node, Polyhedron polyH)
        {
            // Do octree traversal here (from the leaves that are the result of the previous step
            if (node._children.Count() == 0)        //leaf node
            {
                if (node._flag != PolyhedronIntersectEnum.Disjoint)
                    Process(node, polyH, polyH.Faces);      // Process the leaf node if it is not Disjoint
            }
            else
            {
                foreach (OctreeNode chldNode in node._children)     // recursive to get all the leaf nodes
                    Process(chldNode, polyH);
            }
        }

        public static void Process(OctreeNode node, Polyhedron _polyH, List<Face3D> polyHF)
        {
            // 3rd step. Subdivide the cells collected by the step 2 and operate on them with the actual polyhedron to get the detail

            if (node._depth < Octree.MaxDepth)
            {
                int disjointCount = 0;
                int insideCount = 0;

                Split(node);
                List<int> childToRemove = new List<int>();
                List<int> childToTraverse = new List<int>();

                List<Face3D> faceList;
                faceList = Face3D.exclFacesOutsideOfBound(polyHF, node.nodeCellCuboid.cuboidPolyhedron.boundingBox, 0x111);

                if (faceList.Count == 0)
                {
                    // No face inside this cuboid left, no intersection nor completely enclosing the polyH.
                    node._flag = PolyhedronIntersectEnum.Disjoint;
                    node._children.Clear();
                    return;
                }

                for (int i = 0; i < node._children.Count; i++)
                {
                    OctreeNode childNode = node._children[i];
                    //PolyhedronIntersectEnum intS = childNode.Process(polyH);
                    if (Polyhedron.intersect(childNode.nodeCellCuboid.cuboidPolyhedron, faceList))
                    {
                        childToTraverse.Add(i);
                        childNode._flag = PolyhedronIntersectEnum.Intersect;
                        childNode.nodeCellID.setBorderCell();
#if (DBG_OCTREE)
                        if (childNode._depth >= _dbgDepth)
                        {
                            BIMRLCommon refCommon = new BIMRLCommon();
                            string dbgFile = "c:\\temp\\octree\\" + childNode.nodeCellID.ToString() + " - intersect polyH.x3d";
                            BIMRLExportSDOToX3D x3d = new BIMRLExportSDOToX3D(refCommon, dbgFile);
                            x3d.drawCellInX3d(childNode.nodeCellID.ToString());     // draw the cell
                            x3d.exportFacesToX3D(faceList);
                            x3d.endExportToX3D();
                        }
#endif
                        continue;
                    }

                    // If doesn't intersect (passes the check above), either it is fully contained, full contains or disjoint
                    // To optimize the operation, we will use a single sampling point instead of checking the entire polyhedron since a single point can tell if a polyhedron is inside the other one
                    //if (Polyhedron.inside(childNode.nodeCellCuboid.cuboidPolyhedron, polyH))

                    //// No need to check this since the previous step (no 1) would have removed the fullycontaining cells
 
                    // Fully contains check only valid if the parent is fully contains, if intersect, it should never be full contains
                    //if (node._flag == PolyhedronIntersectEnum.FullyContains)
                    //{
                    //    if (Polyhedron.insideCuboid(childNode.nodeCellCuboid.cuboidPolyhedron, faceList[0].vertices[0]))
                    //    {
                    //        // if polyH is entirely inside the cuboid, we will set this for further split (the same as intersection
                    //        childToTraverse.Add(i);       // We will remove the node if it is disjoint, otherwise it will continue splitting until the condition met
                    //        childNode._flag = PolyhedronIntersectEnum.FullyContains;
                    //        childNode.nodeCellID.setBorderCell();
                    //        continue;
                    //    }
                    //}

                    //if (Polyhedron.inside(polyH, childNode.nodeCellCuboid.cuboidPolyhedron))
                    if (Polyhedron.inside(_polyH, childNode.nodeCellCuboid.cuboidPolyhedron.Vertices[3]))
                    {
                        childNode._flag = PolyhedronIntersectEnum.Inside;
                        insideCount++;
#if (DBG_OCTREE)
                        if (childNode._depth >= _dbgDepth)
                        {
                            BIMRLCommon refCommon = new BIMRLCommon();
                            string dbgFile = "c:\\temp\\octree\\" + childNode.nodeCellID.ToString() + " - inside polyH.x3d";
                            BIMRLExportSDOToX3D x3d = new BIMRLExportSDOToX3D(refCommon, dbgFile);
                            x3d.drawCellInX3d(childNode.nodeCellID.ToString());     // draw the cell
                            x3d.exportFacesToX3D(_polyH.Faces);
                            x3d.endExportToX3D();
                        }
#endif
                        continue;
                    }

                    // If the 2 polyH do not intersect, the cuboid does not fully contain the polyH, nor the cuboid is fully inside the polyH, it must be disjoint
                    childNode._flag = PolyhedronIntersectEnum.Disjoint;
                    disjointCount++;
#if (DBG_OCTREE)
                    if (childNode._depth >= _dbgDepth)
                    {
                        BIMRLCommon refCommon = new BIMRLCommon();
                        string dbgFile = "c:\\temp\\octree\\" + childNode.nodeCellID.ToString() + " - disjoint polyH.x3d";
                        BIMRLExportSDOToX3D x3d = new BIMRLExportSDOToX3D(refCommon, dbgFile);
                        x3d.drawCellInX3d(childNode.nodeCellID.ToString());     // draw the cell
                        x3d.exportFacesToX3D(_polyH.Faces);
                        x3d.endExportToX3D();
                    }
#endif
                    continue;

                    // else: the cuboid is completely inside the polyH, keep
                }

                if (disjointCount == 8)
                {
                    // All children are disjoint. Remove all children and set the node to Disjoint
                    node._children.Clear();
                    node._flag = PolyhedronIntersectEnum.Disjoint;
                    return;
                }

                if (insideCount == 8)
                {
                    // All children are inside. Remove all children and set the node to Inside
                    node._children.Clear();
                    node._flag = PolyhedronIntersectEnum.Inside;
                    return;
                }


                if (childToTraverse.Count == 1)
                    OctreeNodeProcess.Process(node._children[childToTraverse[0]], _polyH, faceList);
                else if (childToTraverse.Count > 1)
                {
#if (DEBUG_NOPARALLEL)
                    // Non - parallel option for easier debugging
                    foreach (int i in childToTraverse)
                        {
                            OctreeNodeProcess.Process(node._children[i], _polyH, faceList);
                        }
#else
                    ParallelOptions po = new ParallelOptions();
                    po.MaxDegreeOfParallelism = 8;

                    Parallel.ForEach(childToTraverse, po, i => OctreeNodeProcess.Process(node._children[i], _polyH, faceList));
#endif
                }
                // If there is any disjoint, we need to keep this node as it is. This should be done after we processed all the children to be traversed!!
                if (disjointCount > 0 && disjointCount < 8)
                    return;

                int countGrandChildren = 0;
                // If there is no disjoint, we need to check whether all children are terminal (i.e. child._children.Count == 0)
                foreach (OctreeNode child in node._children)
                    countGrandChildren += child._children.Count;

                // All children are terminal and no disjoint (by implication of previous steps). Remove children
                if (countGrandChildren == 0)
                {
                    node._children.Clear();
                    node._flag = PolyhedronIntersectEnum.IntersectOrInside;
                    return;
                }
            }
            else
            {
                // at _depth == Octree.MaxDepth there is nothing else to do since the test has been done at the parent level and when entering this stage, the test has determined
                // that the cell is intersected with the polyH
            }

            return;
        }

        /// <summary>
        /// Process Octree for a face
        /// </summary>
        /// <param name="_polyH"></param>
        /// <param name="polyHF"></param>
        public static void Process(OctreeNode node, Face3D face)
        {
            if (node._depth < Octree.MaxDepth)
            {
                int disjointCount = 0;

                OctreeNodeProcess.Split(node);
                List<int> childToRemove = new List<int>();
                List<int> childToTraverse = new List<int>();

                for (int i = 0; i < node._children.Count; i++)
                {
                    OctreeNode childNode = node._children[i];
                    if (Polyhedron.intersect(childNode.nodeCellCuboid.cuboidPolyhedron, face))
                    {
                        childToTraverse.Add(i);
                        childNode._flag = PolyhedronIntersectEnum.Intersect;
                        childNode.nodeCellID.setBorderCell();
                        continue;
                    }

                    // If doesn't intersect (passes the check above), either it is fully contained, full contains or disjoint
                    // To optimize the operation, we will use a single sampling point instead of checking the entire polyhedron since a single point can tell if a polyhedron is inside the other one

                    // Fully contains check only valid if the parent is fully contains, if intersect, it should never be full contains
                    if (node._flag == PolyhedronIntersectEnum.FullyContains)
                    {
                        if (Polyhedron.insideCuboid(childNode.nodeCellCuboid.cuboidPolyhedron, face.vertices[2]))
                        {
                            // if polyH is entirely inside the cuboid, we will set this for further split (the same as intersection
                            childToTraverse.Add(i);       // We will remove the node if it is disjoint, otherwise it will continue splitting until the condition met
                            childNode._flag = PolyhedronIntersectEnum.FullyContains;
                            childNode.nodeCellID.setBorderCell();
                            continue;
                        }
                    }

                    // If the face does not intersect the cuboid, or the cuboid does not fully contain the polyH, it must be disjoint
                    childNode._flag = PolyhedronIntersectEnum.Disjoint;
                    disjointCount++;
                    continue;
                }

                if (disjointCount == 8)
                {
                    // All children are disjoint. Remove all children and set the node to Disjoint
                    node._children.Clear();
                    node._flag = PolyhedronIntersectEnum.Disjoint;
                    return;
                }

                if (childToTraverse.Count == 1)
                    OctreeNodeProcess.Process(node._children[childToTraverse[0]], face);
                else if (childToTraverse.Count > 1)
                    Parallel.ForEach(childToTraverse, i => OctreeNodeProcess.Process(node._children[i], face));

                // If there is any disjoint, we need to keep this node as it is. This should be done after we processed all the children to be traversed!!
                if (disjointCount > 0 && disjointCount < 8)
                    return;

                int countGrandChildren = 0;
                // If there is no disjoint, we need to check whether all children are terminal (i.e. child._children.Count == 0)
                foreach (OctreeNode child in node._children)
                    countGrandChildren += child._children.Count;

                // All children are terminal and no disjoint (by implication of previous steps). Remove children
                if (countGrandChildren == 0)
                {
                    node._children.Clear();
                    node._flag = PolyhedronIntersectEnum.IntersectOrInside;
                    return;
                }
            }
            else
            {
                // at _depth == Octree.MaxDepth there is nothing else to do since the test has been done at the parent level and when entering this stage, the test has determined
                // that the cell is intersected with the polyH
            }

            return;
        }

        /// <summary>
        /// Process Octree for a line segment
        /// </summary>
        /// <param name="_polyH"></param>
        /// <param name="polyHF"></param>
        public static void Process(OctreeNode node, LineSegment3D lineSegment)
        {
            if (node._depth < Octree.MaxDepth)
            {
                int disjointCount = 0;

                OctreeNodeProcess.Split(node);
                List<int> childToRemove = new List<int>();
                List<int> childToTraverse = new List<int>();

                for (int i = 0; i < node._children.Count; i++)
                {
                    OctreeNode childNode = node._children[i];
                    if (Polyhedron.intersect(childNode.nodeCellCuboid.cuboidPolyhedron, lineSegment))
                    {
                        childToTraverse.Add(i);
                        childNode._flag = PolyhedronIntersectEnum.Intersect;
                        childNode.nodeCellID.setBorderCell();
                        continue;
                    }

                    // If doesn't intersect (passes the check above), either it is fully contained, full contains or disjoint
                    // To optimize the operation, we will use a single sampling point instead of checking the entire polyhedron since a single point can tell if a polyhedron is inside the other one

                    // Fully contains check only valid if the parent is fully contains, if intersect, it should never be full contains
                    if (node._flag == PolyhedronIntersectEnum.FullyContains)
                    {
                        if (Polyhedron.insideCuboid(childNode.nodeCellCuboid.cuboidPolyhedron, lineSegment.startPoint))
                        {
                            // if polyH is entirely inside the cuboid, we will set this for further split (the same as intersection
                            childToTraverse.Add(i);       // We will remove the node if it is disjoint, otherwise it will continue splitting until the condition met
                            childNode._flag = PolyhedronIntersectEnum.FullyContains;
                            childNode.nodeCellID.setBorderCell();
                            continue;
                        }
                    }

                    // If the Line does not intersect the cuboid, or the cuboid does not fully contain the Line, it must be disjoint
                    childNode._flag = PolyhedronIntersectEnum.Disjoint;
                    disjointCount++;
                    continue;
                }

                if (disjointCount == 8)
                {
                    // All children are disjoint. Remove all children and set the node to Disjoint
                    node._children.Clear();
                    node._flag = PolyhedronIntersectEnum.Disjoint;
                    return;
                }

                if (childToTraverse.Count == 1)
                    OctreeNodeProcess.Process(node._children[childToTraverse[0]], lineSegment);
                else if (childToTraverse.Count > 1)
                    Parallel.ForEach(childToTraverse, i => OctreeNodeProcess.Process(node._children[i], lineSegment));

                // If there is any disjoint, we need to keep this node as it is. This should be done after we processed all the children to be traversed!!
                if (disjointCount > 0 && disjointCount < 8)
                    return;

                int countGrandChildren = 0;
                // If there is no disjoint, we need to check whether all children are terminal (i.e. child._children.Count == 0)
                foreach (OctreeNode child in node._children)
                    countGrandChildren += child._children.Count;

                // All children are terminal and no disjoint (by implication of previous steps). Remove children
                if (countGrandChildren == 0)
                {
                    node._children.Clear();
                    node._flag = PolyhedronIntersectEnum.IntersectOrInside;
                    return;
                }
            }
            else
            {
                // at _depth == Octree.MaxDepth there is nothing else to do since the test has been done at the parent level and when entering this stage, the test has determined
                // that the cell is intersected with the polyH
            }

            return;
        }

        public static bool collectCellIDs(OctreeNode node, out List<CellID64> collCellIDs, out List<int> collBorderFlag)
        {
            collCellIDs = new List<CellID64>();
            collBorderFlag = new List<int>();
            if (node._children.Count == 0)
            {
                // No child in this node, return its cellID
                if (node._flag != PolyhedronIntersectEnum.Disjoint)
                {
                    collCellIDs.Add(node.nodeCellID);
                    collBorderFlag.Add(node._flag == PolyhedronIntersectEnum.Intersect ? 1 : 0);
                }
            }
            else
            {
                foreach (OctreeNode child in node._children)
                {
                    List<CellID64> coll = null;
                    List<int> collFlag = null;
                    if (child._flag != PolyhedronIntersectEnum.Disjoint)
                    {
                        OctreeNodeProcess.collectCellIDs(child, out coll, out collFlag);
                        if (coll.Count > 0)
                        {
                            collCellIDs.AddRange(coll);
                            collBorderFlag.AddRange(collFlag);
                        }
                    }
                }
            }
            return true;
        }

        public static void collecLines (OctreeNode node, out List<Point3D> vertList, out List<int> lineIndex)
        {
            vertList = new List<Point3D>();
            lineIndex = new List<int>();

            if (node._children.Count == 0)
            {
                vertList.AddRange(node.nodeCellCuboid.cuboidPolyhedron.Vertices);
                int[] lines = { 0, 1, 2, 3, 0, 4, 5, 6, 7, 4, 5, 1, 2, 6, 7, 3 };
                lineIndex.AddRange(lines);
            }
            else
            {
                foreach (OctreeNode child in node._children)
                {
                    List<Point3D> verts;
                    List<int> idx;
                    OctreeNodeProcess.collecLines(child, out verts, out idx);
                    if (verts.Count > 0)
                    {
                        int offset = vertList.Count;
                        vertList.AddRange(verts);
                        for(int i = 0; i < idx.Count; i++)
                            idx[i] += offset;                   // offset the indexes based ont the current count of the list
                        lineIndex.AddRange(idx);
                    }
                }
            }
        }
    }
}
