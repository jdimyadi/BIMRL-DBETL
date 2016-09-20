using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
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
using Xbim.Ifc2x3.ActorResource;
using Xbim.Ifc2x3.PropertyResource;
using Xbim.Common.Geometry;
using Xbim.ModelGeometry.Converter;
using Xbim.Ifc2x3.ExternalReferenceResource;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.OctreeLib;

namespace BIMRL
{
    public static class BIMRLUtils
    {
        public static string checkSingleQuote(string source)
        {
            if (String.IsNullOrEmpty(source)) 
                return source;
            return source.Replace("'", "''");      // Oracle needs single quoe in the string to be escaped with another single quote
        }

        /// <summary>
        /// This function is to patch data, updating element's major axes and their OBB at the same time
        /// </summary>
        /// <param name="fedID"></param>
        /// <param name="whereCond"></param>
        public static void updateMajorAxesAndOBB(int fedID, string whereCond)
        {
            BIMRLCommon bimrlCommon = new BIMRLCommon();

            string sqlStmt = "SELECT ELEMENTID, GEOMETRYBODY FROM BIMRL_ELEMENT_" + fedID.ToString("X4") + " WHERE GEOMETRYBODY IS NOT NULL";
            if (!string.IsNullOrEmpty(whereCond))
                sqlStmt += " AND " + whereCond;

            OracleCommand cmd = new OracleCommand(sqlStmt, DBOperation.DBConn);
            OracleDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string elementid = reader.GetString(0);
                SdoGeometry geom = reader.GetValue(1) as SdoGeometry;

                Polyhedron polyH;
                if (!SDOGeomUtils.generate_Polyhedron(geom, out polyH))
                    continue;       // something wrong, unable to get the polyhedron, skip

                BIMRLGeometryPostProcess postProc = new BIMRLGeometryPostProcess(elementid, polyH, bimrlCommon, fedID, null);
                postProc.deriveMajorAxes();
                postProc.trueOBBFaces();
                postProc.projectedFaces();

                //// create OBB topo face information
                //if (postProc.OBB != null)
                //{
                //    Polyhedron obbGeom;
                //    if (SDOGeomUtils.generate_Polyhedron(postProc.OBB, out obbGeom))
                //    {
                //        BIMRLGeometryPostProcess processFaces = new BIMRLGeometryPostProcess(elementid, obbGeom, bimrlCommon, fedID, "OBB");
                //        processFaces.simplifyAndMergeFaces();
                //        processFaces.insertIntoDB(false);
                //    }
                //}
            }
        }

        public static void UpdateElementTransform(XbimModel _model, string projectNumber, string projectName)
        {
            DBOperation.beginTransaction();
            DBOperation.commitInterval = 5000;
            string currStep = string.Empty;
            BIMRLCommon _refBIMRLCommon = new BIMRLCommon();

            int commandStatus = -1;
            int currInsertCount = 0;

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            XbimMatrix3D m3D = new XbimMatrix3D();

            if (string.IsNullOrEmpty(projectName))
                projectName = projectNumber + " - Federated";

            command.CommandText = "SELECT FEDERATEDID FROM bimrl_federatedmodel WHERE PROJECTNUMBER='" + projectNumber + "' AND PROJECTNAME='" + projectName + "'";
            object oFedID = command.ExecuteScalar();
            if (oFedID == null)
                return;

            int fedID = int.Parse(oFedID.ToString());
            command.CommandText = "SELECT ELEMENTID, LINENO FROM bimrl_element_" + fedID.ToString("X4") + " WHERE GEOMETRYBODY IS NOT NULL";
            OracleDataReader reader = command.ExecuteReader();
            SortedDictionary<int, string> elemList = new SortedDictionary<int, string>();

            while (reader.Read())
            {
                string elemid = reader.GetString(0);
                int lineNo = reader.GetInt32(1);
                elemList.Add(lineNo, elemid);
            }

            foreach (KeyValuePair<int,string> elemListItem in elemList)
            {
                IEnumerable<XbimGeometryData> geomDataList = _model.GetGeometryData(elemListItem.Key, XbimGeometryType.TriangulatedMesh);

                if (geomDataList.Count() == 0)
                    continue;                   // no geometry for this product

                XbimGeometryData sdoGeomData = geomDataList.First();
                m3D = sdoGeomData.Transform;

                string sqlStmt = "update BIMRL_ELEMENT_" + fedID.ToString("X4") + " set TRANSFORM_COL1=:1, TRANSFORM_COL2=:2, TRANSFORM_COL3=:3, TRANSFORM_COL4=:4"
                                + " Where elementid = '" + elemListItem.Value + "'";
                // int status = DBOperation.updateGeometry(sqlStmt, sdoGeomData);
                currStep = sqlStmt;
                command.CommandText = sqlStmt;

                try
                {
                    OracleParameter[] sdoGeom = new OracleParameter[4];
                    for (int i = 0; i < sdoGeom.Count(); ++i)
                    {
                        sdoGeom[i] = command.Parameters.Add((i + 1).ToString(), OracleDbType.Object);
                        sdoGeom[i].Direction = ParameterDirection.Input;
                        sdoGeom[i].UdtTypeName = "MDSYS.SDO_GEOMETRY";
                        sdoGeom[i].Size = 1;
                    }

                    SdoGeometry trcol1 = new SdoGeometry();
                    trcol1.Dimensionality = 3;
                    trcol1.LRS = 0;
                    trcol1.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
                    int gType = trcol1.PropertiesToGTYPE();
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

                DBOperation.commitTransaction();
                command.Dispose();

            }

        }
    }
}
