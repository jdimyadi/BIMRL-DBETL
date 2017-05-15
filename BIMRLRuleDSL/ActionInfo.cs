//
// BIMRL (BIM Rule Language) library: this library performs DSL for rule checking using BIM Rule Language that works on BIMRL Simplified Schema on RDBMS. 
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
using System.Data;
using System.Data.Common;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using BIMRL;
using Oracle.DataAccess.Client;

namespace BIMRLInterface
{
    public class ActionInfo
    {
        public enum ActionCommandEnum
        {
            PRINTACTION,
            DRAWACTION,
            SAVEINTOTABLE,
            SAVEINTOBCFFILE,
            NONE
        }

        struct BCFAction
        {
            public string FileInfo;
            public StreamWriter sW;
        }
        BCFAction BCFActionInfo;

        public string X3DFileName;
        public string TableName;
        public bool AppendToTable;
        public List<ActionCommandEnum> ActionCommandSequence;
        public string exportWhereCond;
        public bool highlightItemIsValue;
        public HashSet<string> highlightItems;
        public ColorSpec highlightColor;
        public ColorSpec userGeomColor;
        public string evalTabName;

        public ActionInfo()
        {
            X3DFileName = null ;
            BCFActionInfo = new BCFAction { FileInfo = null, sW = null };
            TableName = null;
            ActionCommandSequence = new List<ActionCommandEnum>();
            exportWhereCond = null;
            highlightItemIsValue = false;
            highlightItems = new HashSet<string>();
            highlightColor = new ColorSpec();
            userGeomColor = new ColorSpec();
            AppendToTable = false;
            evalTabName = null;
        }

        public string BCFFileName
        {
            get { return BCFActionInfo.FileInfo; }
            set { BCFActionInfo.FileInfo = value; }
        }

        public void addCommandSequence(ActionCommandEnum command)
        {
            ActionCommandSequence.Add(command);
        }

        //public string queryDataSqlStmt
        //{
        //    get
        //    {
        //        string whereCond = "";
        //        if (!string.IsNullOrEmpty(resultQkeywInj.whereClInjection))
        //            whereCond = " WHERE " + resultQkeywInj.whereClInjection + " ";

        //        string sqlStmt = "SELECT " + resultQkeywInj.colProjInjection + " FROM " + resultQkeywInj.tabProjInjection + whereCond + resultQkeywInj.suffixInjection;
        //        return sqlStmt;
        //    }
        //}

        public bool hasValue()
        {
            return (!string.IsNullOrEmpty(X3DFileName)
                    || !string.IsNullOrEmpty(BCFActionInfo.FileInfo)
                    || !string.IsNullOrEmpty(TableName)
                    || (ActionCommandSequence.Count > 0)
                    || !string.IsNullOrEmpty(exportWhereCond));
        }

        public void Clear()
        {
            X3DFileName = null;
            BCFActionInfo.FileInfo = null;
            if (BCFActionInfo.sW != null)
                BCFActionInfo.sW.Close();

            TableName = null;
            exportWhereCond = null;
            ActionCommandSequence.Clear();
        }

        public bool writeToBCFFile(DataTable inputRes)
        {
            // loop through the result and write BCF file
            return true;
        }

        public void saveIntoTable(DataTable inputRes)
        {
            DBQueryManager dbQ = new DBQueryManager();
            dbQ.createTableFromDataTable(TableName, inputRes, false, false);

            // BulkCopy does not support User Defined Datatype (UDT) and therefore cannot be used
            //OracleBulkCopy obc = new OracleBulkCopy(DBOperation.DBConn);
            //obc.DestinationTableName = TableName;
            //obc.WriteToServer(inputRes);
            //obc.Close();
        }
    }
}
