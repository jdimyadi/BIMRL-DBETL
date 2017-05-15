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
using System.Collections;
using System.Linq;
using System.Text;


namespace BIMRL.OctreeLib
{

    public class CellID64
    {
        /* This is the 64bit version.
           Cell ID encoding follows the same character encoding done in IFC for compactness and the use of only readable characters that represent
           64 bit ID.
           Each spatial index cell is coded by 3 bit number: Cz*2^2 + Cy*2^1 + Cx
                       110 / 111
                     ---------
                     100 / 101       Upper part of the octants
                       010 / 011
                     ---------
                     000 / 001       Lower part of the octants
         
           The ID will be encoded in a 12 character long encoding and is orgnized the following way:
             * The first 57 bits will be used to store up to 19 level of subdivision cell id: encoded in 64 base character, one character representing 6 bit value, with the exception the very first one tat will be only 3 bits
             * The next 6 bits will represent encoded number of the subdivision level (up to 19, will use of only 5 bits)
             * The remaining 1 bit will be used to mark whether the cell id (in the spatial indexing context) is fully inside a solid or at the border
           The character encoding allows simple 1-D indexing in any type of database to support 3-D spatial indexing.
         */
         private static char[] cellEncoding = {'0','1','2','3','4','5','6','7','8','9','A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
                                                  '^','_','a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z'};

        private static Hashtable cellDecoding = new Hashtable();
        private UInt64 m_CellID = 0;
        private bool _isBorderCell = false;
        static UInt64 mask = 0x3F;
        static UInt64 levelMask = 0x3E;
        static UInt64 flagMask = 0x01;

        /// <summary>
        /// Constructor for CellID class taking in 64bit integer value of the Cell
        /// </summary>
        /// <param name="iCellID"></param>
        public CellID64(UInt64 iCellID)
        {
            // If hashtable is empty, insert the reverse map of the character encoding for fast decoding mechanism. It is static so it will be done once
            if (cellDecoding.Count <= 0)
            {
                for (ushort i = 0; i < 64; i++)
                    cellDecoding.Add(cellEncoding[i], i);
            }

            m_CellID = iCellID;
        }

        /// <summary>
        /// Contructor for CellID taking in the encoded string format of the Cell
        /// </summary>
        /// <param name="sCellID"></param>
        public CellID64(string sCellID)
        {
            // If hashtable is empty, insert the reverse map of the character encoding for fast decoding mechanism. It is static so it will be done once
            if (cellDecoding.Count <= 0)
            {
                for (ushort i = 0; i < 64; i++)
                    cellDecoding.Add(cellEncoding[i], i);
            }
            
            if (sCellID.Length != 12)
            {
                // Raise exception
                throw new FormatException(String.Format("Invalid Spatial Index Cell ID format {0}. Valid format is alphanumerics (plus _ or $) of 12 characters long!", sCellID));                
            }

            m_CellID = this.ToCellID(sCellID);

        }

        public UInt64 iCellID { get { return m_CellID; } }

        /// <summary>
        /// Translate an encoded string format into internal representation of CellID in large integer (128bit)
        /// </summary>
        /// <param name="sCellID"></param>
        /// <returns></returns>
        public UInt64 ToCellID(string sCellID)
        {
            UInt64 theCellID = 0;
            char[] indivChar = sCellID.ToCharArray();

            // Decode the Cell ID back into 64 bit Integer. First 10 characters for the 57-bits cell codes,
            // 1 characters for the level number, and one bit for border flag
            for (int i = 0; i < 11; i++)
            {
                UInt64 tmpCellID = (UInt64) ((ushort) cellDecoding[indivChar[i]]);
                theCellID = theCellID | tmpCellID << (61 - i * 6); 
            }
            theCellID = theCellID | (UInt64)((ushort)cellDecoding[indivChar[11]]);  //The last cell id code will only occupy the upper 3 bits, 2 will be overwriten here
            return theCellID;
        }

        /// <summary>
        /// Return the encoded string of Cell ID based on the prior initialized Cell ID (default is 0)
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            char[] tmpCellIDStr = new char[12];

           
            // Work on the higher part of the Cell ID that represents 60-bit values
            for (int i = 0; i < 11; i++)
            {
                UInt64 tmp = 0;
                tmp = m_CellID >> (61 - i * 6);
                tmpCellIDStr[i] = (char) cellEncoding[tmp & mask];
            }

