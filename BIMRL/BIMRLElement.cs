using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Xbim.IO;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Ifc2x3.SharedBldgServiceElements;
using Xbim.Ifc2x3.StructuralElementsDomain;
using Xbim.Ifc2x3.StructuralAnalysisDomain;
using Xbim.Ifc2x3.SharedComponentElements;
using Xbim.Ifc2x3.ElectricalDomain;
using Xbim.Ifc2x3.RepresentationResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.MeasureResource;
using Xbim.XbimExtensions;
using Xbim.XbimExtensions.SelectTypes;
using Xbim.ModelGeometry;
using Xbim.ModelGeometry.Scene;
using Xbim.XbimExtensions.Interfaces;
using Xbim.Ifc2x3.Extensions;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Xbim.Common.Exceptions;
using System.Diagnostics;
using Xbim.Ifc2x3.ActorResource;
using Xbim.Ifc2x3.PropertyResource;
using Xbim.Common.Geometry;
using XbimGeometry.Interfaces;
using Xbim.ModelGeometry.Converter;
using Xbim.Ifc2x3.ExternalReferenceResource;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.OctreeLib;

namespace BIMRL
{
    public class BIMRLElement
    {
        XbimModel _model;
        BIMRLCommon _refBIMRLCommon;

        struct UnitSetting
        {
            public string unitName;
            public string unitType;    // "METRIC" or "IMPERIAL"
            public double conversionFactor;
        }

        public BIMRLElement(XbimModel m, BIMRLCommon refBIMRLCommon)
        {
            _model = m;
            _refBIMRLCommon = refBIMRLCommon;
        }

        private void processGeometries()
        {
            DBOperation.beginTransaction();
            string currStep = string.Empty;

            int commandStatus = -1;
            int currInsertCount = 0;

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            XbimMatrix3D m3D = new XbimMatrix3D();

            foreach (int productLabel in _refBIMRLCommon.insEntityLabelList)
            {
                IEnumerable<XbimGeometryData> geomDataList = _model.GetGeometryData(productLabel, XbimGeometryType.TriangulatedMesh);
                string prodGuid = _refBIMRLCommon.guidLineNoMapping_Getguid(bimrlProcessModel.currModelID, productLabel);
                // foreach (var geomData in _model.GetGeometryData(XbimGeometryType.TriangulatedMesh))

                if (geomDataList.Count() == 0)
                    continue;                   // no geometry for this product

                int startingOffset = 0;
                List<int> elemInfoArr = new List<int>();
                List<double> arrCoord = new List<double>();

                foreach (XbimGeometryData geomData in geomDataList)
                {
                    //m3D = geomData.Transform;
                    m3D = XbimMatrix3D.FromArray(geomData.DataArray2);      // Xbim 3.0 removes Transform property!
                    XbimTriangulatedModelStream triangleStream = new XbimTriangulatedModelStream(geomData.ShapeData);
                    XbimMeshGeometry3D builder = new XbimMeshGeometry3D();
                    //                    triangleStream.Build(builder);  //This reads only the Vertices and triangle indexes, other modes of Build can read other info (e.g. BuildWithNormal, or BuildPNI)
                    triangleStream.BuildWithNormals(builder, m3D);  //This reads only the Vertices and triangle indexes, other modes of Build can read other info (e.g. BuildWithNormal, or BuildPNI)
                    // from builder, we can get the list of triangle vertices for the SDO_ORDINATE_ARRAY
                    // Since they are all triangles, we can set SDO_ELEM_INFO_ARRAY too. (How to handle case of multiple geometry?)

                    elemInfoArr.Add(startingOffset + 1);     // The first three tuple defines the geometry as solid (1007) starting at position offset 1
                    elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_COMPOUND.SOLID);
                    elemInfoArr.Add(1);
                    elemInfoArr.Add(startingOffset + 1);     // The second three tuple defines that the solid is formed by an exrternal surface (1006) starting at position offset 1
                    elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_COMPOUND.SURFACE_EXTERIOR);
                    elemInfoArr.Add(builder.TriangleIndexCount / 3); // no. of the (triangle) faces that follows
                    int header = startingOffset + 6;
                    //int currFVindex = 0;

                    // double[] arrCoord = new double[builder.TriangleIndexCount * 4];
                    for (int noTr = 0; noTr < builder.TriangleIndexCount / 3; noTr++)
                    {
                        // int arrPos = header + noTr * 3;       // ElemInfoArray uses 3 members in the long array

                        elemInfoArr.Add(startingOffset + noTr * 3 * 4 + 1);  // offset position
                        elemInfoArr.Add((int)SdoGeometryTypes.ETYPE_SIMPLE.POLYGON_EXTERIOR);   // This three tupple defines a face (triangular face) that is made up of 3 points/vertices
                        elemInfoArr.Add(1);

                        // SDO requires the points to be closed, i.e. for triangle, we will need 4 points: v1, v2, v3, v1
                        XbimPoint3D v = new XbimPoint3D();
                        XbimPoint3D v0 = new XbimPoint3D();
                        for (int i = 0; i < 4; i++)
                        {
                            if (i < 3)
                            {
                                v = builder.Positions[builder.TriangleIndices[noTr * 3 + i]];

                                arrCoord.Add(v.X);          // vertex i
                                arrCoord.Add(v.Y);
                                arrCoord.Add(v.Z);

                                // Keep the first point to close the triangle at the end
                                if (i==0)
                                {
                                    v0.X = v.X;
                                    v0.Y = v.Y;
                                    v0.Z = v.Z;
                                }

                                // Evaluate each point to calculate min bounding box of the entire (federated) model
                                if (v.X < _refBIMRLCommon.LLB_X)
                                    _refBIMRLCommon.LLB_X = v.X;
                                else if (v.X > _refBIMRLCommon.URT_X)
                                    _refBIMRLCommon.URT_X = v.X;

                                if (v.Y < _refBIMRLCommon.LLB_Y)
                                    _refBIMRLCommon.LLB_Y = v.Y;
                                else if (v.Y > _refBIMRLCommon.URT_Y)
                                    _refBIMRLCommon.URT_Y = v.Y;

                                if (v.Z < _refBIMRLCommon.LLB_Z)
                                    _refBIMRLCommon.LLB_Z = v.Z;
                                else if (v.Z > _refBIMRLCommon.URT_Z)
                                    _refBIMRLCommon.URT_Z = v.Z;
                            }
                            else
                            {
                                // Close the polygon with the starting point (i=0)
                                arrCoord.Add(v0.X);
                                arrCoord.Add(v0.Y);
                                arrCoord.Add(v0.Z);
                            }
                        }

                    }
                    startingOffset = startingOffset + builder.TriangleIndexCount * 4;
                    //polyHStartingOffset = currFVindex + 3;
                }

