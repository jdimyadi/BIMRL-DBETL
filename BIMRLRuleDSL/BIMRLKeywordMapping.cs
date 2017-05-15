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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.Common;

namespace BIMRLInterface
{
    public static class BIMRLKeywordMapping
    {
        static string[] bimrl_tables = { "BIMRL_RELGROUP", "BIMRL_SPATIALSTRUCTURE", "BIMRL_MODELINFO", "BIMRL_OWNERHISTORY", "BIMRL_ELEMENTPROPERTIES",
                                "BIMRL_PROPERTIES", "BIMRL_TYPE", "BIMRL_TYPEPROPERTIES", "BIMRL_TYPEMATERIAL", "BIMRL_TYPECLASSIFICATION", "BIMRL_CLASSIFASSIGNMENT",
                                "BIMRL_CLASSIFICATION", "BIMRL_ELEMCLASSIFICATION", "BIMRL_ELEMENTMATERIAL", "BIMRL_ELEMENTDEPENDENCY",
                                "BIMRL_RELCONNECTION", "BIMRL_RELAGGREGATION", "BIMRL_RELSPACEBOUNDARY", "BIMRL_ELEMWOGEOM", "BIMRL_ELEMENT", "BIMRL_TOPO_FACE", "BIMRL_TOPOFACEV",
                                       "BIMRL_RELSPACEB_DETAIL", "BIMRL_SPACEBOUNDARYV", "BIMRL_SPATIALINDEX", "BIMRL_PARTGEOMETRY"};
        static Dictionary<string, string> processedKeywords = new Dictionary<string, string>();

        public class FunctionParam
        {
            public string Text { get; set; }
            public int Index { get; set; }
            public FunctionParam()
            {
                Text = null;
                Index = -1;
            }
        }

        public class FunctionParamList
        {
            public List<string> pars { get; set; }
            public int Index { get; set; }
            public FunctionParamList()
            {
                pars = null;
                Index = -1;
            }
        }

        public class QualifierParam
        {
            public BIMRLEnum.functionQualifier qEnum { get; set; }
            public int Index { get; set; }
            public QualifierParam()
            {
                qEnum = BIMRLEnum.functionQualifier.UNDEFINED;
                Index = -1;
            }
        }

        public class ParsedParams
        {
            public FunctionParam Where { get; set; }
            //public FunctionParamList KeyFields { get; set; }
            public FunctionParamList ExceptionFields { get; set; }
            public List<QualifierParam> Qualifiers { get; set; }
            public FunctionParamList AggregateFields { get; set; }
            public string[] unparsedParams { get; set; }
            public ParsedParams()
            {
                Where = new FunctionParam();
                //KeyFields = new FunctionParamList();
                ExceptionFields = new FunctionParamList();
                Qualifiers = new List<QualifierParam>();
                AggregateFields = new FunctionParamList();
                unparsedParams = null;
            }
        }

        public static void Init()
        {
            processedKeywords.Clear();
        }

        public static string expandBIMRLTables(string sqlStmt)
        {
            string tmpStmt = sqlStmt;

            foreach (string tabName in bimrl_tables)
            {
                string extTabName = tabName + "_" + DBQueryManager.FedModelID.ToString("X4");

                // We will not replace a table name if it is already fully qualified with the hex id extension
                //if (!sqlStmt.ToUpper().Contains(extTabName))
                    tmpStmt = Regex.Replace(tmpStmt, tabName + @"([\s;]+|\z)", extTabName + " ", RegexOptions.IgnoreCase);
                    tmpStmt = Regex.Replace(tmpStmt, tabName + "[)]", extTabName + ")", RegexOptions.IgnoreCase);
                    tmpStmt = Regex.Replace(tmpStmt, tabName + "[\"]", extTabName + "\"", RegexOptions.IgnoreCase);
                    tmpStmt = Regex.Replace(tmpStmt, tabName + "[\']", extTabName + "\'", RegexOptions.IgnoreCase);
            }

            return tmpStmt;
        }

        public static keywordInjection expandEntity(bool skipAlias, params string[] inputParams)
        {
            if (inputParams.Count() == 0)
                throw new BIMRLInterfaceRuntimeException("Entity Name cannot be emtpy!");
            if (string.IsNullOrEmpty(inputParams[0]))               // at least one parameter must be supplied
                throw new BIMRLInterfaceRuntimeException("Entity Name cannot be emtpy!");

            string entityName = inputParams[0];
            string alias = null;
            if (inputParams.Count() > 1)
                alias = inputParams[1];

            keywordInjection keywInj = new keywordInjection();

            if (!skipAlias)
            {
                if (string.IsNullOrEmpty(alias))
                    keywInj.whereClInjection = validAlias("\"eE\"") + ".";
                else
                    keywInj.whereClInjection = alias + ".";
            }

            keywInj.whereClInjection += "ELEMENTTYPE IN (SELECT ELEMENTSUBTYPE FROM BIMRL_OBJECTHIERARCHY WHERE ELEMENTTYPE='" + entityName.ToUpper() + "')";

            return keywInj;
        }

        public static keywordInjection expandEntityArray(bool skipAlias, params string[] inputParams)
        {
            if (inputParams.Count() == 0)
                throw new BIMRLInterfaceRuntimeException("Entity Name cannot be emtpy!");
            if (string.IsNullOrEmpty(inputParams[0]))               // at least one parameter must be supplied
                throw new BIMRLInterfaceRuntimeException("Entity Name cannot be emtpy!");

            string entityArray = inputParams[0];
            string alias = null;
            if (inputParams.Count() > 1)
                alias = inputParams[1];

            keywordInjection keywInj = new keywordInjection();

            if (!skipAlias)
            {
                if (string.IsNullOrEmpty(alias))
                    keywInj.whereClInjection = validAlias("\"eE\"") + ".";
                else
                    keywInj.whereClInjection = alias + ".";
            }

            keywInj.whereClInjection += "ELEMENTTYPE IN (SELECT ELEMENTSUBTYPE FROM BIMRL_OBJECTHIERARCHY WHERE ELEMENTTYPE in (" + entityArray.ToUpper() + ") )";

            return keywInj;
        }

        /* General format for the parameters
         * Two special paramaters can be included for all the builtin functions:
         *     - alias: the param value should be prefixed with 'ALIAS:' substring
         *     - where clause like expression: the param value should be prefixed with 'WHERE'. User is responsible in using the correct alias in here, only when it is a single ref we can add the alias
         *     - return value test (e.g. CONTAINS(S, E)=.F.): the param should be prefixed with 'RETVAL:'
         *     - return value operator (e.g. function(A) > number): the param should be prefixed with 'RETVALOP:'
         */

