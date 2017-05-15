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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using BIMRL.Common;

namespace BIMRLMisc
{
    /// <summary>
    /// Interaction logic for ElementIDTest.xaml
    /// </summary>
    public partial class ElementIDTest : Window
    {
        public ElementIDTest()
        {
            InitializeComponent();
        }

        private void Button_convert_toNo_Click(object sender, RoutedEventArgs e)
        {
            ElementID eid = new ElementID(TextBox_ElementIDStr.Text);
            Tuple<UInt64, UInt64> eidNo = eid.ElementIDNo;
            TextBox_ElementIDNo.Text = "[ " + eidNo.Item1.ToString() + ", " + eidNo.Item2.ToString() + " ]";
        }

        private void Button_convert_toStr_Click(object sender, RoutedEventArgs e)
        {
            string tmp = TextBox_ElementIDNo.Text;
            tmp = tmp.Replace("[", "").Replace("]", "").Trim();
            string[] members = tmp.Split(',');
            UInt64 upperPart = UInt64.Parse(members[0]);
            UInt64 lowerPart = UInt64.Parse(members[1]);
            ElementID eid = new ElementID(new Tuple<UInt64, UInt64>(upperPart, lowerPart));
            TextBox_ElementIDStr.Text = eid.ElementIDString;
        }
    }
}
