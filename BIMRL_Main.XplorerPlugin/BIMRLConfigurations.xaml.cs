//
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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BIMRL.OctreeLib;
using BIMRL;

namespace BIMRLMainXplorerPlugin
{
    /// <summary>
    /// Interaction logic for BIMRLDBConnectSpec.xaml
    /// </summary>
    public partial class BIMRLConfigurations : Window
    {
        BIMRLCommon _bimrlCommon = new BIMRLCommon();

        public BIMRLConfigurations()
        {
            InitializeComponent();
            if (DBOperation.DBConn != null)
                Button_Disconnect.IsEnabled = true;
            else
                Button_Disconnect.IsEnabled = false;

            if (DBOperation.objectForSpaceBoundary.Count > 0)
            {
                CB_BeamSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_BeamSB.Content.ToString().ToUpper()];
                CB_ColumnSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_ColumnSB.Content.ToString().ToUpper()];
                CB_CurtainWallSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_CurtainWallSB.Content.ToString().ToUpper()];
                CB_CoveringSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_CoveringSB.Content.ToString().ToUpper()];
                CB_DoorSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_DoorSB.Content.ToString().ToUpper()];
                CB_MemberSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_MemberSB.Content.ToString().ToUpper()];
                CB_OpeningSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_OpeningSB.Content.ToString().ToUpper()];
                CB_PlateSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_PlateSB.Content.ToString().ToUpper()];
                CB_RailingSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_RailingSB.Content.ToString().ToUpper()];
                CB_RampSB.IsChecked = DBOperation.objectForSpaceBoundary["IFCRAMP"];    // Just check IFCRAMP is sufficient as IFCRAMPFLIGHT is always set in pair with IFCRAMP
                CB_RoofSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_RoofSB.Content.ToString().ToUpper()];
                CB_SlabSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_SlabSB.Content.ToString().ToUpper()];
                CB_SpaceSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_SpaceSB.Content.ToString().ToUpper()];
                CB_StairSB.IsChecked = DBOperation.objectForSpaceBoundary["IFCSTAIR"];  // Just check IFCSTAIR is sufficient as IFCSTAIRFLIGHT is always set in pair with IFCSTAIR
                CB_WallSB.IsChecked = DBOperation.objectForSpaceBoundary["IFCWALLSTANDARDCASE"];    // Just check IFCWALLSTANDARDCASE is sufficient as IFCWALL is always set in pair with IFCWALLSTANDARDCASE
                CB_WindowSB.IsChecked = DBOperation.objectForSpaceBoundary[CB_WindowSB.Content.ToString().ToUpper()];
            }

            if (DBOperation.objectForConnection.Count > 0)
            {
                CB_BeamConn.IsChecked = DBOperation.objectForConnection[CB_BeamConn.Content.ToString().ToUpper()];
                CB_ColumnConn.IsChecked = DBOperation.objectForConnection[CB_ColumnConn.Content.ToString().ToUpper()];
                CB_CurtainWallConn.IsChecked = DBOperation.objectForConnection[CB_CurtainWallConn.Content.ToString().ToUpper()];
                CB_CoveringConn.IsChecked = DBOperation.objectForConnection[CB_CoveringConn.Content.ToString().ToUpper()];
                CB_DoorConn.IsChecked = DBOperation.objectForConnection[CB_DoorConn.Content.ToString().ToUpper()];
                CB_MemberConn.IsChecked = DBOperation.objectForConnection[CB_MemberConn.Content.ToString().ToUpper()];
                CB_OpeningConn.IsChecked = DBOperation.objectForConnection[CB_OpeningConn.Content.ToString().ToUpper()];
                CB_PlateConn.IsChecked = DBOperation.objectForConnection[CB_PlateConn.Content.ToString().ToUpper()];
                CB_RailingConn.IsChecked = DBOperation.objectForConnection[CB_RailingConn.Content.ToString().ToUpper()];
                CB_RampConn.IsChecked = DBOperation.objectForConnection["IFCRAMP"];    // Just check IFCRAMP is sufficient as IFCRAMPFLIGHT is always set in pair with IFCRAMP
                CB_RoofConn.IsChecked = DBOperation.objectForConnection[CB_RoofConn.Content.ToString().ToUpper()];
                CB_SlabConn.IsChecked = DBOperation.objectForConnection[CB_SlabConn.Content.ToString().ToUpper()];
                CB_SpaceConn.IsChecked = DBOperation.objectForConnection[CB_SpaceConn.Content.ToString().ToUpper()];
                CB_StairConn.IsChecked = DBOperation.objectForConnection["IFCSTAIR"];  // Just check IFCSTAIR is sufficient as IFCSTAIRFLIGHT is always set in pair with IFCSTAIR
                CB_WallConn.IsChecked = DBOperation.objectForConnection["IFCWALLSTANDARDCASE"];    // Just check IFCWALLSTANDARDCASE is sufficient as IFCWALL is always set in pair with IFCWALLSTANDARDCASE
                CB_WindowConn.IsChecked = DBOperation.objectForConnection[CB_WindowConn.Content.ToString().ToUpper()];
            }
}

        private void TextBox_DBUserID_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBlock_DB_ConnStr.Text = TextBox_DBUserID.Text + "/" + TextBox_DBPassword.Text + "@" + TextBox_DBConn.Text;
        }

        private void TextBox_DBPassword_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBlock_DB_ConnStr.Text = TextBox_DBUserID.Text + "/" + TextBox_DBPassword.Text + "@" + TextBox_DBConn.Text;
        }

        private void TextBox_DBConn_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBlock_DB_ConnStr.Text = TextBox_DBUserID.Text + "/" + TextBox_DBPassword.Text + "@" + TextBox_DBConn.Text;
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            DBOperation.DBUserID = TextBox_DBUserID.Text;
            DBOperation.DBPassword = TextBox_DBPassword.Text;
            DBOperation.DBConnecstring = TextBox_DBConn.Text;

            _bimrlCommon.resetAll();

            // Connect to Oracle DB
            DBOperation.refBIMRLCommon = _bimrlCommon;      // important to ensure DBoperation has reference to this object!!
            if (DBOperation.Connect() == null)
            {
                BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(_bimrlCommon);
                erroDlg.ShowDialog();
                return;
            }

            // For object selections for Space Boundary
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_BeamSB.Content.ToString().ToUpper(), CB_BeamSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_ColumnSB.Content.ToString().ToUpper(), CB_ColumnSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_CurtainWallSB.Content.ToString().ToUpper(), CB_CurtainWallSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_CoveringSB.Content.ToString().ToUpper(), CB_CoveringSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_DoorSB.Content.ToString().ToUpper(), CB_DoorSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_MemberSB.Content.ToString().ToUpper(), CB_MemberSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_OpeningSB.Content.ToString().ToUpper(), CB_OpeningSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_PlateSB.Content.ToString().ToUpper(), CB_PlateSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_RailingSB.Content.ToString().ToUpper(), CB_RailingSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, "IFCRAMP", CB_RampSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, "IFCRAMPFLIGHT", CB_RampSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_RoofSB.Content.ToString().ToUpper(), CB_RoofSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_SlabSB.Content.ToString().ToUpper(), CB_SlabSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_SpaceSB.Content.ToString().ToUpper(), CB_SpaceSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, "IFCSTAIR", CB_StairSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, "IFCSTAIRFLIGHT", CB_StairSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, "IFCWALL", CB_WallSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, "IFCWALLSTANDARDCASE", CB_WallSB.IsChecked.Value);
            registerObjectType(DBOperation.objectForSpaceBoundary, CB_WindowSB.Content.ToString().ToUpper(), CB_WindowSB.IsChecked.Value);

            // For object selections for Connection (e.g. Envelop)
            registerObjectType(DBOperation.objectForConnection, CB_BeamConn.Content.ToString().ToUpper(), CB_BeamConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_ColumnConn.Content.ToString().ToUpper(), CB_ColumnConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_CurtainWallConn.Content.ToString().ToUpper(), CB_CurtainWallConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_CoveringConn.Content.ToString().ToUpper(), CB_CoveringConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_DoorConn.Content.ToString().ToUpper(), CB_DoorConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_MemberConn.Content.ToString().ToUpper(), CB_MemberConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_OpeningConn.Content.ToString().ToUpper(), CB_OpeningConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_PlateConn.Content.ToString().ToUpper(), CB_PlateConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_RailingConn.Content.ToString().ToUpper(), CB_RailingConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, "IFCRAMP", CB_RampConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, "IFCRAMPFLIGHT", CB_RampConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_RoofConn.Content.ToString().ToUpper(), CB_RoofConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_SlabConn.Content.ToString().ToUpper(), CB_SlabConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_SpaceConn.Content.ToString().ToUpper(), CB_SpaceConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, "IFCSTAIR", CB_StairConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, "IFCSTAIRFLIGHT", CB_StairConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, "IFCWALL", CB_WallConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, "IFCWALLSTANDARDCASE", CB_WallConn.IsChecked.Value);
            registerObjectType(DBOperation.objectForConnection, CB_WindowConn.Content.ToString().ToUpper(), CB_WindowConn.IsChecked.Value);

            Close();
        }

        void registerObjectType(Dictionary<string,bool> theDict, string elementType, bool isChecked)
        {
            if (!theDict.ContainsKey(elementType))
                theDict.Add(elementType, isChecked);
            else
                theDict[elementType] = isChecked;
        }

        private void BIMRL_Configurations_Loaded(object sender, RoutedEventArgs e)
        {
            TextBox_DBUserID.Text = DBOperation.DBUserID;
            TextBox_DBPassword.Text = DBOperation.DBPassword;
            TextBox_DBConn.Text = DBOperation.DBConnecstring;
            TextBlock_DB_ConnStr.Text = TextBox_DBUserID.Text + "/" + TextBox_DBPassword.Text + "@" + TextBox_DBConn.Text;
            List<string> octreeLevelData = new List<string>();
            octreeLevelData.Add("2");
            octreeLevelData.Add("3");
            octreeLevelData.Add("4");
            octreeLevelData.Add("5");
            octreeLevelData.Add("6");
            octreeLevelData.Add("7");
            octreeLevelData.Add("8");
            octreeLevelData.Add("9");
            octreeLevelData.Add("10");
            octreeLevelData.Add("11");
            octreeLevelData.Add("12");
            octreeLevelData.Add("13");
            octreeLevelData.Add("14");
            octreeLevelData.Add("15");
            octreeLevelData.Add("16");
            octreeLevelData.Add("17");
            octreeLevelData.Add("18");
            octreeLevelData.Add("19");

            if (DBOperation.OnepushETL)
                CheckBox_OnepushETL.IsChecked = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_Disconnect_Click(object sender, RoutedEventArgs e)
        {
            DBOperation.Disconnect();
            Button_Disconnect.IsEnabled = false;
            Close();
        }

        private void CheckBox_OnepushETL_Checked(object sender, RoutedEventArgs e)
        {
            DBOperation.OnepushETL = true;
        }

        private void CheckBox_OnepushETL_Unchecked(object sender, RoutedEventArgs e)
        {
            DBOperation.OnepushETL = false;
        }
    }
}