        /// <summary>
        /// Returning information whether an element contains in the container. CONTAINS(S,E) returning T/F
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionCONTAINS(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count();
            if (noPar < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias must be specified: CONTAINS(S)!");
            if (string.IsNullOrEmpty(inputParams[0]))
                throw new BIMRLInterfaceRuntimeException("%Error: Element alias cannot be empty: CONTAINS(E)!");
            string containerAlias = inputParams[0];
            int startIdx = 1;
            ParsedParams pars = parseParams(startIdx, inputParams);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                noPar--;    // noPar minus 1 because there is where clause
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string elemAlias = null;
            if (noPar > 1)
                elemAlias = inputParams[1];
            else
                elemAlias = "CO";

            nodeProperty retNP = new nodeProperty();
            string tabName = "BIMRL_ELEMENT";
            enrolTableIntonodeProperty(tabName, elemAlias, null, ref retNP);

            bool useGeometry = false;

            // So far CONTAINS only support qualifier USEGEOMETRY
            if (pars.Qualifiers != null)
            {
                foreach (QualifierParam par in pars.Qualifiers)
                {
                    if (par.qEnum == BIMRLEnum.functionQualifier.USEGEOMETRY)
                        useGeometry = true;
                }
            }

            if (useGeometry)
            {
                string spatialIndTable = "BIMRL_SPATIALINDEX";
                string spatialIndAlias1 = containerAlias + "$";
                string spatialIndAlias2 = elemAlias + "$";
                enrolTableIntonodeProperty(spatialIndTable, spatialIndAlias1, null, ref retNP);
                enrolTableIntonodeProperty(spatialIndTable, spatialIndAlias2, null, ref retNP);

                string spatialCond = spatialIndAlias1 + ".ELEMENTID=" + containerAlias + ".ELEMENTID AND "
                                    + spatialIndAlias2 + ".ELEMENTID=" + elemAlias + ".ELEMENTID AND "
                                    + spatialIndAlias1 + ".CELLID " + BIMRLConstant.tempOp + " " + spatialIndAlias2 + ".CELLID";
                // string spatialCond = "SDO_ANYINTERACT(" + element + ".GEOMETRYBODY, " + containerAlias + ".GEOMETRYBODY) " + BIMRLConstant.tempOp + "'TRUE'";
                BIMRLInterfaceCommon.appendToString(spatialCond, " AND ", ref retNP.keywInj.whereClInjection); 
            }
            else
            {
                int idx;
                string tableName = "BIMRL_SPATIALSTRUCTURE";
                enrolTableIntonodeProperty(tableName, containerAlias, null, ref retNP);

                // BIMRLConstant.tempOp to be replaced in expr according to the detailed in the expr
                retNP.keywInj.whereClInjection = containerAlias + ".SPATIALELEMENTID" + BIMRLConstant.tempOp + elemAlias + ".CONTAINER";
                // If there is BIMRL_ELEMENT with the same alias, we need to restrict it further that the Alias table should only contains the parent id of the container
                if (TableListManager.checkTableAndAlias(tableName, containerAlias, out idx))
                    retNP.keywInj.whereClInjection += " AND " + containerAlias + ".ELEMENTID = " + containerAlias + ".PARENTID";
            }

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = tabName;
                else
                    retNP.forExpr = elemAlias;
            }
            return retNP;
        }

        /// <summary>
        /// Returning the elementtype. Usage: ELEMENTTYPEOF(E), returning the elementtype value/string
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionELEMENTTYPEOF(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count();
            if (inputParams.Count() < 1)
                throw new BIMRLInterfaceRuntimeException("Element cannot be empty!");
            if (string.IsNullOrEmpty(inputParams[0]))
                throw new BIMRLInterfaceRuntimeException("Element cannot be empty!");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (!string.IsNullOrEmpty(pars.Where.Text))
                noPar--;

            nodeProperty retNP = new nodeProperty();
            //int idx;

            string tmpAlias = element;

            string item1 = tmpAlias;
            string item2 = "ELEMENTTYPE";
            enrolColumnIntonodeProperty(item1, item2, ref retNP);

            BIMRLInterfaceCommon.appendToString(pars.Where.Text, " AND ", ref retNP.keywInj.whereClInjection);

            // This can be used in expr to handle ELEMENTTYPEOF(E) != 'IFCWALL' (translated into: E.ELEMENTTYPE != 'IFCWALL')
            if (noPar == 1) // only valid if it has a single parameter that points to the reference of the elementtype. 2 Par only valid with T/F (default T)
                retNP.forExpr = tmpAlias + ".ELEMENTTYPE";

            return retNP;
        }

        /// <summary>
        /// Expected min 2 paramters: par1 - alias for element, and par2 - alias for type (can be null)
        /// the third (opt) will be WHERE clause. Usage: TYPEOF(E) - returning reference to the type information, TYPEOF(E,T) - returning T/F
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionTYPEOF(IList<string> inputParams, bool byTable = false)
        {
            // TYPEOF may have 1 or 2 parameters: TYPEOF(E) returns reference to the Type, TYPEOF(E, T) returns T/F that T is the type of E
            int noPar = inputParams.Count();
            if (inputParams.Count() < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias must be specified: TYPEOF(E)!");
            if (string.IsNullOrEmpty(inputParams[0]))
                throw new BIMRLInterfaceRuntimeException("%Error: Element alias cannot be empty: TYPEOF(E)!");

            string element = inputParams[0];
            int startIdx = 1;
            ParsedParams pars = parseParams(startIdx, inputParams);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                noPar--;    // noPar minus 1 because there is where clause
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string tmpAlias = null;
            if (noPar > 1)
                tmpAlias = inputParams[1];
            else
                tmpAlias = "\"tT\"";

            nodeProperty retNP = new nodeProperty();
            string tabName = "BIMRL_TYPE" ;
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            // BIMRLConstant.tempOp to be replaced in expr according to the detailed in the expr
            retNP.keywInj.whereClInjection = tmpAlias + ".ELEMENTID" + BIMRLConstant.tempOp + " " + element + ".TYPEID";
            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // This can be used in expr to handle TYPEOF(E).NAME LIKE 'WATER FOUNTAIN%' (translated into: "tT".NAME LIKE 'WATER FOUNTAIN%')
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = tabName;
                else
                    retNP.forExpr = tmpAlias;
            }
            return retNP;
        }

