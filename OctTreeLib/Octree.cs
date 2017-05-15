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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;

namespace BIMRL.OctreeLib
{
    public sealed class Octree
    {
        static BoundingBox3D _WorldBB;
        static int _maxDepth = 6;
        //OctreeNode theTree;

        static Dictionary<UInt64, CellData> masterDict;
        static Dictionary<string, Int16> masterDictDB;

        // A Pair of Dict and List to allow fast access to the index tat will be stored into a celldata
        static Dictionary<Tuple<UInt64, UInt64>, int> elemIDDict;  // Keeping the list of element ids in a Dictionary for int value of an index in the List
        static List<Tuple<UInt64, UInt64>> elemIDList;
        
        List<string> candidates;
        Dictionary<UInt64, CellData> userDict;
        static int _ID = -1;
        bool _regen = false;
        public bool userDictKeepOriginalCell { get; set; }

        [Serializable]
        public struct CellData
        {
            public byte nodeType;   // node=0, leaf=1
            //public Dictionary<string, int> data;
            public SortedSet<int> data;
        }

        public enum OctreeCheck
        {
            NOTFOUND,
            NODEFOUND,
            FOUNDANCESTOR,
            FOUNDDESCENDANT
        }

        enum OctreeCellType
        {
            NODE = 0,
            LEAF = 1,
            LEAFWITHANCESTOR = 8,
            LEAFWITHDESCENDANT = 9
        }

        //private static readonly Lazy<Octree> lazy = new Lazy<Octree>(() => new Octree());

        //public static Octree Instance { get { return lazy.Value; } }

        //public Octree()
        //{
        //    theTree = new OctreeNode();
        //    theMasterTree = new OctreeNode();
        //}

        public Dictionary<UInt64, CellData> MasterDict
        {
            get { return masterDict; }
        }

        public List<Tuple<UInt64, UInt64>> ElemIDList
        {
            get { return elemIDList; }
        }

        public Octree(int ID, int? initDictNo, int? maxDepth)
        {
            int initDictSize = 100000;      // arbitrary default initial number for the dictionary
            int maxOctreeDepth = 3;         // initial max level following the UI setting

            if (initDictNo.HasValue)
                if (initDictNo.Value > 0)
                    initDictSize = initDictNo.Value * 10;    // reserve 10x of the initial number of records of elementid
            if (maxDepth.HasValue)
                if (maxDepth.Value > 0)
                    maxOctreeDepth = maxDepth.Value;

            init(ID, initDictSize, maxOctreeDepth, false, false);
        }

        public Octree(int ID, int? initDictNo, int? maxDepth, bool forUserDict)
        {
            int initDictSize = 100000;      // arbitrary default initial number for the dictionary
            int maxOctreeDepth = 3;         // initial max level following the UI setting

            if (initDictNo.HasValue)
                if (initDictNo.Value > 0)
                    initDictSize = initDictNo.Value * 10;
            if (maxDepth.HasValue)
                if (maxDepth.Value > 0)
                    maxOctreeDepth = maxDepth.Value;

            init(ID, initDictSize, maxOctreeDepth, forUserDict, false);
        }

        public Octree(int ID, int? initDictNo, int? maxDepth, bool forUserDict, bool skipRegenDict)
        {
            int initDictSize = 100000;      // arbitrary default initial number for the dictionary
            int maxOctreeDepth = 3;         // initial max level following the UI setting

            if (initDictNo.HasValue)
                if (initDictNo.Value > 0)
                    initDictSize = initDictNo.Value * 10;
            if (maxDepth.HasValue)
                if (maxDepth.Value > 0)
                    maxOctreeDepth = maxDepth.Value;

            init(ID, initDictSize, maxOctreeDepth, forUserDict, skipRegenDict);
        }

        void init(int ID, int initDictNo, int maxDepth, bool forUserDict, bool skipRegenDict)
        {
            if (_ID != ID || masterDict == null)
            {
                _ID = ID;
                if (!deSerializeMasterDict())
                {
                    // If not empty, claer first before reallocating a new ones
                    if (masterDict != null)
                        masterDict.Clear();
                    if (elemIDDict != null)
                        elemIDDict.Clear();
                    if (elemIDList != null)
                        elemIDList.Clear();

                    masterDict = new Dictionary<UInt64, CellData>(initDictNo);
                    elemIDDict = new Dictionary<Tuple<UInt64, UInt64>, int>(initDictNo);
                    elemIDList = new List<Tuple<UInt64, UInt64>>(initDictNo);

                    CellID64 cell = new CellID64("000000000000");
                    masterDict.Add(cell.iCellID, new CellData { nodeType = 0, data = new SortedSet<int>() });
                    if (!skipRegenDict)
                    {
                        regenSpatialIndexDict(ID, ref masterDict);
                        _regen = true;
                    }
                    //DBOperation.executeSingleStmt("TRUNCATE TABLE CELLTREEDETTMP");
                    //masterDictDB = new Dictionary<string, short>(initDictNo);
                    //masterDictDB.Add("000000000000", 0);
                    //if (!skipRegenDict)
                    //{
                    //    regenSpatialIndexDictDB(ID, ref masterDictDB);
                    //    _regen = true;
                    //}
                }
                _maxDepth = maxDepth;
                candidates = new List<string>();
            }
            if (forUserDict)
            {
                if (userDict != null)
                    userDict.Clear();

                userDict = new Dictionary<UInt64, CellData>(1000);
                userDictKeepOriginalCell = false;
            }
        }

        public static int MaxDepth
        {
            get { return _maxDepth; }
            set { _maxDepth = value; }
        }

        public static BoundingBox3D WorldBB
        {
            set { _WorldBB = value; }
            get { return _WorldBB; }
        }

