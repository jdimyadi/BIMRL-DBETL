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
    class DoorOpeningDirection : ExtensionFunctionBase, IBIMRLExtensionFunction
    {
        public DoorOpeningDirection()
        {
        }

        /// <summary>
        /// Get opening direction of a door relative toward a space
        /// </summary>
        /// <param name="inputDT"></param>
        /// <param name="inputParams">The first input parameter must be the space elementid, followed by the door elementid. Output will contain 1=in, -1=out</param>
        public override void InvokeRule(DataTable inputDT, params string[] inputParams)
        {
            BIMRLCommon _refBIMRLCommon = new BIMRLCommon();

            base.InvokeRule(inputDT, inputParams);

            DataColumn column = new DataColumn();
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

            string sqlstmt = "Select a.geometrybody, b.transform_col1, b.transform_col2, b.transform_col3, b.transform_col4 from bimrl_element a, bimrl_element b "
                            + "where a.elementid=:1 and b.elementid=:2";
            sqlstmt = BIMRLKeywordMapping.expandBIMRLTables(sqlstmt);
            OracleCommand cmd = new OracleCommand(sqlstmt, DBOperation.DBConn);
            OracleParameter[] Params = new OracleParameter[2];

            Params[0] = cmd.Parameters.Add("1", OracleDbType.Varchar2);
            Params[1] = cmd.Parameters.Add("2", OracleDbType.Varchar2);
            Params[0].Direction = ParameterDirection.Input;
            Params[1].Direction = ParameterDirection.Input;
            
            string spaceElemid = string.Empty;
            string doorElemid = string.Empty;

            foreach (DataRow row in inputDT.Rows)
            {
                spaceElemid = row[inputParams[0]].ToString();
                doorElemid = row[inputParams[1]].ToString();
                Params[0].Value = spaceElemid;
                Params[1].Value = doorElemid;

                try
                {
                    OracleDataReader reader = cmd.ExecuteReader();
                    reader.Read();
                    SdoGeometry spGeom = reader.GetValue(0) as SdoGeometry;
                    SdoGeometry trC1 = reader.GetValue(1) as SdoGeometry;
                    SdoGeometry trC2 = reader.GetValue(2) as SdoGeometry;
                    SdoGeometry trC3 = reader.GetValue(3) as SdoGeometry;
                    SdoGeometry trC4 = reader.GetValue(4) as SdoGeometry;
                    reader.Close();

                    Polyhedron spaceGeom;
                    SDOGeomUtils.generate_Polyhedron(spGeom, out spaceGeom);

                    List<Point3D> tc1, tc2, tc3, tc4;
                    SDOGeomUtils.generate_Point(trC1, out tc1);
                    SDOGeomUtils.generate_Point(trC2, out tc2);
                    SDOGeomUtils.generate_Point(trC3, out tc3);
                    SDOGeomUtils.generate_Point(trC4, out tc4);
                    Matrix3D m3d = new Matrix3D(tc1[0].X, tc2[0].X, tc3[0].X, tc4[0].X,
                                                tc1[0].Y, tc2[0].Y, tc3[0].Y, tc4[0].Y,
                                                tc1[0].Z, tc2[0].Z, tc3[0].Z, tc4[0].Z,
                                                0, 0, 0, 1);

                    // specify a point at approx 200 mm distance into +Y direction of the door to check the opening position of the door.
                    // Into means the point will be inside the space
                    projectUnit projectUnit = DBOperation.getProjectUnitLength(DBOperation.currSelFedID);
                    DBOperation.currModelProjectUnitLength = projectUnit;
                    double plusYUnit = 0;
                    if (projectUnit == projectUnit.SIUnit_Length_MilliMeter)
                        plusYUnit = 254;
                    else if (projectUnit == projectUnit.SIUnit_Length_Meter)
                        plusYUnit = 0.254;
                    else if (projectUnit == projectUnit.Imperial_Length_Foot)
                        plusYUnit = 0.78;
                    else if (projectUnit == projectUnit.Imperial_Length_Inch)
                        plusYUnit = 10;
                    Point3D pointAtOpeningDir = new Point3D(0, plusYUnit, plusYUnit);   //Raise the point a little bit on Z direction
                    Point3D pointAtOpeningDirTr = m3d.Transform(pointAtOpeningDir);

                    int doorOpenInto = -1;
                    if (Polyhedron.inside(spaceGeom, pointAtOpeningDirTr))
                        doorOpenInto = 1;

                    row["OUTPUT"] = doorOpenInto;
                }
                catch (OracleException e)
                {
                    string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlstmt;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                }
                catch (SystemException e)
                {
                    string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlstmt;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    throw;
                }
            }

            m_Result = inputDT;
        }
    }
}
