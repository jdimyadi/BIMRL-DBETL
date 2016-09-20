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
using BIMRL.OctreeLib;

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
