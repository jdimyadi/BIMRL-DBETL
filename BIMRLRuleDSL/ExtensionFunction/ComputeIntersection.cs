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
    public class ComputeIntersection : ExtensionFunctionBase, IBIMRLExtensionFunction
    {
        public ComputeIntersection()
        {

        }

        /// <summary>
        /// VolumeIntersection function expects the following properties to be set: KEYFIELDS
        /// </summary>
        /// <param name="inputDT"></param>
        /// <param name="inputParams"></param>
        public override void InvokeRule(DataTable inputDT, params string[] inputParams)
        {
            base.InvokeRule(inputDT, inputParams);

            inputParamStruct par1;
            string spIdxTab1 = "BIMRL_SPATIALINDEX";
            string spTab1 = "BIMRL_ELEMENT";
            inputParamStruct par2;
            string spIdxTab2 = "BIMRL_SPATIALINDEX";
            string spTab2 = "BIMRL_ELEMENT";
            DBQueryManager dbQ = new DBQueryManager();

            checkInputParam(inputParams[0], out par1);
            if (string.Compare(par1.domainName, "USERGEOM", true) == 0)
            {
                spIdxTab1 = "USERGEOM_SPATIALINDEX";
                spTab1 = "USERGEOM_GEOMETRY";
            }
            checkInputParam(inputParams[1], out par2);
            if (string.Compare(par2.domainName, "USERGEOM", true) == 0)
            {
                spIdxTab2 = "USERGEOM_SPATIALINDEX";
                spTab2 = "USERGEOM_GEOMETRY";
            }

            // This rule currently does a very simple thing: find intersection of objects specified in the inputDT using KeyFields against objects (IFC*) from the params
            // Here we will consider both WHERE and EXCLUDEID. 
            //      WHERE statement should refer to additional filter of IFC* objects to be tested against.
            //      EXCLUDEID list will be used to exclude items that are part of the Eval results that may be found in the intersection results
            // Based on Qualifier, we may implement the more exact test in future of the candidates using direct object - object intersection (slower)

            DataColumn column;

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.Int32");
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

            DataTable outputDetails = new DataTable();
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "ELEMENTID";
            column.ReadOnly = false;
            column.Unique = false;
            outputDetails.Columns.Add(column);    // Add result details (elementids that intersects) column            column = new DataColumn();
            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "OUTPUTDETAILS";
            column.ReadOnly = false;
            column.Unique = false;
            outputDetails.Columns.Add(column);    // Add result details (elementids that intersects) column

            bool doExact = false;
            foreach (BIMRLEnum.functionQualifier qPar in Qualifiers)
            {
                if (qPar == BIMRLEnum.functionQualifier.EXACT)
                    doExact = true;
            }

            Dictionary<string, List<Tuple<string, string>>> finalResult = new Dictionary<string, List<Tuple<string, string>>>();
            foreach (DataRow dtRow in inputDT.Rows)
            {
                bool hasIntersection = false;
                string elemID = dtRow[par1.sourceColumn].ToString();
                keywordInjection keywI = new keywordInjection();

                keywI.colProjInjection = "T2.ELEMENTID";
                keywI.tabProjInjection = spIdxTab2 + " T2, " + spIdxTab1 + " T1 ";
                keywI.whereClInjection = "T1." + par1.sourceColumn + "='" + elemID + "' AND T2.CELLID=T1.CELLID ";

                string excludeCond = "";
                if (ExceptionFields != null)
                {
                    foreach (string excludeID in ExceptionFields)
                    {
                        string keyVal = dtRow[excludeID].ToString();
                        BIMRLInterfaceCommon.appendToString("'" + keyVal + "'", ",", ref excludeCond);
                    }
                }

                if (!string.IsNullOrEmpty(excludeCond))
                    excludeCond = " T2.ELEMENTID NOT IN (" + excludeCond + ")";
                BIMRLInterfaceCommon.appendToString(excludeCond, " AND ", ref keywI.whereClInjection);

                string addTab = "";
                string addTabAlias = "";
                if (!string.IsNullOrEmpty(par2.sourceTable))
                {
                    string[] tmp = par2.sourceTable.Split();
                    if (tmp.Length > 1)
                    {
                        addTab = par2.sourceTable;
                        addTabAlias = tmp[1];
                    }
                    else
                    {
                        addTabAlias = "T3";
                        addTab = par2.sourceTable + " " + addTabAlias;
                    }
                    BIMRLInterfaceCommon.appendToString(addTab, ", ", ref keywI.tabProjInjection);
                    BIMRLInterfaceCommon.appendToString("T2.ELEMENTID=" + addTabAlias + "." + par2.sourceColumn, " AND ", ref keywI.whereClInjection);
                }

                if (doExact)
                {
                    if (par1.domainName == "USERGEOM")
                        BIMRLInterfaceCommon.appendToString("ET1.GEOMETRY GEOM1", ", ", ref keywI.colProjInjection);
                    else
                        BIMRLInterfaceCommon.appendToString("ET1.GEOMETRYBODY GEOM1", ", ", ref keywI.colProjInjection);
                    BIMRLInterfaceCommon.appendToString(spTab1 + " ET1", ", ", ref keywI.tabProjInjection);
                    BIMRLInterfaceCommon.appendToString("ET1.ELEMENTID=T1.ELEMENTID", " AND ", ref keywI.whereClInjection);

                    if (par2.domainName == "USERGEOM")
                        BIMRLInterfaceCommon.appendToString("ET2.GEOMETRY GEOM2", ", ", ref keywI.colProjInjection);
                    else
                        BIMRLInterfaceCommon.appendToString("ET2.GEOMETRYBODY GEOM2", ", ", ref keywI.colProjInjection);
                    BIMRLInterfaceCommon.appendToString(spTab2 + " ET2", ", ", ref keywI.tabProjInjection);
                    BIMRLInterfaceCommon.appendToString("ET2.ELEMENTID=T2.ELEMENTID", " AND ", ref keywI.whereClInjection);
                }

                string sqlStmt = "SELECT " + keywI.colProjInjection + " FROM " + keywI.tabProjInjection + " WHERE " + keywI.whereClInjection + " ORDER BY T2.ELEMENTID";

                DataTable intersElem = dbQ.queryMultipleRows(sqlStmt);
                //string intersElemList = "";

                string prevElemID = "";
                foreach (DataRow dtRow2 in intersElem.Rows)
                {
                    string elemI = dtRow2["ELEMENTID"].ToString();
                    if (string.Compare(prevElemID, elemI) == 0)
                        continue;           // Skip duplicate elementids
                    else
                        prevElemID = elemI;

                    if (!doExact)
                    {
                        //BIMRLInterfaceCommon.appendToString(elemI, ",", ref intersElemList);
                        DataRow row = outputDetails.NewRow();
                        row["ELEMENTID"] = elemID;
                        row["OUTPUTDETAILS"] = elemI;
                        outputDetails.Rows.Add(row);
                        hasIntersection = true;
                    }
                    else
                    {
                        SdoGeometry geom1 = dtRow2["GEOM1"] as SdoGeometry;
                        object geom1Out;
                        SdoGeometryTypes.GTYPE geomTyp1 = SDOGeomUtils.generateGeometryFromSDO(geom1, out geom1Out);

                        SdoGeometry geom2 = dtRow2["GEOM2"] as SdoGeometry;
                        object geom2Out;
                        SdoGeometryTypes.GTYPE geomTyp2 = SDOGeomUtils.generateGeometryFromSDO(geom2, out geom2Out);

                        bool test = false;
                        if (geom1Out is Polyhedron)
                        {
                            if (geom2Out is Polyhedron)
                                test = Polyhedron.intersect(geom1Out as Polyhedron, geom2Out as Polyhedron);
                            else if (geom2Out is Face3D)
                                test = Polyhedron.intersect(geom1Out as Polyhedron, geom2Out as Face3D);
                            else if (geom2Out is List<LineSegment3D>)
                            {
                                LineSegment3D geom2Out0 = ((List<LineSegment3D>)geom2Out)[0];
                                test = Polyhedron.intersect(geom1Out as Polyhedron, geom2Out0);
                            }
                        }
                        else if (geom1Out is Face3D)
                        {
                            if (geom2Out is Polyhedron)
                                test = Polyhedron.intersect(geom2Out as Polyhedron, geom1Out as Face3D);
                            else if (geom2Out is Face3D)
                            {
                                LineSegment3D outLS;
                                FaceIntersectEnum mode;
                                test = Face3D.intersect(geom1Out as Face3D, geom2Out as Face3D, out outLS, out mode);
                            }
                            else if (geom2Out is List<LineSegment3D>)
                            {
                                LineSegment3D geom2Out0 = ((List<LineSegment3D>)geom2Out)[0];
                                List<Point3D> outPt;
                                test = Face3D.intersect(geom1Out as Face3D, geom2Out0 as LineSegment3D, out outPt);
                            }
                        }
                        else if (geom1Out is List<LineSegment3D>)
                        {
                            LineSegment3D geom1Out0 = ((List<LineSegment3D>)geom1Out)[0];
                            if (geom2Out is Polyhedron)
                                test = Polyhedron.intersect(geom2Out as Polyhedron, geom1Out0);
                            else if (geom2Out is Face3D)
                            {
                                List<Point3D> outPt;
                                test = Face3D.intersect(geom2Out as Face3D, geom1Out0, out outPt);
                            }
                            else if (geom2Out is List<LineSegment3D>)
                            {
                                LineSegment3D geom2Out0 = ((List<LineSegment3D>)geom2Out)[0];
                                Point3D outPt;
                                LineSegmentIntersectEnum mode;
                                test = LineSegment3D.intersect(geom2Out0, geom1Out0, out outPt, out mode);
                            }
                        }
                        else if (geom2Out is Polyhedron)
                        {
                            if (geom1Out is Polyhedron)
                                test = Polyhedron.intersect(geom2Out as Polyhedron, geom1Out as Polyhedron);
                            else if (geom1Out is Face3D)
                                test = Polyhedron.intersect(geom2Out as Polyhedron, geom1Out as Face3D);
                            else if (geom1Out is List<LineSegment3D>)
                            {
                                LineSegment3D geom1Out0 = ((List<LineSegment3D>)geom1Out)[0];
                                test = Polyhedron.intersect(geom2Out as Polyhedron, geom1Out0);
                            }
                        }
                        else if (geom2Out is Face3D)
                        {
                            if (geom1Out is Polyhedron)
                                test = Polyhedron.intersect(geom1Out as Polyhedron, geom2Out as Face3D);
                            else if (geom1Out is Face3D)
                            {
                                LineSegment3D outLS;
                                FaceIntersectEnum mode;
                                test = Face3D.intersect(geom1Out as Face3D, geom2Out as Face3D, out outLS, out mode);
                            }
                            else if (geom1Out is List<LineSegment3D>)
                            {
                                LineSegment3D geom1Out0 = ((List<LineSegment3D>)geom1Out)[0];
                                List<Point3D> outPt;
                                test = Face3D.intersect(geom2Out as Face3D, geom1Out0, out outPt);
                            }
                        }
                        else if (geom2Out is List<LineSegment3D>)
                        {
                            LineSegment3D geom2Out0 = ((List<LineSegment3D>)geom2Out)[0];
                            if (geom1Out is Polyhedron)
                                test = Polyhedron.intersect(geom1Out as Polyhedron, geom2Out0);
                            else if (geom1Out is Face3D)
                            {
                                List<Point3D> outPt;
                                test = Face3D.intersect(geom1Out as Face3D, geom2Out0, out outPt);
                            }
                            else if (geom1Out is List<LineSegment3D>)
                            {
                                LineSegment3D geom1Out0 = ((List<LineSegment3D>)geom1Out)[0];
                                Point3D outPt;
                                LineSegmentIntersectEnum mode;
                                test = LineSegment3D.intersect(geom2Out0, geom1Out0, out outPt, out mode);
                            }
                        }

                        // If the exact intersection test returns true, add elementid into the list
                        if (test)
                        {
                            DataRow row = outputDetails.NewRow();
                            row["ELEMENTID"] = elemID;
                            row["OUTPUTDETAILS"] = elemI;
                            outputDetails.Rows.Add(row);
                            hasIntersection = true;
                        }
                    }
                }

                if (hasIntersection)
                {
                    dtRow["OUTPUT"] = 1;
                }
                else
                {
                    dtRow["OUTPUT"] = 0;
                }
            }

            m_Result = inputDT;
            if (outputDetails.Rows.Count > 0)
            {
                // Insert elementid of collided objects into USERGEOM_OUTPUTDETAILS table (temp)
                //dbQ.insertFromDataTable("USERGEOM_OUTPUTDETAILS", outputDetails);
                dbQ.createTableFromDataTable("USERGEOM_OUTPUTDETAILS", outputDetails, false, true);
            }
        }
    }
}