        /// <summary>
        /// Returning the aggregate: Usage AGGREGATEOF(M, E) - returning T/F, AGGREGATEOF(M) - returning reference to the aggregated element
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionAGGREGATEOF(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias must be provided as an argument to AGGREGATEOF(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string aggrAlias = null;
            string whereCond = null;

            if (noPar > 1)
                aggrAlias = inputParams[1];
            else
                aggrAlias = "\"eA\"";

            string tabName = "BIMRL_RELAGGREGATION";     // default
            string tmpAlias = element;        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string aggrTab = "BIMRL_ELEMENT";
            enrolTableIntonodeProperty(aggrTab, aggrAlias, null, ref retNP);

            whereCond = aggrAlias + ".ELEMENTID = " + element + ".AGGREGATEELEMENTID AND " + element + ".MASTERELEMENTID " + BIMRLConstant.tempOp + " " + element + ".ELEMENTID";
            BIMRLInterfaceCommon.appendToString(whereCond, ", ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "eA".elementtype = 'ABC' or "eA".name
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = aggrTab;
                else
                    retNP.forExpr = aggrAlias;
            }
            return retNP;
        }

        /// <summary>
        ///  Returning aggregate master. Usage: AGGREGATEMASTER(E) - returning reference to the master element, AGGREGATEMASTER(E, M) - returning T/F
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionAGGREGATEMASTER(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias must be provided as an argument to AGGREGATEMASTER(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string whereCond = null;

            string masterAlias = "\"aM\"";

            string tabName = "BIMRL_RELAGGREGATION";     // default
            string tmpAlias = element;        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string masterTab = "BIMRL_ELEMENT";
            enrolTableIntonodeProperty(masterTab, masterAlias, null, ref retNP);

            whereCond = masterAlias + ".ELEMENTID " + BIMRLConstant.tempOp + " " + element + ".MASTERELEMENTID AND " + element + ".AGGREGATEELEMENTID = " + element + ".ELEMENTID";
            BIMRLInterfaceCommon.appendToString(whereCond, ", ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "eSb".elementtype = 'ABC' or "eSb".name
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = masterTab;
                else
                    retNP.forExpr = masterAlias;
            }
            return retNP;
        }

        /// <summary>
        /// Returning the space an element bounds. BOUNDEDSPACE(E) - returning reference to the space that the element is the boundary of, BOUNDEDSPACE(E, S) - returning T/F
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionBOUNDEDSPACE(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias must be provided as an argument to BOUNDEDSPACE(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string spaceAlias = null;
            string whereCond = null;

            if (noPar > 1)
                spaceAlias = inputParams[1];
            else
                spaceAlias = "\"sB\"";

            string tabName = "BIMRL_RELSPACEBOUNDARY";     // default
            string tmpAlias = element;        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string spaceTab = "BIMRL_ELEMENT";
            enrolTableIntonodeProperty(spaceTab, ref spaceAlias, ref retNP);
            whereCond = spaceAlias + ".ELEMENTID " + BIMRLConstant.tempOp +  " " + element + ".SPACEELEMENTID AND " + element + ".BOUNDARYELEMENTID = " + element + ".ELEMENTID";
            BIMRLInterfaceCommon.appendToString(whereCond, ", ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "eSb".elementtype = 'ABC' or "eSb".name
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = spaceTab;
                else
                    retNP.forExpr = spaceAlias;
            }
            return retNP;
        }

        /// <summary>
        /// Provides a reference to the boundary element information
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionBOUNDARYINFO(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty(); 
            if (inputParams.Count < 2)
                throw new BIMRLInterfaceRuntimeException("%Error: At least a space alias and the boundary element alias must be specified: BOUNDARYINFO(D,C1)!");

            string spaceAlias = inputParams[0];
            string boundAlias = inputParams[1];
            ParsedParams pars = parseParams(2, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string tab1Alias = spaceAlias + boundAlias + "$";
            string tabName1 = "BIMRL_SPACEBOUNDARYV";     // default
            enrolTableIntonodeProperty(tabName1, ref tab1Alias, ref retNP);

            string whereCond = tab1Alias + ".SPACEELEMENTID = " + spaceAlias + ".ELEMENTID AND " + boundAlias + ".ELEMENTID = " + tab1Alias + ".BOUNDARYELEMENTID";
            BIMRLInterfaceCommon.appendToString(whereCond, " AND ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            if (byTable)
                retNP.forExpr = tabName1;
            else
                retNP.forExpr = tab1Alias;

            return retNP;
        }

        /// <summary>
        /// Returning the classification. CLASSIFICATIONOF(E) returning reference to the classification information
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionCLASSIFICATIONOF(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty(); 
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: An element alias should be provided as an argument to CLASSIFICATIONOF(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;
            string classifName = null;
            if (noPar > 1)
            {
                // There is the 2nd parameter that specifies the classification name
                classifName = inputParams[1].ToUpper();
            }

            string tabName = "BIMRL_CLASSIFASSIGNMENT" ;     // default
            string tmpAlias = "\"" + element.ToLower() + "C\"";           // define alias using element alias, so that we can use it also for check in HASCLASSIFICATION
            enrolTableIntonodeProperty(tabName, ref tmpAlias, ref retNP);

            string addCond = null;
            // There is a danger here that duplicate qualifier may overlap and override the earlier one, but user should be responsible for it
            if (pars.Qualifiers != null)
            {
                foreach (QualifierParam qPar in pars.Qualifiers)
                {
                    switch (qPar.qEnum)
                    {
                        case BIMRLEnum.functionQualifier.INSTANCEONLY:
                            addCond = tmpAlias + ".FROMTYPE = 'FALSE'";
                            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                            break;
                        case BIMRLEnum.functionQualifier.TYPEONLY:
                            addCond = tmpAlias + ".FROMTYPE = 'TRUE'";
                            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                            break;
                        case BIMRLEnum.functionQualifier.INSTANCEORTYPE:
                            break;
                        default:
                            break;
                    }
                }
            }

            addCond = null;
            if (!string.IsNullOrEmpty(classifName))
            {
                addCond = "UPPER(" + tmpAlias + ".CLASSIFICATIONNAME) = '" + classifName + "'";
            }

            retNP.keywInj.whereClInjection = element + ".ELEMENTID = " + tmpAlias + ".ELEMENTID";
            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. CLASSIFICATIONOF(E).ClassificationItemName = 'ABC'
            if (byTable)
                retNP.forExpr = tabName;
            else
                retNP.forExpr = tmpAlias;

            return retNP;
        }

        /// <summary>
        /// Reference to the connected object: CONNECTEDTO(E), or T/F for CONNECTEDTO(E1, E2)
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionCONNECTEDTO(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias must be provided as an argument to CONNECTEDTO(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string elem2Alias = null;
            string whereCond = null;

            if (noPar > 1)
                elem2Alias = inputParams[1];
            else
                elem2Alias = "\"cT\"";

            string tabName = "BIMRL_RELCONNECTION";     // default
            string tmpAlias = element;        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string elem2Tab = "BIMRL_ELEMENT";
            enrolTableIntonodeProperty(elem2Tab, ref elem2Alias, ref retNP);
            whereCond = "((" + elem2Alias + ".ELEMENTID " + BIMRLConstant.tempOp + " " + element + ".CONNECTEDELEMENTID AND " + element + ".CONNECTINGELEMENTID = " + element + ".ELEMENTID)" +
                        " OR (" + elem2Alias + ".ELEMENTID " + BIMRLConstant.tempOp + " " + element + ".CONNECTINGELEMENTID AND " + element + ".CONNECTEDELEMENTID = " + element + ".ELEMENTID))";
            BIMRLInterfaceCommon.appendToString(whereCond, ", ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "eSb".elementtype = 'ABC' or "eSb".name
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = elem2Tab;
                else
                    retNP.forExpr = elem2Alias;
            }
            return retNP;
        }

        /// <summary>
        /// Reference to the container. CONTAINER(E) - returning reference to the container, CONTAINER(E, S) - returning T/F
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionCONTAINER(IList<string> inputParams, bool byTable = false)
        {
            if (inputParams.Count() < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias must be provided as a parameter CONTAINER(E)!");
            string element = inputParams[0];
            int noPar = inputParams.Count;

            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            nodeProperty retNP = new nodeProperty();

            bool keepContAlias = false;
            string containerAlias = null;
            if (noPar > 1)
            {
                containerAlias = inputParams[1];
                keepContAlias = true;
            }
            else
                containerAlias = "\"eC\"";

            int idx;
            string tableName = "BIMRL_SPATIALSTRUCTURE";
            if (keepContAlias)
                enrolTableIntonodeProperty(tableName, containerAlias, null, ref retNP);
            else
                enrolTableIntonodeProperty(tableName, ref containerAlias, ref retNP);
    
            tableName = "BIMRL_ELEMENT";
            enrolTableIntonodeProperty(tableName, containerAlias, null, ref retNP);

            retNP.keywInj.whereClInjection = containerAlias + ".SPATIALELEMENTID = " + element + ".CONTAINER";
            // If there is BIMRL_ELEMENT with the same alias, we need to restrict it further that the Alias table should only contains the parent id of the container
            if (TableListManager.checkTableAndAlias(tableName, containerAlias, out idx))
                retNP.keywInj.whereClInjection += " AND " + containerAlias + ".ELEMENTID " + BIMRLConstant.tempOp + " " + containerAlias + ".PARENTID";

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = tableName;
                else
                    retNP.forExpr = containerAlias;
            }
            return retNP;
        }

        /// <summary>
        /// Returning reference to the dependency, e.g. Wall -> Door DEPENDENCY(W, D) - returning T/F, DEPENDENCY(W) - returns reference to all the dependent objects
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionDEPENDENCY(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias must be provided as an argument to DEPENDENCY(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string elem2Alias = null;
            string whereCond = null;

            if (noPar > 1)
                elem2Alias = inputParams[1];
            else
                elem2Alias = "\"dE\"";

            string tabName = "BIMRL_ELEMENTDEPENDENCY";     // default
            string tmpAlias = "\"dR\"";        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string elem2Tab = "BIMRL_ELEMENT";
            enrolTableIntonodeProperty(elem2Tab, ref elem2Alias, ref retNP);
            whereCond = elem2Alias + ".ELEMENTID " + BIMRLConstant.tempOp + " " + tmpAlias + ".DEPENDENTELEMENTID AND " + tmpAlias + ".ELEMENTID = " + element + ".ELEMENTID";
            BIMRLInterfaceCommon.appendToString(whereCond, ", ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "dE".elementtype = 'ABC' or "dE".name
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = elem2Tab;
                else
                    retNP.forExpr = elem2Alias;
            }
            return retNP;
        }

        /// <summary>
        /// DEPENDENTTO(E) - returns reference to the host element e.g. DEPENDENTTO(D), or DEPENDENTTO(D, W) - returns T/F
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionDEPENDENTTO(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias must be provided as an argument to DEPENDENTTO(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string elem2Alias = null;
            string whereCond = null;

            if (noPar > 1)
                elem2Alias = inputParams[1];
            else
                elem2Alias = "\"dH\"";

            string tabName = "BIMRL_ELEMENTDEPENDENCY";     // default
            string tmpAlias = "\"dR\"";        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string elem2Tab = "BIMRL_ELEMENT";
            enrolTableIntonodeProperty(elem2Tab, ref elem2Alias, ref retNP);
            whereCond = elem2Alias + ".ELEMENTID " + BIMRLConstant.tempOp + " " + tmpAlias + ".ELEMENTID AND " + tmpAlias + ".DEPENDENTELEMENTID = " + element + ".ELEMENTID";
            BIMRLInterfaceCommon.appendToString(whereCond, ", ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "dH".elementtype = 'ABC' or "dH".name
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = elem2Tab;
                else
                    retNP.forExpr = elem2Alias;
            }
            return retNP;
        }

        /// <summary>
        /// Returning a reference an element as a member of the group GROUPOF(E), or T/F if specifying GROUPOF(E,G)
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionGROUPOF(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias should be provided as an argument to GROUPOF(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string tabName = "BIMRL_RELGROUP";     // default
            string tmpAlias = element;        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string sysTab = "BIMRL_ELEMENT";
            string sysAlias = null;
            if (noPar > 1)
                sysAlias = inputParams[1];
            else
                sysAlias = "\"eGs\"";
            enrolTableIntonodeProperty(sysTab, sysAlias, null, ref retNP);

            string item1 = tmpAlias;
            string item2 = "MemberElementID";

            retNP.keywInj.whereClInjection = item1 + "." + item2 + " = " + element + ".ELEMENTID AND "
                                    + sysAlias + ".ELEMENTID " + BIMRLConstant.tempOp + " " + item1 + ".GROUPELEMENTID ";

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "eGs".elementtype = 'ABC' or "eGs".name
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = sysTab;
                else
                    retNP.forExpr = sysAlias;
            }
            return retNP;
        }

        /// <summary>
        /// HASPROPERTY function. Valid format: HASPROPERTY(E, propertyname, (opt) propertysetname, "where condition",  Qualifier: INSTANCEONLY|TYPEONLY|INSTANCEORTYPE (Default)
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionHASPROPERTY(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty(); 
            if (inputParams.Count < 2)
                throw new BIMRLInterfaceRuntimeException("%Error: An element alias and property should be provided as an argument to HASPROPERTY(E, property)");

            string element = inputParams[0];
            string propertyName = inputParams[1].ToUpper().Replace("\"", ""); // To remove extra "" used in the query to define name that contains a space
            ParsedParams pars = parseParams(2, inputParams);
            if (pars.Where.Index >= 2)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string psetName = null;
            if (noPar > 2)
            {
                // There is the 3rd parameter that specifies the propertygroup/set name
                psetName = inputParams[2].ToUpper().Replace("\"","");   // To remove extra "" used in the query to define name that contains a space
            }

            string tabName = "BIMRL_PROPERTIES"; // default

            // There is a danger here that duplicate qualifier may overlap and override the earlier one, but user should be responsible for it
            if (pars.Qualifiers != null)
            {
                foreach (QualifierParam qPar in pars.Qualifiers)
                {
                    switch (qPar.qEnum)
                    {
                        case BIMRLEnum.functionQualifier.INSTANCEONLY:
                            tabName = "BIMRL_ELEMENTPROPERTIES";
                            break;
                        case BIMRLEnum.functionQualifier.TYPEONLY:
                            tabName = "BIMRL_TYPEPROPERTIES";
                            break;
                        case BIMRLEnum.functionQualifier.INSTANCEORTYPE:
                            tabName = "BIMRL_PROPERTIES";
                            break;
                        default:
                            break;
                    }
                }
            }

            string addCond = null;
            if (!string.IsNullOrEmpty(psetName))
            {
                string op = "=";
                if (psetName.IndexOfAny(new char[] { '%', '_' }) >= 0)
                    op = "LIKE";    // To handle wildcard and turn the statement to use LIKE instead of =
                 addCond = "UPPER(PROPERTYGROUPNAME) " + op +" '" + psetName + "'";
            }

            // BIMRLConstant.notOp to be replaced in expr according to the detailed in the expr
            string opp = "=";
            if (propertyName.IndexOfAny(new char[] { '%', '_' }) >= 0)
                opp = "LIKE";    // To handle wildcard and turn the statement to use LIKE instead of =
            retNP.keywInj.whereClInjection = BIMRLConstant.notOp + " EXISTS (SELECT * FROM " + tabName + " WHERE " + element + ".ELEMENTID = ELEMENTID AND "
                                    + "UPPER(PROPERTYNAME) " + opp + " '" + propertyName + "'";
            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
            retNP.keywInj.whereClInjection += ")";         // close the bracket for EXISTS

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;
          
            return retNP;
        }

        /// <summary>
        /// Returning T/F whether an element has a specific classification, Qualifier: INSTANCEONLY|TYPEONLY|INSTANCEORTYPE (Default)
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionHASCLASSIFICATION(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty(); 
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: An element alias should be provided as an argument to HASCLASSIFICATION(E)");

            string element = inputParams[0];
            string classifCode = null;
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 2)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string classifName = null;
            if (noPar > 1)
            {
                classifCode = inputParams[1].ToUpper();
            }
            if (noPar > 2)
            {
                // There is the 3rd parameter that specifies the classification name
                classifName = inputParams[2].ToUpper();
            }

            string tabName = "BIMRL_CLASSIFASSIGNMENT" ; // default
            string addCond = null;

            // There is a danger here that duplicate qualifier may overlap and override the earlier one, but user should be responsible for it
            if (pars.Qualifiers != null)
            {
                foreach (QualifierParam qPar in pars.Qualifiers)
                {
                    switch (qPar.qEnum)
                    {
                        case BIMRLEnum.functionQualifier.INSTANCEONLY:
                            addCond = "FROMTYPE = 'FALSE'";
                            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                            break;
                        case BIMRLEnum.functionQualifier.TYPEONLY:
                            addCond = "FROMTYPE = 'TRUE'";
                            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                            break;
                        case BIMRLEnum.functionQualifier.INSTANCEORTYPE:
                            break;
                        default:
                            break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(classifCode))
            {
                BIMRLInterfaceCommon.appendToString(" UPPER(CLASSIFICATIONITEMCODE) = '" + classifCode + "'", " AND ", ref addCond);
            }
            if (!string.IsNullOrEmpty(classifName))
            {
                BIMRLInterfaceCommon.appendToString(" AND UPPER(CLASSIFICATIONNAME) = '" + classifName + "'", " AND ", ref addCond);
            }

            // BIMRLConstant.notOp to be replaced in expr according to the detailed in the expr
            retNP.keywInj.whereClInjection = BIMRLConstant.notOp + " EXISTS (SELECT * FROM " + tabName + " WHERE " + element + ".ELEMENTID = ELEMENTID";
            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
            retNP.keywInj.whereClInjection += ")";         // close the bracket for EXISTS

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            return retNP;
        }

        /// <summary>
        /// Returning reference to the material that the element has. MATERIALOF(E) - returning refernce to the instance material, MATERIALOF(E, TYPEONLY) - returning to material points to of its type
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionMATERIALOF(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty(); 
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: An element alias should be provided as an argument to MATERIALOF(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            bool instanceOnly = true;
            // There is a danger here that duplicate qualifier may overlap and override the earlier one, but user should be responsible for it
            if (pars.Qualifiers != null)
            {
                foreach (QualifierParam qPar in pars.Qualifiers)
                {
                    switch (qPar.qEnum)
                    {
                        case BIMRLEnum.functionQualifier.INSTANCEONLY:
                            instanceOnly = true;
                            break;
                        case BIMRLEnum.functionQualifier.TYPEONLY:
                            instanceOnly = false;
                            break;
                        default:
                            instanceOnly = true;
                            break;
                    }
                }
            }

            string matAlias = null;
            string tabName = null;
            if (instanceOnly)
            {

                tabName = "BIMRL_ELEMENTMATERIAL";
                matAlias = "\"eM\"";
                enrolTableIntonodeProperty(tabName, matAlias, null, ref retNP);

                string whereCond = matAlias + ".ELEMENTID = " + element + ".ELEMENTID";
                BIMRLInterfaceCommon.appendToString(whereCond, " AND ", ref retNP.keywInj.whereClInjection);
            }
            else
            {
                tabName = "BIMRL_TYPEMATERIAL";
                matAlias = "\"tM\"";
                enrolTableIntonodeProperty(tabName, matAlias, null, ref retNP);

                string typeTab = "BIMRL_TYPE";
                string typeAlias = "\"tMT\"";
                enrolTableIntonodeProperty(typeTab, typeAlias, null, ref retNP);

                string whereCond = typeAlias + ".ELEMENTID = " + element + ".TYPEID AND " + matAlias + ".ELEMENTID = " + typeAlias + ".ELEMENTID";
                BIMRLInterfaceCommon.appendToString(whereCond, " AND ", ref retNP.keywInj.whereClInjection);
            }

            BIMRLInterfaceCommon.appendToString(pars.Where.Text, " AND ", ref retNP.keywInj.whereClInjection);

            // columnSpec to be used if the function has expr associated to it, e.g. MATERIALOF(E).MaterialName = 'ABC'
            if (byTable)
                retNP.forExpr = tabName;
            else
                retNP.forExpr = matAlias;

            return retNP;

        }

        /// <summary>
        /// Return reference to the Model information: Usage MODELINFO(), or MODELINFO(E)
        /// </summary>
        /// <param name="inputParams">input parameters</param>
        /// <returns></returns>
        public static nodeProperty functionMODELINFO(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count();

            ParsedParams pars = parseParams(0, inputParams);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                noPar--;    // noPar minus 1 because there is where clause
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;
            string element = null;
            if (noPar > 0)
                element = inputParams[0];

            string tmpAlias = "\"mI\"";

            nodeProperty retNP = new nodeProperty();
            string tabName = "BIMRL_MODELINFO";
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);
            
            if (!string.IsNullOrEmpty(element))
            {
                string elemTabName = "BIMRL_ELEMENT";
                enrolTableIntonodeProperty(elemTabName, element, null, ref retNP);
                string whereCond = tmpAlias + ".MODELID = " + element + ".MODELID";
                BIMRLInterfaceCommon.appendToString(whereCond, " AND ", ref retNP.keywInj.whereClInjection);
            }

            // add additional condition if supplied
            BIMRLInterfaceCommon.appendToString(pars.Where.Text, " AND ", ref retNP.keywInj.whereClInjection);

            // This can be used in expr to handle MODELINFO().MODELNAME LIKE 'ABC%' (translated into: "mI".MODELNAME LIKE 'ABC%')
            if (byTable)
                retNP.forExpr = tabName;
            else
                retNP.forExpr = tmpAlias;

            return retNP;
        }

        /// <summary>
        /// Return the reference to the ownerhistory information. OWNERHISTORY(E)
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionOWNERHISTORY(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count();
            if (noPar < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least the element alias should be provided for OWNERHISTORY(E)!");

            string element = inputParams[0];

            ParsedParams pars = parseParams(1, inputParams);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                noPar--;    // noPar minus 1 because there is where clause
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string tmpAlias = "\"oH\"";

            nodeProperty retNP = new nodeProperty();
            string tabName = "BIMRL_OWNERHISTORY";
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string elemTabName = "BIMRL_ELEMENT";
            enrolTableIntonodeProperty(elemTabName, element, null, ref retNP);

            string whereCond = tmpAlias + ".ID = " + element + ".OWNERHISTORYID AND " + tmpAlias + ".MODELID = " + element + ".MODELID";
            BIMRLInterfaceCommon.appendToString(whereCond, " AND ", ref retNP.keywInj.whereClInjection);

            // add additional condition if supplied
            BIMRLInterfaceCommon.appendToString(pars.Where.Text, " AND ", ref retNP.keywInj.whereClInjection);

            // This can be used in expr to handle OWNERHISTORY().OWNINGPERSONNAME LIKE 'ABC%' (translated into: "oH".OWNINGPERSONNAME LIKE 'ABC%')
            if (byTable)
                retNP.forExpr = tabName;
            else
                retNP.forExpr = tmpAlias;

            return retNP;
        }

        /// <summary>
        /// Return the value of the property: PROPERTY(E, propertyname, (opt) propertygroupname, qualifer: INSTANCEONLY|TYPEONLY|INSTANCEORTYPE (default))
        /// </summary>
        /// <param name="inputParams">input parameters</param>
        /// <returns></returns>
        public static nodeProperty functionPROPERTY(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty(); 
            if (inputParams.Count < 2)
                throw new BIMRLInterfaceRuntimeException("%Error: An element alias and a property name should be provided as an argument to PROPERTY(E, propertyname)");

            string element = inputParams[0];
            string propertyName = string.Empty;
            string propertyNames = inputParams[1].ToUpper().Replace("\"", "");  // To remove extra "" used in the query to define name that contains a space
            string[] pNames = propertyNames.Split(',');

            ParsedParams pars = parseParams(2, inputParams);
            if (pars.Where.Index >= 2)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;
            string psetName = null;
            if (noPar > 2)
            {
                // There is the 3rd parameter that specifies the propertygroup/set name
                psetName = inputParams[2].ToUpper().Replace("\"", "");  // To remove extra "" used in the query to define name that contains a space
            }

            string addCond = null;
            string castStr = null;

            string tabName = "BIMRL_PROPERTIES" ;     // default
            string tmpAlias = "\"eP\"";
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            // There is a danger here that duplicate qualifier may overlap and override the earlier one, but user should be responsible for it
            if (pars.Qualifiers != null)
            {
                foreach (QualifierParam qPar in pars.Qualifiers)
                {
                    switch (qPar.qEnum)
                    {
                        case BIMRLEnum.functionQualifier.INSTANCEONLY:
                            addCond = tmpAlias + ".FROMTYPE = 'FALSE'";
                            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                            break;
                        case BIMRLEnum.functionQualifier.TYPEONLY:
                            addCond = tmpAlias + ".FROMTYPE = 'TRUE'";
                            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                            break;
                        case BIMRLEnum.functionQualifier.TO_NUMBER:
                            castStr = "TO_NUMBER";
                            break;
                        case BIMRLEnum.functionQualifier.TO_CHAR:
                            castStr = "TO_CHAR";
                            break;
                        case BIMRLEnum.functionQualifier.INSTANCEORTYPE:
                            break;
                        default:
                            break;
                    }
                }
            }

            string tmpForExpr = string.Empty;
            addCond = null;
            if (!string.IsNullOrEmpty(psetName))
            {
                string op = "=";
                if (psetName.IndexOfAny(new char[] { '%', '_' }) >= 0)
                    op = "LIKE";    // To handle wildcard and turn the statement to use LIKE instead of =

                addCond = "UPPER(" + tmpAlias + ".PROPERTYGROUPNAME) " + op + " '" + psetName + "'";
                enrolColumnIntonodeProperty(tmpAlias, "PROPERTYGROUPNAME", ref retNP);
                //BIMRLCommon.appendToString(tmpAlias + ".PROPERTYGROUPNAME", ", ", ref tmpForExpr);
            }

            string item1 = tmpAlias;
            string item2 = "PROPERTYVALUE";
            enrolColumnIntonodeProperty(item1, item2, ref retNP);

            string pNameCond = string.Empty;
            //enrolColumnIntonodeProperty(item1, "PROPERTYNAME", ref retNP);
            //BIMRLCommon.appendToString(tmpAlias + ".PROPERTYNAME", ", ", ref tmpForExpr);
            foreach (string pName in pNames)
            {
                string pName2 = pName.Replace("'", "").Trim();
                string tmpOp = string.Empty;
                if (pName2.IndexOfAny(new char[] { '%', '_' }) >= 0)
                    tmpOp = "LIKE";
                else
                    tmpOp = "=";

                BIMRLCommon.appendToString("UPPER(" + tmpAlias + ".PROPERTYNAME) " + tmpOp + " '" + pName2 + "'", " OR ", ref pNameCond);
            }
            retNP.keywInj.whereClInjection = element + ".ELEMENTID = " + tmpAlias + ".ELEMENTID AND "
                                    + "(" + pNameCond + ")";
            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. p.PropertyValue = 'ABC'

            string tmpPVal = tmpAlias + ".PROPERTYVALUE";
            if (!string.IsNullOrEmpty(castStr))
                BIMRLCommon.appendToString(castStr + "(" + tmpPVal + ")", ", ", ref tmpForExpr);
            else
                BIMRLCommon.appendToString(tmpPVal, ", ", ref tmpForExpr);

            retNP.forExpr = tmpForExpr;

            return retNP;
        }

        /// <summary>
        /// Function for a simple reference to the Property object
        /// </summary>
        /// <param name="inputParams">Only element alias is needed, but standard WHERE parameter can be added and also ELEMENT/TYPE/OR both (default)</param>
        /// <returns></returns>
        public static nodeProperty functionPROPERTYOF(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: An element alias should be provided as an argument to PROPERTYOF(E)");

            string element = inputParams[0];

            ParsedParams pars = parseParams(2, inputParams);
            if (pars.Where.Index >= 2)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string addCond = null;

            string tabName = "BIMRL_PROPERTIES";     // default
            string tmpAlias = "\"eP\"";
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            // There is a danger here that duplicate qualifier may overlap and override the earlier one, but user should be responsible for it
            if (pars.Qualifiers != null)
            {
                foreach (QualifierParam qPar in pars.Qualifiers)
                {
                    switch (qPar.qEnum)
                    {
                        case BIMRLEnum.functionQualifier.INSTANCEONLY:
                            addCond = tmpAlias + ".FROMTYPE = 'FALSE'";
                            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                            break;
                        case BIMRLEnum.functionQualifier.TYPEONLY:
                            addCond = tmpAlias + ".FROMTYPE = 'TRUE'";
                            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                            break;
                        case BIMRLEnum.functionQualifier.INSTANCEORTYPE:
                            break;
                        default:
                            break;
                    }
                }
            }

            retNP.keywInj.whereClInjection = element + ".ELEMENTID = " + tmpAlias + ".ELEMENTID";
            BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            if (byTable)
                retNP.forExpr = tabName;
            else
                retNP.forExpr = tmpAlias;

            return retNP;
        }

        /// <summary>
        /// Get SpaceBoundary information. It has qualifier option that uses geometry to collect the information. It returns reference to the spaceboundary element of a space
        /// Usage: SPACEBOUNDARY(S), Qualifer: USEGEOMETRY
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionSPACEBOUNDARY(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: A space alias name should be provided as an argument to SPACEBOUNDARY(S)");

            string space = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string tabName = "BIMRL_RELSPACEBOUNDARY";     // default
            string tmpAlias = space;        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            nodeProperty retNP2 = new nodeProperty();
            string boundTab = "BIMRL_ELEMENT" ;
            string boundAlias = "\"eSb\"";
            enrolTableIntonodeProperty(boundTab, ref boundAlias, ref retNP2);
            BIMRLInterfaceCommon.appendToString(retNP2.keywInj.tabProjInjection, ", ", ref retNP.keywInj.tabProjInjection);

            // There is a danger here that duplicate qualifier may overlap and override the earlier one, but user should be responsible for it
            if (pars.Qualifiers != null)
            {
                foreach (QualifierParam qPar in pars.Qualifiers)
                {
                    switch (qPar.qEnum)
                    {
                        case BIMRLEnum.functionQualifier.USEGEOMETRY:
                            // We need to deal with it very specially, especially when the operator is = .F. -> may need deferred execution, and/or alternate statement
                            break;
                        default:
                            break;
                    }
                }
            }

            string item1 = tmpAlias;
            string item2 = "SpaceElementID";
            enrolColumnIntonodeProperty(item1, item2, ref retNP);

            retNP.keywInj.whereClInjection = item1 + "." + item2 + " = " + space + ".ELEMENTID AND "
                                    + boundAlias + ".ELEMENTID = " + item1 + ".BOUNDARYELEMENTID";

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "eSb".elementtype = 'ABC' or "eSb".name
            if (byTable)
                retNP.forExpr = boundTab;
            else
                retNP.forExpr = boundAlias;

            return retNP;
        }

        /// <summary>
        /// Get System of an element E if SYSTEMOF(E), or return T/F if SYSTEMOF(E, Y)
        /// </summary>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static nodeProperty functionSYSTEMOF(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias should be provided as an argument to SYSTEMOF(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string tabName = "BIMRL_RELGROUP";     // default
            string tmpAlias = element;        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string sysTab = "BIMRL_ELEMENT";
            string sysAlias = null;
            if (noPar > 1)
                sysAlias = inputParams[1];
            else
                sysAlias = "\"eGs\"";
            enrolTableIntonodeProperty(sysTab, sysAlias, null, ref retNP);

            string item1 = tmpAlias;
            string item2 = "MemberElementID";
            //enrolColumnIntonodeProperty(item1, item2, ref retNP);

            retNP.keywInj.whereClInjection = item1 + "." + item2 + " = " + element + ".ELEMENTID AND "
                                    + sysAlias + ".ELEMENTID " + BIMRLConstant.tempOp + " " + item1 + ".GROUPELEMENTID AND GROUPELEMENTTYPE = 'IFCSYSTEM'";

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "eGs".elementtype = 'ABC' or "eGs".name
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = sysTab;
                else
                    retNP.forExpr = sysAlias;
            }
            return retNP;
        }

        /// <summary>
        /// Check unique value of a property of an object. Usage: UNIQUEVALUE(E, propertyname, (opt) propertygroupname, qualifier: INSTANCEONLY|TYPEONLY|INSTANCEORTYPE (default)
        /// </summary>
        /// <param name="inputParams">input parameters</param>
        /// <returns></returns>
        public static nodeProperty functionUNIQUEVALUE(IList<string> inputParams, bool byTable = false)
        {
            string[] validDirAttrs = { "NAME", "LONGNAME", "DESCRIPTION", "OBJECTTYPE", "TAG" };
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 2)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias and a property name should be provided as an argument to UNIQUEVALUE(E, <Propertyname>)");

            string element = inputParams[0];
            string propertyName = inputParams[1].Replace("\"", "");   // To remove extra "" used in the query to define name that contains a space
            
            ParsedParams pars = parseParams(2, inputParams);
            if (pars.Where.Index >= 2)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string etabName = "BIMRL_ELEMENT";

            bool isDirectAttr = false;
            string propGroupName = null;
            if (noPar <= 2)
            {
                foreach (string validDirAttr in validDirAttrs)
                {
                    if (string.Compare(validDirAttr, propertyName, true) == 0)
                    {
                        isDirectAttr = true;
                        break;
                    }
                }
                if (!isDirectAttr)
                    throw new BIMRLInterfaceRuntimeException("%Error: PropertyName '" + propertyName + "' is not part of the direct attributes!");

                string colProj = null;
                if (!ColumnListManager.checkAndRegisterColumnItems(element, propertyName))
                    colProj = "'" + propertyName.ToUpper() + "', " + element + "." + propertyName + ", count(*) Count";
                else
                    colProj = "'" + propertyName.ToUpper() + ", count(*) Count";
                BIMRLInterfaceCommon.appendToString(colProj, ", ", ref retNP.keywInj.colProjInjection);

                //int idx = -1;
                //if (!TableListManager.checkTableAndAlias(etabName, element, out idx))
                //    BIMRLInterfaceCommon.appendToString(etabName + " " + element, ", ", ref retNP.keywInj.tabProjInjection);
                enrolTableIntonodeProperty(etabName, element, null, ref retNP);

                if (!string.IsNullOrEmpty(pars.Where.Text))
                    BIMRLInterfaceCommon.appendToString(pars.Where.Text, " AND ", ref retNP.keywInj.whereClInjection);
                else
                    BIMRLInterfaceCommon.appendToString("1=1", " AND ", ref retNP.keywInj.whereClInjection);

                string suffix = "GROUP BY " + element + "." + propertyName + " HAVING COUNT(*) " + BIMRLConstant.tempOp + "1";   // the 1=1 is a dummy required to make sure this can be inserted into Where clause
                BIMRLInterfaceCommon.appendToString(suffix, " ", ref retNP.keywInj.suffixInjection);
            }
            else
            {
                propGroupName = inputParams[2].ToUpper().Replace("\"", "");   // To remove extra "" used in the query to define name that contains a space

                if (!string.IsNullOrEmpty(pars.Where.Text))
                    BIMRLInterfaceCommon.appendToString(pars.Where.Text, " AND ", ref retNP.keywInj.whereClInjection);

                string addCond = null;

                // There is a danger here that duplicate qualifier may overlap and override the earlier one, but user should be responsible for it
                if (pars.Qualifiers != null)
                {
                    foreach (QualifierParam qPar in pars.Qualifiers)
                    {
                        switch (qPar.qEnum)
                        {
                            case BIMRLEnum.functionQualifier.INSTANCEONLY:
                                addCond = "FROMTYPE = 'FALSE'";
                                BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                                break;
                            case BIMRLEnum.functionQualifier.TYPEONLY:
                                addCond = "FROMTYPE = 'TRUE'";
                                BIMRLInterfaceCommon.appendToString(addCond, " AND ", ref retNP.keywInj.whereClInjection);
                                break;
                            case BIMRLEnum.functionQualifier.INSTANCEORTYPE:
                                break;
                            default:
                                break;
                        }
                    }
                }
                //int idx = -1;
                //if (!TableListManager.checkTableAndAlias(etabName, element, out idx))
                //    BIMRLInterfaceCommon.appendToString(etabName + " " + element, ", ", ref retNP.keywInj.tabProjInjection);
                enrolTableIntonodeProperty(etabName, element, null, ref retNP);

                string tabName = "BIMRL_PROPERTIES";
                string tabAlias = "\"eP\"";
                //idx = -1;
                //if (!TableListManager.checkTableAndAlias(tabName, tabAlias, out idx))
                //    BIMRLInterfaceCommon.appendToString(tabName + " " + tabAlias, ", ", ref retNP.keywInj.tabProjInjection);
                enrolTableIntonodeProperty(tabName, tabAlias, null, ref retNP);

                string colProj = null;
                if (!string.IsNullOrEmpty(propGroupName))
                {
                    if (!ColumnListManager.checkAndRegisterColumnItems(tabAlias, "PROPERTYGROUPNAME"))
                    {
                        colProj = tabAlias + ".PROPERTYGROUPNAME";
                        BIMRLInterfaceCommon.appendToString(colProj, ", ", ref retNP.keywInj.colProjInjection);
                    }
                }

                if (!ColumnListManager.checkAndRegisterColumnItems(element, "PROPERTYNAME"))
                    colProj = tabAlias + ".PROPERTYNAME" + ", count(*) Count";
                else
                    colProj = "count(*) Count";
                BIMRLInterfaceCommon.appendToString(colProj, ", ", ref retNP.keywInj.colProjInjection);

                // BIMRLConstant.notOp to be replaced in expr according to the detailed in the expr

                string where = tabAlias + ".ELEMENTID = " + element + ".ELEMENTID";
                BIMRLInterfaceCommon.appendToString(where, " AND ", ref retNP.keywInj.whereClInjection);

                if (!string.IsNullOrEmpty(propGroupName))
                {
                    string op = "=";
                    if (propGroupName.IndexOfAny(new char[] { '%', '_' }) >= 0)
                        op = "LIKE";
                    where = "UPPER(" + tabAlias + ".PROPERTYGROUPNAME) " + op + " '" + propGroupName.ToUpper() + "'";
                    BIMRLInterfaceCommon.appendToString(where, " AND ", ref retNP.keywInj.whereClInjection);
                }

                string opp = "=";
                if (propertyName.IndexOfAny(new char[] { '%', '_' }) >= 0)
                    opp = "LIKE";
                where = "UPPER (" + tabAlias + ".PROPERTYNAME) " + opp + " '" + propertyName.ToUpper() + "'";
                BIMRLInterfaceCommon.appendToString(where, " AND ", ref retNP.keywInj.whereClInjection);

                BIMRLInterfaceCommon.appendToString("GROUP BY", " ", ref retNP.keywInj.suffixInjection);

                where = null;
                if (!string.IsNullOrEmpty(propGroupName))
                {
                    where = tabAlias + ".PROPERTYGROUPNAME, ";
                }
                where += tabAlias + ".PROPERTYNAME, " + tabAlias + ".PROPERTYVALUE HAVING COUNT(*) " + BIMRLConstant.tempOp + " 1";
                BIMRLInterfaceCommon.appendToString(where, " ", ref retNP.keywInj.suffixInjection);
            }

            return retNP;
        }

        /// <summary>
        /// Get Zone of the element specified ZONEOF(E), or return T/F if ZONEOF(E, Z)
        /// </summary>
        /// <param name="inputParams">List of input parameters</param>
        /// <returns></returns>
        public static nodeProperty functionZONEOF(IList<string> inputParams, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias should be provided as an argument to ZONEOF(E)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;
            if (pars.Qualifiers != null)
                noPar -= pars.Qualifiers.Count;

            string tabName = "BIMRL_RELGROUP";     // default
            string tmpAlias = element;        // We will use the same alias as the element
            enrolTableIntonodeProperty(tabName, tmpAlias, null, ref retNP);

            string sysTab = "BIMRL_ELEMENT";
            string sysAlias = null;
            if (noPar > 1)
                sysAlias = inputParams[1];
            else
                sysAlias = "\"eGs\"";
            enrolTableIntonodeProperty(sysTab, sysAlias, null, ref retNP);

            string item1 = tmpAlias;
            string item2 = "MemberElementID";
            enrolColumnIntonodeProperty(item1, item2, ref retNP);

            retNP.keywInj.whereClInjection = item1 + "." + item2 + " = " + element + ".ELEMENTID AND "
                                    + sysAlias + ".ELEMENTID " + BIMRLConstant.tempOp + " " + item1 + ".GROUPELEMENTID AND GROUPELEMENTTYPE = 'IFCZONE'";

            if (!string.IsNullOrEmpty(pars.Where.Text))
                retNP.keywInj.whereClInjection += " AND " + pars.Where.Text;

            // columnSpec to be used if the function has expr associated to it, e.g. "eGs".elementtype = 'ABC' or "eGs".name
            if (noPar <= 1)
            {
                if (byTable)
                    retNP.forExpr = sysTab;
                else
                    retNP.forExpr = sysAlias;
            }
            return retNP;
        }

        public static nodeProperty faceOrientation(IList<string> inputParams, BIMRLEnum.functionQualifier fQual, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias should be provided as an argument to TOP(E) | BOTTOM(E) | SIDE(E) | UNDERSIDE(E) | TOPSIDE(E()");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;

            string orientation;
            if (fQual == BIMRLEnum.functionQualifier.TOP)
                orientation = "'" + BIMRLEnum.functionQualifier.TOP.ToString() + "'";
            else if (fQual == BIMRLEnum.functionQualifier.BOTTOM)
                orientation = "'" + BIMRLEnum.functionQualifier.BOTTOM.ToString() + "'";
            else if (fQual == BIMRLEnum.functionQualifier.SIDE)
                orientation = "'" + BIMRLEnum.functionQualifier.SIDE.ToString() + "'";
            else if (fQual == BIMRLEnum.functionQualifier.TOPSIDE)
                orientation = "'" + BIMRLEnum.functionQualifier.TOPSIDE.ToString() + "'";
            else if (fQual == BIMRLEnum.functionQualifier.UNDERSIDE)
                orientation = "'" + BIMRLEnum.functionQualifier.UNDERSIDE.ToString() + "'";
            else
                return retNP;

            string tabname = "BIMRL_TOPO_FACE";
            string alias = "\"fO\"";
            enrolTableIntonodeProperty(tabname, alias, null, ref retNP);
            string whereCl = alias + ".ORIENTATION = " + orientation + " AND " + alias + ".ELEMENTID = " + element + ".ELEMENTID" ;
            //string item1 = alias;
            //string item2 = "ID";
            //enrolColumnIntonodeProperty(item1, item2, ref retNP);
            if (byTable)
                retNP.forExpr = tabname;
            else
                retNP.forExpr = alias;

            retNP.keywInj.whereClInjection = whereCl;
            return retNP;
        }

        public static nodeProperty functionDOORLEAF(IList<string> inputParams, BIMRLEnum.functionQualifier fQual, bool byTable = false)
        {
            int noPar = inputParams.Count;
            nodeProperty retNP = new nodeProperty();
            if (inputParams.Count < 1)
                throw new BIMRLInterfaceRuntimeException("%Error: At least an element alias should be provided as an argument to DOORLEAF(D)");

            string element = inputParams[0];
            ParsedParams pars = parseParams(1, inputParams);
            if (pars.Where.Index >= 1)
                noPar--;

            string sqlStmt = "SELECT A.ELEMENTID, A.ID FROM BIMRL_TOPO_FACE A, BIMRL_ELEMENT " + element + " WHERE A.ELEMENTID=" + element + ".ELEMENTID AND ATTRIBUTE='PANEL - FRONT'";
            sqlStmt = sqlStmt = BIMRLKeywordMapping.expandBIMRLTables(sqlStmt);

            OracleCommand cmd = new OracleCommand(sqlStmt, DBOperation.DBConn);
            OracleDataReader reader = cmd.ExecuteReader();
            string elemid = "";
            string faceid = "";

            if (reader.Read())
            {
                elemid = reader.GetString(0);
                faceid = reader.GetString(1);
            }

            if (reader.RowSize > 0)
            {

            }
            else
            {

            }

            reader.Dispose();

            string tabname = "BIMRL_TOPO_FACE";
            string alias = "\"fO\"";
            enrolTableIntonodeProperty(tabname, alias, null, ref retNP);
            //string whereCl = alias + ".ORIENTATION = " + orientation + " AND " + alias + ".ELEMENTID = '" + element + "'";
            string item1 = alias;
            string item2 = "ID";
            enrolColumnIntonodeProperty(item1, item2, ref retNP);

            if (byTable)
                retNP.forExpr = tabname;
            else
                retNP.forExpr = alias;

            return retNP;
        }

        static string validAlias(string startingAlias)
        {
            string tmpAlias = startingAlias;
            int cnt = 1;
            while (TableListManager.aliasRegistered(tmpAlias))
                tmpAlias = startingAlias + cnt++.ToString();

            TableListManager.registerAliasOnly(tmpAlias);

            return tmpAlias;
        }

        /// <summary>
        /// Parse the input parameters. It needs to recognize and tag the following keywords:
        ///     WHERE:, 
        /// </summary>
        /// <param name="startIdx"></param>
        /// <param name="inputParams"></param>
        /// <returns></returns>
        public static ParsedParams parseParams(int startIdx, IList<string> inputParams)
        {
            ParsedParams res = new ParsedParams();

            if (inputParams.Count() <= startIdx)            // no of params is less than the requested index
                return res;

            List<string> unparsedInput = new List<string>();
            for (int i = startIdx; i < inputParams.Count(); ++i)
            {
                if (string.IsNullOrEmpty(inputParams[i]))
                    continue;   // skip empty string

                if (inputParams[i].StartsWith("'"))
                    inputParams[i] = inputParams[i].Trim('\'');      // remove leading and trailing single quote if any
                else
                    inputParams[i] = inputParams[i].Trim('"');

                string[] words = inputParams[i].Split(' ');
                BIMRLEnum.functionQualifier qualif;
                if (Enum.TryParse(words[0].ToUpper(), out qualif))
                {
                    QualifierParam qualifP = new QualifierParam { qEnum = qualif, Index = i };
                    res.Qualifiers.Add(qualifP);
                }
                else if (string.Compare("WHERE", 0, words[0], 0, 5, true) == 0)
                {
                    string tmp = inputParams[i].Remove(0, 5).Trim();
                    if (!string.IsNullOrEmpty(tmp))
                        res.Where = new FunctionParam { Text = tmp, Index = i };
                }
                //else if (string.Compare("KEYFIELDS", 0, words[0], 0, 9, true) == 0)
                //{
                //    string tmp = inputParams[i].Remove(0, 9).Trim();
                //    if (!string.IsNullOrEmpty(tmp))
                //    {
                //        string[] keyVals = tmp.Split(',');
                //        List<string> keyFieldVals = new List<string>();
                //        foreach (string keyV in keyVals)
                //        {
                //            keyFieldVals.Add(keyV);
                //        }
                //        res.KeyFields = new FunctionParamList { pars = keyFieldVals, Index = i };
                //    }
                //}
                else if (string.Compare("EXCLUDEID", 0, words[0], 0, 9, true) == 0)
                {
                    string tmp = inputParams[i].Remove(0, 9).Trim();
                    if (!string.IsNullOrEmpty(tmp))
                    {
                        string[] excVals = tmp.Split(',');
                        List<string> excFieldVals = new List<string>();
                        foreach (string keyV in excVals)
                        {
                            excFieldVals.Add(keyV);
                        }
                        res.ExceptionFields = new FunctionParamList { pars = excFieldVals, Index = i };
                    }
                }
                else if (string.Compare("AGGREGATE", 0, words[0], 0, 9, true) == 0)
                {
                    string tmp = inputParams[i].Remove(0, 9).Trim();
                    tmp = tmp.Replace("(", "").Replace(")", "").Trim();

                    if (!string.IsNullOrEmpty(tmp))
                    {
                        string[] aggrPars = tmp.Split(',');
                        List<string> aggrParList = new List<string>();
                        foreach (string aggrPar in aggrPars)
                        {
                            aggrParList.Add(aggrPar);
                        }
                        res.AggregateFields = new FunctionParamList { pars = aggrParList, Index = i };
                        res.Qualifiers.Add(new QualifierParam { qEnum = BIMRLEnum.functionQualifier.AGGREGATE, Index = i });
                    }
                }
                else
                {
                    unparsedInput.Add(inputParams[i]);
                }
            }
            if (unparsedInput.Count > 0)
                res.unparsedParams = unparsedInput.ToArray();

            return res;
        }

        static void enrolTableIntonodeProperty(string tabName, ref string tabAlias, ref nodeProperty nodeP)
        {
            int idx;
            if (!TableListManager.checkTableAndAlias(tabName, tabAlias, out idx))
            {
                tabAlias = validAlias(tabAlias);    // check and find non-conflicting alias
                string tabStr = tabName + " " + tabAlias;
                BIMRLInterfaceCommon.appendToString(tabStr, ", ", ref nodeP.keywInj.tabProjInjection);
                TableSpec tabSpec = new TableSpec { tableName = tabName, alias = tabAlias };
                TableListManager.addOrUpdateMember(BIMRLEnum.Index.NEW, tabSpec);
            }
        }

        static void enrolTableIntonodeProperty(string tabName, string tabAlias, string originalName, ref nodeProperty nodeP)
        {
            int idx;
            if (!TableListManager.checkTableAndAlias(tabName, tabAlias, out idx))
            {
                string tabStr = tabName + " " + tabAlias;
                BIMRLInterfaceCommon.appendToString(tabStr, ", ", ref nodeP.keywInj.tabProjInjection);
                TableSpec tabSpec = new TableSpec { tableName = tabName, alias = tabAlias, originalName = originalName};
                TableListManager.addOrUpdateMember(BIMRLEnum.Index.NEW, tabSpec);
            }
        }


        static void enrolColumnIntonodeProperty(string colSpecItem1, string colSpecItem2, ref nodeProperty nodeP)
        {
            int idx;
            if (!ColumnListManager.checkColumnItems(colSpecItem1, colSpecItem2, out idx))
            {
                string colStr = colSpecItem1 + "." + colSpecItem2;
                BIMRLInterfaceCommon.appendToString(colStr, ", ", ref nodeP.keywInj.colProjInjection);
                ColumnSpec colSpec = new ColumnSpec { item1 = colSpecItem1, item2 = colSpecItem2, alias = null };
                ColumnListManager.addMember(colSpec);
            }
        }
    }
}
