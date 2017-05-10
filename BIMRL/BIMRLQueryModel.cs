using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using Xbim.Common.Geometry;
using NetSdoGeometry;
using BIMRL.OctreeLib;

namespace BIMRL
{
    public struct BIMRLFedModel
    {
        public int FederatedID { get; set; }
        public string ProjectNumber { get; set; }
        public string ProjectName { get; set; }
        public string WorldBoundingBox { get; set; }
        public int OctreeMaxDepth { get; set; }
        public DateTime? LastUpdateDate { get; set; }
    }

    public struct BIMRLModelInfo
    {
        public int ModelID { get; set; }
        public string ModelName { get; set; }
        public string Source { get; set; }
        public XbimPoint3D? Location { get; set; }
        public XbimMatrix3D? Transformation { get; set; }
        public XbimVector3D? Scale { get; set; }
        public int NumberOfElement { get; set; }
    }

    public class BIMRLQueryModel
    {
        BIMRLCommon _refBIMRLCommon;

        public BIMRLQueryModel(BIMRLCommon refBIMRLCommon)
        {
            _refBIMRLCommon = refBIMRLCommon;
        }

        public List<BIMRLFedModel> getFederatedModels()
        {
            List<BIMRLFedModel> fedModels = new List<BIMRLFedModel>();

            DBOperation.beginTransaction();
            string currStep = string.Empty;
            SdoGeometry worldBB = new SdoGeometry();
            
            try
            {
                string sqlStmt = "select federatedID, ProjectNumber, ProjectName, WORLDBBOX, MAXOCTREELEVEL, LastUpdateDate from BIMRL_FEDERATEDMODEL order by federatedID";
                OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    BIMRLFedModel fedModel = new BIMRLFedModel();
                    fedModel.FederatedID = reader.GetInt32(0);
                    fedModel.ProjectNumber = reader.GetString(1);
                    fedModel.ProjectName = reader.GetString(2);
                    if (!reader.IsDBNull(3))
                    {
                        worldBB = reader.GetValue(3) as SdoGeometry;
                        Point3D LLB = new Point3D(worldBB.OrdinatesArrayOfDoubles[0], worldBB.OrdinatesArrayOfDoubles[1], worldBB.OrdinatesArrayOfDoubles[2]);
                        Point3D URT = new Point3D(worldBB.OrdinatesArrayOfDoubles[3], worldBB.OrdinatesArrayOfDoubles[4], worldBB.OrdinatesArrayOfDoubles[5]);
                        fedModel.WorldBoundingBox = LLB.ToString() + " " + URT.ToString();
                    }
                    if (!reader.IsDBNull(4))
                        fedModel.OctreeMaxDepth = reader.GetInt16(4);
                    if (!reader.IsDBNull(5))
                        fedModel.LastUpdateDate = reader.GetDateTime(5);

                    fedModels.Add(fedModel);
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }

            return fedModels;
        }

        public List<BIMRLModelInfo> getModelInfos(int fedModelID)
        {
            List<BIMRLModelInfo> modelInfos = new List<BIMRLModelInfo>();

            DBOperation.beginTransaction();
            string currStep = string.Empty;

            try
            {
                // Temporary: currently we don't support transformation information (Xbim does not provide that information) and I have not handle Oracle UDT yet
                // string sqlStmt = "Select ModelID, ModelName, Source, Location, Transformation, Scale from BIMRL_MODELINFO_" + fedModelID.ToString("X4");
                string sqlStmt = "Select b.ModelID, a.ModelName, a.Source, count(b.modelid) as \"No. Element\" from BIMRL_MODELINFO_" + fedModelID.ToString("X4")
                                + " a, BIMRL_ELEMENT_" + fedModelID.ToString("X4") + " b WHERE b.modelid=a.modelid "
                                + "group by b.modelid, modelname, source order by ModelID";
                OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    BIMRLModelInfo modelInfo = new BIMRLModelInfo();
                    modelInfo.ModelID = reader.GetInt32(0);
                    modelInfo.ModelName = reader.GetString(1);
                    modelInfo.Source = reader.GetString(2);
                    modelInfo.NumberOfElement = reader.GetInt32(3);
                    //if (!reader.IsDBNull(3))
                        //modelInfo.Location = (XbimPoint3D) reader.GetValue(3);
                    //if (!reader.IsDBNull(4))
                        //modelInfo.Transformation = (XbimMatrix3D)reader.GetValue(4);
                    //if (!reader.IsDBNull(5))
                        //modelInfo.Scale = (XbimVector3D)reader.GetValue(5);

                    modelInfos.Add(modelInfo);
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }

            return modelInfos;
        }

        public void deleteModel (int federatedModelID)
        {
            string currStep = "Dropping existing model tables (ID: " + federatedModelID.ToString("X4") + ")";
            try
            {
                int retStat = DBOperation.dropModelTables(federatedModelID);

                string sqlStmt = "delete from BIMRL_FEDERATEDMODEL where FEDERATEDID=" + federatedModelID;
                OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
                DBOperation.beginTransaction();
                //OracleTransaction txn = DBOperation.DBconnShort.BeginTransaction();
                command.ExecuteNonQuery();
                DBOperation.commitTransaction();
                //txn.Commit();
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
        }

        public void deleteModel (string projectName, string projectNumber)
        {
            object federatedModelID = null;
            string SqlStmt = "Select FEDERATEDID from BIMRL_FEDERATEDMODEL where PROJECTNAME = '" + projectName + "' and PROJECTNUMBER = '" + projectNumber + "'";
            try
            {
                OracleCommand command = new OracleCommand(SqlStmt, DBOperation.DBConn);
                federatedModelID = command.ExecuteScalar();
                int? fedID = federatedModelID as int?;
                if (fedID.HasValue)
                    deleteModel(fedID.Value);
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + SqlStmt;
                _refBIMRLCommon.StackPushError(excStr);
            }
        }

        public DataTable checkModelExists (int fedID)
        {
            string whereClause = "FEDERATEDID=" + fedID;
            DataTable modelInfo = new DataTable();
            bool exist = checkModelExists(whereClause, out modelInfo);
            return modelInfo;
        }

        public DataTable checkModelExists(string projectName, string projectNumber)
        {
            string whereClause = "PROJECTNAME='" + projectName + "'" + " AND PROJECTNUMBER='" + projectNumber + "'";
            DataTable modelInfo = new DataTable();
            bool exist = checkModelExists(whereClause, out modelInfo);
            return modelInfo;
        }

        bool checkModelExists(string whereClause, out DataTable modelInfo)
        {
            DataTable qResult = new DataTable();
            modelInfo = qResult;

            string SqlStmt = "Select * from BIMRL_FEDERATEDMODEL where " + whereClause;
            try
            {
                OracleCommand command = new OracleCommand(SqlStmt, DBOperation.DBConn);
                OracleDataAdapter qAdapter = new OracleDataAdapter(command);
                qAdapter.Fill(qResult);
                if (qResult != null)
                    if (qResult.Rows.Count > 0)
                        return true;
                return false;
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + SqlStmt;
                _refBIMRLCommon.StackPushError(excStr);
                return false;
            }
        }
    }
}