                SdoGeometry sdoGeomData = new SdoGeometry();
                // Assume solid only for now
                sdoGeomData.Dimensionality = 3;
                sdoGeomData.LRS = 0;
                if (geomDataList.Count() == 1)
                    sdoGeomData.GeometryType = (int)SdoGeometryTypes.GTYPE.SOLID;
                else
                    sdoGeomData.GeometryType = (int)SdoGeometryTypes.GTYPE.MULTISOLID;
                int gType = sdoGeomData.PropertiesToGTYPE();

                sdoGeomData.ElemArrayOfInts = elemInfoArr.ToArray();
                sdoGeomData.OrdinatesArrayOfDoubles = arrCoord.ToArray();


                if (!string.IsNullOrEmpty(prodGuid))
                {
                    // Found match, update the table with geometry data
                    string sqlStmt = "update BIMRL_ELEMENT_" + bimrlProcessModel.currFedID.ToString("X4") + " set GEOMETRYBODY=:1, TRANSFORM_COL1=:2, TRANSFORM_COL2=:3, TRANSFORM_COL3=:4, TRANSFORM_COL4=:5"
                                    + " Where elementid = '" + prodGuid + "'";
                    // int status = DBOperation.updateGeometry(sqlStmt, sdoGeomData);
                    currStep = sqlStmt;
                    command.CommandText = sqlStmt;

                    try
                    {
                        OracleParameter[] sdoGeom = new OracleParameter[5];
                        for (int i = 0; i < sdoGeom.Count(); ++i)
                        {
                            sdoGeom[i] = command.Parameters.Add((i+1).ToString(), OracleDbType.Object);
                            sdoGeom[i].Direction = ParameterDirection.Input;
                            sdoGeom[i].UdtTypeName = "MDSYS.SDO_GEOMETRY";
                            sdoGeom[i].Size = 1;
                        }
                        sdoGeom[0].Value = sdoGeomData;

                        //SdoGeometry xAxis = new SdoGeometry();
                        //xAxis.Dimensionality = 3;
                        //xAxis.LRS = 0;
                        //xAxis.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                        //gType = xAxis.PropertiesToGTYPE();
                        //SdoPoint xAxisV = new SdoPoint();
                        //XbimVector3D axis = m3D.Right;
                        //axis.Normalize();
                        //xAxisV.XD = axis.X;
                        //xAxisV.YD = axis.Y;
                        //xAxisV.ZD = axis.Z;

                        SdoGeometry trcol1 = new SdoGeometry();
                        trcol1.Dimensionality = 3;
                        trcol1.LRS = 0;
                        trcol1.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                        gType = trcol1.PropertiesToGTYPE();
                        SdoPoint trcol1V = new SdoPoint();
                        trcol1V.XD = m3D.M11;
                        trcol1V.YD = m3D.M12;
                        trcol1V.ZD = m3D.M13;
                        trcol1.SdoPoint = trcol1V;
                        sdoGeom[1].Value = trcol1;

                        SdoGeometry trcol2 = new SdoGeometry();
                        trcol2.Dimensionality = 3;
                        trcol2.LRS = 0;
                        trcol2.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                        gType = trcol2.PropertiesToGTYPE();
                        SdoPoint trcol2V = new SdoPoint();
                        trcol2V.XD = m3D.M21;
                        trcol2V.YD = m3D.M22;
                        trcol2V.ZD = m3D.M23;
                        trcol2.SdoPoint = trcol2V;
                        sdoGeom[2].Value = trcol2;

                        SdoGeometry trcol3 = new SdoGeometry();
                        trcol3.Dimensionality = 3;
                        trcol3.LRS = 0;
                        trcol3.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                        gType = trcol3.PropertiesToGTYPE();
                        SdoPoint trcol3V = new SdoPoint();
                        trcol3V.XD = m3D.M31;
                        trcol3V.YD = m3D.M32;
                        trcol3V.ZD = m3D.M33;
                        trcol3.SdoPoint = trcol3V;
                        sdoGeom[3].Value = trcol3;

                        SdoGeometry trcol4 = new SdoGeometry();
                        trcol4.Dimensionality = 3;
                        trcol4.LRS = 0;
                        trcol4.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                        gType = trcol4.PropertiesToGTYPE();
                        SdoPoint trcol4V = new SdoPoint();
                        trcol4V.XD = m3D.OffsetX;
                        trcol4V.YD = m3D.OffsetY;
                        trcol4V.ZD = m3D.OffsetZ;
                        trcol4.SdoPoint = trcol4V;
                        sdoGeom[4].Value = trcol4;

                        commandStatus = command.ExecuteNonQuery();
                        command.Parameters.Clear();

                        currInsertCount++;

                        if (currInsertCount % DBOperation.commitInterval == 0)
                        {
                            //Do commit at interval but keep the long transaction (reopen)
                            DBOperation.commitTransaction();
                        }
                    }
                    catch (OracleException e)
                    {
                        string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                        _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                        //command.Dispose();   // Log Oracle error and continue
                        command = new OracleCommand(" ", DBOperation.DBConn);
                        // throw;
                    }
                    catch (SystemException e)
                    {
                        string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                        _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                        throw;
                    }
                }

