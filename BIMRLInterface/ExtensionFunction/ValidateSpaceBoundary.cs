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
using BIMRL.OctreeLib;

namespace BIMRLInterface.ExtensionFunction 
{
    public class ValidateSpaceBoundary : ExtensionFunctionBase, IBIMRLExtensionFunction 
    {
        //DataTable m_Table;
        //DataTable m_Result;

        //BIMRLCommon m_BIMRLCommonRef = new BIMRLCommon();

        public ValidateSpaceBoundary()
        {

        }
      
        DataTable setupInitialTable(DataTable inputTable)
        {
            DataTable firstQuery = inputTable.Clone();
            // Define primary key
            firstQuery.PrimaryKey = new DataColumn[] { firstQuery.Columns["SpaceElementId"], firstQuery.Columns["BoundaryElementId"] };

            firstQuery = inputTable.Copy();

            return firstQuery;
}

        DataTable geomSpaceBoundaryQuery(string spaceAlias, string boundAlias, string queryFilter)
        {
            DataTable secondQuery = this.m_Table.Clone();

            if (string.IsNullOrEmpty(spaceAlias))
                spaceAlias = "\"sB\"";

            if (string.IsNullOrEmpty(boundAlias))
                boundAlias = "\"bB\"";

            string sqlStmt = "SELECT /*+ ordered use_nl (a," + spaceAlias + ") use_nl(a," + boundAlias + ") */ " + spaceAlias + ".ELEMENTID SpaceElementId, " + spaceAlias + ".NAME SpaceName, "
                            + spaceAlias + ".LONGNAME SpaceLongName, " + boundAlias + ".ELEMENTID BoundaryElementId, " + boundAlias + ".ELEMENTTYPE BoundaryElementType, " + boundAlias + ".NAME BoundaryElementName "
                            + "FROM TABLE (SDO_JOIN('BIMRL_ELEMENT_" + DBQueryManager.FedModelID.ToString("X4") + "', 'GEOMETRYBODY', 'BIMRL_ELEMENT_" + DBQueryManager.FedModelID.ToString("X4")  
                            + "', 'GEOMETRYBODY')) A, BIMRL_ELEMENT " + spaceAlias + ", BIMRL_ELEMENT " + boundAlias + " "
                            + "WHERE A.ROWID1 = " + spaceAlias + ".ROWID AND A.ROWID2 = " + boundAlias + ".ROWID AND " + spaceAlias + ".ROWID < " + boundAlias + ".ROWID "
                            + "AND " + spaceAlias + ".ELEMENTTYPE = 'IFCSPACE' AND SDO_GEOM.RELATE(" + spaceAlias + ".GEOMETRYBODY, 'ANYINTERACT', " + boundAlias + ".GEOMETRYBODY, 0.005) = 'TRUE'";
            BIMRLInterfaceCommon.appendToString(queryFilter, " AND ", ref sqlStmt);

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            try
            {
                command.CommandText = BIMRLKeywordMapping.expandBIMRLTables(sqlStmt);
                OracleDataAdapter qAdapter = new OracleDataAdapter(command);
                qAdapter.Fill(secondQuery);

                return secondQuery;
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n" + command.CommandText;
                this.m_BIMRLCommonRef.StackPushError(excStr);
               if (DBOperation.UIMode)
               {
                  BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(this.m_BIMRLCommonRef);
                  erroDlg.ShowDialog();
               }
               else
                  Console.Write(m_BIMRLCommonRef.ErrorMessages);
               command.Dispose();
                return null;
            }
        }

        DataTable unionSpaceBoundaryTables (DataTable firstQuery, DataTable secondQuery)
        {
            // firstQuery.Merge(secondQuery, true, MissingSchemaAction.Add);
            DataTable mergedTable = firstQuery.Clone();

            DataColumn column;
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "BYRELSPACEBOUNDARY";
            column.ReadOnly = false;
            column.Unique = false;
            mergedTable.Columns.Add(column);

            DataColumn column2;
            column2 = new DataColumn();
            column2.DataType = System.Type.GetType("System.String");
            column2.ColumnName = "BYGEOMETRY";
            column2.ReadOnly = false;
            column2.Unique = false;
            mergedTable.Columns.Add(column2);

            int currPos = 0;
            // First we merge data in Table 2 into table 1 with simply updating the "ByGeometry" column to "Y" and delete the record from Table 2
            // Other columns are not checked/added since it is "assumed" the Process and preceedingQuery are synchronized for the column list
            foreach (DataRow ftRow in firstQuery.Select())
            {
                mergedTable.ImportRow(ftRow);
                DataRow currRow = mergedTable.Rows[currPos];
                currRow["BYRELSPACEBOUNDARY"] = "Y";
                string spaceEID = (string) ftRow["SpaceElementId"];
                string boundEID = (string)ftRow["BoundaryElementId"];
                DataRow[] stRows = secondQuery.Select("SpaceElementId='" + spaceEID + "' AND BoundaryElementId = '" + boundEID + "'");
                if (stRows.Count() > 0)
                {
                    currRow["ByGeometry"] = "Y";
                    foreach (DataRow stRow in stRows)
                        stRow.Delete();
                }
                currPos++;
            }

            // Next, we go through remaining records in the Table 2 and add into the mergedTable with only "ByGeometry" column set to "Y" and "ByRelSpaceBoundary" to be null
            foreach (DataRow stRow in secondQuery.Select())
            {
                mergedTable.ImportRow(stRow);
                DataRow currRow = mergedTable.Rows[currPos];
                currRow["ByGeometry"] = "Y";
                currPos++;
            }

            return mergedTable;
        }