        public static List<string> parentCellList(CellID64 cellID)
        {
            List<string> cidList = new List<string>();
            CellID64 pCell = cellID;
            for (int i = CellID64.getLevel(pCell); i > 0; i-- )
            {
                pCell = CellID64.parentCell(pCell);
                cidList.Add(pCell.ToString());
            }
            return cidList;
        }

        public static string parentsCellCondition(CellID64 cellID)
        {
            string tmpStr = "";
            CellID64 pCell = cellID;

            for (int i = CellID64.getLevel(cellID); i > 0; i--)
            {
                if (i < CellID64.getLevel(cellID))
                    tmpStr += " OR ";
                pCell = CellID64.parentCell(pCell);
                tmpStr += "(CELLID = '" + pCell.ToString() + "' OR CELLID = '";
                pCell.setBorderCell();
                tmpStr += pCell.ToString() + "')";
            }
            return tmpStr;
        }

        public static string childrenCellCondition(CellID64 cellID)
        {
            string cellIDStr = cellID.ToString();
            int usedCharIdx = (int) Math.Ceiling( (double) (CellID64.getLevel(cellID) / 2));
            string tmpStr = "(CELLID LIKE '" + cellIDStr.Substring(0, usedCharIdx) + "%' AND CELLID > '" + cellIDStr + "')";

            return tmpStr;
        }

        public void ComputeOctree(string elementID, Polyhedron polyH)
        {
            ComputeOctree(elementID, polyH, false);
        }

        /// <summary>
        /// Compute Octree for a Polyhedron
        /// </summary>
        /// <param name="elementID"></param>
        /// <param name="polyH"></param>
        /// <param name="forUserDict"></param>
        public void ComputeOctree(string elementID, Polyhedron polyH, bool forUserDict)
        {
            // Make sure ElementID string is 22 character long for correct encoding/decoding
            if (elementID.Length < 22)
                elementID = elementID.PadLeft(22, '0');

            ElementID eidNo = new ElementID(elementID);
            Tuple<UInt64, UInt64> elementIDNo = eidNo.ElementIDNo;

            OctreeNode theTree = new OctreeNode();
            // Do it in steps:
            // 1. Find the smallest containing cell based on the PolyH BB, it it to quickly eliminate the irrelevant cells very quickly
            theTree.nodeCellID = OctreeNodeProcess.getSmallestContainingCell(polyH);
            theTree._depth = theTree.nodeCellID.Level;

            // 2. Perform subdivision using the BB first: quick division since there is no expensive intersection. It leaves all the leaves based on BB
            OctreeNodeProcess.ProcessBB(theTree, polyH.boundingBox);

            // 3. Evaluate each leaf nodes for further subdivision using the actual polyhedron (the original algorithm)
            OctreeNodeProcess.Process(theTree, polyH);

            List<CellID64> collCellID;
            List<int> collBorderFlag;
            OctreeNodeProcess.collectCellIDs(theTree, out collCellID, out collBorderFlag);
            for (int i=0; i < collCellID.Count; ++i)
            {
                if (forUserDict)
                    insertDataToUserDict(elementIDNo, collCellID[i], collBorderFlag[i], false);
                else
                    //insertDataToDictDB(elementID, collCellID[i]);
                    insertDataToDict(elementIDNo, collCellID[i]);
            }
        }

        /// <summary>
        /// Compute Octree for a face
        /// </summary>
        /// <param name="elementID"></param>
        /// <param name="face"></param>
        /// <param name="forUserDict"></param>
        public void ComputeOctree(string elementID, Face3D face, bool forUserDict)
        {
            // Make sure ElementID string is 22 character long for correct encoding/decoding
            if (elementID.Length < 22)
                elementID = elementID.PadLeft(22, '0');

            ElementID eidNo = new ElementID(elementID);
            Tuple<UInt64, UInt64> elementIDNo = eidNo.ElementIDNo;

            OctreeNode theTree = new OctreeNode();
            
            // Add a step:
            // 1. Find the smallest containing cell based on the Face BB, it it to quickly eliminate the irrelevant cells very quickly
            theTree.nodeCellID = OctreeNodeProcess.getSmallestContainingCell(face);
            theTree._depth = theTree.nodeCellID.Level;

            OctreeNodeProcess.Process(theTree, face);
            List<CellID64> collCellID;
            List<int> collBorderFlag;
            OctreeNodeProcess.collectCellIDs(theTree, out collCellID, out collBorderFlag);
            for (int i = 0; i < collCellID.Count; ++i)
            {
                if (forUserDict)
                    insertDataToUserDict(elementIDNo, collCellID[i], collBorderFlag[i], false);
                else
                    //insertDataToDictDB(elementID, collCellID[i]);
                    insertDataToDict(elementIDNo, collCellID[i]);
            }
        }

        /// <summary>
        /// Compute Octree for a Line Segment
        /// </summary>
        /// <param name="elementID"></param>
        /// <param name="lineS"></param>
        /// <param name="forUserDict"></param>
        public void ComputeOctree(string elementID, LineSegment3D lineS, bool forUserDict)
        {
            // Make sure ElementID string is 22 character long for correct encoding/decoding
            if (elementID.Length < 22)
                elementID = elementID.PadLeft(22, '0');

            ElementID eidNo = new ElementID(elementID);
            Tuple<UInt64, UInt64> elementIDNo = eidNo.ElementIDNo;

            OctreeNode theTree = new OctreeNode();

            // Add a step:
            // 1. Find the smallest containing cell based on the Face BB, it it to quickly eliminate the irrelevant cells very quickly
            theTree.nodeCellID = OctreeNodeProcess.getSmallestContainingCell(lineS);
            theTree._depth = theTree.nodeCellID.Level;

            OctreeNodeProcess.Process(theTree, lineS);
            List<CellID64> collCellID;
            List<int> collBorderFlag;
            OctreeNodeProcess.collectCellIDs(theTree, out collCellID, out collBorderFlag);
            for (int i = 0; i < collCellID.Count; ++i)
            {
                if (forUserDict)
                    insertDataToUserDict(elementIDNo, collCellID[i], collBorderFlag[i], false);
                else
                    //insertDataToDictDB(elementID, collCellID[i]);
                    insertDataToDict(elementIDNo, collCellID[i]);
            }
        }

