using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
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
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using BIMRL;
using BIMRL.OctreeLib;
using Newtonsoft.Json;

namespace BIMRLInterface
{

   /// <summary>
   /// Interaction logic for MainWindow.xaml
   /// </summary>
   public partial class BIMRLInterfaceWin : Window
   {
      List<ElementSet> elementSets = new List<ElementSet>();
      List<VarsInfo> varsData = new List<VarsInfo>();
      int currQueryResult = 0;
      IList<DataTable> queryResults;
      BIMRLQueryModel _qModel;
      BIMRLCommon BIMRLCommonRef = new BIMRLCommon();
      List<BIMRLFedModel> fedModels = new List<BIMRLFedModel>();

      public BIMRLInterfaceWin()
      {
         try
         {
            InitializeComponent();
            BIMRL_input.Clear();
            BIMRL_output.Clear();

            // Connect to Oracle DB
            DBOperation.refBIMRLCommon = BIMRLCommonRef;      // important to ensure DBoperation has reference to this object!!
            if (DBOperation.Connect() == null)
            {
               BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(BIMRLCommonRef);
               erroDlg.ShowDialog();
               return;
            }

            _qModel = new BIMRLQueryModel(BIMRLCommonRef);
            fedModels = _qModel.getFederatedModels();
            DataGrid_FedModels.AutoGenerateColumns = true;
            DataGrid_FedModels.IsReadOnly = true;
            DataGrid_FedModels.ItemsSource = fedModels;
            DataGrid_FedModels.MinRowHeight = 20;
            Button_Execute1.IsEnabled = false;
            Button_Execute2.IsEnabled = false;

            DataGridVariables.IsReadOnly = true;
            DataGridVariables.AutoGenerateColumns = true;
            DataGridVariables.MinRowHeight = 20;

            varsData = DefinedVarsManager.listVars();
            DataGridVariables.ItemsSource = varsData;

            refreshDefinedGrids();
         }
         catch (Exception ex)
         {
            Logger.writeLog("Error: " + ex.Message);
         }
      }

      private void ClearButton_OnClick(object sender, RoutedEventArgs e)
      {
         BIMRL_input.Clear();
         BIMRL_output.Clear();
         Logger.resetStream();
      }

      private void Run_button_Click(object sender, RoutedEventArgs e)
      {

         AntlrInputStream input = new AntlrInputStream(BIMRL_input.Text);
         bimrlLexer lexer = new bimrlLexer(input);
         CommonTokenStream tokens = new CommonTokenStream(lexer);
         bimrlParser parser = new bimrlParser(tokens);
         parser.RemoveErrorListeners();
         Logger.resetStream();
            
         parser.AddErrorListener(new Error_Listener());

         //IParseTree tree = parser.start_rule();
         IParseTree tree = parser.test_rule();
         ParseTreeWalker walker = new ParseTreeWalker();
         EvalListener eval = new EvalListener(parser);

         walker.Walk(eval, tree);

         // BIMRL_output.Text = tree.ToStringTree(parser);
         string toOutput = new string(Logger.getmStreamContent());
         BIMRL_output.Text = tree.ToStringTree(parser) + '\n' + toOutput;
         
      }

      private void Window_Closed(object sender, EventArgs e)
      {
         Logger.resetStream();
      }

      private void DataGrid_FedModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         BIMRLFedModel selFedModel;
         BIMRLFedModel? selFedModelsItem = DataGrid_FedModels.SelectedItem as BIMRLFedModel?;
         if (selFedModelsItem == null)
               return;     // do nothing, no selection made
         else if (selFedModelsItem.HasValue)
         {
            selFedModel = selFedModelsItem.Value;
            projectUnit projectUnit = DBOperation.getProjectUnitLength(selFedModel.FederatedID);
            DBOperation.currModelProjectUnitLength = projectUnit;

            if (projectUnit == projectUnit.SIUnit_Length_MilliMeter)
            {
               MathUtils.tol = 0.1;
               MathUtils._doubleDecimalPrecision = 1;
               MathUtils._floatDecimalPrecision = 1;
            }
            else if (projectUnit == projectUnit.SIUnit_Length_Meter)
            {
               MathUtils.tol = 0.0001;
               MathUtils._doubleDecimalPrecision = 4;
               MathUtils._floatDecimalPrecision = 4;
            }
            else if (projectUnit == projectUnit.Imperial_Length_Foot)
            {
               MathUtils.tol = 0.0003;
               MathUtils._doubleDecimalPrecision = 4;
               MathUtils._floatDecimalPrecision = 4;
            }
            else if (projectUnit == projectUnit.Imperial_Length_Inch)
            {
               MathUtils.tol = 0.004;
               MathUtils._doubleDecimalPrecision = 3;
               MathUtils._floatDecimalPrecision = 3;
            }
            DBOperation.currSelFedID = selFedModel.FederatedID;     // set a static variable keeping the currently selected Fed Id
            DBQueryManager.FedModelID = selFedModel.FederatedID;

            DBOperation.OctreeSubdivLevel = selFedModel.OctreeMaxDepth;

            Button_Execute1.IsEnabled = true;
            Button_Execute2.IsEnabled = true;
               TextBoxFedModelId.Text = "0x" + selFedModel.FederatedID.ToString("X4");
         }
      }