        public override void Process(DataTable firstTable, string inputParams)
        {
            string[] pars = inputParams.Split('|');

            if (pars.Count() < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: ValidatSpaceBoundary requires at least one parameter for the space alias (S)!");

            string spaceAlias = pars[0].Trim();
            string additionalFilter = null;
            string boundAlias = null;
            if (pars.Count() > 1)
                boundAlias = pars[1].Trim();
            if (pars.Count() > 2)
            {
                string par2 = pars[2].Trim() as string;
                if (par2.StartsWith("'"))
                    par2 = par2.Trim('\'');      // remove leading and training single quote if any
                else
                    par2 = par2.Trim('"');
                if (string.Compare("WHERE", 0, par2, 0, 5, true) == 0)
                {
                    additionalFilter = par2.Remove(0, 5).Trim();
                }
            }

            this.m_Table = setupInitialTable(firstTable);
            DataTable secondQuery = geomSpaceBoundaryQuery(spaceAlias, boundAlias, additionalFilter);
            DataTable res = null;
            if (secondQuery != null)
                 res = unionSpaceBoundaryTables(this.m_Table, secondQuery);

            this.m_Result = res;
        }

        public override keywordInjection preceedingQuery(string inputParams)
        {
            string[] pars = inputParams.Split('|');

            // We expect minimum 2 parameters containing aliases of the Space and the Boundary elements
            if (pars.Count() < 2)
                throw new BIMRLInterfaceRuntimeException("%Error: ValidateSpaceBoundary requires at least arguments (S, B)!");

            string spaceAlias = pars[0].Trim();
            string boundAlias = pars[1].Trim();
            string addCond = null;
            if (pars.Count() > 2)
            {
                for (int i = 2; i < pars.Count(); ++i)
                {
                    string par = pars[i].Trim() as string;
                    if (string.IsNullOrEmpty(par))
                        continue;   // skip empty string

                    if (par.StartsWith("'"))
                        par = par.Trim('\'');      // remove leading and training single quote if any
                    else
                        par = par.Trim('"');

                    if (string.Compare("WHERE", 0, par, 0, 5, true) == 0)
                    {
                        addCond = par.Remove(0, 5).Trim();
                    }
                }
            }

            keywordInjection keywInj = new keywordInjection();

            string spaceElementID = spaceAlias + ".ELEMENTID SpaceElementId";
            if (!ColumnListManager.checkAndRegisterColumnItems(spaceAlias, "ELEMENTID", "SpaceElementId"))
                BIMRLInterfaceCommon.appendToString(spaceElementID, ", ", ref keywInj.colProjInjection);

            string spaceName = spaceAlias + ".NAME SpaceName";
            if (!ColumnListManager.checkAndRegisterColumnItems(spaceAlias, "NAME", "SpaceName"))
                BIMRLInterfaceCommon.appendToString(spaceName, ", ", ref keywInj.colProjInjection);

            string spaceLongName = spaceAlias + ".LONGNAME SpaceLongName";
            if (!ColumnListManager.checkAndRegisterColumnItems(spaceAlias, "LONGNAME", "SpaceLongName"))
                BIMRLInterfaceCommon.appendToString(spaceLongName, ", ", ref keywInj.colProjInjection);

            string boundElementID = boundAlias + ".ELEMENTID BoundaryElementId";
            if (!ColumnListManager.checkAndRegisterColumnItems(boundAlias, "ELEMENTID", "BoundaryElementId"))
                BIMRLInterfaceCommon.appendToString(boundElementID, ", ", ref keywInj.colProjInjection);

            string boundElementType = boundAlias + ".ELEMENTTYPE BoundaryElementType";
            if (!ColumnListManager.checkAndRegisterColumnItems(boundAlias, "ELEMENTTYPE", "BoundaryElementType"))
                BIMRLInterfaceCommon.appendToString(boundElementType, ", ", ref keywInj.colProjInjection);

            string boundName = boundAlias + ".NAME BoundaryElementName";
            if (!ColumnListManager.checkAndRegisterColumnItems(boundAlias, "NAME", "BoundaryElementName"))
                BIMRLInterfaceCommon.appendToString(boundName, ", ", ref keywInj.colProjInjection);

            int idx = -1;
            keywInj.tabProjInjection = "BIMRL_RELSPACEBOUNDARY " + spaceAlias;
            if (!TableListManager.checkTableAndAlias("BIMRL_RELSPACEBOUNDARY", spaceAlias, out idx))
            {
                TableSpec tbS = new TableSpec { tableName = "BIMRL_RELSPACEBOUNDARY", alias = spaceAlias, originalName = null };
                TableListManager.addOrUpdateMember(tbS);
            }

            keywInj.whereClInjection = spaceAlias + ".SPACEELEMENTID = " + spaceAlias + ".ELEMENTID AND " + boundAlias + ".ELEMENTID = " + spaceAlias + ".BOUNDARYELEMENTID";
            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref keywInj.whereClInjection);

            return keywInj;
        }
    }
}
