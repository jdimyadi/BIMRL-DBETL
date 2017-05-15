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
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BIMRL.OctreeLib
{
    public class Face3DLW
    {
        public List<Point3DLW> vertices { get; set; }
        public Face3DLW(Face3D f3d)
        {
            vertices = new List<Point3DLW>();
            foreach (Point3D p in f3d.vertices)
                vertices.Add(new Point3DLW(p));
        }
    }

    public class Face3D
    {
        private Plane3D _basePlane;
        private List<LineSegment3D> _boundaryLines = new List<LineSegment3D>();
        private List<List<LineSegment3D>> _innerBoundaries = new List<List<LineSegment3D>>();
        private List<Point3D> _vertices = new List<Point3D>();
        private List<List<Point3D>> _innerVertices = new List<List<Point3D>>();
        private List<int> _nonColinearEdgesIdx = new List<int>();
        private BoundingBox3D containingBB;

        public Face3D (List<Point3D> vertices)
        {
            _vertices = vertices;
            //for (int i = 0; i < vertices.Count; i++)
            //{
            //    LineSegment3D edge;
            //    if (i == vertices.Count - 1)
            //        edge = new LineSegment3D(vertices[i], vertices[0]);
            //    else
            //        edge = new LineSegment3D(vertices[i], vertices[i + 1]);
            //    _boundaryLines.Add(edge);
            //    if (i > 0 && i < vertices.Count) 
            //    {
            //        if (_boundaryLines[i].baseLine.direction != _boundaryLines[i-1].baseLine.direction)
            //            _nonColinearEdgesIdx.Add(i-1);
            //    }
            //    if (i == vertices.Count -1 )
            //        if (_boundaryLines[i].baseLine.direction != _boundaryLines[0].baseLine.direction)
            //            _nonColinearEdgesIdx.Add(i);
            //}
            generateEdges(vertices, out _boundaryLines, out _nonColinearEdgesIdx);

            //_basePlane = new Plane3D(_vertices[0], _boundaryLines[_nonColinearEdgesIdx[0]].baseLine.direction, _boundaryLines[_nonColinearEdgesIdx[1]].baseLine.direction);
            // Use Newell's method to calculate normal because any vertices > 3 can be concave
            Vector3D faceNormal = normalByNewellMethod(_vertices);
            _basePlane = new Plane3D(_vertices[0], faceNormal);

            containingBB = new BoundingBox3D(_vertices);
        }

        /// <summary>
        /// This is to support initialization of Face3D with holes. The list contains multiple lists, the first one should be the outer list and the rest are the inner lists
        /// Currently support of holes is still very scant, just be able to capture the data. Everything else (operations) are only working for the outer loop without consideration of the holes
        /// </summary>
        /// <param name="vertices"></param>
        public Face3D(List<List<Point3D>> vertices)
        {
            // Initialize the outer boundary information
            _vertices = vertices[0];
            generateEdges(vertices[0], out _boundaryLines, out _nonColinearEdgesIdx);

            //_basePlane = new Plane3D(_vertices[0], _boundaryLines[_nonColinearEdgesIdx[0]].baseLine.direction, _boundaryLines[_nonColinearEdgesIdx[1]].baseLine.direction);

            // Use Newell's method to calculate normal because any vertices > 3 can be concave
            Vector3D faceNormal = normalByNewellMethod(_vertices);
            _basePlane = new Plane3D(_vertices[0], faceNormal);

            containingBB = new BoundingBox3D(_vertices);

            // Now add the inner boundary informations
            for (int i = 1; i < vertices.Count; i++)
            {
                List<Point3D> innerLoop = new List<Point3D>();
                List<LineSegment3D> innerBound = new List<LineSegment3D>();
                List<int> nonColinearBoundIdx = new List<int>();

                _innerVertices.Add(vertices[i]);
                generateEdges(vertices[i], out innerBound, out nonColinearBoundIdx);
                _innerBoundaries.Add(innerBound);
            }
        }

        static void generateEdges(List<Point3D> vertices, out List<LineSegment3D> edgeLIst, out List<int> nonColinearEdgesIdx)
        {
            edgeLIst = new List<LineSegment3D>();
            nonColinearEdgesIdx = new List<int>();
            for (int i = 0; i < vertices.Count; i++)
            {
                LineSegment3D edge = null;
                if (i == vertices.Count - 1)
                {
                    if (vertices[i] != vertices[0])          // add only the last edge with the first vertex if they are not explicitly closed
                        edge = new LineSegment3D(vertices[i], vertices[0]);
                }
                else
                    edge = new LineSegment3D(vertices[i], vertices[i + 1]);
                if (edge != null)
                {
                    edgeLIst.Add(edge);
                    if (i > 0 && i < vertices.Count)
                    {
                        if (edgeLIst[i].baseLine.direction != edgeLIst[i - 1].baseLine.direction)
                            nonColinearEdgesIdx.Add(i - 1);
                    }
                    if (i == vertices.Count - 1)
                        if (edgeLIst[i].baseLine.direction != edgeLIst[0].baseLine.direction)
                            nonColinearEdgesIdx.Add(i);
                }
            }
        }

        public Plane3D basePlane
        {
            get { return _basePlane; }
        }

        public List<Point3D> vertices
        {
            get { return _vertices; }
        }

        public List<Point3D> outerAndInnerVertices
        {
            get
            {
                List<Point3D> tmp = new List<Point3D>();
                tmp.AddRange(_vertices);
                foreach (List<Point3D> verts in _innerVertices)
                    tmp.AddRange(verts);
                return tmp;
            }
        }

        public List<List<Point3D>> verticesWithHoles
        {
            get 
            {
                List<List<Point3D>> tmp = new List<List<Point3D>>(_innerVertices);
                tmp.Insert(0, _vertices);
                return tmp;
            }
        }

        public List<LineSegment3D> boundaries
        {
            get { return _boundaryLines; }
        }

        public List<LineSegment3D> outerAndInnerBoundaries
        {
            get
            {
                List<LineSegment3D> tmp = new List<LineSegment3D>();
                tmp.AddRange(_boundaryLines);
                foreach (List<LineSegment3D> innerBound in _innerBoundaries)
                    tmp.AddRange(innerBound);
                return tmp;
            }
        }

      Dictionary<LineSegment3D, int> _outerAndInnerBoundariesWithDict;
      public Dictionary<LineSegment3D, int> outerAndInnerBoundariesWithDict
      {
         get
         {
            if (_outerAndInnerBoundariesWithDict == null)
            {
               IEqualityComparer<LineSegment3D> segCompare = new SegmentCompare();
               _outerAndInnerBoundariesWithDict = new Dictionary<LineSegment3D, int>(segCompare);
               int count = 0;
               foreach (LineSegment3D lineS in _boundaryLines)
                  _outerAndInnerBoundariesWithDict.Add(lineS, count++);
               foreach (List<LineSegment3D> innerBound in _innerBoundaries)
                  foreach (LineSegment3D lineS in innerBound)
                     _outerAndInnerBoundariesWithDict.Add(lineS, count++);
            }
            return _outerAndInnerBoundariesWithDict;
         }
      }

        public List<List<LineSegment3D>> boundariesWithHoles
        {
            get
            {
                List<List<LineSegment3D>> tmp = new List<List<LineSegment3D>>(_innerBoundaries);
                tmp.Insert(0, _boundaryLines);
                return tmp;
            }
        }

        public BoundingBox3D boundingBox
        {
            get { return containingBB; }
        }

        public void Reverse()
        {
            // First reverse the vertex list, and then regenerate the rest
            _vertices.Reverse();

            _boundaryLines.Clear();
            _nonColinearEdgesIdx.Clear();
            
            for (int i = 0; i < vertices.Count; i++)
            {
                LineSegment3D edge;
                if (i == vertices.Count - 1)
                    edge = new LineSegment3D(vertices[i], vertices[0]);
                else
                    edge = new LineSegment3D(vertices[i], vertices[i + 1]);
                _boundaryLines.Add(edge);
                if (i > 0 && i < vertices.Count)
                {
                    if (_boundaryLines[i].baseLine.direction != _boundaryLines[i - 1].baseLine.direction)
                        _nonColinearEdgesIdx.Add(i - 1);
                }
                if (i == vertices.Count - 1)
                    if (_boundaryLines[i].baseLine.direction != _boundaryLines[0].baseLine.direction)
                        _nonColinearEdgesIdx.Add(i);
            }
            _basePlane = new Plane3D(vertices[0], _boundaryLines[_nonColinearEdgesIdx[0]].baseLine.direction, _boundaryLines[_nonColinearEdgesIdx[1]].baseLine.direction);
            containingBB = new BoundingBox3D(vertices);
        }

        /// <summary>
        /// Touch check between 2 faces. Currently only returns true/false. Should be improved with a new face that overlap between the 2 input faces
        /// </summary>
        /// <param name="F1">Face 1</param>
        /// <param name="F2">Face 2</param>
        /// <returns></returns>
        public static bool touch(Face3D F1, Face3D F2)
        {
            if (Vector3D.Parallels(F1._basePlane.normalVector, F2._basePlane.normalVector))
            {
                // test for any point inside another face
                for (int i = 0; i < F2._vertices.Count; i++)
                {
                    if (Face3D.inside(F1, F2._vertices[i]))
                        return true;
                }
                for (int i = 0; i < F1._vertices.Count; i++)
                {
                    if (Face3D.inside(F2, F1._vertices[i]))
                        return true;
                }
                // if still not returning true, test whether the edges intersect
                List<Point3D> intPoints = new List<Point3D>();
                for (int i = 0; i < F2.boundaries.Count; ++i)
                {
                    if (Face3D.intersect(F1, F2.boundaries[i], out intPoints))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Calculating intersection beween 2 Faces. The outcome of the intersection should be a LineSegment
        /// </summary>
        /// <param name="F1">First Face</param>
        /// <param name="F2">Second Face</param>
        /// <param name="intersectionLine">The resulting intersection line. Zero if no intersection</param>
        /// <param name="mode">The mode of the intersection: No intersection/parallel, partially intersect, no actual intersection/undefined</param>
        /// <returns></returns>
        public static bool intersect(Face3D F1, Face3D F2, out LineSegment3D intersectionLine, out FaceIntersectEnum mode)
        {
            intersectionLine = new LineSegment3D(new Point3D(0,0,0), new Point3D(0,0,0));
            mode = FaceIntersectEnum.Undefined;

            if (F1._basePlane.normalVector == F2._basePlane.normalVector)
            {
                // test points inside another face
                for (int i = 0; i < F2._vertices.Count; i++ )
                {
                    if (!Face3D.inside(F1, F2._vertices[i]))
                        continue;
                    mode = FaceIntersectEnum.Overlap;
                    return true;
                }
                mode = FaceIntersectEnum.NoIntersectionParallel;
                return false;
            }

            LineSegment3D ls1;
            LineSegment3D ls2;
            bool res1 = Plane3D.PPintersect(F1._basePlane, F2, out ls1);
            bool res2 = Plane3D.PPintersect(F2._basePlane, F1, out ls2);
            if (!res1 || !res2)
                return false;       // the faces do not intersect

            // Special case if the intersection occurs only at a single point
            if (ls1.startPoint == ls1.endPoint && ls2.startPoint == ls2.endPoint)
            {
                if (ls1.startPoint.Equals(ls2.startPoint))
                {
                    mode = FaceIntersectEnum.IntersectPartial;
                    return true;
                }
                return false;
            }
            else if (ls1.startPoint.Equals(ls1.endPoint))
            {
                // a single point intersection: ls1 is 0 length linesegnment = point
                if (LineSegment3D.isInSegment(ls2, ls1.startPoint))
                {
                    mode = FaceIntersectEnum.IntersectPartial;
                    return true;
                }
                return false;
            }
            else if (ls2.startPoint.Equals(ls2.endPoint))
            {
                // a single point intersection: ls1 is 0 length linesegnment = point
                if (LineSegment3D.isInSegment(ls1, ls2.startPoint))
                {
                    mode = FaceIntersectEnum.IntersectPartial;
                    return true;
                }
                return false;
            }

            LineSegment3D ovSegment;
            LineSegmentOverlapEnum ovstat = LineSegmentOverlapEnum.Undefined;
            bool lint = LineSegment3D.overlap(ls1, ls2, out ovSegment, out ovstat);
            if (lint)
            {
                intersectionLine = ovSegment;
                mode = FaceIntersectEnum.IntersectPartial;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Calculating an intersection between a Face and a LineSegment
        /// </summary>
        /// <param name="F1">the Face3D</param>
        /// <param name="LS">the Line Segment</param>
        /// <param name="intPoint">the intersection point</param>
        /// <returns></returns>
        public static bool intersect(Face3D F1, LineSegment3D LS, out List<Point3D> intPoints)
        {
            intPoints = new List<Point3D>();
            Point3D intPt = new Point3D();

            // There are 2 possible cases: 1. Line punching the face, 2. Line lies on the same plane as the face
            // Test whether the line is on the same plane
            if (MathUtils.equalTol(Vector3D.DotProduct(F1.basePlane.normalVector, LS.baseLine.direction), 0.0, MathUtils.defaultTol))
            {
                // test whether at least one point of the segment is on the plane
                if (!Plane3D.pointOnPlane(F1.basePlane, LS.startPoint))
                    return false;       // line is parallel with the plane, no intersection

                LineSegmentIntersectEnum mode = LineSegmentIntersectEnum.Undefined;
                for (int i=0; i<F1.boundaries.Count; i++)
                {
                    bool st = LineSegment3D.intersect(F1.boundaries[i], LS, out intPt, out mode);
                    if (st) intPoints.Add(intPt);
                }
                if (intPoints.Count > 0) return true;
                return false;
            }
            else
            {

                bool res = Plane3D.PLintersect(F1.basePlane, LS, out intPt);
                if (res == false) return false;             // intersection occurs beyond the line segment

                // There is intersection point, test whether the point in within (inside the boundary of the face boundaries
                res = inside(F1, intPt);
                if (res)
                {
                    intPoints.Add(intPt);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Test a point is inside a face
        /// </summary>
        /// <param name="F1"></param>
        /// <param name="P1"></param>
        /// <returns></returns>
        public static bool inside (Face3D F1, Point3D P1)
        {
            if (!Plane3D.pointOnPlane(F1.basePlane, P1)) return false;  // Point is not on a plane

            // test Point inside a Face
            // Need to project the plane to 2D (XY-, YZ-, or XZ- plane). It should work well with convex face (esp. triangles and rectangles we will deal with mostly)
            double maxDim = Double.MinValue;
            int DimttoZero = 0;

            // First test whether the plane is already axis-aligned (in this case we can just remove the appropriate axis)
            if (MathUtils.equalTol(F1.basePlane.normalVector.Y, 0.0, MathUtils.defaultTol) && MathUtils.equalTol(F1.basePlane.normalVector.Z, 0.0, MathUtils.defaultTol))
                DimttoZero = 0;     // Ignore X, project to Y-Z plane
            else if (MathUtils.equalTol(F1.basePlane.normalVector.X, 0.0, MathUtils.defaultTol) && MathUtils.equalTol(F1.basePlane.normalVector.Z, 0.0, MathUtils.defaultTol))
                DimttoZero = 1;     // Ignore Y, project to X-Z plane
            else if (MathUtils.equalTol(F1.basePlane.normalVector.X, 0.0, MathUtils.defaultTol) && MathUtils.equalTol(F1.basePlane.normalVector.Y, 0.0, MathUtils.defaultTol))
                DimttoZero = 2;     // Ignore Z, project to X-Y plane
            else
            {
                if (maxDim < Math.Abs(F1.basePlane.normalVector.X))
                {
                    maxDim = Math.Abs(F1.basePlane.normalVector.X);
                    DimttoZero = 0;
                }
                if (maxDim < Math.Abs(F1.basePlane.normalVector.Y))
                {
                    maxDim = Math.Abs(F1.basePlane.normalVector.Y);
                    DimttoZero = 1;
                }
                if (maxDim < Math.Abs(F1.basePlane.normalVector.Z))
                {
                    maxDim = Math.Abs(F1.basePlane.normalVector.Z);
                    DimttoZero = 2;
                }
            }

            // We ignore the largest component, which means the least impact to the projection plane
            List<Point3D> projVert = new List<Point3D>();
            Point3D projIntP = new Point3D(P1.X, P1.Y, P1.Z);
            Point3D rayEndP = new Point3D(P1.X, P1.Y, P1.Z);
            if (DimttoZero == 0)
            {
                for (int i=0; i<F1.vertices.Count; i++)
                {
                    projVert.Add(new Point3D(0.0, F1.vertices[i].Y, F1.vertices[i].Z));
                }
                projIntP.X = 0.0;
                rayEndP.X = 0.0;
                if (Octree.WorldBB == null)
                    rayEndP.Y += Point3D.distance(F1.containingBB.URT, F1.containingBB.LLB) * 1000;
                else
                    rayEndP.Y += Octree.WorldBB.extent * 2;
            }
            else if (DimttoZero == 1)
            {
                for (int i = 0; i < F1.vertices.Count; i++)
                {
                    projVert.Add(new Point3D(F1.vertices[i].X, 0.0, F1.vertices[i].Z));
                }
                projIntP.Y = 0.0;
                rayEndP.Y = 0.0;
                //rayEndP.Z += Point3D.distance(F1.containingBB.URT, F1.containingBB.LLB) * 2;
                if (Octree.WorldBB == null)
                    rayEndP.X += Point3D.distance(F1.containingBB.URT, F1.containingBB.LLB) * 2;    // Use X axis for the ray
                else
                    rayEndP.X += Octree.WorldBB.extent * 2;
            }
            else if (DimttoZero == 2)
            {
                for (int i = 0; i < F1.vertices.Count; i++)
                {
                    projVert.Add(new Point3D(F1.vertices[i].X, F1.vertices[i].Y, 0.0));
                }
                projIntP.Z = 0.0;
                rayEndP.Z = 0.0;
                if (Octree.WorldBB == null)
                    rayEndP.X += Point3D.distance(F1.containingBB.URT, F1.containingBB.LLB) * 2;
                else
                    rayEndP.X += Octree.WorldBB.extent * 2;
            }
            Face3D projFace = new Face3D(projVert);
            
            // define a ray from the intersection point along the X-axis of the projected plane by using long enough line segment beginning from the point, using 
            //    max extent of the face containingBB *2
            LineSegment3D ray = new LineSegment3D(projIntP, rayEndP);

            // Now do intersection between the ray and all the segments of the face. Odd number indicates the point is inside
            // 4 rules to follow:
            //    1. If the segment is upward, exclude the endpoint for intersection (only consider the startpoint)
            //    2. If the segment is downward, exclude the startpoint for intersection (only considers the end point)
            //    3. Ignore the segment that is horizontal (parallel to the ray)
            //    4. ray is always strictly to the right of the Point
            int intCount = 0;
            Point3D iP = new Point3D();
            LineSegmentIntersectEnum mod = LineSegmentIntersectEnum.Undefined;
            for (int i = 0; i < projFace.boundaries.Count; i++)
            {
                if (ray.baseLine.direction == projFace.boundaries[i].baseLine.direction)
                    continue;   //ignore segment that is parallel to the ray (rule #3)

                Point3D pointToExclude = new Point3D();
                if (DimttoZero == 0 || DimttoZero == 1)
                {
                    // for both X-Z and Y-Z plane (Z as vertical axis)
                    if (projFace.boundaries[i].startPoint.Z <= ray.startPoint.Z && projFace.boundaries[i].endPoint.Z > ray.startPoint.Z)
                        pointToExclude = projFace.boundaries[i].endPoint;       // Rule #1
                    else if (projFace.boundaries[i].startPoint.Z > ray.startPoint.Z && projFace.boundaries[i].endPoint.Z <= ray.startPoint.Z)
                        pointToExclude = projFace.boundaries[i].startPoint;     // Rule #2
                }
                else
                {
                    // for X-Y plane (Y as vertical axis)
                    if (projFace.boundaries[i].startPoint.Y <= ray.startPoint.Y && projFace.boundaries[i].endPoint.Y > ray.startPoint.Y)
                        pointToExclude = projFace.boundaries[i].endPoint;       // Rule #1
                    else if (projFace.boundaries[i].startPoint.Y > ray.startPoint.Y && projFace.boundaries[i].endPoint.Y <= ray.startPoint.Y)
                        pointToExclude = projFace.boundaries[i].startPoint;     // Rule #2
                }

                // In the evaluation of the number of intersection between a ray and a face, we will ignore the intersection point that is equal to the rule #2 or #3
                if (LineSegment3D.intersect(ray, projFace.boundaries[i], out iP, out mod) && (iP != pointToExclude))
                    intCount++;
            }
            if (intCount % 2 == 1) return true;
            return false;
        }

        /// <summary>
        /// Test a linesegment is completely inside a bounded face. For it to be true, it must fulfill:
        /// 1. Both endpoints are inside the face
        /// 2. the segment does not intersect the face
        /// </summary>
        /// <param name="F1">The Face</param>
        /// <param name="LS">The Line Segment</param>
        /// <returns>true=inside; otherwise false</returns>
        public static bool inside (Face3D F1, LineSegment3D LS)
        {
            // Line segment must completely lie on the face
            LineSegment3D lineS1 = new LineSegment3D(F1.basePlane.point, LS.startPoint);
            LineSegment3D lineS2 = new LineSegment3D(F1.basePlane.point, LS.endPoint);

            double d1 = Vector3D.DotProduct(F1.basePlane.normalVector, lineS1.baseLine.direction);
            double d2 = Vector3D.DotProduct(F1.basePlane.normalVector, lineS2.baseLine.direction);
            if (!MathUtils.equalTol(d1, 0.0, MathUtils.defaultTol) || !MathUtils.equalTol(d2, 0.0, MathUtils.defaultTol)) return false;     // The segment does not lie on the face/plane
 
            // To optimize the algorithm a little bit, we will change the way we test: 
            // First we do intersection test between the face and the linesegment
            // Second we test at least one point must be inside the face
            List<Point3D> iPoints = new List<Point3D>();
            bool ret = intersect(F1, LS, out iPoints);
            if (ret) return false;                  // The segment intersect the face

            // Test whether at least one point is inside the face
            ret = inside(F1, LS.startPoint);
            if (ret) return true;
            return false;
        }

        /// <summary>
        /// Test a face is completely inside another face. For it to be true, it must fulfill:
        /// 1. All the vertices are inside the other face
        /// 2. There is no intersection between the edges with the other face
        /// </summary>
        /// <param name="F1">The Face</param>
        /// <param name="F2">The other face to test whether it is inside the first face</param>
        /// <returns>true=inside, otherwise false</returns>
        public static bool inside (Face3D F1, Face3D F2)
        {
            // Do intersection first
            for (int i = 0; i < F1.boundaries.Count; i++)
            {
                for (int j = 0; j < F2.boundaries.Count; j++)
                {
                    Point3D iPoint = new Point3D();
                    LineSegmentIntersectEnum mode;
                    if (LineSegment3D.intersect(F1.boundaries[i], F2.boundaries[j], out iPoint, out mode)) return false;    // there is intersection
                }
            }

            // If there is no intersection, at least one vertex of F2 must be inside F1
            if (inside(F1, F2.vertices[0])) return true;    // one vertex of F2 is inside F1 and there is no intersection
            return false;
        }

        /// <summary>
        /// This function return reduced list of faces that is entirely on the LEFT of the specified Axis (only work on Axis parallel to X, or Y, or Z)
        /// The funcion is used for optimizing intersection test between the polyhedron and the Axis-align bounding box (e.g. Octree cell)
        /// </summary>
        /// <param name="axisLoc">The axis location. Must be either X only, or Y only, or Z only</param>
        /// <returns></returns>
        public static List<Face3D> inclFacesBeyondAxis(List<Face3D> faces, Plane3D axisPl)
        {
            List<Face3D> facesBeyond = new List<Face3D>();
            // Only deal with one axis set, either X, Y or Z, otherwise null List will be returned
            if ((axisPl.normalVector.Y == 0.0 && axisPl.normalVector.Z == 0.0) || (axisPl.normalVector.X == 0.0 && axisPl.normalVector.Y == 0.0) || (axisPl.normalVector.X == 0.0 && axisPl.normalVector.Z == 0.0))
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    for (int j = 0; j < faces[i].boundaries.Count; j++)
                    {
                        // Sufficient to test whether there is any of the end point of the segment is beyond the Axis location
                        if (axisPl.normalVector.X != 0.0)
                        {
                            if (faces[i].boundaries[j].startPoint.X >= axisPl.point.X || faces[i].boundaries[j].endPoint.X >= axisPl.point.X)
                            {
                                facesBeyond.Add(faces[i]);
                                break;
                            }
                        }
                        else if (axisPl.normalVector.Y != 0.0)
                        {
                            if (faces[i].boundaries[j].startPoint.Y >= axisPl.point.Y || faces[i].boundaries[j].endPoint.Y >= axisPl.point.Y)
                            {
                                facesBeyond.Add(faces[i]);
                                break;
                            }
                        }
                        if (axisPl.normalVector.Z != 0.0)
                        {
                            if (faces[i].boundaries[j].startPoint.Z >= axisPl.point.Z || faces[i].boundaries[j].endPoint.Z >= axisPl.point.Z)
                            {
                                facesBeyond.Add(faces[i]);
                                break;
                            }
                        }
                    }
                }
            }
            return facesBeyond;
        }

        /// <summary>
        /// This function return reduced list of faces that is entirely on the RIGHT of the specified Axis (only work on Axis parallel to X, or Y, or Z)
        /// The funcion is used for optimizing intersection test between the polyhedron and the Axis-align bounding box (e.g. Octree cell)
        /// </summary>
        /// <param name="axisLoc">The axis location. Must be X only, or Y only, or Z only</param>
        /// <returns></returns>
        public static List<Face3D> exclFacesRightOfAxis(List<Face3D> faces, Plane3D axisPl)
        {
            List<Face3D> facesLeftOfAxis = new List<Face3D>();
            // Only deal with one axis set, either A, Y or Z, otherwise null List will be returned
            if ((axisPl.normalVector.Y == 0.0 && axisPl.normalVector.Z == 0.0) || (axisPl.normalVector.X == 0.0 && axisPl.normalVector.Y == 0.0) || (axisPl.normalVector.X == 0.0 && axisPl.normalVector.Z == 0.0))
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    bool right = true;

                    // Test all face boundaries are beyond the axis location specified
                    for (int j = 0; j < faces[i].boundaries.Count; j++)
                    {
                        // It is sufficient to stop checking if any point of any of the boundaries lies behind the Axis location
                        if (axisPl.normalVector.X != 0.0)
                        {
                            if (!(faces[i].boundaries[j].startPoint.X > axisPl.point.X && faces[i].boundaries[j].endPoint.X > axisPl.point.X))
                            {
                                right = false;
                                break;
                            }
                        }
                        else if (axisPl.normalVector.Y != 0.0)
                        {
                            if (!(faces[i].boundaries[j].startPoint.Y > axisPl.point.Y && faces[i].boundaries[j].endPoint.Y > axisPl.point.Y))
                            {
                                right = false;
                                break;
                            }
                        }
                        if (axisPl.normalVector.Z != 0.0)
                        {
                            if (!(faces[i].boundaries[j].startPoint.Z > axisPl.point.Z && faces[i].boundaries[j].endPoint.Z > axisPl.point.Z))
                            {
                                right = false;
                                break;
                            }
                        }
                    }
                    if (right == false)
                        facesLeftOfAxis.Add(faces[i]);
                }
            }
            return facesLeftOfAxis;
        }

        /// <summary>
        /// This function return reduced list of faces that is entirely on the RIGHT of the specified Axis (only work on Axis parallel to X, or Y, or Z)
        /// The funcion is used for optimizing intersection test between the polyhedron and the Axis-align bounding box (e.g. Octree cell)
        /// </summary>
        /// <param name="axisLoc">The axis location. Must be X only, or Y only, or Z only</param>
        /// <returns></returns>
        public static List<Face3D> exclFacesLeftOfAxis(List<Face3D> faces, Plane3D axisPl)
        {
            List<Face3D> facesRightOfAxis = new List<Face3D>();
            // Only deal with one axis set, either A, Y or Z, otherwise null List will be returned
            if ((axisPl.normalVector.Y == 0.0 && axisPl.normalVector.Z == 0.0) || (axisPl.normalVector.X == 0.0 && axisPl.normalVector.Y == 0.0) || (axisPl.normalVector.X == 0.0 && axisPl.normalVector.Z == 0.0))
            {
                for (int i = 0; i < faces.Count; i++)
                {
                    bool left = true;

                    // Test all face boundaries are beyond the axis location specified
                    for (int j = 0; j < faces[i].boundaries.Count; j++)
                    {
                        // It is sufficient to stop checking if any point of any of the boundaries lies behind the Axis location
                        if (axisPl.normalVector.X != 0.0)
                        {
                            if (!(faces[i].boundaries[j].startPoint.X < axisPl.point.X && faces[i].boundaries[j].endPoint.X < axisPl.point.X))
                            {
                                left = false;
                                break;
                            }
                        }
                        else if (axisPl.normalVector.Y != 0.0)
                        {
                            if (!(faces[i].boundaries[j].startPoint.Y < axisPl.point.Y && faces[i].boundaries[j].endPoint.Y < axisPl.point.Y))
                            {
                                left = false;
                                break;
                            }
                        }
                        if (axisPl.normalVector.Z != 0.0)
                        {
                            if (!(faces[i].boundaries[j].startPoint.Z < axisPl.point.Z && faces[i].boundaries[j].endPoint.Z < axisPl.point.Z))
                            {
                                left = false;
                                break;
                            }
                        }
                    }
                    if (left == false)
                        facesRightOfAxis.Add(faces[i]);
                }
            }
            return facesRightOfAxis;
        }

        public static List<Face3D> exclFacesOutsideOfBound(List<Face3D> faces, BoundingBox3D bound, UInt16 axes)
        {
            Plane3D lowerBoundX = new Plane3D(bound.LLB, new Vector3D(1.0, 0.0, 0.0));
            Plane3D lowerBoundY = new Plane3D(bound.LLB, new Vector3D(0.0, 1.0, 0.0));
            Plane3D lowerBoundZ = new Plane3D(bound.LLB, new Vector3D(0.0, 0.0, 1.0));
            Plane3D upperBoundX = new Plane3D(bound.URT, new Vector3D(1.0, 0.0, 0.0));
            Plane3D upperBoundY = new Plane3D(bound.URT, new Vector3D(0.0, 1.0, 0.0));
            Plane3D upperBoundZ = new Plane3D(bound.URT, new Vector3D(0.0, 0.0, 1.0));

            List<Face3D> reducedList = faces;

            // filter each direction
            if ((axes & 0x100) == 0x100)
            {
                // filter X direction
                if (reducedList.Count > 0)
                    reducedList = exclFacesLeftOfAxis(reducedList, lowerBoundX);
                if (reducedList.Count > 0)
                    reducedList = exclFacesRightOfAxis(reducedList, upperBoundX);
            }

            if ((axes & 0x010) == 0x010)
            {
                // filter Y direction
                if (reducedList.Count > 0)
                    reducedList = exclFacesLeftOfAxis(reducedList, lowerBoundY);
                if (reducedList.Count > 0)
                    reducedList = exclFacesRightOfAxis(reducedList, upperBoundY);
            }

            if ((axes & 0x001) == 0x001)
            {
                // filter Z direction
                if (reducedList.Count > 0)
                    reducedList = exclFacesLeftOfAxis(reducedList, lowerBoundZ);
                if (reducedList.Count > 0)
                    reducedList = exclFacesRightOfAxis(reducedList, upperBoundZ);
            }

            return reducedList;
        }

        public override string ToString()
        {
            string pr = "Base plane: \n" + "P" + this.basePlane.point.ToString() + "\nN" + this.basePlane.normalVector.ToString()
                        + "\nVertices:";
            for (int i=0; i<this.vertices.Count; i++)
            {
                pr += "\n" + this.vertices[i].ToString();
            }
            return pr;
        }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(new Face3DLW(this));
        }

        public static bool validateFace (List<Point3D> vertices)
        {
            List<List<Point3D>> ListOfList = new List<List<Point3D>>();
            ListOfList.Add(vertices);
            if (!validateFace(ListOfList))
                return false;
            else
                return true;
        }

        public static bool validateFace (Face3D face)
        {
            if (double.IsNaN(face.basePlane.normalVector.X) || double.IsNaN(face.basePlane.normalVector.Y) || double.IsNaN(face.basePlane.normalVector.Z)
                || (face.basePlane.normalVector.X == 0.0 && face.basePlane.normalVector.Y == 0.0 && face.basePlane.normalVector.Z == 0.0))
                return false;

            return true;
        }

        public static bool validateFace (List<List<Point3D>> vertexLists)
        {
            if (vertexLists.Count < 1)
                return false;

            foreach (List<Point3D> pList in vertexLists)
            {
                // Face has less than minimum 3 vertices
                if (pList.Count < 3)
                    return false;

                List<LineSegment3D> boundaryLines;
                List<int> nonColinearEdgesIdx;

                // There is no enough non-colinear edges to form a valid Face
                generateEdges(pList, out boundaryLines, out nonColinearEdgesIdx);
                if (nonColinearEdgesIdx.Count < 2)
                    return false;

                //Plane3D basePlane = new Plane3D(pList[0], boundaryLines[nonColinearEdgesIdx[0]].baseLine.direction, boundaryLines[nonColinearEdgesIdx[1]].baseLine.direction);
                // Use Newell's method to calculate normal because any vertices > 3 can be concave
                Vector3D faceNormal = normalByNewellMethod(pList);
                Plane3D basePlane = new Plane3D(pList[0], faceNormal);

                // If we can't get a normal, then the face is most likely not valid
                if (double.IsNaN(basePlane.normalVector.X) || double.IsNaN(basePlane.normalVector.Y) || double.IsNaN(basePlane.normalVector.Z))
                    return false;
                if (basePlane.normalVector.X == 0.0 && basePlane.normalVector.Y == 0.0 && basePlane.normalVector.Z == 0.0)
                    return false;

                // Skip the check for point on plane as it may not be as critical and it is too sensitive to tolerance
                //foreach (Point3D p in pList)
                //{
                //    // Test all points are on the plane
                //    if (!Plane3D.pointOnPlane(basePlane, p))
                //        return false;
                //}
            }
            return true;
        }

        static Vector3D normalByNewellMethod(List<Point3D> vertices)
        {
            Vector3D normal = new Vector3D(0,0,0);
            if (vertices.Count == 3)
            {
                // If there are only 3 vertices, which is definitely a plannar face, we will use directly 2 vectors and calculate the cross product for the normal vector
                Vector3D v1 = vertices[1] - vertices[0];
                Vector3D v2 = vertices[2] - vertices[1];
                normal = Vector3D.CrossProduct(v1, v2);
            }
            else
            {
                // Use Newell algorithm only when there are more than 3 vertices to handle non-convex face and colinear edges
                for (int i = 0; i < vertices.Count; i++)
                {
                    if (i == vertices.Count - 1)
                    {
                        //The last vertex
                        normal.X += (vertices[i].Y - vertices[0].Y) * (vertices[i].Z + vertices[0].Z);
                        normal.Y += (vertices[i].Z - vertices[0].Z) * (vertices[i].X + vertices[0].X);
                        normal.Z += (vertices[i].X - vertices[0].X) * (vertices[i].Y + vertices[0].Y);
                    }
                    else
                    {
                        normal.X += (vertices[i].Y - vertices[i + 1].Y) * (vertices[i].Z + vertices[i + 1].Z);
                        normal.Y += (vertices[i].Z - vertices[i + 1].Z) * (vertices[i].X + vertices[i + 1].X);
                        normal.Z += (vertices[i].X - vertices[i + 1].X) * (vertices[i].Y + vertices[i + 1].Y);
                    }
                }
            }
            normal.Normalize();
            return normal;
        }

        /// <summary>
        /// A static function to offset a face. It basically shifts the entire face according to the supplies vector value
        /// </summary>
        /// <param name="theFace">the Face to be shifted</param>
        /// <param name="offsetVector">the vector value to shift the Face</param>
        /// <returns>the new Face</returns>
        public static Face3D offsetFace(Face3D theFace, Vector3D offsetVector)
        {
            List<List<Point3D>> newFaceVerts = new List<List<Point3D>>();
            List<Point3D> newOuterVerts = new List<Point3D>();
            foreach (Point3D p in theFace.verticesWithHoles[0])
            {
                Point3D newP = new Point3D();
                newP = p + offsetVector;
                newOuterVerts.Add(newP);
            }
            newFaceVerts.Add(newOuterVerts);
            for (int i = 1; i < theFace.verticesWithHoles.Count; ++i)
            {
                List<Point3D> newInnerVerts = new List<Point3D>();
                foreach (Point3D p in theFace.verticesWithHoles[i])
                {
                    Point3D newP = new Point3D();
                    newP = p + offsetVector;
                    newInnerVerts.Add(newP);
                }
                newFaceVerts.Add(newInnerVerts);
            }
            Face3D newFace = new Face3D(newFaceVerts);
            return newFace;
        }

        public static bool isNullFace(Face3D theFace)
        {
            Point3D firstP = null;
            bool first = true;
            bool nullFace = false;
            foreach (Point3D p in theFace.vertices)
            {
                if (first)
                {
                    firstP = p;
                    first = false;
                }
                else
                {
                    if (p == firstP)
                        nullFace = true;
                    else
                        return false;
                }
            }
            return nullFace;
        }
    }
}