        void insertDataToDict(Tuple<UInt64, UInt64> elementID, CellID64 cellID)
        {
            CellData cellData;
            if (!masterDict.TryGetValue(cellID.iCellID, out cellData))
            {
                // no entry yet for this cell
                createCellInDict(elementID, cellID);
                masterDict.TryGetValue(cellID.iCellID, out cellData);
            }

            if (cellData.nodeType == 1)         //it's leaf, add the elementID
            {
                if (cellData.data == null)
                {
                    cellData.data = new SortedSet<int>();
                }

                cellData.data.Add(getIndexForElementID(elementID));

                //int flag;
                //if (!cellData.data.TryGetValue(elementID, out flag))
                //{
                //    cellData.data.Add(elementID, borderFlag);
                //}
                //else
                //{
                //    cellData.data[elementID] = borderFlag;
                //}
            }
            else   // it's a node, we must traverse down and add elementID to all the leaves. Not ideal to pass the same flag, but better than none
            {
                insertDataToDict(elementID, CellID64.newChildCellId(cellID, 0));
                insertDataToDict(elementID, CellID64.newChildCellId(cellID, 1));
                insertDataToDict(elementID, CellID64.newChildCellId(cellID, 2));
                insertDataToDict(elementID, CellID64.newChildCellId(cellID, 3));
                insertDataToDict(elementID, CellID64.newChildCellId(cellID, 4));
                insertDataToDict(elementID, CellID64.newChildCellId(cellID, 5));
                insertDataToDict(elementID, CellID64.newChildCellId(cellID, 6));
                insertDataToDict(elementID, CellID64.newChildCellId(cellID, 7));
            }
        }