      private void ClearBellow_Button_Click(object sender, RoutedEventArgs e)
      {
         BIMRL_output.Clear();
         Logger.resetStream();
      }

      private void Button_Execute_Click(object sender, RoutedEventArgs e)
      {
         //string[] statements = BIMRL_input.Text.Split(';');

         //foreach (string stmt in statements)
         //{
         string stmt = BIMRL_input.Text;
               AntlrInputStream input = new AntlrInputStream(stmt);
               bimrlLexer lexer = new bimrlLexer(input);
               CommonTokenStream tokens = new CommonTokenStream(lexer);
               bimrlParser parser = new bimrlParser(tokens);
               parser.RemoveErrorListeners();
               Logger.resetStream();

               parser.AddErrorListener(new Error_Listener());

               // Setup default tolerance using the selected model id in the UI
               //projectUnit projectUnit = DBOperation.getProjectUnitLength(DBQueryManager.FedModelID);
               //DBOperation.currModelProjectUnitLength = projectUnit;
               //if (projectUnit == projectUnit.SIUnit_Length_MilliMeter)
               //{
               //    MathUtils.tol = 0.1;
               //    MathUtils._doubleDecimalPrecision = 1;
               //    MathUtils._floatDecimalPrecision = 1;
               //}
               //else if (projectUnit == projectUnit.SIUnit_Length_Meter)
               //{
               //    MathUtils.tol = 0.0001;
               //    MathUtils._doubleDecimalPrecision = 4;
               //    MathUtils._floatDecimalPrecision = 4;
               //}
               //else if (projectUnit == projectUnit.Imperial_Length_Foot)
               //{
               //    MathUtils.tol = 0.0003;
               //    MathUtils._doubleDecimalPrecision = 4;
               //    MathUtils._floatDecimalPrecision = 4;
               //}
               //else if (projectUnit == projectUnit.Imperial_Length_Inch)
               //{
               //    MathUtils.tol = 0.004;
               //    MathUtils._doubleDecimalPrecision = 3;
               //    MathUtils._floatDecimalPrecision = 3;
               //}


               IParseTree tree = parser.start_rule();
               ParseTreeWalker walker = new ParseTreeWalker();
               // ExecListener eval = new ExecListener(parser);
               ExecListener eval = new ExecListener(tokens);
               eval.clearQueryResults();
               eval.clearQueryConsoleOutput();
               // ParseTreeProperty<BIMRLExpr> bimrlExpr = new ParseTreeProperty<BIMRLExpr>();

               try
               {
                  walker.Walk(eval, tree);
                  refreshDefinedGrids();

                  //if (!string.IsNullOrEmpty(eval.lastQueryString))
                  BIMRL_output.Text += eval.queryConsoleOutput;

                  if (eval.queryResults != null)
                  {
                     if (eval.queryResults.Count > 0 && eval.queryResults[0] != null)   // if null, there is no result
                     {
                           if (eval.queryResults.Count > 1)
                              Button_NextQueryResult.IsEnabled = true;

                           TextBox_totalpage.Text = eval.queryResults.Count.ToString();
                           TextBox_currentpage.Text = "1";
                           queryResults = eval.queryResults;
                           DataGridResults.AutoGenerateColumns = true;
                           DataGridResults.IsReadOnly = true;
                           currQueryResult = 0;
                           DataView dView = new DataView();
                           dView = queryResults[currQueryResult].DefaultView;
                           TextBox_Rows.Text = dView.Count.ToString();
                           DataGridResults.ItemsSource = dView;
                           DataGridResults.MinRowHeight = 20;

                           if (CheckBox_SaveToJson.IsChecked == true && !String.IsNullOrEmpty(TextBox_JsonFIleName.Text))
                           {
                              DataSet qResDataSet = new DataSet();
                              qResDataSet.Tables.AddRange(eval.queryResults.ToArray());
                              string json = JsonConvert.SerializeObject(qResDataSet, Formatting.Indented);
                              using (StreamWriter file = File.CreateText(TextBox_JsonFIleName.Text))
                              {
                                 JsonSerializer serializer = new JsonSerializer();
                                 serializer.Serialize(file, qResDataSet);
                              }
                           }
                     }
                  }
                  if (eval.queryConsoleOutput != null)
                  {
                     foreach (string consoleOutput in eval.queryConsoleOutput)
                           BIMRL_output.Text += consoleOutput + "\n";
                  }

                  if (Logger.loggerStream.Length > 0)
                  {
                     string toOutput = new string(Logger.getmStreamContent());
                     BIMRL_output.Text = tree.ToStringTree(parser) + '\n' + toOutput;
                  }
               }
               catch (BIMRLInterfaceRuntimeException bimrlEx)
               {
                  refreshDefinedGrids();
                  if (eval.queryConsoleOutput != null)
                  {
                     foreach (string consoleOutput in eval.queryConsoleOutput)
                           BIMRL_output.Text += consoleOutput + "\n";
                  }
                  BIMRLErrorDialog errorDlg = new BIMRLErrorDialog(bimrlEx.Message);
                  errorDlg.ShowDialog();
               }
         //}
      }

