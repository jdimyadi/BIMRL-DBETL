using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.invicara.empire.EmpireBIMDataTile.OctreeLib
{
    public class Triangle3D
    {
        private Face3D _baseFace;

        public Triangle3D (Point3D v1, Point3D v2, Point3D v3)
        {
            List<Point3D> vertices = new List<Point3D>();
            vertices.Add(v1);
            vertices.Add(v2);
            vertices.Add(v3);
            _baseFace = new Face3D(vertices);
        }
    }
}
