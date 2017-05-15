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
using BIMRL.OctreeLib;

namespace BIMRLInterface.ExtensionFunction
{
    public class VolumeIntersection : ExtensionFunctionBase, IBIMRLExtensionFunction
    {
        public VolumeIntersection()
        {

        }

        /// <summary>
        /// VolumeIntersection function expects the following properties to be set: KEYFIELDS
        /// </summary>
        /// <param name="inputDT"></param>
        /// <param name="inputParams"></param>
        public override void InvokeRule(DataTable inputDT, params string[] inputParams)
        {
            base.InvokeRule(inputDT,inputParams);
            // This rule currently does a very simple thing: get the spatial index of an object, intersect it with another geometry and give the est. of intersected area (compared to the first object)

            inputParamStruct par1;
            string spIdxTab1 = "BIMRL_SPATIALINDEX";
            inputParamStruct par2;
            string spIdxTab2 = "BIMRL_SPATIALINDEX";
            checkInputParam(inputParams[0], out par1);
            if (string.Compare(par1.domainName, "USERGEOM", true) == 0)
                spIdxTab1 = "USERGEOM_SPATIALINDEX";
                            
            checkInputParam(inputParams[1], out par2);
            if (string.Compare(par2.domainName, "USERGEOM", true) == 0)
                spIdxTab2 = "USERGEOM_SPATIALINDEX";
 
            DataColumn column;
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Double");
            column.ColumnName = "OUTPUT";
            column.ReadOnly = false;
            column.Unique = false;
            try
            {
                inputDT.Columns.Add(column);    // Add column result
            }
            catch (System.Data.DuplicateNameException)
            {
                // ignore error for duplicate column and continue
            }

            foreach(DataRow dtRow in inputDT.Rows)
            {
                // We process par2 first. We expect this to be either IFC objects or simply 
                string elemID2 = dtRow[par2.sourceColumn].ToString();
                string sqlStmt = "SELECT DEPTH, COUNT(*) CELLCOUNT FROM " + spIdxTab2 + " WHERE ELEMENTID='" + elemID2 + "' GROUP BY DEPTH";
                DBQueryManager dbQ = new DBQueryManager();
                DataTable idxElem = dbQ.queryMultipleRows(sqlStmt);
                double elemVol = 0.0;
                if (idxElem != null)
                {
                    foreach (DataRow dtRow2 in idxElem.Rows)
                    {
                        int depth = int.Parse(dtRow2["DEPTH"].ToString());
                        int count = int.Parse(dtRow2["CELLCOUNT"].ToString());
                        elemVol += count * Math.Pow(8, (10 - depth));
                    }
                }

                // For par1 in this function, we expect the name refers to the column name in inputDT
                string elemID = dtRow[par1.sourceColumn].ToString();
                sqlStmt = "SELECT A.DEPTH, COUNT(*) CELLCOUNT FROM " + spIdxTab1 + " A JOIN " + spIdxTab2 + " B ON A.CELLID=B.CELLID "
                            + "WHERE A.ELEMENTID='" + elemID + "' AND B.ELEMENTID='" + elemID2 + "' GROUP BY A.DEPTH";
                DataTable idxUserGeom = dbQ.queryMultipleRows(sqlStmt);
                double userGeomVol = 0.0;
                if (idxUserGeom != null)
                {
                    foreach (DataRow dtRow2 in idxUserGeom.Rows)
                    {
                        int depth = int.Parse(dtRow2["DEPTH"].ToString());
                        int count = int.Parse(dtRow2["CELLCOUNT"].ToString());
                        userGeomVol += count * Math.Pow(8, (10 - depth));
                    }
                }

                double coverage = userGeomVol / elemVol;
                dtRow["OUTPUT"] = coverage;
            }

            m_Result = inputDT;
        }
    }
}
