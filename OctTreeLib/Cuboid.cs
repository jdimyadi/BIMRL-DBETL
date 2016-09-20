using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BIMRL.OctreeLib
{
    public class Cuboid
    {
        private Polyhedron _polyHRep;

        Point3D origin;
        Point3D centroid;

        /// <summary>
        /// make 3D cube
        /// </summary>
        /// <param name="Origin">center</param>
        /// <param name="Width">Representative X-coordinate</param>
        /// <param name="Height">Representative Y-coordinate</param>
        /// <param name="Depth">Representative Z-coordinate</param>
        public Cuboid(Point3D Origin, double xLength, double yLength, double zLength)
        {
            origin = Origin;

            centroid = new Point3D();
            centroid.X = origin.X + 0.5 * xLength;
            centroid.Y = origin.Y + 0.5 * yLength;
            centroid.Z = origin.Z + 0.5 * zLength;

            // Define list of corrdinates for the cuboid vertices to be fed to the polyhedron

            List<double> cuboidVerCoords = new List<double>();
            // Point #1
            cuboidVerCoords.Add(origin.X);
            cuboidVerCoords.Add(origin.Y);
            cuboidVerCoords.Add(origin.Z);

            // Point #2
            cuboidVerCoords.Add(origin.X + xLength);
            cuboidVerCoords.Add(origin.Y);
            cuboidVerCoords.Add(origin.Z);

            // Point #3
            cuboidVerCoords.Add(origin.X);
            cuboidVerCoords.Add(origin.Y + yLength);
            cuboidVerCoords.Add(origin.Z);

            // Point #4
            cuboidVerCoords.Add(origin.X + xLength);
            cuboidVerCoords.Add(origin.Y + yLength) ;
            cuboidVerCoords.Add(origin.Z);

            // Point #5
            cuboidVerCoords.Add(origin.X);
            cuboidVerCoords.Add(origin.Y);
            cuboidVerCoords.Add(origin.Z + zLength);

            // Point #6
            cuboidVerCoords.Add(origin.X + xLength);
            cuboidVerCoords.Add(origin.Y);
            cuboidVerCoords.Add(origin.Z + zLength);

            // Point #7
            cuboidVerCoords.Add(origin.X);
            cuboidVerCoords.Add(origin.Y + yLength);
            cuboidVerCoords.Add(origin.Z + zLength);

            // Point #8
            cuboidVerCoords.Add(origin.X + xLength);
            cuboidVerCoords.Add(origin.Y + yLength);
            cuboidVerCoords.Add(origin.Z + zLength);

            // Create list of face index to the list of coordinates representing the cuboid rectangular faces (in four tupple) - there will be 6 faces in total        
            List<int> idxFaceCoords = new List<int>();

            // Face #1 - front face
            idxFaceCoords.Add(0*3);
            idxFaceCoords.Add(1*3);
            idxFaceCoords.Add(5*3);
            idxFaceCoords.Add(4*3);

            // Face #2 - right face
            idxFaceCoords.Add(1*3);
            idxFaceCoords.Add(3*3);
            idxFaceCoords.Add(7*3);
            idxFaceCoords.Add(5*3);

            // Face #3 - back face
            idxFaceCoords.Add(3*3);
            idxFaceCoords.Add(2*3);
            idxFaceCoords.Add(6*3);
            idxFaceCoords.Add(7*3);

            // Face #4 - left face
            idxFaceCoords.Add(2*3);
            idxFaceCoords.Add(0*3);
            idxFaceCoords.Add(4*3);
            idxFaceCoords.Add(6*3);

            // Face #5 - bottom face
            idxFaceCoords.Add(0*3);
            idxFaceCoords.Add(2*3);
            idxFaceCoords.Add(3*3);
            idxFaceCoords.Add(1*3);

            // Face #6 - top face
            idxFaceCoords.Add(4*3);
            idxFaceCoords.Add(5*3);
            idxFaceCoords.Add(7*3);
            idxFaceCoords.Add(6*3);

            _polyHRep = new Polyhedron(PolyhedronFaceTypeEnum.RectangularFaces, true, cuboidVerCoords, idxFaceCoords, null);
        }

        public Point3D Centroid
        {
            get { return centroid; }
        }

        public Polyhedron cuboidPolyhedron
        {
            get { return _polyHRep; }
        }

        public double extent
        {
            get { return _polyHRep.boundingBox.extent; }
        }

        public Face3D TopFace
        {
            get
            {
                List<Point3D> verts = new List<Point3D>();
                Point3D l = _polyHRep.boundingBox.LLB;
                Point3D u = _polyHRep.boundingBox.URT;
                verts.Add(new Point3D(l.X, l.Y, u.Z));
                verts.Add(new Point3D(u.X, l.Y, u.Z));
                verts.Add(new Point3D(u.X, u.Y, u.Z));
                verts.Add(new Point3D(l.X, u.Y, u.Z));
                return new Face3D(verts);
            }
        }

        public Face3D BottomFace
        {
            get
            {
                List<Point3D> verts = new List<Point3D>();
                Point3D l = _polyHRep.boundingBox.LLB;
                Point3D u = _polyHRep.boundingBox.URT;
                verts.Add(new Point3D(l.X, l.Y, l.Z));
                verts.Add(new Point3D(u.X, l.Y, l.Z));
                verts.Add(new Point3D(u.X, u.Y, l.Z));
                verts.Add(new Point3D(l.X, u.Y, l.Z));
                return new Face3D(verts);
            }
        }

        public Face3D FrontFace
        {
            get
            {
                List<Point3D> verts = new List<Point3D>();
                Point3D l = _polyHRep.boundingBox.LLB;
                Point3D u = _polyHRep.boundingBox.URT;
                verts.Add(new Point3D(l.X, l.Y, l.Z));
                verts.Add(new Point3D(u.X, l.Y, l.Z));
                verts.Add(new Point3D(u.X, l.Y, u.Z));
                verts.Add(new Point3D(l.X, l.Y, u.Z));
                return new Face3D(verts);
            }
        }

        public Face3D BackFace
        {
            get
            {
                List<Point3D> verts = new List<Point3D>();
                Point3D l = _polyHRep.boundingBox.LLB;
                Point3D u = _polyHRep.boundingBox.URT;
                verts.Add(new Point3D(l.X, u.Y, l.Z));
                verts.Add(new Point3D(u.X, u.Y, l.Z));
                verts.Add(new Point3D(u.X, u.Y, u.Z));
                verts.Add(new Point3D(l.X, u.Y, u.Z));
                return new Face3D(verts);
            }
        }

        public Face3D RightFace
        {
            get
            {
                List<Point3D> verts = new List<Point3D>();
                Point3D l = _polyHRep.boundingBox.LLB;
                Point3D u = _polyHRep.boundingBox.URT;
                verts.Add(new Point3D(u.X, l.Y, l.Z));
                verts.Add(new Point3D(u.X, u.Y, l.Z));
                verts.Add(new Point3D(u.X, u.Y, u.Z));
                verts.Add(new Point3D(u.X, l.Y, u.Z));
                return new Face3D(verts);
            }
        }

        public Face3D LeftFace
        {
            get
            {
                List<Point3D> verts = new List<Point3D>();
                Point3D l = _polyHRep.boundingBox.LLB;
                Point3D u = _polyHRep.boundingBox.URT;
                verts.Add(new Point3D(l.X, l.Y, l.Z));
                verts.Add(new Point3D(l.X, u.Y, l.Z));
                verts.Add(new Point3D(l.X, u.Y, u.Z));
                verts.Add(new Point3D(l.X, l.Y, u.Z));
                return new Face3D(verts);
            }
        }

        public bool intersectWith (Polyhedron PH2)
        {
            return Polyhedron.intersect(this._polyHRep, PH2);
        }

        public bool intersectWith (Face3D F)
        {
            return Polyhedron.intersect(this._polyHRep, F);
        }

        public bool intersectWith (LineSegment3D LS)
        {
            return Polyhedron.intersect(this._polyHRep, LS);
        }

        public bool isInside (Polyhedron PH)
        {
            return Polyhedron.inside(this._polyHRep, PH);
        }

        public bool isInside (Face3D F)
        {
            return Polyhedron.inside(this._polyHRep, F);
        }

        public bool isInside (LineSegment3D LS)
        {
            return Polyhedron.inside(this._polyHRep, LS);
        }

        public bool isInside (Point3D P)
        {
            return Polyhedron.inside(this._polyHRep, P);
        }

        public override string ToString()
        {
            return _polyHRep.ToString();
        }
    }
}