        void createCellInDict(Tuple<UInt64, UInt64> elementID, CellID64 cellID)
        {
            CellID64 parentID = CellID64.parentCell(cellID);
            CellData cellData;
            if (!masterDict.TryGetValue(parentID.iCellID, out cellData))
            {
                createCellInDict(elementID, parentID);
                masterDict.TryGetValue(parentID.iCellID, out cellData);
            }

            // entry found, need to create all the entries for the children and transfer all the data into the new cells
            // remove the current elementid in the data first if present. It will be added later on
            if (cellData.data != null)
            {
                cellData.data.Remove(getIndexForElementID(elementID));
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 0).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 0).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>(cellData.data) });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 1).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 1).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>(cellData.data) });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 2).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 2).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>(cellData.data) });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 3).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 3).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>(cellData.data) });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 4).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 4).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>(cellData.data) });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 5).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 5).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>(cellData.data) });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 6).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 6).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>(cellData.data) });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 7).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 7).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>(cellData.data) });
                // reset cellData and set the nodeType to "node"
                cellData.data.Clear();
            }
            else
            {
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 0).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 0).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>() });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 1).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 1).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>() });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 2).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 2).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>() });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 3).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 3).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>() });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 4).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 4).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>() });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 5).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 5).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>() });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 6).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 6).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>() });
                if (!masterDict.ContainsKey(CellID64.newChildCellId(parentID, 7).iCellID))
                    masterDict.Add(CellID64.newChildCellId(parentID, 7).iCellID, new CellData { nodeType = 1, data = new SortedSet<int>() });
            }
            cellData.nodeType = 0;
            masterDict[parentID.iCellID] = cellData;
        }

        //static string findCellSQL = "SELECT CELLTYPE FROM CELLTREETMP WHERE CELLID=:cellid";
        //static OracleCommand findCellCmd = new OracleCommand(findCellSQL, DBOperation.DBConn);
        //static OracleParameter findCellPar = findCellCmd.Parameters.Add("cellid", OracleDbType.Varchar2);
        //static string findCellDetSQL = "SELECT ELEMENTID FROM CELLTREEDETTMP WHERE CELLID=:cellid";
        //static OracleCommand findCellDetCmd = new OracleCommand(findCellDetSQL, DBOperation.DBConn);
        //static OracleParameter findCellDetPar = findCellDetCmd.Parameters.Add("cellid", OracleDbType.Varchar2);
        //static string cellInsSQL = "INSERT INTO CELLTREETMP(CELLID, CELLTYPE) VALUES (:cid, :type)";
        //static OracleCommand cellInsCmd = new OracleCommand(cellInsSQL, DBOperation.DBConn);
        //static OracleParameter[] cellInsPars = new OracleParameter[2];
        //static string cellDetInsSQL = "INSERT INTO CELLTREEDETTMP(CELLID, ELEMENTID) VALUES (:cid, :eid)";
        //static OracleCommand cellDetInsCmd = new OracleCommand(cellDetInsSQL, DBOperation.DBConn);
        //static OracleParameter[] cellDetInsPars = new OracleParameter[2];
        static BIMRLCommon refCellBIMRLCommon = new BIMRLCommon();

        void insertDataToDictDB(string elementID, CellID64 cellID)
        {
            CellData cellData;
            cellData.nodeType = 1;
            string sqlStmt = null;

            short cellType;
            try
            {
                if (!masterDictDB.TryGetValue(cellID.ToString(), out cellType))
                {
                    // no entry yet for this cell
                    createCellInDictDB(elementID, cellID);
                    masterDictDB.TryGetValue(cellID.ToString(), out cellType);
                }

            //    if (!masterDict.TryGetValue(cellID.ToString(), out cellData))
            //{
            //    // no entry yet for this cell
            //    createCellInDict(elementID, cellID);
            //    masterDict.TryGetValue(cellID.ToString(), out cellData);
            //}

                if (cellType == 1)         //it's leaf, add the elementID
                {
                    DBOperation.executeSingleStmt("INSERT INTO CELLTREEDETTMP (CELLID,ELEMENTID) VALUES ('" + cellID.ToString() + "','" + elementID.ToString() + "')" );
                }
                else   // it's a node, we must traverse down and add elementID to all the leaves. Not ideal to pass the same flag, but better than none
                {
                    insertDataToDictDB(elementID, CellID64.newChildCellId(cellID, 0));
                    insertDataToDictDB(elementID, CellID64.newChildCellId(cellID, 1));
                    insertDataToDictDB(elementID, CellID64.newChildCellId(cellID, 2));
                    insertDataToDictDB(elementID, CellID64.newChildCellId(cellID, 3));
                    insertDataToDictDB(elementID, CellID64.newChildCellId(cellID, 4));
                    insertDataToDictDB(elementID, CellID64.newChildCellId(cellID, 5));
                    insertDataToDictDB(elementID, CellID64.newChildCellId(cellID, 6));
                    insertDataToDictDB(elementID, CellID64.newChildCellId(cellID, 7));
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
                refCellBIMRLCommon.StackPushError(excStr);
            }
        }

        void createCellInDictDB(string elementID, CellID64 cellID)
        {
            CellID64 parentID = CellID64.parentCell(cellID);
            string sqlStmt = null;

            try
            {
                //findCellPar.Direction = ParameterDirection.Input;
                //findCellPar.Value = parentID.ToString();
                //sqlStmt = findCellSQL;
                //object celltypeObj = findCellCmd.ExecuteScalar();
                //if (celltypeObj == null)
                short cellType;
                if (!masterDictDB.TryGetValue(parentID.ToString(), out cellType))
                {
                    createCellInDictDB(elementID, parentID);
                    masterDictDB.TryGetValue(parentID.ToString(), out cellType);
                    //celltypeObj = findCellCmd.ExecuteScalar();
                    //if (celltypeObj != null)
                    //    cellData.nodeType = int.Parse(celltypeObj.ToString());
                }

                // entry found, need to create all the entries for the children and transfer all the data into the new cells
                // remove the current elementid in the data first if present. It will be added later on
                //if (cellData.data != null)
                //cellData.data.Remove(elementID);
                DBOperation.executeSingleStmt("DELETE FROM CELLTREEDETTMP WHERE CELLID='" + parentID.ToString() + "' AND ELEMENTID='" + elementID.ToString() + "'");
                for (int i = 0; i < 8; ++i)
                {
                    //findCellPar.Value = cellIDIns;
                    //sqlStmt = findCellSQL;
                    //celltypeObj = findCellCmd.ExecuteScalar();
                    //if (celltypeObj == null)

                    string childID = CellID64.newChildCellId(parentID, i).ToString();
                    if (!masterDictDB.ContainsKey(childID))
                        masterDictDB.Add(childID, 1);

                    DBOperation.executeSingleStmt("INSERT INTO CELLTREEDETTMP (CELLID,ELEMENTID) SELECT '" + childID 
                        + "',ELEMENTID FROM CELLTREEDETTMP WHERE CELLID='" + parentID.ToString() +"'");
                }
                // reset cellData and set the nodeType to "node"
                //cellData.nodeType = 0;
                masterDictDB[parentID.ToString()] = 0;
                DBOperation.executeSingleStmt("DELETE FROM CELLTREEDETTMP WHERE CELLID='" + parentID.ToString() + "'");
                // DBOperation.executeSingleStmt("UPDATE CELLTREETMP SET CELLTYPE=0 WHERE CELLID='" + parentID.ToString() + "'");
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
                refCellBIMRLCommon.StackPushError(excStr);
            }
        }

        void insertDataToUserDict(Tuple<UInt64, UInt64> elementID, CellID64 cellID, int borderFlag, bool traverseDepth)
        {
            CellData cellData;
            List<UInt64> foundID;
            OctreeCheck retEnum = findNodeInDict(cellID.iCellID, traverseDepth, out foundID);

            if (retEnum == OctreeCheck.NOTFOUND)
            {
                // no entry yet for this cell
                //Dictionary<string, int> data = new Dictionary<string, int>();
                SortedSet<int> data = new SortedSet<int>();
                byte cellType = (byte)OctreeCellType.LEAF;
                //data.Add(elementID, borderFlag);     // borderflag is not used anymore
                data.Add(getIndexForElementID(elementID));     // borderflag is not used anymore
                cellData = new CellData { nodeType = cellType, data = data };
                userDict.Add(cellID.iCellID, cellData);
            }
            else if (retEnum == OctreeCheck.NODEFOUND)
            {
                cellData = masterDict[cellID.iCellID];
                if (cellData.nodeType == 1)         //it's leaf, add the elementID
                {
                    //Dictionary<string,int> iData = new Dictionary<string,int>();
                    SortedSet<int> iData = new SortedSet<int>();
                    byte cellType = (byte)OctreeCellType.LEAF;
                    //iData.Add(elementID, borderFlag);
                    iData.Add(getIndexForElementID(elementID));
                    CellData cdata;
                    if (!userDict.TryGetValue(cellID.iCellID, out cdata))
                    {
                        // entry is not found in the userdict yet
                        cdata = new CellData();
                        cdata.nodeType = cellType;
                        cdata.data = iData;
                        userDict.Add(cellID.iCellID, cdata);
                    }
                    else
                    {
                        if (cdata.data == null)
                        {
                            cdata.data = new SortedSet<int>();
                        }
                        cdata.data.Add(getIndexForElementID(elementID));
                        
                        //int flag;
                        //if (!cdata.data.TryGetValue(elementID, out flag))
                        //    cdata.data.Add(elementID, borderFlag);
                        //else
                        //    cdata.data[elementID] = borderFlag;
                    }
                }
                else   // it's a node, we must traverse down and add elementID to all the leaves. Not ideal to pass the same flag, but better than none
                {
                    insertDataToUserDict(elementID, CellID64.newChildCellId(cellID, 0), borderFlag, true);
                    insertDataToUserDict(elementID, CellID64.newChildCellId(cellID, 1), borderFlag, true);
                    insertDataToUserDict(elementID, CellID64.newChildCellId(cellID, 2), borderFlag, true);
                    insertDataToUserDict(elementID, CellID64.newChildCellId(cellID, 3), borderFlag, true);
                    insertDataToUserDict(elementID, CellID64.newChildCellId(cellID, 4), borderFlag, true);
                    insertDataToUserDict(elementID, CellID64.newChildCellId(cellID, 5), borderFlag, true);
                    insertDataToUserDict(elementID, CellID64.newChildCellId(cellID, 6), borderFlag, true);
                    insertDataToUserDict(elementID, CellID64.newChildCellId(cellID, 7), borderFlag, true);
                }
            }
            else if (retEnum == OctreeCheck.FOUNDANCESTOR || retEnum == OctreeCheck.FOUNDDESCENDANT)
            {
                // Add the current ID entry into the userDict
                //Dictionary<string, int> data = new Dictionary<string, int>();
                SortedSet<int> data = new SortedSet<int>();

                byte cellType = (byte) OctreeCellType.LEAF;
                if (retEnum == OctreeCheck.FOUNDANCESTOR)
                    cellType = (int) OctreeCellType.LEAFWITHANCESTOR;
                if (retEnum == OctreeCheck.FOUNDDESCENDANT)
                    cellType = (int) OctreeCellType.LEAFWITHDESCENDANT;

                if (userDictKeepOriginalCell)
                {
                    //data.Add(elementID, borderFlag);
                    data.Add(getIndexForElementID(elementID));
                    cellData = new CellData { nodeType = cellType, data = data };
                    userDict.Add(cellID.iCellID, cellData);
                }
                // Now loop through the Ancestor ID(s) found and create new entry(ies) in the userDict
                foreach (UInt64 id in foundID)
                {
                    CellData cData;
                    if (!userDict.TryGetValue(id, out cData))
                    {
                        //Dictionary<string, int> iData = new Dictionary<string, int>();
                        //iData.Add(elementID, borderFlag);
                        SortedSet<int> iData = new SortedSet<int>();
                        iData.Add(getIndexForElementID(elementID));
                        cData = new CellData { nodeType = (int)OctreeCellType.LEAF, data = iData };
                        userDict.Add(id, cData);
                    }
                    else
                    {
                        if (cData.data == null)
                        {
                            cData.data = new SortedSet<int>();
                        }
                        cData.data.Add(getIndexForElementID(elementID));
                        
                        //int flag;
                        //if (!cData.data.TryGetValue(elementID, out flag))
                        //    cData.data.Add(elementID, 1);
                        //else
                        //    cData.data[elementID] = 1;
                    }
                }
            }
        }

        OctreeCheck findNodeInDict(UInt64 nodeID, bool depthOnly, out List<UInt64> IDsFound)
        {
            IDsFound = new List<UInt64>();
            CellID64 cellid = new CellID64(nodeID);

            if (cellid.Level > MaxDepth)
                return OctreeCheck.NOTFOUND;

            CellData outData;
            if (masterDict.TryGetValue(nodeID, out outData))
            {
                IDsFound.Add(nodeID);
                return OctreeCheck.NODEFOUND;
            }

            // No node found at the exact location
            // 1. try to find ancestor (it is easier to get)

            bool found = false;
            if (!depthOnly)
            {
                while (!found)
                {
                    CellID64 parentcell = CellID64.parentCell(cellid);
                    if (parentcell.iCellID == 0)
                        break;      // reached root cell, it means not found

                    found = masterDict.TryGetValue(parentcell.iCellID, out outData);
                    if (found)
                        IDsFound.Add(parentcell.iCellID);
                    else
                        cellid = parentcell;
                }
            }
            // if still not found, search for the children
            if (found)
            {
                return OctreeCheck.FOUNDANCESTOR;
            }
            else
            {
                // Reset the cellid to the original cellid and not the overwritten one by "found ancestor" logic 
                cellid = new CellID64(nodeID);
                if (cellid.Level >= Octree.MaxDepth)
                    return OctreeCheck.NOTFOUND;

                // We will only search for descendants if it is not at the max depth since node at max depth definitely does not have any descendants
                List<UInt64> outID;
                // OctreeCheck ret = findDescendants(cellid, out outID);        // This version uses DB query
                OctreeCheck ret = findDescendantLeafNodesInDict(cellid, out outID);     // This version uses Dict search
                if (ret == OctreeCheck.FOUNDDESCENDANT)
                    IDsFound.AddRange(outID);

                return ret;
                //for (int i =0; i<8; ++i)
                //{
                //    List<string> outID;
                //    CellID64 childNode = CellID64.newChildCellId(cellid, i);
                //    OctreeCheck ret = findNodeInDict(childNode.ToString(), out outID);
                //    if (ret == OctreeCheck.NODEFOUND || ret == OctreeCheck.FOUNDDESCENDANT)
                //    {
                //        foundSomeNode |= true;
                //        IDsFound.AddRange(outID);
                //    }
                //}
                //if (foundSomeNode)
                //{
                //    return OctreeCheck.FOUNDDESCENDANT;
                //}
            }

            //return OctreeCheck.NOTFOUND;
        }

        OctreeCheck findDescendantLeafNodesInDict(CellID64 cellid, out List<UInt64> outIDs)
        {
            outIDs = new List<UInt64>();

            if (cellid.Level >= MaxDepth)
                return OctreeCheck.NOTFOUND;

            for (int i = 0; i < 8; ++i)
            {
                CellID64 childID = CellID64.newChildCellId(cellid, i);
                CellData cData;
                bool found = masterDict.TryGetValue(childID.iCellID, out cData);
                if (found)
                {
                    if (cData.nodeType == (int)OctreeCellType.LEAF)
                    {
                        outIDs.Add(childID.iCellID);
                    }
                    else if (cData.nodeType == (byte)OctreeCellType.NODE)
                    {
                        List<UInt64> outList;
                        OctreeCheck ret = findDescendantLeafNodesInDict(childID, out outList);
                        if (outList.Count > 0)
                            outIDs.AddRange(outList);
                    }
                }
            }
            if (outIDs.Count > 0)
                return OctreeCheck.FOUNDDESCENDANT;
            else
                return OctreeCheck.NOTFOUND;
        }

        /// <summary>
        /// Collect ALL the cellids from the master dictionary
        /// </summary>
        /// <param name="elementIDList"></param>
        /// <param name="cellIDStrList"></param>
        /// <param name="borderFlagList"></param>
        //public void collectSpatialIndex(out List<string> elementIDList, out List<string> cellIDStrList, out List<int> XMinB, out List<int> YMinB, out List<int> ZMinB,
        //                                out List<int> XMaxB, out List<int> YMaxB, out List<int>ZMaxB, out List<int> depthList)
        //{
        //    int initArraySize = masterDict.Count;   // estimated initial size of the list to hold the array data
        //    elementIDList = new List<string>(initArraySize);
        //    cellIDStrList = new List<string>(initArraySize);

        //    XMinB = new List<int>(initArraySize);
        //    YMinB = new List<int>(initArraySize);
        //    ZMinB = new List<int>(initArraySize);
        //    XMaxB = new List<int>(initArraySize);
        //    YMaxB = new List<int>(initArraySize);
        //    ZMaxB = new List<int>(initArraySize);
        //    depthList = new List<int>(initArraySize);

        //    int XMin;
        //    int YMin;
        //    int ZMin;
        //    int XMax;
        //    int YMax;
        //    int ZMax;

        //    foreach (KeyValuePair<UInt64, CellData> dictEntry in masterDict)
        //    {
        //        CellID64 cellID = new CellID64(dictEntry.Key);
        //        string cellIDstr = cellID.ToString();
        //        CellID64.getCellIDComponents(cellID, out XMin, out YMin, out ZMin, out XMax, out YMax, out ZMax);
        //        int cellLevel = CellID64.getLevel(cellID);

        //        if (dictEntry.Value.data != null && dictEntry.Value.nodeType != 0)
        //        {
        //            foreach (int tupEID in dictEntry.Value.data)
        //            {
        //                List<int> cBound = new List<int>();

        //                ElementID eID = new ElementID(getElementIDByIndex(tupEID));
        //                elementIDList.Add(eID.ElementIDString);
        //                //cellIDStrList.Add(cellID.ToString());
        //                cellIDStrList.Add(cellIDstr);

        //                //CellID64.getCellIDComponents(cellID, out XMin, out YMin, out ZMin, out XMax, out YMax, out ZMax);
        //                XMinB.Add(XMin);
        //                YMinB.Add(YMin);
        //                ZMinB.Add(ZMin);
        //                XMaxB.Add(XMax);
        //                YMaxB.Add(YMax);
        //                ZMaxB.Add(ZMax);
        //                //depthList.Add(CellID64.getLevel(cellID));
        //                depthList.Add(cellLevel);
        //            }
        //        }
        //    }
        //    //serializeMasterDict();
        //}

        //public void collectSpatialIndexDB(out List<string> elementIDList, out List<string> cellIDStrList, out List<int> XMinB, out List<int> YMinB, out List<int> ZMinB,
        //                        out List<int> XMaxB, out List<int> YMaxB, out List<int> ZMaxB, out List<int> depthList)
        //{
        //    int initArraySize = masterDictDB.Count;   // estimated initial size of the list to hold the array data
        //    elementIDList = new List<string>(initArraySize);
        //    cellIDStrList = new List<string>(initArraySize);

        //    XMinB = new List<int>(initArraySize);
        //    YMinB = new List<int>(initArraySize);
        //    ZMinB = new List<int>(initArraySize);
        //    XMaxB = new List<int>(initArraySize);
        //    YMaxB = new List<int>(initArraySize);
        //    ZMaxB = new List<int>(initArraySize);
        //    depthList = new List<int>(initArraySize);

        //    int XMin;
        //    int YMin;
        //    int ZMin;
        //    int XMax;
        //    int YMax;
        //    int ZMax;

        //    OracleCommand cmd = new OracleCommand("SELECT CELLID, ELEMENTID FROM CELLTREEDETTMP", DBOperation.DBConn);
        //    OracleDataReader reader = cmd.ExecuteReader();
        //    while (reader.Read())
        //    {
        //        string cellidStr = reader.GetString(0);
        //        string eidStr = reader.GetString(1);

        //        CellID64 cellID = new CellID64(cellidStr);
        //        elementIDList.Add(eidStr);
        //        cellIDStrList.Add(cellidStr);

        //        CellID64.getCellIDComponents(cellID, out XMin, out YMin, out ZMin, out XMax, out YMax, out ZMax);
        //        XMinB.Add(XMin);
        //        YMinB.Add(YMin);
        //        ZMinB.Add(ZMin);
        //        XMaxB.Add(XMax);
        //        YMaxB.Add(YMax);
        //        ZMaxB.Add(ZMax);
        //        depthList.Add(CellID64.getLevel(cellID));
        //    }
        //    reader.Dispose();
        //    cmd.Dispose();
        //}

        void serializeMasterDict()
        {
            return;

            //// We will serialize this Dictionary into a binary file so that we can deseriaize again when we need this information back, e.g. generating the user dict
            //// Default file name will be "C:\ProgramData\BIMRL\MasterOctree_<Fed Model ID in Hex>.bin"
            //String serFileName = "C:\\ProgramData\\BIMRL\\MasterOctree_" + _ID.ToString("X4") + ".bin";
            //if (File.Exists(serFileName))
            //    File.Delete(serFileName);

            //FileStream fs = new FileStream(serFileName, FileMode.Create);

            //// Construct a BinaryFormatter and use it to serialize the data to the stream.
            //BinaryFormatter formatter = new BinaryFormatter();
            //try
            //{
            //    formatter.Serialize(fs, masterDict);
            //}
            //catch (SerializationException e)
            //{
            //    Console.WriteLine("Failed to serialize. Reason: " + e.Message);
            //    File.Delete(serFileName);
            //    throw;
            //}
            //finally
            //{
            //    fs.Close();
            //}
        }

        bool deSerializeMasterDict()
        {
            return false;
            // Serialize still has problem for a large size because system complains of Int32 max value has been exceeded
            //String serFileName = "C:\\ProgramData\\BIMRL\\MasterOctree_" + _ID.ToString("X4") + ".bin";
            //if (!File.Exists(serFileName))
            //    return false;           // File does not exist, must build the Dict from the table instead

            //FileStream fs = new FileStream(serFileName, FileMode.Open);
            //try
            //{
            //    BinaryFormatter formatter = new BinaryFormatter();

            //    // Deserialize the hashtable from the file and  
            //    // assign the reference to the local variable.
            //    masterDict = (Dictionary<string, CellData>)formatter.Deserialize(fs);
            //}
            //catch (SerializationException e)
            //{
            //    Console.WriteLine("Failed to deserialize. Reason: " + e.Message);
            //    throw;
            //}
            //finally
            //{
            //    fs.Close();
            //}

            //return true;
        }

        /// <summary>
        /// Collect ALL the cellids from userDict for populating transient geometry(ies)
        /// </summary>
        /// <param name="elementIDList"></param>
        /// <param name="cellIDStrList"></param>
        /// <param name="borderFlagList"></param>
        public void collectSpatialIndexUserDict(out List<string> elementIDList, out List<string> cellIDStrList, out List<int> XMinB, out List<int> YMinB, out List<int> ZMinB, 
                                                out List<int> XMaxB, out List<int> YMaxB, out List<int> ZMaxB, out List<int> depthList, out List<int> cellType)
        {
            int initArraySize = 50000;

            elementIDList = new List<string>(initArraySize);
            cellIDStrList = new List<string>(initArraySize);
            cellType = new List<int>(initArraySize);
            XMinB = new List<int>(initArraySize);
            YMinB = new List<int>(initArraySize);
            ZMinB = new List<int>(initArraySize);
            XMaxB = new List<int>(initArraySize);
            YMaxB = new List<int>(initArraySize);
            ZMaxB = new List<int>(initArraySize);
            depthList = new List<int>(initArraySize);

            int XMin;
            int YMin;
            int ZMin;
            int XMax;
            int YMax;
            int ZMax;

            foreach (KeyValuePair<UInt64, CellData> dictEntry in userDict)
            {
                CellID64 cellID = new CellID64(dictEntry.Key);
                if (dictEntry.Value.data != null)
                {
                    foreach (int tupEID in dictEntry.Value.data)
                    {
                        ElementID eID = new ElementID(getElementIDByIndex(tupEID));
                        
                        // For UserGeom, the ID i s generated from string of a simple number padded left with '0'. Now we need to remove them
                        int end0Pos = 0;
                        for (int i = 0; i < eID.ElementIDString.Length; ++i)
                        {
                            if (eID.ElementIDString[i] != '0')
                            {
                                end0Pos = i;
                                break;
                            }
                        }
                        string userGeomID = eID.ElementIDString.Remove(0, end0Pos);

                        elementIDList.Add(userGeomID);
                        cellIDStrList.Add(cellID.ToString());
                        // cellType.Add(eID.Value); 
                        cellType.Add(dictEntry.Value.nodeType);

                        CellID64.getCellIDComponents(cellID, out XMin, out YMin, out ZMin, out XMax, out YMax, out ZMax);
                        XMinB.Add(XMin);
                        YMinB.Add(YMin);
                        ZMinB.Add(ZMin);
                        XMaxB.Add(XMax);
                        YMaxB.Add(YMax);
                        ZMaxB.Add(ZMax);
                        depthList.Add(CellID64.getLevel(cellID));
                    }
                }
            }
        }

        OctreeCheck findDescendants(CellID64 cellid, out List<UInt64> IDsFound)
        {
            OctreeCheck ret = getCellDescendants(cellid, _ID, out IDsFound);
            return ret;
        }

        //public void collectLines(out List<double> coordList, out List<int> lineIndex)
        //{
        //    coordList = new List<double>();
        //    lineIndex = new List<int>();

        //    List<Point3D> coords;
        //    theTree.collecLines(out coords, out lineIndex);
        //    foreach (Point3D pt in coords)
        //    {
        //        coordList.Add(pt.X);
        //        coordList.Add(pt.Y);
        //        coordList.Add(pt.Z);
        //    }
        //}

        public static void regenSpatialIndexDict(int fedID, ref Dictionary<UInt64, Octree.CellData> regenSpIndexTree)
        {
            BIMRLCommon refBIMRLCommon = new BIMRLCommon();
            string sqlStmt = "SELECT ELEMENTID, CELLID FROM BIMRL_SPATIALINDEX_" + fedID.ToString("X4");
            try
            {
                OracleCommand cmd = new OracleCommand(sqlStmt, DBOperation.DBConn);
                cmd.FetchSize = 100000;
                OracleDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string elementid = reader.GetString(0);
                    ElementID eID = new ElementID(elementid);
                    string cellid = reader.GetString(1);
                    CellID64 cell = new CellID64(cellid);
                    if (!regenSpIndexTree.ContainsKey(cell.iCellID))
                    {
                        //Dictionary<string, int> cData = new Dictionary<string, int>();
                        //cData.Add(elementid, 0);    // the flag is no longer used, any value doesn't matter
                        SortedSet<int> cData = new SortedSet<int>();
                        cData.Add(getIndexForElementID(eID.ElementIDNo));    // the flag is no longer used, any value doesn't matter
                        Octree.CellData data = new Octree.CellData { nodeType = 1, data = cData };
                        regenSpIndexTree.Add(cell.iCellID, data);
                    }
                    else
                    {
                        Octree.CellData data = regenSpIndexTree[cell.iCellID];
                        //Dictionary<string, int> cData = data.data;
                        //cData.Add(elementid, 0);
                        SortedSet<int> cData = data.data;
                        cData.Add(getIndexForElementID(eID.ElementIDNo));
                    }
                }
                reader.Dispose();
                cmd.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
                refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
                refBIMRLCommon.StackPushError(excStr);
                throw;
            }
        }

        //public static void regenSpatialIndexDictDB(int fedID, ref Dictionary<string, short> regenSpIndexTree)
        //{
        //    BIMRLCommon refBIMRLCommon = new BIMRLCommon();
        //    List<string> cellIDList = new List<string>();
        //    List<string> eidList = new List<string>();

        //    string sqlStmt = "SELECT ELEMENTID, CELLID FROM BIMRL_SPATIALINDEX_" + fedID.ToString("X4");
        //    try
        //    {
        //        OracleCommand cmd = new OracleCommand(sqlStmt, DBOperation.DBConn);
        //        cmd.FetchSize = 1000;
        //        OracleDataReader reader = cmd.ExecuteReader();
        //        while (reader.Read())
        //        {
        //            string elementid = reader.GetString(0);
        //            string cellid = reader.GetString(1);
        //            if (!regenSpIndexTree.ContainsKey(cellid))
        //            {
        //                regenSpIndexTree.Add(cellid, 1);    // Only LEAF nodes are stored in the DB table
        //            }
        //            cellIDList.Add(cellid);
        //            eidList.Add(elementid);
        //        }
        //        reader.Dispose();
        //        cmd.Dispose();

        //        cmd.CommandText = "INSERT INTO CELLTREEDETTMP (CELLID, ELEMENTID) VALUES (:cid, :eid)";
        //        OracleParameter[] cellDetInsPars = new OracleParameter[2];
        //        cellDetInsPars[0] = cmd.Parameters.Add("cid", OracleDbType.Varchar2);
        //        cellDetInsPars[0].Direction = ParameterDirection.Input;
        //        cellDetInsPars[1] = cmd.Parameters.Add("cid", OracleDbType.Varchar2);
        //        cellDetInsPars[1].Direction = ParameterDirection.Input;
        //        int insertCnt = 5000;
        //        while (cellIDList.Count > 0)
        //        {
        //            if (cellIDList.Count < insertCnt)
        //                insertCnt = cellIDList.Count;
        //            cellDetInsPars[0].Value = cellIDList.GetRange(0, insertCnt).ToArray();
        //            cellDetInsPars[1].Value = eidList.GetRange(0, insertCnt).ToArray();
        //            cmd.ArrayBindCount = insertCnt;

        //            cmd.ExecuteNonQuery();

        //            cellIDList.RemoveRange(0, insertCnt);
        //            eidList.RemoveRange(0, insertCnt);
        //        }
        //    }
        //    catch (OracleException e)
        //    {
        //        string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
        //        refBIMRLCommon.StackPushError(excStr);
        //    }
        //    catch (SystemException e)
        //    {
        //        string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
        //        refBIMRLCommon.StackPushError(excStr);
        //        throw;
        //    }
        //}

        public static Octree.OctreeCheck getCellDescendants(CellID64 cellid, int fedID, out List<UInt64> IDsFound)
        {
            IDsFound = new List<UInt64>();
            Octree.OctreeCheck ret;

            string whereCond = Octree.childrenCellCondition(cellid);
            string sqlStmt = "SELECT UNIQUE CELLID FROM BIMRL_SPATIALINDEX_" + fedID.ToString("X4") + " WHERE " + whereCond;
            DataTable dt = new DataTable();
            OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
            OracleDataAdapter dtAdapter = new OracleDataAdapter(command);
            dtAdapter.Fill(dt);
            if (dt.Rows.Count > 0)
                ret = Octree.OctreeCheck.FOUNDDESCENDANT;
            else
                ret = Octree.OctreeCheck.NOTFOUND;

            foreach (DataRow dtRow in dt.Rows)
            {
                CellID64 cell = new CellID64(dtRow["CELLID"].ToString());
                IDsFound.Add(cell.iCellID);
            }

            return ret;
        }

        public static int getIndexForElementID (Tuple<UInt64,UInt64> elemID)
        {
            int theIdx;
            if (!elemIDDict.TryGetValue(elemID, out theIdx))
            {
                theIdx = elemIDList.Count;
                elemIDDict.Add(elemID, theIdx);
                elemIDList.Add(elemID);
            }
            return theIdx;
        }

        public static Tuple<UInt64,UInt64> getElementIDByIndex(int theIdx)
        {
            return elemIDList[theIdx];
        }
    }
}

