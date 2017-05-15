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
//using System.Data.OracleClient;
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
using Newtonsoft.Json;

namespace BIMRLInterface.ExtensionFunction
{
    public class GetCoordinates : ExtensionFunctionBase, IBIMRLExtensionFunction
    {
        public GetCoordinates()
        {

        }

        /// <summary>
        /// GetCoordinates function
        /// </summary>
        /// <param name="inputDT"></param>
        /// <param name="inputParams"></param>
        public override void InvokeRule(DataTable inputDT, params string[] inputParams)
        {
            base.InvokeRule(inputDT,inputParams);

            DataColumn column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
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

            // Add column for the JSon string of the coordinates on each column specified in the parameters
            foreach (string par in inputParams)
            {
                column = new DataColumn();
                column.DataType = System.Type.GetType("System.String");
                column.MaxLength = 4000;
                //column.DataType = System.Type.GetType("System.Data.OracleClient.OracleLob");
                column.ColumnName = par + "_COORDS";
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
            }

            foreach (DataRow row in inputDT.Rows)
            {
                foreach (string par in inputParams)
                {
                    SdoGeometry geom = row[par] as SdoGeometry;
                    if (geom == null)
                        continue;

                    string geomOutStr = string.Empty;
                    object geomOut;
                    SdoGeometryTypes.GTYPE geomtype = SDOGeomUtils.generateGeometryFromSDO(geom, out geomOut);

                    if (geomtype == SdoGeometryTypes.GTYPE.LINE || geomtype == SdoGeometryTypes.GTYPE.MULTILINE)
                    {
                        List<LineSegment3D> lineList = geomOut as List<LineSegment3D>;
                        List<LineSegment3DLW> lines = new List<LineSegment3DLW>();
                        foreach (LineSegment3D l3d in lineList)
                            lines.Add(new LineSegment3DLW(l3d));
                        geomOutStr = JsonConvert.SerializeObject(lines);
                    }
                    else if (geomtype == SdoGeometryTypes.GTYPE.POLYGON || geomtype == SdoGeometryTypes.GTYPE.MULTIPOLYGON)
                    {
                        // Only handle a single Polygon
                        Face3D faceGeom = geomOut as Face3D;
                        geomOutStr = faceGeom.ToJsonString();
                    }
                    else if (geomtype == SdoGeometryTypes.GTYPE.SOLID || geomtype == SdoGeometryTypes.GTYPE.MULTISOLID)
                    {
                        // Only handle a single Polyhedron
                        Polyhedron polyH = geomOut as Polyhedron;
                        geomOutStr = polyH.ToJsonString();
                    }
                    else
                    {
                        // Unsupported
                        continue;
                    }
                    row[par + "_COORDS"] = geomOutStr;
                }
            }
            m_Result = inputDT;
        }
    }
}
