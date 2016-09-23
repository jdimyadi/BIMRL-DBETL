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
using Xbim.Presentation.XplorerPluginSystem;
using Xbim.IO;
using BIMRL;
using BIMRL.OctreeLib;


namespace push2BIMRL.XplorerPlugin
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [XplorerUiElement(PluginWindowUiContainerEnum.Dialog, PluginWindowActivation.OnMenu, "Load Model to BIMRL")]
    public partial class ETLLoad : Window, IXbimXplorerPluginWindow
    {
        public ETLLoad()
        {
            InitializeComponent();

            button_OK.IsEnabled = false;
            if (_model != null)
            {
                if (String.Compare(_model.IfcProject.Name, "Empty Project") != 0)
                    button_OK.IsEnabled = true;
            }

            // Check existence of the model in BIMRL: if exist write message to let user know that it will overwrite the existing one
        }

        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            if (checkBox_ETLOption.IsEnabled)
                DBOperation.OnepushETL = true;
            else
                DBOperation.OnepushETL = false;
        }

        private IXbimXplorerPluginMasterWindow _parentWindow;
        public string WindowTitle { get; private set; }

        public void BindUi(IXbimXplorerPluginMasterWindow mainWindow)
        {
            _parentWindow = mainWindow;
            _model = mainWindow.Model;
            if (_model != null)
            {
                if (String.Compare(_model.IfcProject.Name, "Empty Project") != 0)
                    button_OK.IsEnabled = true;
            }
            SetBinding(ModelProperty, new Binding());
        }

        static XbimModel _model;

        public XbimModel Model
        {
            get { return (XbimModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        public static DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(XbimModel), typeof(ETLLoad),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnSelectedEntityChanged));

        private static void OnSelectedEntityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // if any UI event should happen it needs to be specified here
            var ctrl = d as ETLLoad;
            if (ctrl != null)
            {
                if (e.Property.Name == "Model")
                {
                    // ctrl.txtElementReport.Text = "Model updated";
                }
                else if (e.Property.Name == "SelectedEntity")
                {

                }
            }
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void button_OK_Click(object sender, RoutedEventArgs e)
        {
            if (checkBox_ETLOption.IsEnabled)
                DBOperation.OnepushETL = true;
            else
                DBOperation.OnepushETL = false;

            bimrlProcessModel bimrlPM = new bimrlProcessModel(_model, false);

            Close();
        }
    }
}
