using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace com.invicara.empire.EmpireBIMDataTile.OctreeLib
{
    /// <summary>
    /// A Octree is a structure designed to 3D partition space so
    /// that it's faster to find out what is inside or outside a given 
    /// volume. See http://en.wikipedia.org/wiki/Octree
    /// This Octree contains items that inheret from Ihas3DLocation
    /// it will store a reference to the item in the Octa 
    /// that is just big enough to hold it. Each Octa has a bucket that 
    /// contain multiple items.
    /// </summary>
    public class Octree
    {
        protected  OctreeNode root;
        private Point3D octreeLLB, octreeURT;

        public Octree(float xMin, float yMin, float zMin, float xMax, float yMax, float zMax)
        {
            root = new OctTreeNode(xMin, yMin,zMin, xMax, yMax, zMax);
            octreeLLB.X = xMin;
            octreeLLB.Y = yMin;
            octreeLLB.Z = zMin;
            octreeURT.X = xMax;
            octreeURT.Y = yMax;
            octreeURT.Z = zMax;
        }

        /// <summary>
        /// Get the count of items in the Octree
        /// </summary>
        public int Count { get { return root.Count; } }

        public List<Ihas3DLocation> AllItems
        {
            get { return root.AllItems; }
        }

        /// <summary>
        /// Insert the feature into the Octree
        /// </summary>
        /// <param name="item"></param>
        public void Insert(Ihas3DLocation item)
        {
            if (!item.isInside(root))
            {
                item.isInside(root);
                throw new Exception("Item Is out of Octa-Tree Range");
            }

            root.Insert(item);
        }

        /// <summary>
        /// Query the Octree, returning the items that are in the given area
        /// </summary>
        /// <param name="area"></param>
        /// <returns></returns>
        public List<Ihas3DLocation> Query(Cuboid queryVolume)
        {
            return root.Query(queryVolume);
        }

        public double distance(CellID64 startCell, CellID64 endCell)
        {
            double dist = Double.MaxValue;

            return dist;
        }

        public double distance(Point3D startPoint, CellID64 endCell)
        {
            double dist = Double.MaxValue;

            return dist;
        }

        public double distance(CellID64 startCell, Point3D endPoint)
        {
            double dist = Double.MaxValue;

            return dist;
        }

        public Int64 unitDistance(CellID64 startCell, CellID64 endCell)
        {
            Int64 dist = Int64.MaxValue;

            return dist;
        }

        public Int64 unitDistance(Point3D startPoint, CellID128 endCell)
        {
            Int64 dist = Int64.MaxValue;

            return dist;
        }

        public Int64 unitDistance(CellID128 startCell, Point3D endPoint)
        {
            Int64 dist = Int64.MaxValue;

            return dist;
        }
    }
}
