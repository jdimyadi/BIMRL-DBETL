using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Collections;
using System.IO;
using System.Reflection;
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using NetSdoGeometry;

namespace BIMRL.OctreeLib
{
   public static class DBOperation
   {
      private static string m_connStr;
      private static OracleTransaction m_longTrans;
      private static bool transactionActive = false;
      private static int currInsertCount = 0;
      public static int commitInterval { get; set; }
      public static string operatorToUse { get; set; }
      public static string DBUserID { get; set; }
      public static string DBPassword { get; set; }
      public static string DBConnecstring { get; set; }
      public static BIMRLCommon refBIMRLCommon { get; set; }
      public static projectUnit currModelProjectUnitLength = projectUnit.SIUnit_Length_Meter;
      public static Dictionary<string, bool> objectForSpaceBoundary = new Dictionary<string, bool>();
      public static Dictionary<string, bool> objectForConnection = new Dictionary<string, bool>();
      public static int currSelFedID { get; set; }
      private static Dictionary<int, Tuple<Point3D, Point3D, int>> worldBBInfo = new Dictionary<int, Tuple<Point3D, Point3D, int>>();
      public static bool UIMode {get; set;} = true;

        public static OracleConnection Connect(string username, string password, string DBconnectstring)
        {
            DBUserID = username;
            DBPassword = password;
            DBConnecstring = DBconnectstring;

            if (m_DBconn != null)
                return m_DBconn;             // already connected

            string constr = "User Id=" + username + ";Password=" + password + ";Data Source=" + DBconnectstring;
            string currStep = string.Empty;
            try
            {
                currStep = "Connecting to Oracle using: " + constr;
                m_DBconn = new OracleConnection(constr);
                currStep = "Opening Oracle connection using: " + constr;
                m_DBconn.Open();
                m_connStr = constr;
                //if (m_DBconnShort == null)
                //{
                //    m_DBconnShort = new OracleConnection(constr);
                //    m_DBconnShort.Open();
                //    m_ShortTransaction = m_DBconnShort.BeginTransaction();
                //}
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                refBIMRLCommon.StackPushError(excStr);
                throw;
            }
            return m_DBconn;
        }

        public static OracleConnection Connect()
        {
            if (m_DBconn != null)
                return m_DBconn;             // already connected

            if (string.IsNullOrEmpty(DBUserID))
            {
                // set defaults

                DBOperation.DBUserID = "wawan";
                DBOperation.DBPassword = "wawan";
                DBOperation.DBConnecstring = "pdborcl";
            }

            string constr = "User Id=" + DBUserID + ";Password=" + DBPassword + ";Data Source=" + DBConnecstring;
            string currStep = string.Empty;
            try
            {
                currStep = "Connecting to Oracle using: " + constr;
                m_DBconn = new OracleConnection(constr);
                currStep = "Opening Oracle connection using: " + constr;
                m_DBconn.Open();
                m_connStr = constr;
                //if (m_DBconnShort == null)
                //{
                //    m_DBconnShort = new OracleConnection(constr);
                //    m_DBconnShort.Open();
                //    m_ShortTransaction = m_DBconnShort.BeginTransaction();
                //}
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                refBIMRLCommon.StackPushError(excStr);
                throw;
            }
            return m_DBconn;
        }

        private static OracleConnection m_DBconn;
        public static OracleConnection DBConn
        {
            get 
            {
                if (m_DBconn == null)
                    Connect();

                return m_DBconn; 
            }
        }

        // This is to initiate a second DB connection, useful to perform a short transaction separate from the main one (e.g. if
        // there is a need to execute a DDL statement in the middle of transaction, or commit certian change
        // Should be kept very short
        //private static OracleConnection m_DBconnShort;
        //private static OracleTransaction m_ShortTransaction;
        //public static OracleConnection DBconnShort
        //{
        //    get {
        //        if (m_DBconnShort == null)
        //        {
        //            m_DBconnShort = new OracleConnection(m_connStr);
        //            m_DBconnShort.Open();
        //            m_ShortTransaction = m_DBconnShort.BeginTransaction();
        //        }
        //        return m_DBconnShort;
        //    }
        //}

        private static int m_OctreeSubdivLevel = 6;    // default value
        public static int OctreeSubdivLevel
        {
            get { return m_OctreeSubdivLevel; }
            set { m_OctreeSubdivLevel = value; }
        }

