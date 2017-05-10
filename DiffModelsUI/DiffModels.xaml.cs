using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
//using System.Windows.Shapes;
using Microsoft.Win32;
using BIMRL;
using BIMRL.OctreeLib;
using Oracle.DataAccess.Client;

namespace DiffModelsUI
{
   /// <summary>
   /// Interaction logic for MainWindow.xaml
   /// </summary>
   public partial class DiffModels : Window
   {
      BIMRLCommon BIMRLCommonRef = new BIMRLCommon();
      int modelIDNew = -1;
      int modelIDRef = -1;
      string outputFileName = "";

      public DiffModels()
      {
         InitializeComponent();
         DBOperation.refBIMRLCommon = BIMRLCommonRef;      // important to ensure DBoperation has reference to this object!!
         if (DBOperation.Connect() == null)
         {
            if (DBOperation.UIMode)
            {
               BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(BIMRLCommonRef);
               erroDlg.ShowDialog();
            }
            else
               Console.Write(BIMRLCommonRef.ErrorMessages);
            return;
         }

         BIMRLQueryModel qModel = new BIMRLQueryModel(BIMRLCommonRef);
         IList<BIMRLFedModel> fedModels = qModel.getFederatedModels();
         dataGrid_ModelList.AutoGenerateColumns = true;
         dataGrid_ModelList.IsReadOnly = true;
         dataGrid_ModelList.ItemsSource = fedModels;
         dataGrid_ModelList.MinRowHeight = 20;

         button_1stModel.IsEnabled = false;
         button_2ndModel.IsEnabled = false;
         button_Run.IsEnabled = false;
      }

      private void button_1stModel_Click(object sender, RoutedEventArgs e)
      {
         BIMRLFedModel? selFedModelsItem = dataGrid_ModelList.SelectedItem as BIMRLFedModel?;
         if (selFedModelsItem == null)
            return;     // do nothing, no selection made
         else if (selFedModelsItem.HasValue)
         {
            BIMRLFedModel selModel = selFedModelsItem.Value;
            if (modelIDRef >= 0 && modelIDRef == selModel.FederatedID)
               return;  // Can't select the same model to compare
            textBox_1stModel.Text = "(ID: " + selModel.FederatedID.ToString() + ") " + selModel.ProjectNumber + " - " + selModel.ProjectName;
            modelIDNew = selModel.FederatedID;
         }
         //if (!string.IsNullOrEmpty(textBox_2ndModel.Text))
         //   button_Run.IsEnabled = true;
      }

      private void button_2ndModel_Click(object sender, RoutedEventArgs e)
      {
         BIMRLFedModel? selFedModelsItem = dataGrid_ModelList.SelectedItem as BIMRLFedModel?;
         if (selFedModelsItem == null)
            return;     // do nothing, no selection made
         else if (selFedModelsItem.HasValue)
         {
            BIMRLFedModel selModel = selFedModelsItem.Value;
            if (modelIDNew >= 0 && modelIDNew == selModel.FederatedID)
               return;  // Can't select the same model to compare

            textBox_2ndModel.Text = "(ID: " + selModel.FederatedID.ToString() + ") " + selModel.ProjectNumber + " - " + selModel.ProjectName;
            modelIDRef = selModel.FederatedID;
         }
         //if (!string.IsNullOrEmpty(textBox_2ndModel.Text))
         //   button_Run.IsEnabled = true;
      }

      private void button_Run_Click(object sender, RoutedEventArgs e)
      {
         BIMRLDiffOptions options = new BIMRLDiffOptions();
         options.CheckNewAndDeletedObjects = checkBox_NewDelObjects.IsChecked.Value;
         options.CheckGeometriesDiffBySignature = checkBox_CheckGeometryBySignature.IsChecked.Value;
         double tol;
         if (double.TryParse(textBox_Tolerance.Text, out tol))
            options.GeometryCompareTolerance = tol;
         options.CheckTypeAndTypeAssignments = checkBox_CheckTypeAssignments.IsChecked.Value;
         options.CheckContainmentRelationships = checkBox_CheckContainmentRelations.IsChecked.Value;
         options.CheckOwnerHistory = checkBox_CheckOwnerHistory.IsChecked.Value;
         options.CheckProperties = checkBox_CheckProperties.IsChecked.Value;
         options.CheckMaterials = checkBox_CheckMaterials.IsChecked.Value;
         options.CheckClassificationAssignments = checkBox_Classifications.IsChecked.Value;
         options.CheckGroupMemberships = checkBox_CheckGroupMemberships.IsChecked.Value;
         options.CheckAggregations = checkBox_CheckAggregations.IsChecked.Value;
         options.CheckConnections = checkBox_CheckConnections.IsChecked.Value;
         options.CheckElementDependencies = checkBox_CheckElementDependencies.IsChecked.Value;
         options.CheckSpaceBoundaries = checkBox_CheckSpaceBoundaries.IsChecked.Value;

         BIMRLDiffModels diffModels = new BIMRLDiffModels(modelIDNew, modelIDRef, BIMRLCommonRef);
         diffModels.RunDiff(outputFileName, options: options);
      }

      private void button_Cancel_Click(object sender, RoutedEventArgs e)
      {
         Close();
      }

      private void dataGrid_ModelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
         BIMRLFedModel? selFedModelsItem = dataGrid_ModelList.SelectedItem as BIMRLFedModel?;
         if (selFedModelsItem == null)
            return;     // do nothing, no selection made
         else if (selFedModelsItem.HasValue)
         {
            button_1stModel.IsEnabled = true;
            button_2ndModel.IsEnabled = true;
         }
      }

      private void textBox_TextChanged(object sender, TextChangedEventArgs e)
      {
         
      }

      private void button_Browse_Click(object sender, RoutedEventArgs e)
      {
         var dlg = new OpenFileDialog
         {
            Title = "Select output Json file name.",
            Filter = "Json output|*.json", // Filter files by extension
            CheckFileExists = false,
            Multiselect = false
         };
         var done = dlg.ShowDialog(this);

         if (!done.Value)
            return;

         if (!dlg.FileName.Any())
         {
            if (string.IsNullOrEmpty(textBox_OutputJson.Text))
            {
               outputFileName = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "diffOutput.json";
            }
            else
               outputFileName = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + textBox_OutputJson.Text;
         }
         else
            outputFileName = dlg.FileName;

         textBox_OutputJson.Text = outputFileName;
         if (!string.IsNullOrEmpty(outputFileName))
            button_Run.IsEnabled = true;
      }

      private void textBox_OutputJson_LostFocus(object sender, RoutedEventArgs e)
      {
         if (!string.IsNullOrEmpty(textBox_OutputJson.Text))
         {
            outputFileName = textBox_OutputJson.Text;
            try
            {
               FileStream oFile = File.Create(outputFileName);
               oFile.Close();
               File.Delete(outputFileName);
               textBlock_Message.Text = "";
            }
            catch (Exception excp)
            {
               textBlock_Message.Foreground = Brushes.Red ;
               textBlock_Message.FontWeight = FontWeights.ExtraBold;

               textBlock_Message.Text = "ERROR: Output file cannot be created! " + excp.Message;
            }
         }
      }
   }
}