            tmpCellIDStr[11] = (char) cellEncoding [m_CellID & flagMask];

            return new string(tmpCellIDStr);
        }


        /// <summary>
        /// Print out binary format of the Cell ID
        /// </summary>
        /// <returns></returns>
        public string ToStringBinary()
        {
            string numericString = string.Empty;
            byte[] bytes = BitConverter.GetBytes(this.m_CellID);

            for (int ctr = bytes.GetUpperBound(0); ctr >= bytes.GetLowerBound(0); ctr--)
            {
                string byteString = Convert.ToString(bytes[ctr], 2);
                byteString = new String('0', 8 - byteString.Length) + byteString;
                //numericString += byteString + " ";
                numericString += byteString;
            }
            string finalString = "";
            for (int i = 0; i < numericString.Length-3; i+=3 )
            {
                string sub = numericString.Substring(i, 3);
                finalString += sub + " ";
            }

            return finalString.Trim();
        }

        public bool isBorderCell()
        {
            //return ((this.iCellID & flagMask) == 1);
            return _isBorderCell;
        }

        public void setBorderCell()
        {
            //m_CellID = m_CellID | 0x1 ;     // set the last bit to 1
            _isBorderCell = true;
        }

        public void setInsideCell()
        {
            //m_CellID = m_CellID & 0x0 ;     // set the last bit to 0
            _isBorderCell = false;
        }

        public static CellID64 newChildCellId(CellID64 seed, int nodeId)
        {
            UInt64 cellidMask = 0xFFFFFFFFFFFFFF80;

            int level = getLevel(seed) + 1;
            UInt64 tmpCellid = seed.iCellID;

            // reset level and flag from the original/seed data
            tmpCellid = tmpCellid & cellidMask;
            tmpCellid = tmpCellid | ((UInt64)nodeId << (64 - level * 3));
            tmpCellid |= (UInt64) level << 1;
            CellID64 tmpCell = new CellID64(tmpCellid);

            return tmpCell;
        }

        public int Level
        {
            get { return (int)(m_CellID & levelMask) >> 1; }
        }

        public static int getLevel(CellID64 cell)
        {
            return (int)(cell.iCellID & levelMask) >> 1;
        }

        public static int getLevel(string cellIdStr)
        {
            CellID64 cellId = new CellID64(cellIdStr);
            return (int)(cellId.iCellID & levelMask) >> 1;
        }

        public static CellID64 parentCell(CellID64 cell)
        {
            UInt64 a = 0;
            for (int i = 0; i < CellID64.getLevel(cell)-1 ; i++)
            {
                a |= cell.iCellID & (UInt64)7 << (61 - i * 3);
            }
            a |= (UInt64)(CellID64.getLevel(cell) - 1) << 1;
            CellID64 pCell = new CellID64(a);
            return pCell;
        }

        public static List<Point3D> getCellIdxCorner(CellID64 cell)
        {
            List<Point3D> cornerLoc = new List<Point3D>();
            Point3D pos = new Point3D();
            int depth = CellID64.getLevel(cell);
            Vector3D tileSize = cellSize(depth);

            pos = getCellIdxLoc(cell);
            cornerLoc.Add(pos);
            Point3D pos2 = new Point3D(pos.X + tileSize.X, pos.Y + tileSize.Y, pos.Z + tileSize.Z);
            cornerLoc.Add(pos2);

            return cornerLoc;
        }

        public static Point3D getCellIdxLoc(CellID64 cell)
        {
            Point3D pos = new Point3D();
            int XOffset = 0;
            int YOffset = 0;
            int ZOffset = 0;
            int depth = CellID64.getLevel(cell);

            for (int i = 0; i < depth; i++)
            {
                int tmpOffset = (int)((cell.iCellID & (UInt64)0x1 << (61 - i * 3)) >> (61 - i*3 - (depth-1-i)));
                XOffset |= tmpOffset;
                tmpOffset = (int)((cell.iCellID & (UInt64)0x1 << (62 - i * 3)) >> (62 - i*3 - (depth-1-i)));
                YOffset |= tmpOffset;
                tmpOffset = (int)((cell.iCellID & (UInt64)0x1 << (63 - i * 3)) >> (63 - i*3 - (depth-1-i)));
                ZOffset |= tmpOffset;
            }
            Vector3D tileSize = cellSize(depth);
            pos.X = XOffset * tileSize.X + Octree.WorldBB.LLB.X;
            pos.Y = YOffset * tileSize.Y + Octree.WorldBB.LLB.Y;
            pos.Z = ZOffset * tileSize.Z + Octree.WorldBB.LLB.Z;
            return pos;
        }