        private static bool m_OnepushETL = false;
        public static bool OnepushETL
        {
            get { return m_OnepushETL; }
            set { m_OnepushETL = value; }
        }

        public static int executeSingleStmt(string sqlStmt, bool commit=true)
        {
            int commandStatus = -1;
            OracleCommand command = new OracleCommand(sqlStmt, DBConn);
            DBOperation.beginTransaction();
            //command.Transaction = m_ShortTransaction;
            try
            {
                commandStatus = command.ExecuteNonQuery();
                if (commit)
                    DBOperation.commitTransaction();
                //m_ShortTransaction.Commit();
                //m_ShortTransaction = m_DBconnShort.BeginTransaction();
            }
            catch (OracleException e)
            {
                // Ignore error
                // txn.Rollback();
                string excStr = "%%Error - " + e.Message + "\n\t" + sqlStmt;
                refBIMRLCommon.StackPushError(excStr);
                command.Dispose();
                // throw;
            }
            command.Dispose();
            return commandStatus;
        }

        // these 3 methods must be done in series of 3: start with beginTransaction and ends with endTransaction
        public static void beginTransaction()
        {
            if (!transactionActive)
            {
                m_longTrans = m_DBconn.BeginTransaction();
                transactionActive = true;
            }
            currInsertCount = 0;    // reset the insert count
        }

        public static void commitTransaction()
        {
            if (transactionActive)
            {
                m_longTrans.Commit();
                m_longTrans = m_DBconn.BeginTransaction();
            }
        }

        public static void rollbackTransaction()
        {
            if (transactionActive)
            {
                m_longTrans.Rollback();
                m_longTrans = m_DBconn.BeginTransaction();
            }
        }

