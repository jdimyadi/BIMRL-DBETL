using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Xbim.Presentation.XplorerPluginSystem;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Common;
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
            IfcStore model = _model as IfcStore;
            string projName = string.Empty;
            IIfcProject project = model.Instances.OfType<IIfcProject>(true).FirstOrDefault();

            //if (model.IfcSchemaVersion == IfcSchemaVersion.Ifc4)
            //{
            //    var project = model.Instances.FirstOrDefault<Xbim.Ifc4.Kernel.IfcProject>();
            //    projName = project.Name;
            //}
            //else if (model.IfcSchemaVersion == IfcSchemaVersion.Ifc2X3)
            //{
            //    var project = model.Instances.FirstOrDefault<Xbim.Ifc2x3.Kernel.IfcProject>();
            //    projName = project.Name;
            //}

            if (String.Compare(projName, "Empty Project") != 0)
               button_OK.IsEnabled = true;
         }
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
            IfcStore model = _model as IfcStore;
            string projName = string.Empty;
            IIfcProject project = model.Instances.OfType<IIfcProject>(true).FirstOrDefault();

            if (String.Compare(projName, "Empty Project") != 0)
                  button_OK.IsEnabled = true;
         }
         SetBinding(ModelProperty, new Binding());
      }

      static IModel _model;

      public IModel Model
        {
            get { return (IModel)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        public static DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(IfcStore), typeof(ETLLoad),
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
            if (checkBox_ETLOption.IsChecked.Value)
                DBOperation.OnepushETL = true;
            else
                DBOperation.OnepushETL = false;

            bimrlProcessModel bimrlPM = new bimrlProcessModel(_model, false);

            Close();
        }
    }
}