                DBOperation.commitTransaction();
                command.Dispose();

            }

        }

        public void processElements()
        {
            DBOperation.beginTransaction();

            int commandStatus = -1;
            int currInsertCount = 0;
            string container = string.Empty;
            string SqlStmt;
            string currStep = string.Empty;

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            try
            {

                // Process Project "properties"
                IEnumerable<IfcProject> projects = _model.InstancesLocal.OfType<IfcProject>();
                // Insert only ONE project from the first one. Therefore needs to check its existence first
                IfcProject project = projects.First();
                SqlStmt = "SELECT COUNT(*) FROM BIMRL_ELEMENT_" + bimrlProcessModel.currFedID.ToString("X4") + " WHERE ELEMENTID='" + project.GlobalId.ToString() + "'";
                OracleCommand chkCmd = new OracleCommand(SqlStmt, DBOperation.DBConn);
                object ret = chkCmd.ExecuteScalar();
                int iRet = Convert.ToInt32(ret.ToString());
                if (iRet == 0)
                {
                    SqlStmt = "INSERT INTO BIMRL_ELEMENT_" + bimrlProcessModel.currFedID.ToString("X4") + " (ELEMENTID, ELEMENTTYPE, MODELID) VALUES ("
                                + "'" + project.GlobalId.ToString() + "', 'IFCPROJECT', " + bimrlProcessModel.currModelID.ToString() + ")";
                    OracleCommand cmd = new OracleCommand(SqlStmt, DBOperation.DBConn);
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }

                IEnumerable<IfcProduct> elements = _model.InstancesLocal.OfType<IfcProduct>(true).Where
                    (et => !(et is IfcSpatialStructureElement || et is IfcPort || et is IfcVirtualElement
                        || et is IfcAnnotation));
                foreach (IfcProduct el in elements)
                {
                    IfcElement elem = el as IfcElement;
                    string guid = el.GlobalId;
                    string typeName = el.GetType().Name.ToUpper();
                    // IFC Type
                    string typGuid = string.Empty;
                    IEnumerable<IfcRelDefinesByType> relTyp = el.IsDefinedBy.OfType<IfcRelDefinesByType>();
                    if (relTyp != null)
                    {
                        // Only one Type can be assigned to an Element based on IFC schema WR1
                        IfcRelDefinesByType typ = relTyp.FirstOrDefault();
                        if (typ != null)
                            typGuid = typ.RelatingType.GlobalId.ToString();
                    }
                    // Owner History, skip for now

                    string elName = BIMRLUtils.checkSingleQuote(el.Name);
                    string elObjectType = BIMRLUtils.checkSingleQuote(el.ObjectType);
                    string elDesc = BIMRLUtils.checkSingleQuote(el.Description);
                    int IfcLineNo = el.EntityLabel;
                    string tag = elem.Tag.ToString();
                    IfcRelContainedInSpatialStructure relContainer = elem.ContainedInStructure.FirstOrDefault();
                    if (relContainer == null)
                        container = string.Empty;
                    else
                        container = relContainer.RelatingStructure.GlobalId.ToString();

                    // from el, we can get all IFC related attributes (property set?), including relationships. But first we need to populate BIMRL_ELEMENT table first before building the relationships
                    // Keep a mapping between IFC guid used as a key in BIMRL and the IFC line no of the entity
                    _refBIMRLCommon.guidLineNoMappingAdd(bimrlProcessModel.currModelID, IfcLineNo, guid);

                    string columnSpec = "Elementid, LineNo, ElementType, ModelID";
                    string valueList = "'" + guid + "'," + IfcLineNo.ToString() + ",'" + typeName + "'," + bimrlProcessModel.currModelID.ToString();

                    if (!string.IsNullOrEmpty(typGuid))
                    {
                        columnSpec += ", TypeID";
                        valueList += ", '" + typGuid + "'";
                    }
                    if (!string.IsNullOrEmpty(elName))
                    {
                        columnSpec += ", Name";
                        valueList += ", '" + elName + "'";
                    }
                    if (!string.IsNullOrEmpty(elDesc))
                    {
                        columnSpec += ", Description";
                        valueList += ", '" + elDesc + "'";
                    }
                    if (!string.IsNullOrEmpty(elObjectType))
                    {
                        columnSpec += ", ObjectType";
                        valueList += ", '" + elObjectType + "'";
                    }
                    if (!string.IsNullOrEmpty(tag))
                    {
                        columnSpec += ", Tag";
                        valueList += ", '" + tag + "'";
                    }
                    if (!string.IsNullOrEmpty(container))
                    {
                        columnSpec += ", Container";
                        valueList += ", '" + container + "'";
                    }

                    Tuple<int, int> ownHEntry = new Tuple<int, int>(Math.Abs(el.OwnerHistory.EntityLabel), bimrlProcessModel.currModelID);
                    if (_refBIMRLCommon.OwnerHistoryExist(ownHEntry))
                    {
                        columnSpec += ", OwnerHistoryID";
                        valueList += ", " + Math.Abs(el.OwnerHistory.EntityLabel);
                    }

                    SqlStmt = "Insert into BIMRL_Element_" + bimrlProcessModel.currFedID.ToString("X4") + "(" + columnSpec + ") Values (" + valueList + ")";
                    command.CommandText = SqlStmt;
                    currStep = SqlStmt;
                    commandStatus = command.ExecuteNonQuery();

                    // Add intormation of the product label (LineNo into a List for the use later to update the Geometry
                    _refBIMRLCommon.insEntityLabelListAdd(Math.Abs(IfcLineNo));
                    currInsertCount++;

                    if (currInsertCount % DBOperation.commitInterval == 0)
                    {
                        //Do commit at interval but keep the long transaction (reopen)
                        DBOperation.commitTransaction();
                    }
                }

                // Process Group objects (IfcSystem and IfcZone)

                IEnumerable<IfcGroup> groups = _model.InstancesLocal.OfType<IfcGroup>(true).Where
                    (et => (et is IfcSystem || et is IfcZone));
                foreach (IfcGroup el in groups)
                {
                    string guid = el.GlobalId;
                    string typeName = el.GetType().Name.ToUpper();
                    string elName = BIMRLUtils.checkSingleQuote(el.Name);
                    string elObjectType = BIMRLUtils.checkSingleQuote(el.ObjectType);
                    string elDesc = BIMRLUtils.checkSingleQuote(el.Description);
                    int IfcLineNo = el.EntityLabel;

                    _refBIMRLCommon.guidLineNoMappingAdd(bimrlProcessModel.currModelID, IfcLineNo, guid);

                    string columnSpec = "Elementid, LineNo, ElementType, ModelID";
                    string valueList = "'" + guid + "'," + IfcLineNo.ToString() + ",'" + typeName + "'," + bimrlProcessModel.currModelID.ToString();

                    if (!string.IsNullOrEmpty(elName))
                    {
                        columnSpec += ", Name";
                        valueList += ", '" + elName + "'";
                    }
                    if (!string.IsNullOrEmpty(elDesc))
                    {
                        columnSpec += ", Description";
                        valueList += ", '" + elDesc + "'";
                    }
                    if (!string.IsNullOrEmpty(elObjectType))
                    {
                        columnSpec += ", ObjectType";
                        valueList += ", '" + elObjectType + "'";
                    }

                    Tuple<int, int> ownHEntry = new Tuple<int, int>(Math.Abs(el.OwnerHistory.EntityLabel), bimrlProcessModel.currModelID);
                    if (_refBIMRLCommon.OwnerHistoryExist(ownHEntry))
                    {
                        columnSpec += ", OwnerHistoryID";
                        valueList += ", " + Math.Abs(el.OwnerHistory.EntityLabel);
                    }

                    SqlStmt = "Insert into BIMRL_Element_" + bimrlProcessModel.currFedID.ToString("X4") + "(" + columnSpec + ") Values (" + valueList + ")";

                    command.CommandText = SqlStmt;
                    currStep = SqlStmt;
                    commandStatus = command.ExecuteNonQuery();

                    currInsertCount++;

                    if (currInsertCount % DBOperation.commitInterval == 0)
                    {
                        //Do commit at interval but keep the long transaction (reopen)
                        DBOperation.commitTransaction();
                    }
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                command.Dispose();
                throw;
            }
            catch (SystemException e)
            {
                string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                throw;
            }

            DBOperation.commitTransaction();
            command.Dispose();

            // After all elements are processed, proceed with Geometries
            processGeometries();

            processProperties();
        }

        private void processProperties()
        {
            BIMRLProperties bimrlProp = new BIMRLProperties(_refBIMRLCommon);
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

            string SqlStmt = "Insert into BIMRL_ElementProperties_" + bimrlProcessModel.currFedID.ToString("X4") + "(ElementId, PropertyGroupName, PropertyName, PropertyValue, PropertyDataType"
             + ", PropertyUnit) Values (:1, :2, :3, :4, :5, :6)";
            command.CommandText = SqlStmt;
            string currStep = SqlStmt;

            OracleParameter[] Param = new OracleParameter[6];
            for (int i = 0; i < 6; i++)
            {
                Param[i] = command.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
                Param[i].Direction = ParameterDirection.Input;
            }

            List<string> arrEleGuid = new List<string>();
            List<string> arrPGrpName = new List<string>();
            List<string> arrPropName = new List<string>();
            List<string> arrPropVal = new List<string>();
            List<OracleParameterStatus> arrPropValBS = new List<OracleParameterStatus>();
            List<string> arrPDatatyp = new List<string>();
            List<string> arrPUnit = new List<string>();
            List<OracleParameterStatus> arrPUnitBS = new List<OracleParameterStatus>();

            // Process Project "properties"
            IEnumerable<IfcProject> projects = _model.InstancesLocal.OfType<IfcProject>();
            // Insert only ONE project from the first one. Therefore needs to check its existence first
            IfcProject project = projects.First();
            SqlStmt = "SELECT COUNT(*) FROM BIMRL_PROPERTIES_" + bimrlProcessModel.currFedID.ToString("X4") + " WHERE ELEMENTID='" + project.GlobalId.ToString() + "'";
            OracleCommand chkCmd = new OracleCommand(SqlStmt, DBOperation.DBConn);
            object ret = chkCmd.ExecuteScalar();
            int iRet = Convert.ToInt32(ret.ToString());
            if (iRet == 0)
            {
                Vector3D trueNorthDir = new Vector3D();
                foreach (IfcGeometricRepresentationContext repCtx in project.RepresentationContexts)
                {
                    if (repCtx.TrueNorth != null)
                    {
                        trueNorthDir.X = repCtx.TrueNorth.X;
                        trueNorthDir.Y = repCtx.TrueNorth.Y;
                        trueNorthDir.Z = repCtx.TrueNorth.Z;
                        arrEleGuid.Add(project.GlobalId);
                        arrPGrpName.Add("IFCATTRIBUTES");
                        arrPropName.Add("TRUENORTH");
                        arrPropVal.Add(trueNorthDir.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add("VECTOR");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        break;
                    }
                }
                // Other project properties
                if (project.Description != null)
                {
                    arrEleGuid.Add(project.GlobalId);
                    arrPGrpName.Add("IFCATTRIBUTES");
                    arrPropName.Add("DESCRIPTION");
                    arrPropVal.Add(project.Description.Value);
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPDatatyp.Add("STRING");
                    arrPUnit.Add(string.Empty);
                    arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                }
                if (project.ObjectType != null)
                {
                    arrEleGuid.Add(project.GlobalId);
                    arrPGrpName.Add("IFCATTRIBUTES");
                    arrPropName.Add("OBJECTTYPE");
                    arrPropVal.Add(project.ObjectType.Value);
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPDatatyp.Add("STRING");
                    arrPUnit.Add(string.Empty);
                    arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                }
                if (project.LongName != null)
                {
                    arrEleGuid.Add(project.GlobalId);
                    arrPGrpName.Add("IFCATTRIBUTES");
                    arrPropName.Add("LONGNAME");
                    arrPropVal.Add(project.LongName);
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPDatatyp.Add("STRING");
                    arrPUnit.Add(string.Empty);
                    arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                }
                if (project.Phase != null)
                {
                    arrEleGuid.Add(project.GlobalId);
                    arrPGrpName.Add("IFCATTRIBUTES");
                    arrPropName.Add("PHASE");
                    arrPropVal.Add(project.Phase);
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPDatatyp.Add("STRING");
                    arrPUnit.Add(string.Empty);
                    arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                }

                // Process project units
                IEnumerable<IfcUnit> units = project.UnitsInContext.Units;
                Dictionary<string, UnitSetting> unitItems = new Dictionary<string, UnitSetting>();
                foreach (IfcUnit unit in units)
                {
                    // We will not handle IfcDerivedUnit yet at this time, only IfcNamedUnit (IfcConversionBasedUnit and IfcSIUnit)
                    if (unit is IfcSIUnit)
                    {
                        IfcSIUnit unitSI = unit as IfcSIUnit;
                        string unitType = unitSI.UnitType.ToString();
                        UnitSetting unitS = new UnitSetting();
                        unitS.unitName = unitSI.Name.ToString();
                        if (unitSI.Prefix != null)
                            unitS.unitName = unitSI.Prefix.ToString() + " " + unitS.unitName;
                        unitS.unitType = "METRIC";
                        unitItems.Add(unitType, unitS);
                    }
                }
                foreach (IfcUnit unit in units)
                {
                    if (unit is IfcConversionBasedUnit)
                    {
                        IfcConversionBasedUnit cUnit = unit as IfcConversionBasedUnit;
                        string unitType = cUnit.UnitType.ToString();
                        string name = cUnit.Name;
                        double conversionFactor = (double)cUnit.ConversionFactor.ValueComponent.Value;
                        UnitSetting unitS;
                        if (unitItems.TryGetValue(unitType, out unitS))
                        {
                            unitS.unitName = name;
                            unitS.unitType = "IMPERIAL";
                            unitS.conversionFactor = conversionFactor;
                        }
                        else
                        {
                            unitS = new UnitSetting();
                            unitS.unitName = name;
                            unitS.unitType = "IMPERIAL";
                            unitS.conversionFactor = conversionFactor;
                            unitItems.Add(unitType, unitS);
                        }
                    }
                }
                // Now we collect all the dictionary entries and set them into array for property insertion
                foreach (KeyValuePair<string, UnitSetting> unitItem in unitItems)
                {
                    arrEleGuid.Add(project.GlobalId);
                    arrPGrpName.Add("IFCATTRIBUTES");
                    arrPropName.Add(unitItem.Key);
                    arrPropVal.Add(unitItem.Value.unitName);
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPDatatyp.Add(unitItem.Value.unitType);
                    if (unitItem.Value.conversionFactor != null)
                    {
                        arrPUnit.Add(unitItem.Value.conversionFactor.ToString());
                        arrPUnitBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }
                }

                // Process the rest of the project properties in property sets (if any)
                bimrlProp.processElemProperties(project);
            }

            IEnumerable<IfcProduct> elements = _model.InstancesLocal.OfType<IfcProduct>(true).Where
                (et => !(et is IfcFeatureElement || et is IfcVirtualElement
                    || et is IfcAnnotation));
            foreach (IfcProduct el in elements)
            {
                if (el is IfcSite)
                {
                    IfcSite sse_s = el as IfcSite;

                    int noPar = 6;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++ )
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("REFLATITUDE");
                    arrPropName.Add("REFLONGITUDE");
                    arrPropName.Add("REFELEVATION");
                    arrPropName.Add("LANDTITLENUMBER");
                    arrPropName.Add("SITEADDRESS");
                    arrPropName.Add("COMPOSITIONTYPE");

                    arrPropVal.Add(sse_s.RefLatitude.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(sse_s.RefLongitude.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(sse_s.RefElevation.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(BIMRLUtils.checkSingleQuote(sse_s.LandTitleNumber.ToString()));
                    if (sse_s.SiteAddress != null)
                    {
                        arrPropVal.Add(BIMRLUtils.checkSingleQuote(sse_s.SiteAddress.ToString()));
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(String.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    arrPropVal.Add(sse_s.CompositionType.ToString());
                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcBuilding)
                {
                    IfcBuilding sse_b = el as IfcBuilding;

                    int noPar = 4;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("ELEVATIONOFREFHEIGHT");
                    arrPropName.Add("ELEVATIONOFTERRAIN");
                    arrPropName.Add("BUILDINGADDRESS");
                    arrPropName.Add("COMPOSITIONTYPE");

                    arrPropVal.Add(sse_b.ElevationOfRefHeight.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(sse_b.ElevationOfTerrain.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    if (sse_b.BuildingAddress != null)
                    {
                        arrPropVal.Add(BIMRLUtils.checkSingleQuote(sse_b.BuildingAddress.ToString()));
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    else
                    {
                        arrPropVal.Add(String.Empty);
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    arrPropVal.Add(sse_b.CompositionType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcBuildingStorey)
                {
                    IfcBuildingStorey sse_bs = el as IfcBuildingStorey;

                    int noPar = 2;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("ELEVATION");
                    arrPropName.Add("COMPOSITIONTYPE");

                    arrPropVal.Add(sse_bs.Elevation.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(sse_bs.CompositionType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                /* Various Element specific attributes to be inserted into Property tables as group IFCATTRIBUTES
                 */
                else if (el is IfcBuildingElementProxy)
                {
                    IfcBuildingElementProxy elem = el as IfcBuildingElementProxy;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("COMPOSITIONTYPE");

                    if (elem.CompositionType != null)
                    {
                        arrPropVal.Add(elem.CompositionType.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }
                
                else if (el is IfcCovering)
                {
                    IfcCovering elem = el as IfcCovering;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("PREDEFINEDTYPE");

                    if (elem.PredefinedType != null)
                    {
                        arrPropVal.Add(elem.PredefinedType.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcDistributionControlElement)
                {
                    IfcDistributionControlElement elem = el as IfcDistributionControlElement;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("CONTROLELEMENTID");

                    if (elem.ControlElementId != null)
                    {
                        arrPropVal.Add(elem.ControlElementId.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcDoor)
                {
                    IfcDoor elem = el as IfcDoor;


                    int noPar = 2;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("OVERALLHEIGHT");
                    arrPropName.Add("OVERALLWIDTH");

                    if (elem.OverallHeight != null)
                    {
                        arrPropVal.Add(elem.OverallHeight.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    if (elem.OverallWidth == null)
                    {
                        arrPropVal.Add(elem.OverallWidth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");


                } 

                else if (el is IfcElectricDistributionPoint)
                {
                    IfcElectricDistributionPoint elem = el as IfcElectricDistributionPoint;

                    int noPar = 2;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("DISTRIBUTIONPOINTFUNCTION");
                    arrPropName.Add("USERDEFINEDFUNCTION");

                    if (elem.UserDefinedFunction != null)
                    {
                        arrPropVal.Add(elem.UserDefinedFunction.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    arrPropVal.Add(elem.DistributionPointFunction.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcElementAssembly)
                {
                    IfcElementAssembly elem = el as IfcElementAssembly;

                    int noPar = 2;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("ASSEMBLYPLACE");
                    arrPropName.Add("PREDEFINEDTYPE");

                    if (elem.AssemblyPlace != null)
                    {
                        arrPropVal.Add(elem.AssemblyPlace.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    arrPropVal.Add(elem.PredefinedType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }
 
                else if (el is IfcFooting)
                {
                    IfcFooting elem = el as IfcFooting;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("PREDEFINEDTYPE");

                    arrPropVal.Add(elem.PredefinedType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcPile)
                {
                    IfcPile elem = el as IfcPile;

                    int noPar = 2;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("PREDEFINEDTYPE");
                    arrPropName.Add("CONSTRUCTIONTYPE");

                    if (elem.ConstructionType != null)
                    {
                        arrPropVal.Add(elem.ConstructionType.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    arrPropVal.Add(elem.PredefinedType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcRailing)
                {
                    IfcRailing elem = el as IfcRailing;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("PREDEFINEDTYPE");

                    if (elem.PredefinedType != null)
                    {
                        arrPropVal.Add(elem.PredefinedType.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcRamp)
                {
                    IfcRamp elem = el as IfcRamp;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("SHAPETYPE");

                    arrPropVal.Add(elem.ShapeType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                } 

                else if (el is IfcReinforcingBar)
                {
                    IfcReinforcingBar elem = el as IfcReinforcingBar;

                    int noPar = 6;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("STEELGRADE");
                    arrPropName.Add("NOMINALDIAMETER");
                    arrPropName.Add("CROSSSECTIONAREA");
                    arrPropName.Add("BARLENGTH");
                    arrPropName.Add("BARROLE");
                    arrPropName.Add("BARSURFACE");

                    if (elem.SteelGrade != null)
                    {
                        arrPropVal.Add(elem.SteelGrade.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    arrPropVal.Add(elem.NominalDiameter.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(elem.CrossSectionArea.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    
                    if (elem.BarLength != null)
                    {
                        arrPropVal.Add(elem.BarLength.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    
                    arrPropVal.Add(elem.BarRole.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    if (elem.BarSurface != null)
                    {
                        arrPropVal.Add(elem.BarSurface.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcReinforcingMesh)
                {
                    IfcReinforcingMesh elem = el as IfcReinforcingMesh;

                    int noPar = 9;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("STEELGRADE");
                    arrPropName.Add("MESHLENGTH");
                    arrPropName.Add("MESHWIDTH");
                    arrPropName.Add("LONGITUDINALBARNOMINALDIAMETER");
                    arrPropName.Add("TRANSVERSEBARNOMINALDIAMETER");
                    arrPropName.Add("LONGITUDINALBARCROSSSECTIONAREA");
                    arrPropName.Add("TRANSVERSEBARCROSSSECTIONAREA");
                    arrPropName.Add("LONGITUDINALBARSPACING");
                    arrPropName.Add("TRANSVERSEBARSPACING");

                    if (elem.SteelGrade != null)
                    {
                        arrPropVal.Add(elem.SteelGrade.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    if (elem.MeshLength == null)
                    {
                        arrPropVal.Add(elem.MeshLength.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    if (elem.MeshWidth != null)
                    {
                        arrPropVal.Add(elem.MeshWidth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    arrPropVal.Add(elem.LongitudinalBarNominalDiameter.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(elem.TransverseBarNominalDiameter.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(elem.LongitudinalBarCrossSectionArea.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(elem.TransverseBarCrossSectionArea.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(elem.LongitudinalBarSpacing.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(elem.TransverseBarSpacing.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcRoof)
                {
                    IfcRoof elem = el as IfcRoof;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("SHAPETYPE");

                    arrPropVal.Add(elem.ShapeType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                } 

                else if (el is IfcSlab)
                {
                    IfcSlab elem = el as IfcSlab;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("PREDEFINEDTYPE");

                    if (elem.PredefinedType != null)
                    {
                        arrPropVal.Add(elem.PredefinedType.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcStair)
                {
                    IfcStair elem = el as IfcStair;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("SHAPETYPE");

                    arrPropVal.Add(elem.ShapeType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                } 

                else if (el is IfcStairFlight)
                {
                    IfcStairFlight elem = el as IfcStairFlight;

                    int noPar = 4;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("NUMBEROFRISER");
                    arrPropName.Add("NUMBEROFTHREADS");
                    arrPropName.Add("RISERHEIGHT");
                    arrPropName.Add("TREADLENGTH");

                    if (elem.NumberOfRiser != null)
                    {
                        arrPropVal.Add(elem.NumberOfRiser.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    if (elem.NumberOfTreads != null)
                    {
                        arrPropVal.Add(elem.NumberOfTreads.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    if (elem.RiserHeight != null)
                    {
                        arrPropVal.Add(elem.RiserHeight.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    if (elem.TreadLength!= null)
                    {
                        arrPropVal.Add(elem.TreadLength.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcTendon)
                {
                    IfcTendon elem = el as IfcTendon;

                    int noPar = 8;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("PREDEFINEDTYPE");
                    arrPropName.Add("NOMINALDIAMETER");
                    arrPropName.Add("CROSSSECTIONAREA");
                    arrPropName.Add("TENSIONFORCE");
                    arrPropName.Add("PRESTRESS");
                    arrPropName.Add("FRICTIONCOEFFICIENT");
                    arrPropName.Add("ANCHORAGESLIP");
                    arrPropName.Add("MINCURVATURERADIUS");

                    arrPropVal.Add(elem.PredefinedType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(elem.NominalDiameter.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPropVal.Add(elem.CrossSectionArea.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);

                    if (elem.TensionForce != null)
                    {
                        arrPropVal.Add(elem.TensionForce.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    if (elem.PreStress == null)
                    {
                        arrPropVal.Add(elem.PreStress.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    if (elem.FrictionCoefficient != null)
                    {
                        arrPropVal.Add(elem.FrictionCoefficient.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    if (elem.AnchorageSlip == null)
                    {
                        arrPropVal.Add(elem.AnchorageSlip.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    if (elem.MinCurvatureRadius != null)
                    {
                        arrPropVal.Add(elem.MinCurvatureRadius.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcTendonAnchor)
                {
                    IfcTendonAnchor elem = el as IfcTendonAnchor;

                    int noPar = 1;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("STEELGRADE");

                    if (elem.SteelGrade != null)
                    {
                        arrPropVal.Add(elem.SteelGrade.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcTransportElement)
                {
                    IfcTransportElement elem = el as IfcTransportElement;

                    int noPar = 3;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("OPERATIONTYPE");
                    arrPropName.Add("CAPACITYBYWEIGHT");
                    arrPropName.Add("CAPACITYBYNUMBER");

                    if (elem.OperationType != null)
                    {
                        arrPropVal.Add(elem.OperationType.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    if (elem.CapacityByWeight != null)
                    {
                        arrPropVal.Add(elem.CapacityByWeight.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    if (elem.CapacityByNumber != null)
                    {
                        arrPropVal.Add(elem.CapacityByNumber.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }

                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }

                else if (el is IfcWindow)
                {
                    IfcWindow elem = el as IfcWindow;

                    int noPar = 2;
                    for (int i = 0; i < noPar; i++) arrEleGuid.Add(el.GlobalId);
                    for (int i = 0; i < noPar; i++) arrPGrpName.Add("IFCATTRIBUTES");
                    for (int i = 0; i < noPar; i++)
                    {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    }

                    arrPropName.Add("OVERALLHEIGHT");
                    arrPropName.Add("OVERALLWIDTH");

                    if (elem.OverallHeight != null)
                    {
                        arrPropVal.Add(elem.OverallHeight.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    if (elem.OverallWidth == null)
                    {
                        arrPropVal.Add(elem.OverallWidth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                    }
                    else
                    {
                        arrPropVal.Add(string.Empty);
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                    }
                    for (int i = 0; i < noPar; i++) arrPDatatyp.Add("STRING");
                }
                else if (el is IfcPort)
                {
                    // We will deal with IfcDistributionPort in a special way. We temporarily keep the port information in a dict.
                    // Later one we will match the port to the element when processing IfcRelConenctsPortToElement, and update the dictionary value
                    // The actual relationship will be inserted upon processing IfcRelConnectsPorts
                    // --> This is done because using inverse (even it works) is too SLOW!!
                    IfcDistributionPort dPort;
                    if (el is IfcDistributionPort)
                        dPort = el as IfcDistributionPort;
                    else
                        continue;       // skip if not IfcDistributionPort (for MEP connectivity)
                    Dictionary<string,string> portElemVal = new Dictionary<string,string>();
                    if (dPort.FlowDirection != null)
                    {
                        portElemVal.Add("ATTRIBUTENAME", "FlowDirection");
                        portElemVal.Add("ATTRIBUTEVALUE", dPort.FlowDirection.ToString());
                    }
                    _refBIMRLCommon.PortToElemAdd(dPort.GlobalId.ToString(), portElemVal);
                }
                else
                {
                    // not supported Type
                }

                /* 
                 * Now process all other properties from property set(s)
               */
               bimrlProp.processElemProperties(el);

                if (arrEleGuid.Count >= DBOperation.commitInterval)
                {

                    Param[0].Value = arrEleGuid.ToArray();
                    Param[0].Size = arrEleGuid.Count;
                    Param[1].Value = arrPGrpName.ToArray();
                    Param[1].Size = arrPGrpName.Count;
                    Param[2].Value = arrPropName.ToArray();
                    Param[2].Size = arrPropName.Count;
                    Param[3].Value = arrPropVal.ToArray();
                    Param[3].Size = arrPropVal.Count;
                    Param[3].ArrayBindStatus = arrPropValBS.ToArray();
                    Param[4].Value = arrPDatatyp.ToArray();
                    Param[4].Size = arrPDatatyp.Count;

                    try
                    {
                        command.ArrayBindCount = arrEleGuid.Count;    // No of values in the array to be inserted
                        int commandStatus = command.ExecuteNonQuery();
                        DBOperation.commitTransaction();
                        arrEleGuid.Clear();
                        arrPGrpName.Clear();
                        arrPropName.Clear();
                        arrPropVal.Clear();
                        arrPropValBS.Clear();
                        arrPDatatyp.Clear();
                    }
                    catch (OracleException e)
                    {
                        string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + currStep;
                        _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                        // Ignore any error
                        arrEleGuid.Clear();
                        arrPGrpName.Clear();
                        arrPropName.Clear();
                        arrPropVal.Clear();
                        arrPropValBS.Clear();
                        arrPDatatyp.Clear();
                        continue;
                    }
                    catch (SystemException e)
                    {
                        string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                        _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                        throw;
                    }
                }
            }

            if (arrEleGuid.Count > 0)
            {

                Param[0].Value = arrEleGuid.ToArray();
                Param[0].Size = arrEleGuid.Count;
                Param[1].Value = arrPGrpName.ToArray();
                Param[1].Size = arrPGrpName.Count;
                Param[2].Value = arrPropName.ToArray();
                Param[2].Size = arrPropName.Count;
                Param[3].Value = arrPropVal.ToArray();
                Param[3].Size = arrPropVal.Count;
                Param[3].ArrayBindStatus = arrPropValBS.ToArray();
                Param[4].Value = arrPDatatyp.ToArray();
                Param[4].Size = arrPDatatyp.Count;

                try
                {
                    command.ArrayBindCount = arrEleGuid.Count;    // No of values in the array to be inserted
                    int commandStatus = command.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                }
                catch (OracleException e)
                {
                    string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + currStep;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    // Ignore any error
                }
                catch (SystemException e)
                {
                    string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    throw;
                }
            }

            DBOperation.commitTransaction();
            command.Dispose();
        }

    }
}
