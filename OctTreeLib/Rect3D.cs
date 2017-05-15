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
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace BIMRL.OctreeLib
{
    public class Rect3DLW
    {
        public Point3DLW min { get; set; }
        public Point3DLW max { get; set; }
        public Rect3DLW(Rect3D rect)
        {
            min = new Point3DLW(rect.Min);
            max = new Point3DLW(rect.Max);
        }
    }

    public class Rect3D
    {

        private static Rect3D _empty;

        public static Rect3D Empty
        {
            get { return Rect3D._empty; }
            set
            {
                _empty = new Rect3D(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
            }

        }

        private Point3D _location;
        private double _sizeX;
        private double _sizeY;
        private double _sizeZ;
       

        public double SizeX
        {
            get { return _sizeX; }
            set { _sizeX = value; }
        }


        public double SizeY
        {
            get { return _sizeY; }
            set { _sizeY = value; }
        }
        
        public double SizeZ
        {
            get { return _sizeZ; }
            set { _sizeZ = value; }
        }
      

        public Point3D Location
        {
            get
            {
                return _location;
            }
            set
            {
                this._location = value;
            }
        }
        
        
        public double X
        {
            get
            {
                return _location.X;
            }
            set
            {
                _location.X = value;
            }
        }
        public double Y
        {
            get
            {
                return _location.Y;
            }
            set
            {
                _location.Y = value;
            }
        }
        public double Z
        {
            get
            {
                return _location.Z;
            }
            set
            {
                _location.Z = value;
            }
        }

        public bool IsEmpty
        {
            get
            {
                return SizeX < 0.0;
            }
        }

        public Rect3D(double x, double y, double z, double sizeX, double sizeY, double sizeZ)
        {
            _location = new Point3D(x, y, z);
            _sizeX = sizeX;
            _sizeY = sizeY;
            _sizeZ = sizeZ;
        }

        public Rect3D(Point3D p1, Point3D p2)
        {
            _location.X = Math.Min(p1.X, p2.X);
            _location.Y = Math.Min(p1.Y, p2.Y);
            _location.Z = Math.Min(p1.Z, p2.Z);
            this._sizeX = Math.Max(p1.X, p2.X) - _location.X;
            this._sizeY = Math.Max(p1.Y, p2.Y) - _location.Y;
            this._sizeZ = Math.Max(p1.Z, p2.Z) - _location.Z;
        }

        static Rect3D()
        {
            _empty = new Rect3D ( double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity );
        }

        public Rect3D(Point3D highpt)
        {
            _location = highpt;
            _sizeX = (double)0.0;
            _sizeY = (double)0.0;
            _sizeZ = (double)0.0;
        }

        public Rect3D(Vector3D vMin, Vector3D vMax)
        {
            _location.X = Math.Min(vMin.X, vMax.X);
            _location.Y = Math.Min(vMin.Y, vMax.Y);
            _location.Z = Math.Min(vMin.Z, vMax.Z);
            this._sizeX = Math.Max(vMin.X, vMax.X) - _location.X;
            this._sizeY = Math.Max(vMin.Y, vMax.Y) - _location.Y;
            this._sizeZ = Math.Max(vMin.Z, vMax.Z) - _location.Z;
        }
        
        /// <summary>
        /// Minimum vertex
        /// </summary>
        public Point3D Min
        {
            get
            {
                return new Point3D(_location.X+_sizeX,_location.Y+_sizeY,_location.Z+_sizeZ);
            }
        }
        /// <summary>
        /// Maximum vertex
        /// </summary>
        public Point3D Max
        {
            get
            {
                return this.Location;
            }
        }

        static public Rect3D Inflate( float x, float y, float z)
        {
            Rect3D rect = Rect3D.Empty;
            rect.X -= (double)x; rect.Y -= (double)y; rect.Z -= (double)z;
            rect.SizeX += (double)x * 2; rect.SizeY += (double)y * 2; rect.SizeZ += (double)z * 2;
            return rect;
        }

        static public Rect3D Inflate(double x, double y, double z)
        {
            Rect3D rect = Rect3D.Empty;
            rect.X -= x; rect.Y -= y; rect.Z -= z;
            rect.SizeX += x * 2; rect.SizeY += y * 2; rect.SizeZ += z * 2;
            return rect;
        }

        static public Rect3D Inflate( float d)
        {
            Rect3D rect = Rect3D.Empty;
            rect.X -= (double)d; rect.Y -= (double)d; rect.Z -= (double)d;
            rect.SizeX += (double)d * 2; rect.SizeY += (double)d * 2; rect.SizeZ += (double)d * 2;
            return rect;
        }

        static public Rect3D Inflate(double d)
        {
            Rect3D rect = Rect3D.Empty;
            rect.X -= d; rect.Y -= d; rect.Z -= d;
            rect.SizeX += d * 2; rect.SizeY += d * 2; rect.SizeZ += d * 2;
            return rect;
        }


        /// <summary>
        /// Calculates the centre of the 3D rect
        /// </summary>
        /// <param name="rect3D"></param>
        /// <returns></returns>
        public Point3D Centroid()
        {
            if (IsEmpty) 
                return new Point3D(0, 0, 0);
            else
                return new Point3D((X + SizeX / 2), (Y + SizeY / 2), (Z + SizeZ / 2));
        }


        static public Rect3D TransformBy(Rect3D rect3d, Matrix3D m)
        {
            Point3D min = rect3d.Min;
            Point3D max = rect3d.Max;
            Vector3D up = m.Up;
            Vector3D right = m.Right;
            Vector3D backward = m.Backward;
            var xa = right * min.X;
            var xb = right * max.X;

            var ya = up * min.Y;
            var yb = up * max.Y;

            var za = backward * min.Z;
            var zb = backward * max.Z;

            return new Rect3D(
                Vector3D.Min(xa, xb) + Vector3D.Min(ya, yb) + Vector3D.Min(za, zb) + m.Translation,
                Vector3D.Max(xa, xb) + Vector3D.Max(ya, yb) + Vector3D.Max(za, zb) + m.Translation
            );
            
        }

        public void Union(Rect3D bb)
        {
            if (IsEmpty)
            {
                this.Location = bb.Location;
                this.SizeX = bb.SizeX;
                this.SizeY = bb.SizeY;
                this.SizeZ = bb.SizeZ;
            }
            else if (!bb.IsEmpty)
            {
                double numX = Math.Min(X, bb.X);
                double numY = Math.Min(Y, bb.Y);
                double numZ = Math.Min(Z, bb.Z);
                _sizeX = Math.Max((double)(X + _sizeX), (double)(bb.X + bb._sizeX)) - numX;
                _sizeY = Math.Max((double)(Y + _sizeY), (double)(bb.Y + bb._sizeY)) - numY;
                _sizeZ = Math.Max((double)(Z + _sizeZ), (double)(bb.Z + bb._sizeZ)) - numZ;
                X = numX;
                Y = numY;
                Z = numZ;
            }
        }

        public void Union(Point3D highpt)
        {
            Union(new Rect3D(highpt, highpt));
        }

        public bool Contains(double x, double y, double z)
        {
            if (this.IsEmpty)
            {
                return false;
            }
            return this.ContainsCoords((double)x, (double)y, (double)z);
        }

        public bool Contains(Point3D pt)
        {
            if (this.IsEmpty)
            {
                return false;
            }
            return this.ContainsCoords(pt.X, pt.Y, pt.Z);
        }

        private bool ContainsCoords(double x, double y, double z)
        {
            return (((((x >= _location.X) && (x <= (_location.X + this._sizeX))) && ((y >= _location.Y) && (y <= (_location.Y + this._sizeY)))) && (z >= _location.Z)) && (z <= (_location.Z + this._sizeZ)));
  
        }

        public bool Contains(Rect3D rect)
        {
            if (this.IsEmpty)
            {
                return false;
            }

            return this.ContainsCoords(rect.X, rect.Y, rect.Z) && this.ContainsCoords(rect.X + rect.SizeX, rect.Y + rect.SizeY, rect.Z+rect.SizeZ);
        }

       /// <summary>
       /// Returns the radius of the sphere that contains this bounding box rectangle 3D
       /// </summary>
       /// <returns></returns>
        public double Radius()
        {
            Vector3D max = new Vector3D(SizeX, SizeY, SizeZ);
            double len = max.Length;
            if (len != 0)
                return  len / 2;
            else
                return 0;
        }

        /// <summary>
        /// Returns the length of the largest diagonal
        /// </summary>
        /// <returns></returns>
        public double Length()
        {
            Vector3D max = new Vector3D(SizeX, SizeY, SizeZ);
            return max.Length;
        }

        public override string ToString()
        {
            string pr = "P" + _location.ToString() + "\nSize (x,yz): (" + _sizeX.ToString() + ", " + _sizeY.ToString() + ", " + _sizeZ.ToString() + ")";
            return pr;
        }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(new Rect3DLW(this));
        }
    }
}
