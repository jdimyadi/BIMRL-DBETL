using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Data;
using BIMRL;
using BIMRL.OctreeLib;
using Oracle.DataAccess.Client;

namespace BIMRLMisc
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        OracleConnection DBconn;
        OracleTransaction trx;
        string ifcSchemaVer = "IFC2X3";

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                DBconn = new OracleConnection("User Id=wawan;Password=wawan;Data Source=pdborcl");
                DBconn.Open();
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;
            }
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_ObjH_file_Click(object sender, RoutedEventArgs e)
        {
            trx = DBconn.BeginTransaction();

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.DefaultExt = ".csv";
            dlg.Filter = "Comma separated values (.csv)|*.csv";
            Nullable<bool> result = dlg.ShowDialog();
            string filename = "ifcproduct.csv";
            if (result == true)
            {
                filename = dlg.FileName;
                TextBox_FObjH_filename.Text = filename;
            }

            OracleCommand command = new OracleCommand(" ", DBconn);
            string SqlStmt = "Insert into BIMRL_OBJECTHIERARCHY (IfcSchemaVer, ElementType, ElementSubType, Abstract, LevelsRemoved) "
             + " Values ('" + ifcSchemaVer + "', :1, :2, :3, :4)";
            command.CommandText = SqlStmt;
            string currStep = SqlStmt;
            
            OracleParameter[] Param = new OracleParameter[4];
            for (int i = 0; i < 4; i++)
            {
                Param[i] = command.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
                Param[i].Direction = ParameterDirection.Input;
            }

            List<string> arrElementType = new List<string>();
            List<string> arrSubElementType = new List<string>();
            List<int> arrAbstractType = new List<int>();
            List<OracleParameterStatus> arrAbsTypeBS = new List<OracleParameterStatus>();
            List<int> arrLevelsRemoved = new List<int>();

            StreamReader reader = new StreamReader(filename);
            {
                string line;
                bool isABS = false;

                while ((line = reader.ReadLine()) != null)
                {
                    string[] tok = line.Split(',');
                    int tokNo = 0;

                    for (int i=0; i<tok.Length; i++)
                    {
                        tok[i] = tok[i].Trim();
                        if (string.IsNullOrEmpty(tok[i]))
                            break;
                        tokNo++;
                    }

                    for (int i = 0; i < tokNo - 1; i++)
                    {
                        if (tok[i].StartsWith("(ABS)"))
                            isABS = true;
                        else
                            isABS = false;
                        tok[i] = tok[i].Replace("(ABS)", "").Trim();
                        int levelRemovedCnt = 1;

                        for (int j = i+1; j < tokNo; j++)
                        {
                            arrElementType.Add(tok[i].ToUpper());
                            arrAbstractType.Add(1);
                            if (isABS)
                                arrAbsTypeBS.Add(OracleParameterStatus.Success);
                            else
                                arrAbsTypeBS.Add(OracleParameterStatus.NullInsert);

                            string subE = tok[j].Replace("(ABS)", "").Trim().ToUpper();
                            arrSubElementType.Add(subE);
                            arrLevelsRemoved.Add(levelRemovedCnt);
                            levelRemovedCnt++;
                        }

                    }
                    // Add the last item as an entry too

                    arrElementType.Add(tok[tokNo - 1].ToUpper());
                    arrSubElementType.Add(tok[tokNo - 1].ToUpper());
                    arrAbstractType.Add(1);
                    arrAbsTypeBS.Add(OracleParameterStatus.NullInsert);
                    arrLevelsRemoved.Add(0);

                    if (arrElementType.Count > 0)
                    {
                        Param[0].Value = arrElementType.ToArray();
                        Param[0].Size = arrElementType.Count;
                        Param[1].Value = arrSubElementType.ToArray();
                        Param[1].Size = arrSubElementType.Count;
                        Param[2].Value = arrAbstractType.ToArray();
                        Param[2].Size = arrAbstractType.Count;
                        Param[2].ArrayBindStatus = arrAbsTypeBS.ToArray();
                        Param[3].Value = arrLevelsRemoved.ToArray();
                        Param[3].Size = arrLevelsRemoved.Count;

                        try
                        {
                            command.ArrayBindCount = arrElementType.Count;    // No of values in the array to be inserted
                            int commandStatus = command.ExecuteNonQuery();
                            // DBOperation.commitTransaction();
                            arrElementType.Clear();
                            arrSubElementType.Clear();
                            arrAbstractType.Clear();
                            arrAbsTypeBS.Clear();
                            arrLevelsRemoved.Clear();
                        }
                        catch (OracleException er)
                        {
                            string excStr = "%%Insert Error (IGNORED) - " + er.Message + "\n\t" + currStep;
                            // _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                            // Ignore any error
                        }
                    }
                }

                trx.Commit();
                command.Dispose();
            }
        }

        private void RadioButton_Ifc2x3_Checked(object sender, RoutedEventArgs e)
        {
            ifcSchemaVer = "IFC2X3";
        }

        private void RadioButton_Ifc4_Checked(object sender, RoutedEventArgs e)
        {
            ifcSchemaVer = "IFC4";
        }

        private void Button_PCA_Click(object sender, RoutedEventArgs e)
        {
            //PrincipalComponentAnalysis pca = new PrincipalComponentAnalysis();
            //Vector3D[] mAxes = pca.identifyMajorAxes();
            PCATest pcaTest = new PCATest();
            pcaTest.ShowDialog();
        }

        private void Button_ElementID_Click(object sender, RoutedEventArgs e)
        {
            ElementIDTest eidTest = new ElementIDTest();
            eidTest.ShowDialog();
        }
    }
}
