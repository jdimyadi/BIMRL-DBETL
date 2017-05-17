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
using BIMRL.Common;
using BIMRL;

namespace BIMRL.BIMRL_ETLConfig
{
    /// <summary>
    /// Interaction logic for ErrorDialog.xaml
    /// </summary>
    public partial class Error_Dialog : Window
    {
        private BIMRLCommon _refBIMRLCommon;
        string m_localMsg = null;

        public Error_Dialog()
        {
            InitializeComponent();
        }

        public Error_Dialog(BIMRLCommon bimrlCommonRef)
        {
            InitializeComponent();
            _refBIMRLCommon = bimrlCommonRef;
        }

        public Error_Dialog(string localMsg)
        {
            InitializeComponent();
            m_localMsg = localMsg;
        }

        private void Button_Error_OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Error_Dialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize image
            if (_refBIMRLCommon != null)
            {
                if (_refBIMRLCommon.BIMRLErrorStackCount > 0)
                {
                    string wholeStack = string.Empty;
                    while (true)
                    {
                        wholeStack += _refBIMRLCommon.StackPopError() + "\n";
                        if (_refBIMRLCommon.BIMRLErrorStackCount == 0) break;
                    }
                    TextBox_Error_Box.Text = wholeStack;
                }
            }
            else if (!String.IsNullOrEmpty(m_localMsg))
                TextBox_Error_Box.Text = m_localMsg;
        }


    }
}
