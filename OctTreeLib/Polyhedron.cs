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
    /// <summary>
    /// Polyhedron holds an indexed mesh data, mainly those relevant to the geometry, i.e. vertices and triangles
    /// The polyhedron is used for spatial index creation and therefore needs to differentiate a solid or a surface (solid will have indexes for the interior surface won't)
    /// </summary>
    public class Polyhedron
    {
        List<Point3D> _vertices = new List<Point3D>();  // List of vertices in a list of Point3D
        List<Face3D> _Faces = new List<Face3D>();
        BoundingBox3D _containingBB;
        // OctreeIndex = List<OctreeCell>;
        bool _isSolid = true;  // initially should be declared by the caller (assumed solid by default as most data will be). Later on can be verified using Euler eq.

        /// <summary>
        /// This is a constructor from raw indexed mesh data following openCTM style, i.e. float[] vertexCoord, int[] idx for triangle vertices.
        /// Data will be first organized into the structured format
        /// </summary>
        /// <param name="type"></param>
        /// <param name="vertCoords"></param>
        /// <param name="idxFaceCoord">index to the vertCoords list element for a Face Vertex (a point), which is double value, which means index will look like 0, 3, 6, ...</param>
        /// <param name="noFaceVerts"></param>
        public Polyhedron(PolyhedronFaceTypeEnum type, bool isSolid, List<double> vertCoords, List<int> idxFaceCoord, List<int> noFaceVerts)
        {
            int iter=0;

            // Organized the vertices in a list of Point3D
            while (iter < vertCoords.Count)
            {
                _vertices.Add(new Point3D(vertCoords[iter], vertCoords[iter+1], vertCoords[iter+2]));
                iter += 3;
            }

            iter = 0;
            int noV = 0;
            int noF = 0;

            while (iter < idxFaceCoord.Count && (iter/3) < (idxFaceCoord.Count/3))    // skip incomplete face at the end if any such thing occurs
            {
                List<Point3D> tmpVert = new List<Point3D>();
                if (type == PolyhedronFaceTypeEnum.ArbitraryFaces)
                    noV = noFaceVerts[noF];
                else
                    noV = (int)type;

                for (int j=0; j<noV; j++)
                {
                    Point3D vertex = _vertices[idxFaceCoord[iter]/3];
                    iter ++;
                    tmpVert.Add(vertex);
                }
                _Faces.Add(new Face3D(tmpVert));
                if (type == PolyhedronFaceTypeEnum.ArbitraryFaces) 
                    noF++;
            }

            _isSolid = isSolid;
        }

        public Polyhedron(List<Face3D> faceList)
        {
            _Faces = faceList;
            foreach (Face3D f in faceList)
            {
                _vertices.AddRange(f.outerAndInnerVertices);
            }
        }

        /// <summary>
        /// Return the enclosing bounding box (axis-aligned) of the polyhedron
        /// </summary>
        public BoundingBox3D boundingBox
        {
            get
            {
                _containingBB = new BoundingBox3D(_vertices);
                return _containingBB;
            }
        }


        /// <summary>
        /// Return the list of Face3D that makes up the polyhedron
        /// </summary>
        public List<Face3D> Faces
        {
            get { return _Faces; }
        }

        /// <summary>
        /// Return the list of vertices of the polyhedron
        /// </summary>
        public List<Point3D> Vertices
        {
            get { return _vertices; }
        }

        /// <summary>
        /// Testing a point inside an optimized type of polyhedron, i.e. cuboid.
        /// This optimized version is very fast since it only compares a position compared to a AABB. It is designed for testing a point inside an Octree cell
        /// </summary>
        /// <param name="cuboidPolyH">Caller is resposible to ensure that the Polyhedron is really a cuboid</param>
        /// <param name="aPoint">a point</param>
        /// <returns></returns>
        public static bool insideCuboid(Polyhedron cuboidPolyH, Point3D aPoint)
        {
            if ((aPoint.X >= cuboidPolyH.boundingBox.LLB.X && aPoint.X <= cuboidPolyH.boundingBox.URT.X)
                && (aPoint.Y >= cuboidPolyH.boundingBox.LLB.Y && aPoint.Y <= cuboidPolyH.boundingBox.URT.Y)
                && (aPoint.Z >= cuboidPolyH.boundingBox.LLB.Z && aPoint.Z <= cuboidPolyH.boundingBox.URT.Z)
                )
                return true;
            else
                return false;
        }

        /// <summary>
        /// Testing a point inside a polyhedron is similar with the 2D version of a point inside a polygon by testing intersection between a ray starting from the point.
        /// If the number of intersection is odd the point is inside. In 3D the intersection is against the Faces
        /// </summary>
        /// <param name="polyH"></param>
        /// <param name="aPoint"></param>
        /// <returns></returns>
        public static bool inside (Polyhedron polyH, Point3D aPoint)
        {
            double extent = 0;
            if (Octree.WorldBB == null)
                extent = Point3D.distance(polyH.boundingBox.LLB, polyH.boundingBox.URT);
            else
                extent = Octree.WorldBB.extent;

            // define a ray using linesegment from the point toward and along +X-axis with 2*the extent of the World BB to ensure ray is always long enough
            LineSegment3D ray = new LineSegment3D(aPoint, new Point3D(aPoint.X + 2 * extent, aPoint.Y, aPoint.Z));
            List<Face3D> reducedList = Face3D.inclFacesBeyondAxis(polyH.Faces, new Plane3D(aPoint, new Vector3D(1.0, 0.0, 0.0)));  // beyond YZ plane   
            List<Point3D> corners = new List<Point3D>();
            corners.Add(aPoint);
            corners.Add(aPoint);
            BoundingBox3D bound = new BoundingBox3D(corners);
            if (reducedList.Count > 0)
                reducedList = Face3D.exclFacesOutsideOfBound(reducedList, bound, 0x011);   // reduce list on Y and Z both direction
            if (reducedList.Count == 0) return false;   // no faces left, either they are all on the left or they are all on the right

            int iCount = 0;
            for (int i=0; i<reducedList.Count; i++)
            {
                List<Point3D> intPts = new List<Point3D>();
                if (Face3D.intersect(reducedList[i], ray, out intPts))
                    iCount++;
            }
            if ((iCount % 2) == 1) return true;
            return false;
        }

        /// <summary>
        /// To determine that a line segment is inside (completely inside) of a polyhedron, it must satisfy the following:
        /// 1. both end points are inside the polyhedron
        /// 2. There no intersection between the segment and the polyhedron
        /// </summary>
        /// <param name="polyH"></param>
        /// <param name="lineS"></param>
        /// <returns></returns>
        public static bool inside(Polyhedron polyH, LineSegment3D lineS)
        {
            // reducing the face candidate list is less expensive than inside test, do it first
            Point3D leftX = new Point3D();
            Point3D rightX = new Point3D();
            leftX.X = lineS.startPoint.X < lineS.endPoint.X ? lineS.startPoint.X : lineS.endPoint.X;
            rightX.X = lineS.startPoint.X < lineS.endPoint.X ? lineS.endPoint.X : lineS.startPoint.X;
            List<Face3D> reducedList = Face3D.inclFacesBeyondAxis(polyH.Faces, new Plane3D(leftX, new Vector3D(1.0, 0.0, 0.0)));
            // reducedList = Face3D.exclFacesBeyondAxis(reducedList, rightX);   // cannot remove this otherwise inside test for StartPoint may not be correct!!!
            List<Point3D> corners = new List<Point3D>();
            corners.Add(lineS.startPoint);
            corners.Add(lineS.endPoint);
            BoundingBox3D bound = new BoundingBox3D(corners);
            if (reducedList.Count > 0)
                reducedList = Face3D.exclFacesOutsideOfBound(reducedList, bound, 0x011);   // reduce list on Y and Z both direction
            if (reducedList.Count == 0) return false;   // no faces left, either they are all on the left or they are all on the right

            // inside test for both segment ends. Test one by one so that we can exit when any one of them are not inside
            if (!inside(polyH, lineS.startPoint)) return false;
            if (!inside(polyH, lineS.endPoint)) return false;

            // Now test whether there is any intersection. If there is, the segment is not completely inside
            for (int i=0; i<reducedList.Count; i++)
            {
                List<Point3D> iPoints = new List<Point3D>();
                if (Face3D.intersect(reducedList[i], lineS, out iPoints))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// To determin a face is inside a polyhedron, all the segments must be completely inside the polyhedron (fulfills the inside test for linesegment) 
        /// </summary>
        /// <param name="polyH"></param>
        /// <param name="face"></param>
        /// <returns></returns>
        public static bool inside(Polyhedron polyH, Face3D face)
        {
            for (int i=0; i<face.boundaries.Count; i++)
            {
                if (!inside(polyH, face.boundaries[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// To check a polyhedron inside another polyhedron, we have to check that the entire polyhedron is inside the other (all faces inside fulfilling the inside test for face)
        /// </summary>
        /// <param name="polyH"></param>
        /// <param name="otherPolyH">The polyhedron that needs to be checked inside polyH</param>
        /// <returns></returns>
        public static bool inside(Polyhedron polyH, Polyhedron otherPolyH)
        {
            for (int i=0; i<otherPolyH.Faces.Count; i++)
            {
                if (!inside(polyH, otherPolyH.Faces[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Testing intersection between a line segment and a polyhedron only. It stops at the first intersection
        /// </summary>
        /// <param name="polyH">polyhedron</param>
        /// <param name="lineS">Line segment</param>
        /// <returns></returns>
        public static bool intersect(Polyhedron polyH, LineSegment3D lineS)
        {
            List<Point3D> corners = new List<Point3D>();
            corners.Add(lineS.startPoint);
            corners.Add(lineS.endPoint);
            BoundingBox3D bound = new BoundingBox3D(corners);
            List<Face3D> reducedList = Face3D.exclFacesOutsideOfBound(polyH.Faces, bound, 0x111);
            if (reducedList.Count == 0) return false;   // no faces left, either they are all on the left or they are all on the right


            // Now test whether there is any intersection.
            for (int i = 0; i < reducedList.Count; i++)
            {
                List<Point3D> iPoints = new List<Point3D>();
                if (Face3D.intersect(reducedList[i], lineS, out iPoints))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Testing intersection between a line segment and a polyhedron and return ALL intersection points
        /// </summary>
        /// <param name="polyH">Polyhedron</param>
        /// <param name="lineS">Line segment</param>
        /// <param name="intPoints">List of intersection points</param>
        /// <returns></returns>
        public static bool intersect(Polyhedron polyH, LineSegment3D lineS, out List<Point3D> intPoints)
        {
            List<Point3D> iPoints = new List<Point3D>();
            intPoints = iPoints;

            List<Point3D> corners = new List<Point3D>();
            corners.Add(lineS.startPoint);
            corners.Add(lineS.endPoint);
            BoundingBox3D bound = new BoundingBox3D(corners);
            List<Face3D> reducedList = Face3D.exclFacesOutsideOfBound(polyH.Faces, bound, 0x111);
            if (reducedList.Count == 0) return false;   // no faces left, either they are all on the left or they are all on the right

            // Now test whether there is any intersection. It needs to complete the test with entire faces to collect the intersection points
            for (int i = 0; i < reducedList.Count; i++)
            {
                List<Point3D> iPts = new List<Point3D>();
                if (Face3D.intersect(reducedList[i], lineS, out iPts))
                {
                    for (int j = 0; j < iPts.Count; j++)
                        iPoints.Add(iPts[j]);
                }
            }
            if (iPoints.Count > 0) return true;
            return false;
        }
         
        /// <summary>
        /// Test intersection between a polyhedron and a face. There is optmization applied for axis-aligned face (useful for Octree cells as they are all axis aligned)
        /// </summary>
        /// <param name="polyH">The Polyhedron</param>
        /// <param name="face">The face to test the intersection</param>
        /// <returns>true=intersected; false otherwise</returns>
        public static bool intersect(Polyhedron polyH, Face3D face)
        {
            List<Face3D> faceList = new List<Face3D>();
            BoundingBox3D bound = new BoundingBox3D(face.vertices);

            faceList = Face3D.exclFacesOutsideOfBound(polyH.Faces, bound, 0x111);

            if (faceList.Count == 0) return false;  // There is no face remaining to test, return false

            for (int i = 0; i < faceList.Count; i++)
            {
                FaceIntersectEnum mode;
                LineSegment3D intL = new LineSegment3D(new Point3D(), new Point3D());
                bool status = Face3D.intersect(face, faceList[i], out intL, out mode);
                if (status == true) return true;        // return true as soon as an intersection is detected
            }
            return false;
        }

        /// <summary>
        /// Test intersection between 2 Polyhedron. It is basically going through a smaller set of faces and test each one of them against the polyhedron
        /// The smaller set of faces is chosen as they are likely to be more optimized for Octree cells that are always axis aligned
        /// </summary>
        /// <param name="polyH"></param>
        /// <param name="otherPolyH"></param>
        /// <returns></returns>
        public static bool intersect(Polyhedron polyH, Polyhedron otherPolyH)
        {
            // Iterate faces to perform intersect test based on the polyhedron that has smaller number of faces
            if (polyH.Faces.Count < otherPolyH.Faces.Count)
            {
                for (int i=0; i<polyH.Faces.Count; i++)
                {
                    if (intersect(otherPolyH, polyH.Faces[i])) return true;
                }
            }
            else
            {
                for (int i = 0; i < otherPolyH.Faces.Count; i++)
                {
                    if (intersect(polyH, otherPolyH.Faces[i])) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A variation of polyH - polyH intersection, allowing only a subset of a polyH in form of a list of Face3D to be evaluated
        /// </summary>
        /// <param name="polyH"></param>
        /// <param name="faceList"></param>
        /// <returns></returns>
        public static bool intersect(Polyhedron polyH, List<Face3D> faceList)
        {
            for (int i = 0; i < faceList.Count; i++)
            {
                if (intersect(polyH, faceList[i])) return true;
            }
            return false;
        }

        public override string ToString()
        {
            string pr = "Polyhedron:";
            for (int i=0; i<this.Faces.Count; i++)
            {
                pr += "\nFace " + i.ToString() + ":\n" + this.Faces[i].ToString();
            }
            return pr;
        }

        public string ToJsonString()
        {
            List<Face3DLW> faces = new List<Face3DLW>();
            foreach (Face3D f in this.Faces)
                faces.Add(new Face3DLW(f));
            return JsonConvert.SerializeObject(faces);
        }
    }
}
