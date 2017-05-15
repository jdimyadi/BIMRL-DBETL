﻿//
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using IErrorNode = Antlr4.Runtime.Tree.IErrorNode;
using ITerminalNode = Antlr4.Runtime.Tree.ITerminalNode;
using IToken = Antlr4.Runtime.IToken;
using ParserRuleContext = Antlr4.Runtime.ParserRuleContext;
using BIMRL;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using BIMRL.OctreeLib;

namespace BIMRLInterface
{
    public struct ElementSet
    {
        public string Name { get; set; }
        public int noRecord { get; set; }
    }

    public class DefinedElementSetManager
    {
        static BIMRLCommon m_BIMRLCommonRef = new BIMRLCommon();
        static bool initialized = false;
        static string sqlStmt;


        private static void Initialize()
        {
            m_BIMRLCommonRef.resetAll();

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
               return;
            }
            DBOperation.beginTransaction();
            initialized = true;
        }

        public static List<string> ElementSetList
        {
            get
            {
                if (!initialized)
                    Initialize();

                List<string> eSetList = new List<string>();
                OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
                try
                {
                    sqlStmt = "SELECT NAME FROM ELEMENTSETLIST";
                    command.CommandText = sqlStmt;
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string esetName;
                        esetName = reader.GetString(0);
                        eSetList.Add(esetName);
                    }

                }
                catch (OracleException)
                {
                    // Ignore error
                }
                return eSetList;
            }
        }

        public static bool create(string elementset)
        {
            if (!initialized)
                Initialize();

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

            try
            {
                sqlStmt = "SELECT TABLE_NAME FROM USER_TABLES WHERE TABLE_NAME = '" + elementset.ToUpper() + "'";
                command.CommandText = sqlStmt;
                object tab = command.ExecuteScalar();
                if (tab == null)
                {
                    sqlStmt = "CREATE TABLE " + elementset.ToUpper() + " (ELEMENTID VARCHAR2(22), ELEMENTTYPE VARCHAR2(64))";
                    command.CommandText = sqlStmt;
                    int commandStatus = command.ExecuteNonQuery();
                    sqlStmt = "INSERT INTO ELEMENTSETLIST VALUES ('" + elementset.ToUpper() + "')";
                    command.CommandText = sqlStmt;
                    commandStatus = command.ExecuteNonQuery();
                }
                else
                {
                    BIMRLErrorDialog erroDlg = new BIMRLErrorDialog("%%Error in CREATE ELEMENTSET " + elementset.ToUpper() + ". Elementset with the same name already exists!");
                    erroDlg.ShowDialog();
                    return false;
                }
                DBOperation.commitTransaction();
                return true;
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t in Creating Elementset " + elementset.ToUpper();
                m_BIMRLCommonRef.StackPushError(excStr);
               if (DBOperation.UIMode)
               {
                  BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLCommonRef);
                  erroDlg.ShowDialog();
               }
               else
                  Console.Write(m_BIMRLCommonRef.ErrorMessages);
               command.Dispose();
                return false;
            }
        }

        public static bool register(string elementset)
        {
            if (!initialized)
                Initialize();

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

            try
            {
                sqlStmt = "SELECT TABLE_NAME FROM USER_TABLES WHERE TABLE_NAME = '" + elementset.ToUpper() + "'";
                command.CommandText = sqlStmt;
                object tab = command.ExecuteScalar();
                if (tab == null)
                {
                    sqlStmt = "INSERT INTO ELEMENTSETLIST VALUES ('" + elementset.ToUpper() + "')";
                    command.CommandText = sqlStmt;
                    int commandStatus = command.ExecuteNonQuery();
                }
                else
                {
                    // Do nothing, already registered
                }
                DBOperation.commitTransaction();
                return true;
            }
            catch (OracleException e)
            {
                if (e.Number == 1)
                {
                    // Ignore error of unique index violation
                    command.Dispose();
                    return true;
                }
                string excStr = "%%Error - " + e.Message + "\n\t in Creating Elementset " + elementset.ToUpper();
                m_BIMRLCommonRef.StackPushError(excStr);
               if (DBOperation.UIMode)
               {
                  BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLCommonRef);
                  erroDlg.ShowDialog();
               }
               else
                  Console.Write(m_BIMRLCommonRef.ErrorMessages);
               command.Dispose();
                return false;
            }
        }

        public static List<ElementSet> list()
        {
            if (!initialized)
                Initialize();

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            List<ElementSet> elemsetList = new List<ElementSet>();
            //DefinedElementSetManager tmp = new DefinedElementSetManager();
            foreach (string entry in ElementSetList)
            {
                try
                {
                    ElementSet eSet = new ElementSet();
                    eSet.Name = entry;
                    sqlStmt = "SELECT COUNT(*) FROM " + entry;
                    command.CommandText = sqlStmt;
                    object count = command.ExecuteScalar();
                    eSet.noRecord = int.Parse(count.ToString());
                    elemsetList.Add(eSet);
                }
                catch (OracleException)
                {
                    // Ignore error
                }
            }
            command.Dispose();
            return elemsetList;
        }

        public static bool delete(string elementset)
        {
            if (!initialized)
                Initialize();

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

            try
            {
                sqlStmt = "DELETE FROM " + elementset.ToUpper();
                command.CommandText = sqlStmt;
                int commandStatus = command.ExecuteNonQuery();
                DBOperation.commitTransaction();
                return true;
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t in deleting Elementset " + elementset.ToUpper();
                m_BIMRLCommonRef.StackPushError(excStr);
               if (DBOperation.UIMode)
               {
                  BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLCommonRef);
                  erroDlg.ShowDialog();
               }
               else
                  Console.Write(m_BIMRLCommonRef.ErrorMessages);
                DBOperation.rollbackTransaction();
                command.Dispose();
                return false;
            }
        }

        public static bool drop(string elementset)
        {
            if (!initialized)
                Initialize();

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

            try
            {
                // Delete the record
                sqlStmt = "DELETE FROM ELEMENTSETLIST WHERE NAME='" + elementset.ToUpper() + "'";
                command.CommandText = sqlStmt;
                int commandStatus = command.ExecuteNonQuery();

                // Drop the table
                sqlStmt = "DROP TABLE " + elementset.ToUpper();
                command.CommandText = sqlStmt;
                commandStatus = command.ExecuteNonQuery();
                return true;
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t in drop Elementset " + elementset.ToUpper();
                m_BIMRLCommonRef.StackPushError(excStr);
               if (DBOperation.UIMode)
               {
                  BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLCommonRef);
                  erroDlg.ShowDialog();
               }
               else
                  Console.Write(m_BIMRLCommonRef.ErrorMessages);
               command.Dispose();
                return false;
            }
        }

        public static void dropAll()
        {
            //DefinedElementSetManager tmp = new DefinedElementSetManager();
            foreach (string eset in ElementSetList)
            {
                drop(eset);
            }
        }


    }
}