      private void refreshDefinedGrids()
      {
         varsData.Clear();
         elementSets.Clear();

         elementSets = DefinedElementSetManager.list();

         varsData = DefinedVarsManager.listVars();

         DataGridVariables.AutoGenerateColumns = true;
         DataGridVariables.IsReadOnly = true;
         DataGridVariables.ItemsSource = varsData;
         DataGridVariables.MinRowHeight = 20;
      }

      private void ButtonClearResults_Click(object sender, RoutedEventArgs e)
      {
         DataGridVariables.AutoGenerateColumns = true;
         DataGridVariables.IsReadOnly = true;
         DataGridVariables.ItemsSource = null;
         DataGridVariables.MinRowHeight = 20;

         if (queryResults != null) 
               queryResults.Clear();
      }

      //private void TextBoxFedModelId_TextChanged(object sender, TextChangedEventArgs e)
      //{
      //    int modelID;
      //    if (int.TryParse(TextBoxFedModelId.Text, out modelID))
      //        DBQueryManager.FedModelID = modelID;
      //    else
      //        DBQueryManager.FedModelID = 0;      // default value given
      //}

      private void Button_PreviousQueryResult_Click(object sender, RoutedEventArgs e)
      {
         if (currQueryResult > 0)
               currQueryResult--;

         TextBox_currentpage.Text = (currQueryResult + 1).ToString();
         DataGridResults.AutoGenerateColumns = true;
         DataGridResults.IsReadOnly = true;
         DataView dView = new DataView();
         dView = queryResults[currQueryResult].DefaultView;
         TextBox_Rows.Text = dView.Count.ToString();
         DataGridResults.ItemsSource = dView;
         DataGridResults.MinRowHeight = 20;

         if (currQueryResult < queryResults.Count - 1)
               Button_NextQueryResult.IsEnabled = true;
         if (currQueryResult == 0)
               Button_PreviousQueryResult.IsEnabled = false;    // turn off the button, we are at the min

      }

      private void ButtonNextQueryResult_Click(object sender, RoutedEventArgs e)
      {
         if (currQueryResult < queryResults.Count - 1)
               currQueryResult++;

         TextBox_currentpage.Text = (currQueryResult + 1).ToString();
         DataGridResults.AutoGenerateColumns = true;
         DataGridResults.IsReadOnly = true;
         DataView dView = new DataView();
         dView = queryResults[currQueryResult].DefaultView;
         TextBox_Rows.Text = dView.Count.ToString();
         DataGridResults.ItemsSource = dView;
         DataGridResults.MinRowHeight = 20;

         if (currQueryResult > 0)
               Button_PreviousQueryResult.IsEnabled = true;
         if (currQueryResult == queryResults.Count - 1)
               Button_NextQueryResult.IsEnabled = false;    // turn off the button, we are at the max
      }

   }
}