        public static int insertRow(string sqlStmt)
        {
            if (!transactionActive)
            {
                return -1;
                // no transaction opened
            }
            int commandStatus = -1;
            OracleCommand command = new OracleCommand(sqlStmt, m_DBconn);
            string currStep = sqlStmt;

            try
            {
                commandStatus = command.ExecuteNonQuery();
                currInsertCount++;          // increment insert count

                if (currInsertCount % commitInterval == 0)
                {
                    //Do commit at interval but keep the long transaction (reopen)
                    m_longTrans.Commit();
                    m_longTrans = m_DBconn.BeginTransaction();
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                refBIMRLCommon.StackPushError(excStr);
                command.Dispose();
                throw;
            }
            command.Dispose();
            return commandStatus;
        }

        public static int insertMultipleRow(string sqlStmt, List<string[]> arrayParams)
        {
            if (!transactionActive)
            {
                return -1;
                // no transaction opened
            }
            Int32 commandStatus = -1;
            OracleCommand command = new OracleCommand(sqlStmt, m_DBconn);
            string currStep = sqlStmt;

            try
            {
                OracleParameter[] Param = new OracleParameter[arrayParams.Count];
                for (int i = 0; i < arrayParams.Count; i++)
                {
                    Param[i] = command.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
                    Param[i].Direction = ParameterDirection.Input;
                    Param[i].Value = arrayParams[i];
                    Param[i].Size = arrayParams[i].Count();
                }
                command.ArrayBindCount = arrayParams[0].Count();    // No of values in the array to be inserted
                commandStatus = command.ExecuteNonQuery();

                currInsertCount += arrayParams.Count;          // increment insert count

                if (currInsertCount % commitInterval == 0)
                {
                    //Do commit at interval but keep the long transaction (reopen)
                    m_longTrans.Commit();
                    m_longTrans = m_DBconn.BeginTransaction();
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                refBIMRLCommon.StackPushError(excStr);
                command.Dispose();
                throw;
            }
            command.Dispose();
            return commandStatus;
        }

        public static void endTransaction(bool commit)
        {
            if (transactionActive)
            {
                if (commit)
                    m_longTrans.Commit();
                else
                    m_longTrans.Rollback();
            }
            transactionActive = false;
            currInsertCount = 0;
        }

        public static void Disconnect()
        {
            if (m_DBconn != null)
            {
                m_DBconn.Close();
                //m_DBconnShort.Close();
                m_DBconn.Dispose();
                //m_DBconnShort.Dispose();
                m_DBconn = null;
                //m_DBconnShort = null;
            }
        }

        public static FedIDStatus getFederatedID (string projName, string projNumber, out int fedID)
        {
            FedIDStatus stat = FedIDStatus.FedIDExisting;
            string currStep = "Getting federated ID";

            // Create separate connection with a short duration

            string SqlStmt = "Select FEDERATEDID from BIMRL_FEDERATEDMODEL where PROJECTNAME = '" + projName + "' and PROJECTNUMBER = '" + projNumber + "'";
            OracleCommand command = new OracleCommand(SqlStmt, DBConn);
            object fID = null;

            try
            {
                fID = command.ExecuteScalar();
                if (fID == null)
                {
                    // Create a new record
                    command.CommandText = "Insert into BIMRL_FEDERATEDMODEL (PROJECTNAME, PROJECTNUMBER) values ('" + projName + "', '" + projNumber + "')";
                    DBOperation.beginTransaction();
                    //OracleTransaction txn = m_DBconnShort.BeginTransaction();
                    command.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                    //txn.Commit();
                    //txn.Dispose();
                    command.CommandText = SqlStmt;
                    fID = command.ExecuteScalar();
                    stat = FedIDStatus.FedIDNew;
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                refBIMRLCommon.StackPushError(excStr);
                command.Dispose();
                throw;
            }

            fedID = Convert.ToInt32(fID.ToString());
            command.Dispose();

            return stat;
        }

        public static int getModelID (int fedID)
        {
            string sqlStmt = "Select SEQ_BIMRL_MODELINFO_" + fedID.ToString("X4") + ".nextval from dual";
            OracleCommand cmd = new OracleCommand(sqlStmt, m_DBconn);
            int newModelID = Convert.ToInt32(cmd.ExecuteScalar().ToString());

            cmd.Dispose();
            return newModelID;
        }

        public static int createModelTables (int ID)
        {
            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            string exePath = new FileInfo(location.AbsolutePath).Directory.FullName;
            string crtabScript = Path.Combine(exePath, "script", "BIMRL_crtab.sql");
            return executeScript(crtabScript, ID);
        }

        public static int executeScript (string filename, int ID)
        {
            int cmdStat = 0;
            string line;
            string stmt = string.Empty;
            string idStr = ID.ToString("X4");
            OracleCommand cmd = new OracleCommand(" ", m_DBconn);
            string currStep = string.Empty;

            bool commentStart = false;
            StreamReader reader = new StreamReader(filename);
            while ((line = reader.ReadLine()) != null)
            {
                line.Trim();
                if (line.StartsWith("/*"))
                {
                    commentStart = true;
                    continue;
                }
                if (line.EndsWith("*/"))
                {
                    commentStart = false;
                    continue;
                }
                if (line.StartsWith("//") || line.StartsWith("/") || commentStart) continue;  // PLSQL end line, skip

                line = line.Replace("&1", idStr);
                stmt += " " + line;
                if (line.EndsWith(";"))
                {
                    try
                    {
                        cmd.CommandText = stmt.Remove(stmt.Length - 1);   // remove the ;
                        currStep = cmd.CommandText;
                        cmdStat = cmd.ExecuteNonQuery();
                        stmt = string.Empty;    // reset stmt
                    }
                    catch (OracleException e)
                    {
                        string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                        refBIMRLCommon.StackPushIgnorableError(excStr);
                        stmt = string.Empty;    // reset stmt
                        continue;
                    }
                }
            }
            reader.Close();
            cmd.Dispose();
            return cmdStat;
        }

        public static int dropModelTables(int ID)
        {
         var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
         string exePath = new FileInfo(location.AbsolutePath).Directory.FullName;
         string drtabScript = Path.Combine(exePath, "script", "BIMRL_drtab.sql");
         return executeScript(drtabScript, ID);
        }

        public static projectUnit getProjectUnitLength(int fedID)
        {
            projectUnit projectUnit = projectUnit.SIUnit_Length_Meter;

            string SqlStmt = "Select PROPERTYVALUE from BIMRL_PROPERTIES_" + fedID.ToString("X4") + " P, BIMRL_ELEMENT_" + fedID.ToString("X4") + " E"
                            + " where P.ELEMENTID=E.ELEMENTID and E.ELEMENTTYPE='IFCPROJECT' AND PROPERTYGROUPNAME='IFCATTRIBUTES' AND PROPERTYNAME='LENGTHUNIT'";
            string currStep = SqlStmt;
            OracleCommand command = new OracleCommand(SqlStmt, DBConn);

            try
            {
                object unitS = command.ExecuteScalar();
                if (unitS != null)
                {
                    string unitString = unitS as string;
                    if (string.Compare(unitString, "MILLI METRE",true) == 0)
                        projectUnit = projectUnit.SIUnit_Length_MilliMeter;
                    else if (string.Compare(unitString, "INCH", true) == 0)
                        projectUnit = projectUnit.Imperial_Length_Inch;
                    else if (string.Compare(unitString, "FOOT", true) == 0)
                        projectUnit = projectUnit.Imperial_Length_Foot;
                }

            }
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                refBIMRLCommon.StackPushError(excStr);
                command.Dispose();
            }
            command.Dispose();
            currModelProjectUnitLength = projectUnit;
            return projectUnit;
        }

        public static bool getWorldBB(int federatedId, out Point3D llb, out Point3D urt)
        {
            Tuple<Point3D,Point3D,int> bbinfo;

            // If the information is already in the dictionary, return the info
            if (worldBBInfo.TryGetValue(federatedId, out bbinfo))
            {
                llb = bbinfo.Item1;
                urt = bbinfo.Item2;
                Octree.WorldBB = new BoundingBox3D(llb, urt);
                Octree.MaxDepth = bbinfo.Item3;
                return true;
            }

            string sqlStmt = "select WORLDBBOX, MAXOCTREELEVEL from BIMRL_FEDERATEDMODEL WHERE FEDERATEDID=" + federatedId.ToString();
            OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
            command.CommandText = sqlStmt;
            llb = null;
            urt = null;

            try
            {
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    SdoGeometry sdoGeomData = reader.GetValue(0) as SdoGeometry;
                    int maxDepth = reader.GetInt32(1);

                    llb = new Point3D();
                    llb.X = sdoGeomData.OrdinatesArrayOfDoubles[0];
                    llb.Y = sdoGeomData.OrdinatesArrayOfDoubles[1];
                    llb.Z = sdoGeomData.OrdinatesArrayOfDoubles[2];

                    urt = new Point3D();
                    urt.X = sdoGeomData.OrdinatesArrayOfDoubles[3];
                    urt.Y = sdoGeomData.OrdinatesArrayOfDoubles[4];
                    urt.Z = sdoGeomData.OrdinatesArrayOfDoubles[5];

                    Octree.WorldBB = new BoundingBox3D(llb, urt);
                    Octree.MaxDepth = maxDepth;

                    // Add the new info into the dictionary
                    worldBBInfo.Add(federatedId, new Tuple<Point3D,Point3D,int>(llb,urt,maxDepth));

                    return true;
                }
                reader.Dispose();
                return false;
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
                refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
                refBIMRLCommon.StackPushError(excStr);
                throw;
            }
            return false;
        }

        public static int computeRecomOctreeLevel(int fedID)
        {
            Point3D llb;
            Point3D urt;

            if (getWorldBB(fedID, out llb, out urt))
            {
                double dX = urt.X - llb.X;
                double dY = urt.Y - llb.Y;
                double dZ = urt.Z - llb.Z;
                double largestEdge = dX;        // set this as initial value

                if (dY > dX && dY > dZ)
                    largestEdge = dY;
                else if (dZ > dY && dZ > dX)
                    largestEdge = dZ;

                double defaultTh = 200;     // value in mm
                double threshold = defaultTh;
                projectUnit pjUnit = getProjectUnitLength(fedID);
                if (pjUnit == projectUnit.SIUnit_Length_MilliMeter)
                {
                    threshold = defaultTh;
                }
                else if (pjUnit == projectUnit.SIUnit_Length_Meter)
                {
                    threshold =  defaultTh / 1000;
                }
                else if (pjUnit == projectUnit.Imperial_Length_Foot)
                {
                    threshold = defaultTh / 304.8;
                }
                else if (pjUnit == projectUnit.Imperial_Length_Inch)
                {
                    threshold = defaultTh / 25.4;
                }
                
                double calcV = largestEdge;
                int level = 0;
                while (calcV > threshold)
                {
                    calcV = calcV / 2;
                    level++;
                }
                return level;
            }
            else
                return -1;
        }
    }
}
