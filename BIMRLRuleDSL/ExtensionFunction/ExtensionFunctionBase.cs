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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL;
using BIMRLInterface;
using BIMRL.Common;

namespace BIMRLInterface.ExtensionFunction 
{
    public class ExtensionFunctionBase : IBIMRLExtensionFunction
    {
        public struct inputParamStruct
        {
            public string domainName;
            public string sourceTable;
            public string sourceColumn;
            public string whereCondition;
        }

        protected DataTable m_Table {get; set;}
        protected DataTable m_Result { get; set; }
        protected int m_currentRow {get; set;}

        protected BIMRLCommon m_BIMRLCommonRef = new BIMRLCommon();

        //////
        /// For the following properties, please make sure they are synchronized with ParsedParams class in the bimrlKeywordMapping !!!!
        /////
        //protected List<string> KeyFields { get; set; }
        public string WhereCondition { get; set; }
        public List<string> ExceptionFields { get; set; }
        public List<BIMRLEnum.functionQualifier> Qualifiers { get; set; }
        public List<string> AggregateFields { get; set; }

        public ExtensionFunctionBase()
        {
            m_currentRow = -1;
        }
      
        public void parseParams(string inputParams, out List<string> Params, out string where_clause)
        {
            Params = new List<string>();
            where_clause = "";
            
            string[] pars = inputParams.Split('|');
            foreach (string par in pars)
            {
                string thispar = par;
                if (par.StartsWith("'"))
                    thispar = par.Trim('\'');      // remove leading and training single quote if any
                else
                    thispar = par.Trim('"');
                if (string.Compare("WHERE", 0, par, 0, 5, true) == 0)
                {
                    where_clause = par.Remove(0, 5).Trim();
                }
                else
                {
                    Params.Add(thispar);
                }
            }
        }

        public virtual void Process(DataTable firstTable, string inputParams)
        {
            m_Table = firstTable;

            // parse input paramaters
            List<string> inputPars;
            string where_cl;
            parseParams(inputParams, out inputPars, out where_cl);

            // in this default implementation, result = input
            m_Result = m_Table;
        }

        /// <summary>
        /// The main function to be implemented by any extension function. It should contains the logic to perform the function intended to do
        /// </summary>
        /// <param name="inputDT">Input data in form of DataTable</param>
        /// <param name="inputParams">List of the main parameters as specified in the BIMRL syntax with the exception of: WHERE, KEYFIELDS, and Qualifiers. They are set as properties</param>
        public virtual void InvokeRule(DataTable inputDT, params string[] inputParams)
        {
            // If qualifier AGGREGATE is specified, we will create columns only based on the list of aggregated column to guarantee the uniqueness of column value
            // This is used for result that must aggreegate the values from input
            if (Qualifiers.Contains(BIMRLEnum.functionQualifier.AGGREGATE))
            {
                DataView view = new DataView(inputDT);
                m_Result = view.ToTable(true, AggregateFields.ToArray());
            }
            else
               m_Result = inputDT;
        }

        public virtual keywordInjection preceedingQuery(string inputParams)
        {
            // parse input paramaters
            List<string> inputPars;
            string where_cl;
            parseParams(inputParams, out inputPars, out where_cl);

            keywordInjection keywInj = new keywordInjection();

            return keywInj;
        }

        public bool ReadResult()
        {
            if (m_Result.Rows.Count == 0)
                return false;

            if (m_currentRow < 0)
                m_currentRow = 0;       // start the sequence
            else if (m_currentRow >= 0)
                m_currentRow++;

            if (m_currentRow >= m_Result.Rows.Count)
                return false;
            else
                return true;
        }

        public DataTable GetTableResult()
        {
            if (m_Result != null)
                return m_Result;
            else
                return null;
        }

        public double? GetDoubleResult(string columnName)
        {
            return m_Result.Rows[m_currentRow].Field<double>(columnName);
        }

        public int? GetIntegerResult(string columnName)
        {
            return m_Result.Rows[m_currentRow].Field<int>(columnName);
        }

        public string GetStringResult(string columnName)
        {
            return m_Result.Rows[m_currentRow].Field<string>(columnName);
        }

        public bool? GetBooleanResult(string columnName)
        {
            return m_Result.Rows[m_currentRow].Field<bool>(columnName);
        }

        public void checkInputParam(string inputParam, out inputParamStruct paramStruct)
        {
            // Checking DOMAIN that is signified by a colon in front of the parameter
            string[] domain = inputParam.Split(':');
            paramStruct = new inputParamStruct();
            if (domain.Length > 1)
            {
                // detected a DOMAIN:...
                paramStruct.domainName = domain[0];
                // test for TABLE.COLUMN for elementid source
                string[] tabCol = domain[1].Split('.');
                if (tabCol.Length > 1)
                {
                    paramStruct.sourceTable = tabCol[0];
                    paramStruct.sourceColumn = tabCol[1];
                }
                else if ((tabCol.Length == 1) && (string.Compare(tabCol[0], domain[1]) == 0))
                {
                    // Only COLUMN is specified
                    paramStruct.sourceColumn = tabCol[0];
                }
                else
                {
                    // Only TABLE is specified, default column to ELEMENTID
                    paramStruct.sourceTable = tabCol[0];
                    paramStruct.sourceColumn = "ELEMENTID";
                }
            }
            else if (domain.Length == 1 && (string.Compare(domain[0], inputParam) == 0))
            {
                // only a single name specified, check for IFC*
                if (string.Compare(domain[0], 0, "IFC", 0, 3, true) == 0)
                {
                    // It is IFC objets
                    paramStruct.domainName = "IFC";
                    paramStruct.sourceTable = "BIMRL_ELEMENT \"Ifc\"";
                    paramStruct.sourceColumn = "ELEMENTID";
                    paramStruct.whereCondition = "\"Ifc\".ELEMENTTYPE IN (SELECT ELEMENTSUBTYPE FROM BIMRL_OBJECTHIERARCHY WHERE ELEMENTTYPE IN ('" + domain[0] + "') AND IFCSCHEMAVER='IFC2X3')";
                }
                else
                {
                    paramStruct.domainName = "BIMRL";
                    paramStruct.sourceColumn = domain[0];
                }
            }
            else
            {
                // Consist only a DOMAIN
                if (string.Compare(domain[0], "USERGEOM", true) == 0)
                    paramStruct.domainName = "USERGEOM";
                else
                    paramStruct.domainName = "BIMRL";           // Ignore anything else and default it to BIMRL
                paramStruct.sourceColumn = "ELEMENTID";         //default
            }
        }
    }
}