        public static void getCellIDComponents(CellID64 cell, out int XMinComp, out int YMinComp, out int ZMinComp, out int XMaxComp, out int YMaxComp, out int ZMaxComp)
        {
            XMinComp = 0;
            YMinComp = 0;
            ZMinComp = 0;
            XMaxComp = 0;
            YMaxComp = 0;
            ZMaxComp = 0;
            int depth = CellID64.getLevel(cell);
            int maxDepth = 19;

            for (int i = 0; i < maxDepth; i++)
            {
                int tmpOffset = (int)((cell.iCellID & (UInt64)0x1 << (61 - i * 3)) >> (61 - i * 3 - (maxDepth - 1 - i)));
                XMinComp |= tmpOffset;
                tmpOffset = (int)((cell.iCellID & (UInt64)0x1 << (62 - i * 3)) >> (62 - i * 3 - (maxDepth - 1 - i)));
                YMinComp |= tmpOffset;
                tmpOffset = (int)((cell.iCellID & (UInt64)0x1 << (63 - i * 3)) >> (63 - i * 3 - (maxDepth - 1 - i)));
                ZMinComp |= tmpOffset;
            }
            // Get the max index by adding 1 (at depth) to get the next neighbor cell
            XMaxComp = XMinComp + (0x1 << (19 - depth));
            YMaxComp = YMinComp + (0x1 << (19 - depth));
            ZMaxComp = ZMinComp + (0x1 << (19 - depth));
        }

        public static CellID64 cellAtMaxDepth(Point3D point)
        {
            return cellAtDepth(point, Octree.MaxDepth);
        }

        public static CellID64 cellAtDepth(Point3D point, int depth)
        {
            Vector3D relPos = new Vector3D();
            
            // adjust point not to exceed the World BB boundaries. Note that the position MUST be a little inside the box
            if (point.X <= Octree.WorldBB.LLB.X)
                point.X = Octree.WorldBB.LLB.X + 0.000001;
            else if (point.X >= Octree.WorldBB.URT.X)
                point.X = Octree.WorldBB.URT.X - 0.000001;

            if (point.Y <= Octree.WorldBB.LLB.Y)
                point.Y = Octree.WorldBB.LLB.Y + 0.000001;
            else if (point.Y >= Octree.WorldBB.URT.Y)
                point.Y = Octree.WorldBB.URT.Y - 0.000001;

            if (point.Z <= Octree.WorldBB.LLB.Z)
                point.Z = Octree.WorldBB.LLB.Z + 0.000001;
            else if (point.Z >= Octree.WorldBB.URT.Z)
                point.Z = Octree.WorldBB.URT.Z - 0.000001;
            //

            relPos = point - Octree.WorldBB.LLB;
            Vector3D cellSizeAtDepth = cellSize(depth);
            int cellIdX = (int)Math.Floor(relPos.X / cellSizeAtDepth.X);
            int cellIdY = (int)Math.Floor(relPos.Y / cellSizeAtDepth.Y);
            int cellIdZ = (int)Math.Floor(relPos.Z / cellSizeAtDepth.Z);
            UInt64 iCellID = 0;
            for (int i = depth-1; i >= 0; i--)
            {
                UInt64 z = (UInt64)(cellIdZ & (1 << i)) << (63 - (depth-1 - i)*3 - i);
                UInt64 y = (UInt64)(cellIdY & (1 << i)) << (62 - (depth-1 - i)*3 - i);
                UInt64 x = (UInt64)(cellIdX & (1 << i)) << (61 - (depth-1 - i)*3 - i);
                UInt64 tmp = z | y | x;
                iCellID |= tmp;
            }
            iCellID |= (UInt64)depth << 1;

            CellID64 cellID = new CellID64(iCellID);
            return cellID;
        }

        public static Vector3D cellSize(int level)
        {
            Vector3D tile = new Vector3D();
            tile.X = Octree.WorldBB.XLength / (Math.Pow(2, level));
            tile.Y = Octree.WorldBB.YLength / (Math.Pow(2, level));
            tile.Z = Octree.WorldBB.ZLength / (Math.Pow(2, level));
            return tile;
        }
    }
}
