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
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL;
using BIMRL.OctreeLib;

namespace BIMRLInterface
{
    public class DBQueryManager
    {
        BIMRLCommon m_BIMRLCommonRef = new BIMRLCommon();
        string m_SqlStmt = null;
        public static int FedModelID;
        public static string errorMsg;

        public DBQueryManager()
        {
            connectDB();
        }

        bool connectDB()
        {
            // Connect to Oracle DB
            DBOperation.refBIMRLCommon = m_BIMRLCommonRef;      // important to ensure DBoperation has reference to this object!!
            if (DBOperation.Connect() == null)
            {
               if (DBOperation.UIMode)
               {
                  BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLCommonRef);
                  erroDlg.ShowDialog();
               }
               else
                  Console.Write(m_BIMRLCommonRef.ErrorMessages);
               return false;
            }
            DBOperation.beginTransaction();
            return true;
        }

        public DataTable querySingleRow(string sqlStmt)
        {
            if (String.IsNullOrEmpty(sqlStmt))
                return null;

            m_SqlStmt = BIMRLKeywordMapping.expandBIMRLTables(sqlStmt);     // expand BIMRL tables inside the sql statement

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            try
            {
                command.CommandText = m_SqlStmt;
                OracleDataAdapter qAdapter = new OracleDataAdapter(command);
                DataTable qResult = new DataTable();
                qAdapter.FillSchema(qResult, SchemaType.Source);
                qAdapter.Fill(qResult);
                return qResult;
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n" + command.CommandText;
                m_BIMRLCommonRef.StackPushError(excStr);
               if (DBOperation.UIMode)
               {
                  BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLCommonRef);
                  erroDlg.ShowDialog();
               }
               else
                  Console.Write(m_BIMRLCommonRef.ErrorMessages);
               command.Dispose();
                return null;
            }
        }

        public DataTable queryMultipleRows(string sqlStmt)
        {
            if (String.IsNullOrEmpty(sqlStmt))
                return null;

            m_SqlStmt = BIMRLKeywordMapping.expandBIMRLTables(sqlStmt);     // expand BIMRL tables inside the sql statement

            DataTable queryDataTableBuffer = new DataTable();

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

            // TODO!!!!! This one still gives mysterious error if the "alias".* on BIMRLEP$<var> has different column list in previous statement
            // The * seems to "remember" the earlier one. If the number of columns are shorter than the previous one, it will throw OracleException for the "missing"/unrecognized column name
            try
            {
               command.CommandText = m_SqlStmt;
               OracleDataReader reader = command.ExecuteReader();
               queryDataTableBuffer.Load(reader);

               //OracleDataAdapter qAdapter = new OracleDataAdapter(command);

               //qAdapter.Fill(queryDataTableBuffer);
               //command.Dispose();
               //qAdapter.Dispose();

            }
            catch (OracleException e)
            {
               string excStr = "%%Error - " + e.Message + "\n" + command.CommandText;
               m_BIMRLCommonRef.StackPushError(excStr);
               if (DBOperation.UIMode)
               {
                  BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLCommonRef);
                  erroDlg.ShowDialog();
               }
               else
                  Console.Write(m_BIMRLCommonRef.ErrorMessages);
         }

            command.Dispose();
            return queryDataTableBuffer;
        }

        public int queryIntoTable(string tableName, string sqlStmt, bool tempTableOption)
        {
            int retCount = -1; 
            
            if (String.IsNullOrEmpty(m_SqlStmt) && String.IsNullOrEmpty(sqlStmt))
                return retCount;

            string globalTemp = "";
            string preserveRows = "";
            if (tempTableOption)
            {
                globalTemp = "GLOBAL TEMPORARY";
                preserveRows = "ON COMMIT PRESERVE ROWS";
            }

            string sqlStmt2;
            if (!string.IsNullOrEmpty(sqlStmt))
                sqlStmt2 = BIMRLKeywordMapping.expandBIMRLTables(sqlStmt);
            else
                sqlStmt2 = BIMRLKeywordMapping.expandBIMRLTables(m_SqlStmt);     // expand BIMRL tables inside the sql statement

            sqlStmt2 = "CREATE " + globalTemp + " TABLE " + tableName + " " + preserveRows + " AS " + sqlStmt2;
            string sqlCheckRow = "SELECT COUNT(*) FROM " + tableName;
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            try
            {
                command.CommandText = "TRUNCATE TABLE " + tableName + " CASCADE";
                command.ExecuteNonQuery();
                command.CommandText = "DROP TABLE " + tableName + " CASCADE CONSTRAINTS";
                command.ExecuteNonQuery();
            }
            catch (OracleException e)
            {
                string excStr = "%%Information - " + e.Message + "\n" + command.CommandText;
                m_BIMRLCommonRef.StackPushIgnorableError(excStr);
            }

            try
            {
                command.CommandText = sqlStmt2;
                command.ExecuteNonQuery();
                command.CommandText = sqlCheckRow;
                object cnt = command.ExecuteScalar();
                if (cnt != null)
                    int.TryParse(cnt.ToString(), out retCount);
                command.Dispose();
                return retCount;
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n" + command.CommandText;
                m_BIMRLCommonRef.StackPushError(excStr);
                errorMsg = m_BIMRLCommonRef.ErrorMessages;
                command.Dispose();
                return retCount;
            }
        }

        public int runNonQuery(string sqlStmt)
        {
            // default is not to ignore error
            return runNonQuery(sqlStmt, false);
        }

        public int runNonQuery(string sqlStmt, bool ignoreError)
        {
            if (String.IsNullOrEmpty(sqlStmt))
                return 0;

            m_SqlStmt = BIMRLKeywordMapping.expandBIMRLTables(sqlStmt);     // expand BIMRL tables inside the sql statement

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            try
            {
                command.CommandText = m_SqlStmt;

                int status = command.ExecuteNonQuery();
                return status;
            }
            catch (OracleException e)
            {
                if (ignoreError)
                {
                    command.Dispose();
                    return 0;
                }
                string excStr = "%%Error - " + e.Message + "\n" + command.CommandText;
                m_BIMRLCommonRef.StackPushError(excStr);
                errorMsg = m_BIMRLCommonRef.ErrorMessages;
                command.Dispose();
                return 0;
            }
        }

        public bool insertFromDataTable(string tableName, DataTable sourceDT)
        {
            // WARNING: BulkCopy does not work with UDT (e.g. SdoGeometry)
            using (OracleBulkCopy bulkCopy = new OracleBulkCopy(DBOperation.DBConn))
            {
                bulkCopy.BatchSize = 100;
                bulkCopy.DestinationTableName = tableName;
                try
                {
                    bulkCopy.WriteToServer(sourceDT);
                }
                catch (OracleException e)
                {
                    string excStr = "%%Error - " + e.Message + "\n" + "Inserting into " + tableName;
                    m_BIMRLCommonRef.StackPushError(excStr);
                     if (DBOperation.UIMode)
                     {
                        BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLCommonRef);
                        erroDlg.ShowDialog();
                     }
                     else
                        Console.Write(m_BIMRLCommonRef.ErrorMessages);
                     return false;
                }
            }
            return true;
        }


        public int createTableFromDataTable(string tableName, DataTable sourceDT, bool appendMode, bool tempTable)
        {
            int stat;
            string sqlStr = null;

            if (sourceDT == null)
                return -1;

            OracleCommand command = new OracleCommand("", DBOperation.DBConn);

            // Get the primary key if any

            string keyCol = null;
            foreach (DataColumn pKey in sourceDT.PrimaryKey)
            {
                BIMRLInterfaceCommon.appendToString(pKey.ColumnName, ", ", ref keyCol);
            }

            OracleParameter[] pars = new OracleParameter[sourceDT.Columns.Count];

            string colProj = "";
            string valProj = "";
            int i = 0;
            foreach (DataColumn col in sourceDT.Columns)
            {
                string oraType = null;
                string colName = col.ColumnName;
                int colSize = col.MaxLength;
                if (colSize <= 0)
                    colSize = 256;  // set to max 256 if not defined

                Type coltype = col.DataType;
                switch (coltype.ToString())
                {
                    case "System.String":
                        oraType = "VARCHAR2(" + colSize.ToString() + ")";
                        pars[i] = command.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
                        break;
                    case "System.Decimal":
                        oraType = "NUMBER";
                        pars[i] = command.Parameters.Add(i.ToString(), OracleDbType.Decimal);
                        break;
                    case "System.Double":
                        oraType = "FLOAT";
                        pars[i] = command.Parameters.Add(i.ToString(), OracleDbType.Double);
                        break;
                    case "System.Int64":
                        oraType = "INTEGER";
                        pars[i] = command.Parameters.Add(i.ToString(), OracleDbType.Int64);
                        break;
                    case "System.Int32":
                        oraType = "INTEGER";
                        pars[i] = command.Parameters.Add(i.ToString(), OracleDbType.Int32);
                        break;
                    case "System.Int16":
                        oraType = "INTEGER";
                        pars[i] = command.Parameters.Add(i.ToString(), OracleDbType.Int16);
                        break;
                    case "System.DateTime":
                        oraType = "DATE";
                        pars[i] = command.Parameters.Add(i.ToString(), OracleDbType.Date);
                        break;
                    case "NetSdoGeometry.SdoGeometry":
                        oraType = "SDO_GEOMETRY";
                        pars[i] = command.Parameters.Add(i.ToString(), OracleDbType.Object);
                        pars[i].UdtTypeName = "MDSYS.SDO_GEOMETRY";
                        break;
                    default:
                        throw new BIMRLInterfaceRuntimeException("%Error: " + coltype.ToString() + " not implemented!");
                }

                pars[i].Direction = ParameterDirection.Input;

                string colStr = colName + " " + oraType;
                if (!col.AllowDBNull)
                    colStr += " NOT NULL";
                BIMRLInterfaceCommon.appendToString(colName, ", ", ref colProj);
                BIMRLInterfaceCommon.appendToString(":" + i.ToString(), ", ", ref valProj);
                BIMRLInterfaceCommon.appendToString(colStr, ", ", ref sqlStr);
                i++;
            }
            if (!string.IsNullOrEmpty(keyCol))
                BIMRLInterfaceCommon.appendToString("PRIMARY KEY (" + keyCol + ")", ", ", ref sqlStr);

            if (!appendMode)
            {
                // Drop table first since table structure will not be the same for different statement
                runNonQuery("TRUNCATE TABLE " + tableName + " CASCADE", true);
                runNonQuery("DROP TABLE " + tableName + " CASCADE CONSTRAINTS", true);
                string preTmp = "";
                string postTmp = "";
                if (tempTable)
                {
                    preTmp = "GLOBAL TEMPORARY";
                    postTmp = "ON COMMIT PRESERVE ROWS";
                }
                sqlStr = "CREATE " + preTmp + " TABLE " + tableName + "( " + sqlStr + " ) " + postTmp;
                stat = runNonQuery(sqlStr, true);
            }

            command.CommandText = "INSERT INTO " + tableName + " (" + colProj + ") VALUES (" + valProj + ")";
            foreach (DataRow rdata in sourceDT.Rows)
            {
                for(int j = 0; j<sourceDT.Columns.Count; ++j)
                {
                    pars[j].Value = rdata[j];
                    // pars[j].Size = 1;
                    if (rdata.IsNull(j))
                        pars[j].Status = OracleParameterStatus.NullInsert;
                    else
                        pars[j].Status = OracleParameterStatus.Success;
                }
                try
                {
                    command.ExecuteNonQuery();
                }
                catch (OracleException e)
                {
                    string excStr = "%%Insert Error - " + e.Message + "\n\t" + command.CommandText;
                    m_BIMRLCommonRef.StackPushIgnorableError(excStr);
                    errorMsg = m_BIMRLCommonRef.ErrorMessages;
                    // Ignore any error
                }
                catch (SystemException e)
                {
                    string excStr = "%%Insert Error - " + e.Message + "\n\t" + command.CommandText;
                    m_BIMRLCommonRef.StackPushError(excStr);
                    errorMsg = m_BIMRLCommonRef.ErrorMessages;
                    throw;
                }
            }
            DBOperation.commitTransaction();

            return sourceDT.Rows.Count; 
        }
    }
}
