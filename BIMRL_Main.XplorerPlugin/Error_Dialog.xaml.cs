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
