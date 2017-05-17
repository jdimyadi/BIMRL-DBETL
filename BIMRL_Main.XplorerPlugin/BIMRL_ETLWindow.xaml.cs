﻿//
// BIMRL (BIM Rule Language) Simplified Schema ETL (Extract, Transform, Load) library: this library transforms IFC data into BIMRL Simplified Schema for RDBMS. 
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
using Xbim.Presentation.XplorerPluginSystem;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Common;
using BIMRL.Common;
using BIMRL.BIMRLGraph;
using BIMRL;

namespace BIMRLMainXplorerPlugin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [XplorerUiElement(PluginWindowUiContainerEnum.Dialog, PluginWindowActivation.OnMenu, "BIMRL ETL Window")]
    public partial class BIMRL_ETLWindow : Window, IXbimXplorerPluginWindow
    {
        BIMRLQueryModel _qModel;
        BIMRLCommon BIMRLCommonRef = new BIMRLCommon();
        List<BIMRLModelInfo> modelInfos = new List<BIMRLModelInfo>();
        List<FederatedModelInfo> fedModels = new List<FederatedModelInfo>();
        bool drawOctree = false;
        bool drawFacesOnly = false;
        bool drawWorldBB = false;
        bool drawElemGeom = false;
        bool drawUserGeom = false;

        public BIMRL_ETLWindow()
        {
            InitializeComponent();
            WindowTitle = "BIMRL ETL Environment";
            BIMRLCommonRef.resetAll();

            try
            {
               // Connect to Oracle DB
               DBOperation.refBIMRLCommon = BIMRLCommonRef;      // important to ensure DBoperation has reference to this object!!
               DBOperation.ExistingOrDefaultConnection();
            }
            catch
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

            // Temp: disabled first for testing
            Button_genX3D.IsEnabled = false;    // Disable Gen X3D button until the file name is filled
            Button_EnhanceSpB.IsEnabled = false;
            Button_genGraph.IsEnabled = false;
      }

      private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_genX3D_Click(object sender, RoutedEventArgs e)
        {
            string whereCond = string.Empty;

            int fedModelID = 0;
            FederatedModelInfo selFedModel;
            FederatedModelInfo selFedModelsItem = DataGrid_FedModels.SelectedItem as FederatedModelInfo;
            if (selFedModelsItem == null)
                return;     // do nothing, no selection made
            else
            {
                selFedModel = selFedModelsItem;
                fedModelID = selFedModel.FederatedID;
            }

            try
            {
                BIMRLModelInfo selModelInfo;
                BIMRLModelInfo? selModelInfosItem = DataGrid_ModelInfos.SelectedItem as BIMRLModelInfo?;
                if (selModelInfosItem != null)
                    if (selModelInfosItem.HasValue)
                    {
                        selModelInfo = selModelInfosItem.Value;
                        whereCond += " and ModelID = " + selModelInfo.ModelID + " ";
                    }

                if (!string.IsNullOrEmpty(TextBox_Additional_Condition.Text))
                {
                    if (!string.IsNullOrEmpty(whereCond))
                        whereCond += " AND ";
                    whereCond += " " + TextBox_Additional_Condition.Text + " ";
                }
                BIMRLExportSDOToX3D x3dExp = new BIMRLExportSDOToX3D(BIMRLCommonRef, TextBox_X3D_filename.Text);
                if (!string.IsNullOrEmpty(TextBox_AlternateUserTable.Text))
                    x3dExp.altUserTable = TextBox_AlternateUserTable.Text;
                x3dExp.exportToX3D(fedModelID, whereCond, drawElemGeom, drawUserGeom, drawFacesOnly, drawOctree, drawWorldBB);
                x3dExp.endExportToX3D();
            }
            catch (SystemException excp)
            {
                string excStr = "%% Error - " + excp.Message + "\n\t";
                BIMRLCommonRef.StackPushError(excStr);
                if (BIMRLCommonRef.BIMRLErrorStackCount > 0)
                    showError(null);
            }
        }

        private void Button_Browse_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".x3d";

            dlg.Filter = "X3D documents (.x3d)|*.x3d|All files (*.*)|*.*";
            dlg.AddExtension = true;
            dlg.CheckPathExists = true;
            dlg.ValidateNames = true;

            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                string filename = dlg.FileName;
                TextBox_X3D_filename.Text = filename;
                Button_genX3D.IsEnabled = true;
            }
        }

        private void DataGrid_FedModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FederatedModelInfo selFedModelsItem = DataGrid_FedModels.SelectedItem as FederatedModelInfo;
            if (selFedModelsItem == null)
                return;     // do nothing, no selection made
            else
            {
                modelInfos.Clear();     // clear the list first
                DBOperation.currFedModel = selFedModelsItem;
                modelInfos = _qModel.getModelInfos(DBOperation.currFedModel.FederatedID);
                DataGrid_ModelInfos.AutoGenerateColumns = true;
                DataGrid_ModelInfos.IsReadOnly = true;
                DataGrid_ModelInfos.ItemsSource = modelInfos;
                Button_RegenGeometry.IsEnabled = true;
                Button_EnhanceSpB.IsEnabled = true;
                Button_genGraph.IsEnabled = true;
                projectUnit projectUnit = DBOperation.getProjectUnitLength(DBOperation.currFedModel.FederatedID);
                DBOperation.currModelProjectUnitLength = projectUnit;
               
            // Model now is always in Meter
                //if (projectUnit == projectUnit.SIUnit_Length_MilliMeter)
                //{
                //    MathUtils.tol = 0.1;
                //    MathUtils._doubleDecimalPrecision = 1;
                //    MathUtils._floatDecimalPrecision = 1;
                //}
                //else if (projectUnit == projectUnit.SIUnit_Length_Meter)
                //{
                    MathUtils.tol = 0.0001;
                    MathUtils._doubleDecimalPrecision = 4;
                    MathUtils._floatDecimalPrecision = 4;
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
                DBOperation.currSelFedID = DBOperation.currFedModel.FederatedID;     // set a static variable keeping the currently selected Fed Id

                DBOperation.OctreeSubdivLevel = DBOperation.currFedModel.OctreeMaxDepth;
                TextBox_OctreeLevel.Text = DBOperation.currFedModel.OctreeMaxDepth.ToString();
            }
        }

        private void DataGrid_ModelInfos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BIMRLModelInfo? selModelInfosItem = DataGrid_ModelInfos.SelectedItem as BIMRLModelInfo?;
            Button_RegenGeometry.IsEnabled = true;
        }

        private void TextBox_X3D_filename_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextBox_X3D_filename.Text))
                Button_genX3D.IsEnabled = true;
        }

        private void CheckBox_DrawOctree_Checked(object sender, RoutedEventArgs e)
        {
            drawOctree = true;
        }

        private void CheckBox_DrawOctree_Unchecked(object sender, RoutedEventArgs e)
        {
            drawOctree = false;
        }

        private void Button_RegenGeometry_Click(object sender, RoutedEventArgs e)
        {
            BIMRLSpatialIndex spIdx = new BIMRLSpatialIndex(BIMRLCommonRef);
            DBOperation.commitInterval = 5000;
            double currentTol = MathUtils.tol;

            var location = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
            string exePath = new FileInfo(location.AbsolutePath).Directory.FullName;

            // Temporarily change the tolerance if it is set in the UI
            if (!string.IsNullOrEmpty(TextBox_Tolerance.Text))
            {
                double tolSetting;
                if (double.TryParse(TextBox_Tolerance.Text, out tolSetting))
                    MathUtils.tol = tolSetting;
            }

            if (!string.IsNullOrEmpty(TextBox_OctreeLevel.Text))
            {
                int level = -1;
                level = int.Parse(TextBox_OctreeLevel.Text);
                if (level > 0)
                    DBOperation.OctreeSubdivLevel = level;
            }

            int FedID = -1;
            FederatedModelInfo selFedModelsItem = DataGrid_FedModels.SelectedItem as FederatedModelInfo;
            if (selFedModelsItem == null)
                return;     // do nothing, no selection made
            else
            {
               FederatedModelInfo selFedModel = selFedModelsItem;
                FedID = selFedModel.FederatedID;
            }

            try
            {
                if (FedID >= 0)
                {
                    string whereCond = null;
                    string updOctreeLevel = "";
                    if (!string.IsNullOrEmpty(TextBox_Additional_Condition.Text))
                    {
                        whereCond = TextBox_Additional_Condition.Text;
                        // Spatial always needs to be reconstructed even when one object is updated to maintain integrity of non-overlapping octree concept
                        if (regenSpatialIndex)
                        {
                            // We need the existing data to regenerate the dictionary. Truncate operation will be deferred until just before insert into the table
                            // DBOperation.executeSingleStmt("TRUNCATE TABLE BIMRL_SPATIALINDEX_" + FedID.ToString("X4"));
                            // DBOperation.executeSingleStmt("DELETE FROM BIMRL_SPATIALINDEX_" + FedID.ToString("X4") + " WHERE " + whereCond);
                        }
                        if (regenBoundaryFaces)
                            DBOperation.executeSingleStmt("DELETE FROM " + DBOperation.formatTabName("BIMRL_TOPO_FACE", FedID) + " WHERE " + whereCond);
                    }
                    else
                    {
                        FederatedModelInfo fedModel = DBOperation.getFederatedModelByID(FedID);
                  if (DBOperation.DBUserID.Equals(fedModel.Owner))
                  {
                     if (regenSpatialIndex)
                        DBOperation.executeSingleStmt("TRUNCATE TABLE " + DBOperation.formatTabName("BIMRL_SPATIALINDEX", FedID));
                     if (regenBoundaryFaces)
                        DBOperation.executeSingleStmt("TRUNCATE TABLE " + DBOperation.formatTabName("BIMRL_TOPO_FACE", FedID));
                  }
                  else
                  {
                     if (regenSpatialIndex)
                        DBOperation.executeSingleStmt("DELETE FROM " + DBOperation.formatTabName("BIMRL_SPATIALINDEX", FedID));
                     if (regenBoundaryFaces)
                        DBOperation.executeSingleStmt("DELETE FROM " + DBOperation.formatTabName("BIMRL_TOPO_FACE", FedID));
                  }
               }

                    // Update Spatial index (including major axes and OBB) and Boundary faces
                    if (regenSpatialIndex && regenBoundaryFaces && _majorAxes)
                    {
                        spIdx.createSpatialIndexFromBIMRLElement(FedID, whereCond, true);
                        BIMRLUtils.updateMajorAxesAndOBB(FedID, whereCond);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_SpatialIndexes.sql"), FedID);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_TopoFace.sql"), FedID);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_MajorAxes.sql"), FedID);
                        updOctreeLevel = "MAXOCTREELEVEL=" + DBOperation.OctreeSubdivLevel;
                    }
                    else if (regenSpatialIndex && regenBoundaryFaces && !_majorAxes)
                    {
                        spIdx.createSpatialIndexFromBIMRLElement(FedID, whereCond, true);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_SpatialIndexes.sql"), FedID);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_TopoFace.sql"), FedID);
                        updOctreeLevel = "MAXOCTREELEVEL=" + DBOperation.OctreeSubdivLevel;
                    }
                    // Update Spatial index (including major axes and OBB) only
                    else if (regenSpatialIndex && !regenBoundaryFaces && _majorAxes)
                    {
                        spIdx.createSpatialIndexFromBIMRLElement(FedID, whereCond, false);
                        BIMRLUtils.updateMajorAxesAndOBB(FedID, whereCond);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_SpatialIndexes.sql"), FedID);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_MajorAxes.sql"), FedID);
                        updOctreeLevel = "MAXOCTREELEVEL=" + DBOperation.OctreeSubdivLevel;
                    }
                    // Update Boundary faces and MajorAxes
                    else if (!regenSpatialIndex && regenBoundaryFaces && _majorAxes)
                    {
                        spIdx.createFacesFromBIMRLElement(FedID, whereCond);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_TopoFace.sql"), FedID);
                        BIMRLUtils.updateMajorAxesAndOBB(FedID, whereCond);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_MajorAxes.sql"), FedID);
                    }
                    // Update Spatial Index only
                    else if (regenSpatialIndex && !regenBoundaryFaces && !_majorAxes)
                    {
                        spIdx.createSpatialIndexFromBIMRLElement(FedID, whereCond, false);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_SpatialIndexes.sql"), FedID);
                        updOctreeLevel = "MAXOCTREELEVEL=" + DBOperation.OctreeSubdivLevel;
                    }
                    // update faces only
                    else if (!regenSpatialIndex && regenBoundaryFaces && !_majorAxes)
                    {
                        spIdx.createFacesFromBIMRLElement(FedID, whereCond);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_TopoFace.sql"), FedID);
                    }
                    // Update only the major axes and OBB only
                    else if (!regenSpatialIndex && !regenBoundaryFaces && _majorAxes)
                    {
                        BIMRLUtils.updateMajorAxesAndOBB(FedID, whereCond);
                        DBOperation.executeScript(Path.Combine(exePath, "script", "BIMRL_Idx_MajorAxes.sql"), FedID);
                    }
                    else
                    {
                        // Invalid option
                    }

                    string sqlStmt = "UPDATE BIMRL_FEDERATEDMODEL SET LASTUPDATEDATE=sysdate";
                    BIMRLCommon.appendToString(updOctreeLevel, ", ", ref sqlStmt);
                    BIMRLCommon.appendToString("WHERE FEDERATEDID=" + FedID, " ", ref sqlStmt);
                    DBOperation.executeSingleStmt(sqlStmt);
                }
            }
            catch (SystemException excp)
            {
                string excStr = "%% Error - " + excp.Message + "\n\t";
                BIMRLCommonRef.StackPushError(excStr);
                if (BIMRLCommonRef.BIMRLErrorStackCount > 0)
                    showError(null);
            }
            MathUtils.tol = currentTol;
        }

        private void CheckBox_FacesOnly_Checked(object sender, RoutedEventArgs e)
        {
            drawFacesOnly = true;
        }

        private void CheckBox_FacesOnly_Unchecked(object sender, RoutedEventArgs e)
        {
            drawFacesOnly = false;
        }

        static bool regenSpatialIndex = false;
        private void CheckBox_RegenSpatialIndex_Checked(object sender, RoutedEventArgs e)
        {
            regenSpatialIndex = true;
        }

        private void CheckBox_RegenSpatialIndex_Unchecked(object sender, RoutedEventArgs e)
        {
            regenSpatialIndex = false;
        }

        static bool regenBoundaryFaces = false;
        private void CheckBox_RegenBoundFaces_Checked(object sender, RoutedEventArgs e)
        {
            regenBoundaryFaces = true;
        }

        private void CheckBox_RegenBoundFaces_Unchecked(object sender, RoutedEventArgs e)
        {
            regenBoundaryFaces = false;
        }

        static bool _deleteModel = false;
      private void CheckBox_DeleteModel_Checked(object sender, RoutedEventArgs e)
      {
         if (DBOperation.currFedModel.Owner.Equals(DBOperation.DBUserID))
         {
            _deleteModel = true;
            Button_DeleteModel.IsEnabled = true;
         }
         else
         {
            _deleteModel = false;
            Button_DeleteModel.IsEnabled = false;
         }
      }

        private void CheckBox_DeleteModel_Unchecked(object sender, RoutedEventArgs e)
        {
            _deleteModel = false;
            Button_DeleteModel.IsEnabled = false;
        }

        private void Button_DeleteModel_Click(object sender, RoutedEventArgs e)
        {
            FederatedModelInfo selFedModel;
            int FedID = -1;
            FederatedModelInfo selFedModelsItem = DataGrid_FedModels.SelectedItem as FederatedModelInfo;
            if (selFedModelsItem == null)
                return;     // do nothing, no selection made
            else
            {
                selFedModel = selFedModelsItem;
                FedID = selFedModel.FederatedID;
            }
            try
            {
                if (FedID >= 0 && _deleteModel)
                {
                    _qModel.deleteModel(FedID);

                    // Refresh the model list
                    fedModels = _qModel.getFederatedModels();
                    DataGrid_FedModels.AutoGenerateColumns = true;
                    DataGrid_FedModels.IsReadOnly = true;
                    DataGrid_FedModels.ItemsSource = fedModels;
                    DataGrid_FedModels.MinRowHeight = 20;

                    // Always turn off the check box to delete the model
                    _deleteModel = false;
                    CheckBox_DeleteModel.IsChecked = false;
                    Button_DeleteModel.IsEnabled = false;
                }
            }
            catch (SystemException excp)
            {
                string excStr = "%% Error - " + excp.Message + "\n\t";
                BIMRLCommonRef.StackPushError(excStr);
                if (BIMRLCommonRef.BIMRLErrorStackCount > 0)
                    showError(null);
            }
        }

        static bool _majorAxes = true;
        private void CheckBox_MajorAxes_Checked(object sender, RoutedEventArgs e)
        {
            _majorAxes = true;
        }

        private void CheckBox_MajorAxes_Unchecked(object sender, RoutedEventArgs e)
        {
            _majorAxes = false;
        }

        private void Button_EnhanceSpB_Click(object sender, RoutedEventArgs e)
        {
            string whereCond = string.Empty;
            double currentTol = MathUtils.tol;

            // Temporarily change the tolerance if it is set in the UI
            if (!string.IsNullOrEmpty(TextBox_Tolerance.Text))
            {
                double tolSetting;
                if (double.TryParse(TextBox_Tolerance.Text, out tolSetting))
                    MathUtils.tol = tolSetting;
            }

            int fedModelID = 0;
            FederatedModelInfo selFedModel;
            FederatedModelInfo selFedModelsItem = DataGrid_FedModels.SelectedItem as FederatedModelInfo;
            if (selFedModelsItem == null)
                return;     // do nothing, no selection made
            else
            {
                selFedModel = selFedModelsItem;
                fedModelID = selFedModel.FederatedID;
            }

            try
            {
                if (!string.IsNullOrEmpty(TextBox_Additional_Condition.Text))
                {
                    BIMRLCommon.appendToString(TextBox_Additional_Condition.Text, " AND ", ref whereCond);
                    string whereCondD = Regex.Replace(whereCond, "elementid", "spaceelementid", RegexOptions.IgnoreCase);
                    DBOperation.executeSingleStmt("DELETE FROM " + DBOperation.formatTabName("BIMRL_RELSPACEB_DETAIL", fedModelID) + " WHERE " + whereCondD);
                }
                else
                {
                     if (selFedModelsItem.Owner.Equals(DBOperation.DBUserID))
                        DBOperation.executeSingleStmt("TRUNCATE TABLE " + DBOperation.formatTabName("BIMRL_RELSPACEB_DETAIL", fedModelID));
                     else
                        DBOperation.executeSingleStmt("DELETE FROM " + DBOperation.formatTabName("BIMRL_RELSPACEB_DETAIL", fedModelID));
                }

                DBOperation.commitInterval = 5000;
                EnhanceBRep eBrep = new EnhanceBRep();
                eBrep.enhanceSpaceBoundary(whereCond);

                // We will procees the normal face first and then after that the spacial ones (OBB, PROJOBB)
                string whereCond2 = whereCond;
                BIMRLCommon.appendToString(" TYPE NOT IN ('OBB','PROJOBB')", " AND ", ref whereCond2);
                eBrep.ProcessOrientation(whereCond2);
                whereCond2 = whereCond;
                BIMRLCommon.appendToString(" TYPE='OBB'", " AND ", ref whereCond2);
                eBrep.ProcessOrientation(whereCond2);
                whereCond2 = whereCond;
                BIMRLCommon.appendToString(" TYPE='PROJOBB'", " AND ", ref whereCond2);
                eBrep.ProcessOrientation(whereCond2);
            }
            catch (SystemException excp)
            {
                string excStr = "%% Error - " + excp.Message + "\n\t";
                BIMRLCommonRef.StackPushError(excStr);
                if (BIMRLCommonRef.BIMRLErrorStackCount > 0)
                    showError(null);
            }
            MathUtils.tol = currentTol;
        }

        private void CheckBox_DrawWorldBB_Checked(object sender, RoutedEventArgs e)
        {
            drawWorldBB = true;
        }

        private void CheckBox_DrawWorldBB_Unchecked(object sender, RoutedEventArgs e)
        {
            drawWorldBB = false;
        }

        private void CheckBox_DrawElemGeom_Checked(object sender, RoutedEventArgs e)
        {
            drawElemGeom = true;
        }

        private void CheckBox_DrawElemGeom_Unchecked(object sender, RoutedEventArgs e)
        {
            drawElemGeom = false;
        }

        private void CheckBox_DrawUserGeom_Checked(object sender, RoutedEventArgs e)
        {
            drawUserGeom = true;
        }

        private void CheckBox_DrawUserGeom_Unchecked(object sender, RoutedEventArgs e)
        {
            drawUserGeom = false;
        }

        private void Button_OctreeLevelCompute_Click(object sender, RoutedEventArgs e)
        {
            int fedModelID = 0;
            FederatedModelInfo selFedModel;
            FederatedModelInfo selFedModelsItem = DataGrid_FedModels.SelectedItem as FederatedModelInfo;

            try
            {
                if (selFedModelsItem == null)
                    return;     // do nothing, no selection made
                else
                {
                    selFedModel = selFedModelsItem;
                    fedModelID = selFedModel.FederatedID;

                    int level = DBOperation.computeRecomOctreeLevel(fedModelID);
                    if (level > 0)
                        TextBox_OctreeLevel.Text = level.ToString();
                }
            }
            catch (SystemException excp)
            {
                string excStr = "%% Error - " + excp.Message + "\n\t";
                BIMRLCommonRef.StackPushError(excStr);
                if (BIMRLCommonRef.BIMRLErrorStackCount > 0)
                    showError(null);
            }
        }

        public void showError(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                BIMRLCommonRef.StackPushError(message);
                //BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(BIMRLCommonRef);
                //erroDlg.ShowDialog();
                textBox_ConsoleOutput.Text = "";
                textBox_ConsoleOutput.Text += BIMRLCommonRef.ErrorMessages;
            }
            else
                textBox_ConsoleOutput.Text = message;
        }

        private void TextBox_Tolerance_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_genGraph_Click(object sender, RoutedEventArgs e)
        {
            // 1. Generate Circulation graph data in the database
            int FedID = -1;
            FederatedModelInfo selFedModelsItem = DataGrid_FedModels.SelectedItem as FederatedModelInfo;
            if (selFedModelsItem == null)
                return;     // do nothing, no selection made
            else
            {
                FederatedModelInfo selFedModel = selFedModelsItem;
                FedID = selFedModel.FederatedID;
            }

            GraphData graphData = new GraphData();
            graphData.createCirculationGraph(FedID);
            if (GraphData.refBimrlCommon.BIMRLErrorStackCount > 0)
            {
                string wholeStack = string.Empty;
                while (true)
                {
                    wholeStack += GraphData.refBimrlCommon.StackPopError() + "\n";
                    if (GraphData.refBimrlCommon.BIMRLErrorStackCount == 0) break;
                }
                return;
            }

            // 2. (TODO) Generate Adjacency graph data in the database
            graphData.createSpaceAdjacencyGraph(FedID);
            if (GraphData.refBimrlCommon.BIMRLErrorStackCount > 0)
            {
                string wholeStack = string.Empty;
                while (true)
                {
                    wholeStack += GraphData.refBimrlCommon.StackPopError() + "\n";
                    if (GraphData.refBimrlCommon.BIMRLErrorStackCount == 0) break;
                }
                return;
            }
        }

        private IXbimXplorerPluginMasterWindow _parentWindow;
        public string WindowTitle { get; private set; }

        public void BindUi(IXbimXplorerPluginMasterWindow mainWindow)
        {
            _parentWindow = mainWindow;
            _model = mainWindow.Model;
            SetBinding(ModelProperty, new Binding());
        }

        static IModel _model;
        
        public IModel Model
        {
            get { return (IModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        public static DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(IfcStore), typeof(BIMRL_ETLWindow),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnSelectedEntityChanged));

        private static void OnSelectedEntityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // if any UI event should happen it needs to be specified here
            var ctrl = d as BIMRL_ETLWindow;
            if (ctrl == null)
                return;
            switch (e.Property.Name)
            {
                case "Model":
                    //Debug.WriteLine("Model Updated");
                    ctrl.OnPropertyChanged(e);
                    // ModelProperty =
                    break;
                case "SelectedEntity":
                    break;
            }
        }

        private void textBox_ConsoleOutput_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
