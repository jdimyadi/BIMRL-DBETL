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
    public class LineOfSight : ExtensionFunctionBase, IBIMRLExtensionFunction
    {
        private LineOfSight()
        {

        }

        private bool locateNearestNeighborCells(string eID1, string eID2, out CellID64 cellID1, out CellID64 cellID2)
        {
            bool ret= true;
            cellID1 = new CellID64(0);
            cellID2 = new CellID64(0);

            OracleCommand cmd = new OracleCommand("", DBOperation.DBConn);
            OracleDataReader reader;
            string sqlStmt = "SELECT CELLID FROM BIMRL_SPATIALINDEX_" + DBQueryManager.FedModelID.ToString("X4") + " WHERE ELEMENTID='" + eID1 + "' AND CELLID LIKE '%1'";
            cmd.CommandText = sqlStmt;
            reader = cmd.ExecuteReader();
            List<string> cellIDList1 = new List<string>();
            if (reader.Read())
            {
                cellIDList1.Add(reader.GetString(0));
            }
            reader.Dispose();

            sqlStmt = "SELECT CELLID FROM BIMRL_SPATIALINDEX_" + DBQueryManager.FedModelID.ToString("X4") + " WHERE ELEMENTID='" + eID2 + "' AND CELLID LIKE '%1'";
            cmd.CommandText = sqlStmt;
            reader = cmd.ExecuteReader();
            List<string> cellIDList2 = new List<string>();
            if (reader.Read())
            {
                cellIDList2.Add(reader.GetString(0));
            }
            reader.Dispose();

            foreach (string cID1 in cellIDList1)
            {
                cellID1 = new CellID64(cID1);
                Vector3D cell1Size = CellID64.cellSize(CellID64.getLevel(cellID1));
                Cuboid cell1Cuboid = new Cuboid(CellID64.getCellIdxLoc(cellID1), cell1Size.X, cell1Size.Y, cell1Size.Z);
                foreach (string cID2 in cellIDList2)
                {
                    cellID2 = new CellID64(cID2);
                    Vector3D cell2Size = CellID64.cellSize(CellID64.getLevel(cellID2));
                    Cuboid cell2Cuboid = new Cuboid(CellID64.getCellIdxLoc(cellID2), cell2Size.X, cell2Size.Y, cell2Size.Z);
                    double distance = Point3D.distance(cell1Cuboid.Centroid, cell2Cuboid.Centroid);
                }
            }


            return ret;
        }

        public override keywordInjection preceedingQuery(string inputPar)
        {
            keywordInjection keywInj = new keywordInjection();


            return keywInj;
        }

        public override void Process(DataTable inputTable, string inputPar)
        {
            DataTable dt = new DataTable();
            // separate objects in group 1 and objects in group 2
            List<string> group1Objects = new List<string>();
            List<string> group2Objects = new List<string>();
            foreach (DataRow row in inputTable.Rows)
            {
                string eID1 = (string) row["ElementId1"]; 
                string eType1 = (string) row["ElementType1"];
                string eID2 = (string)row["ElementId2"];
                string eType2 = (string)row["ElementType2"];
                CellID64 cellID1;
                CellID64 cellID2;
                bool ret = locateNearestNeighborCells(eID1, eID2, out cellID1, out cellID2);
            }

        }

    }
}
