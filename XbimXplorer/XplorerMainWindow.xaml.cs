#region XbimHeader

// The eXtensible Building Information Modelling (xBIM) Toolkit
// Solution:    XbimComplete
// Project:     XbimXplorer
// Filename:    XplorerMainWindow.xaml.cs
// Published:   01, 2012
// Last Edited: 9:05 AM on 20 12 2011
// (See accompanying copyright.rtf)

#endregion

#region Directives

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml;
using Microsoft.Win32;
using Xbim.IO;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Presentation;
using Xbim.XbimExtensions;
using Xbim.ModelGeometry;
using Xbim.ModelGeometry.Scene;
using Xbim.XbimExtensions.Interfaces;
using Xbim.Ifc2x3.Extensions;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Xbim.Common.Exceptions;
using System.Diagnostics;
using Xbim.Ifc2x3.ActorResource;
using Xbim.Common.Geometry;
using Xbim.COBie.Serialisers;
using Xbim.COBie;
using Xbim.COBie.Contracts;
using Xbim.ModelGeometry.Converter;
using XbimXplorer.Dialogs;
using BIMRL;
#endregion


namespace XbimXplorer
{
    /// <summary>
    ///   Interaction logic for Window1.xaml
    /// </summary>
    public partial class XplorerMainWindow : Window
    {
        private BackgroundWorker _worker;
        public static RoutedCommand CreateFederationCmd = new RoutedCommand();
        public static RoutedCommand EditFederationCmd = new RoutedCommand();
        public static RoutedCommand OpenFederationCmd = new RoutedCommand();
        public static RoutedCommand InsertCmd = new RoutedCommand();
        public static RoutedCommand ExportCOBieCmd = new RoutedCommand();
        public static RoutedCommand BIMRLConfigurationCmd = new RoutedCommand();
        public static RoutedCommand BIMRLShowModelCmd = new RoutedCommand();
        public static RoutedCommand PushToBIMRLCmd = new RoutedCommand();
        public static RoutedCommand UpdateBIMRLTransformCmd = new RoutedCommand();
        
        private string _currentModelFileName;
        private string _temporaryXbimFileName;
        private string _defaultFileName;

        public XplorerMainWindow()
        {
            InitializeComponent();
            this.Closed += new EventHandler(XplorerMainWindow_Closed);
            this.Loaded += XplorerMainWindow_Loaded;
            this.Closing += new CancelEventHandler(XplorerMainWindow_Closing);
        }

        void XplorerMainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_worker != null && _worker.IsBusy)
                e.Cancel = true; //do nothing if a thread is alive
            else
                e.Cancel = false;

        }

        void XplorerMainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1) //load one if specified
            {
                StatusBar.Visibility = Visibility.Visible;
                string toOpen = args[1];
                CreateWorker();
                string ext = Path.GetExtension(toOpen);
                switch (ext)
                {
                    case ".ifc": //it is an Ifc File
                    case ".ifcxml": //it is an IfcXml File
                    case ".ifczip": //it is a xip file containing xbim or ifc File
                    case ".zip": //it is a xip file containing xbim or ifc File
                        CloseAndDeleteTemporaryFiles();
                        _worker.DoWork += OpenIfcFile;
                        _worker.RunWorkerAsync(toOpen);
                        break;
                    case ".xbimf":
                    case ".xbim": //it is an xbim File, just open it in the main thread
                        CloseAndDeleteTemporaryFiles();
                        _worker.DoWork += OpenXbimFile;
                        _worker.RunWorkerAsync(toOpen);
                        break;
                    default:
                        break;
                }
            }
            else //just create an empty model
            {
                XbimModel model = XbimModel.CreateTemporaryModel();
                model.Initialise();
                ModelProvider.ObjectInstance = model;
                ModelProvider.Refresh();
            }
        }

        void XplorerMainWindow_Closed(object sender, EventArgs e)
        {

            CloseAndDeleteTemporaryFiles();
        }

        void DrawingControl_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            
        }


        public IPersistIfcEntity SelectedItem
        {
            get { return (IPersistIfcEntity)GetValue(SelectedItemProperty); }
            set { SetValue(SelectedItemProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SelectedItem.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register("SelectedItem", typeof(IPersistIfcEntity), typeof(XplorerMainWindow), 
                                        new UIPropertyMetadata(null, new PropertyChangedCallback(OnSelectedItemChanged)));


        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            XplorerMainWindow mw = d as XplorerMainWindow;
            if (mw != null && e.NewValue is IPersistIfcEntity)
            {
                IPersistIfcEntity label = (IPersistIfcEntity)e.NewValue;
                mw.EntityLabel.Text = label !=null ? "#" + label.EntityLabel.ToString() : "";
            }
            else
                mw.EntityLabel.Text = "";
        }


        private ObjectDataProvider ModelProvider
        {
            get
            {
                return MainFrame.DataContext as ObjectDataProvider;
               
            }
            
        }

        public XbimModel Model
        {
            get
            {
                ObjectDataProvider op = MainFrame.DataContext as ObjectDataProvider;
                return op == null ? null : op.ObjectInstance as XbimModel;
            }
        }

        private void OpenIfcFile(object s, DoWorkEventArgs args)
        {
            BackgroundWorker worker = s as BackgroundWorker;
            string ifcFilename = args.Argument as string;

            XbimModel model = new XbimModel();
            try
            {
                _temporaryXbimFileName = Path.GetTempFileName();
                _defaultFileName = Path.GetFileNameWithoutExtension(ifcFilename);
                model.CreateFrom(ifcFilename, _temporaryXbimFileName, worker.ReportProgress);
                model.Open(_temporaryXbimFileName, XbimDBAccess.ReadWrite);
                XbimMesher.GenerateGeometry(model, null, worker.ReportProgress);


               // model.Close();
                if (worker.CancellationPending == true) //if a cancellation has been requested then don't open the resulting file
                {
                    try
                    {
                        model.Close();
                        if (File.Exists(_temporaryXbimFileName))
                            File.Delete(_temporaryXbimFileName); //tidy up;
                        _temporaryXbimFileName = null;
                        _defaultFileName = null;
                    }
                    catch (Exception)
                    {


                    }
                    return;
                }
              //  model.Open(_temporaryXbimFileName, XbimDBAccess.ReadWrite, worker.ReportProgress);

                args.Result = model;

            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Error reading " + ifcFilename);
                string indent = "\t";
                while (ex != null)
                {
                    sb.AppendLine(indent + ex.Message);
                    ex = ex.InnerException;
                    indent += "\t";
                }

                args.Result = new Exception(sb.ToString());

            }
        }

        private void InsertIfcFile(object s, DoWorkEventArgs args)
        {
            BackgroundWorker worker = s as BackgroundWorker;
            string ifcFilename = args.Argument as string;

            XbimModel model = new XbimModel();
            try
            {
                _temporaryXbimFileName = Path.GetTempFileName();
                _defaultFileName = Path.GetFileNameWithoutExtension(ifcFilename);
                model.CreateFrom(ifcFilename, _temporaryXbimFileName, worker.ReportProgress);
                model.Open(_temporaryXbimFileName, XbimDBAccess.ReadWrite);
                XbimMesher.GenerateGeometry(model, null, worker.ReportProgress);
                model.Close();
                if (worker.CancellationPending == true) //if a cancellation has been requested then don't open the resukting file
                {
                    try
                    {
                        if (File.Exists(_temporaryXbimFileName))
                            File.Delete(_temporaryXbimFileName); //tidy up;
                        _temporaryXbimFileName = null;
                        _defaultFileName = null;
                    }
                    catch (Exception)
                    {


                    }
                    return;
                }
               // model.Open(_temporaryXbimFileName, XbimDBAccess.Read, worker.ReportProgress);
                this.Dispatcher.BeginInvoke(new Action(() => { Model.AddModelReference(_temporaryXbimFileName, "Organisation X", IfcRole.BuildingOperator); }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Error reading " + ifcFilename);
                string indent = "\t";
                while (ex != null)
                {
                    sb.AppendLine(indent + ex.Message);
                    ex = ex.InnerException;
                    indent += "\t";
                }

                args.Result = new Exception(sb.ToString());

            }
        }


        /// <summary>
        ///   This is called when we explcitly want to open an xBIM file
        /// </summary>
        /// <param name = "s"></param>
        /// <param name = "args"></param>
        private void OpenXbimFile(object s, DoWorkEventArgs args)
        {
            BackgroundWorker worker = s as BackgroundWorker;
            string fileName = args.Argument as string;
            XbimModel model = new XbimModel();
            try
            {
                _currentModelFileName = fileName.ToLower();
                model.Open(fileName, XbimDBAccess.Read, worker.ReportProgress); //load entities into the model
                args.Result = model;
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Error reading " + fileName);
                string indent = "\t";
                while (ex != null)
                {
                    sb.AppendLine(indent + ex.Message);
                    ex = ex.InnerException;
                    indent += "\t";
                }

                args.Result = new Exception(sb.ToString());
            }
        }

        private void dlg_InsertXbimFile(object sender, CancelEventArgs e)
        {
             OpenFileDialog dlg = sender as OpenFileDialog;
            if (dlg != null)
            {
                FileInfo fInfo = new FileInfo(dlg.FileName);
                string ext = fInfo.Extension.ToLower();
                StatusBar.Visibility = Visibility.Visible;
                
                CreateWorker();
                if (dlg.FileName.ToLower() == _currentModelFileName) //same file do nothing
                   return;
                switch (ext)
                {
                    case ".ifc": //it is an Ifc File
                    case ".ifcxml": //it is an IfcXml File
                    case ".ifczip": //it is a xip file containing xbim or ifc File
                    case ".zip": //it is a xip file containing xbim or ifc File
                        _worker.DoWork += InsertIfcFile;
                        _worker.RunWorkerAsync(dlg.FileName);
                        break;
                    case ".xbimf":
                    case ".xbim": //it is an xbim File, just open it in the main thread
                        Model.AddModelReference(dlg.FileName,"Organisation X",IfcRole.BuildingOperator);
                        break;
                    default:
                        break;
                }
            }
        }

        private void dlg_OpenXbimFile(object sender, CancelEventArgs e)
        {
            OpenFileDialog dlg = sender as OpenFileDialog;
            if (dlg != null)
            {
                FileInfo fInfo = new FileInfo(dlg.FileName);
                string ext = fInfo.Extension.ToLower();
                StatusBar.Visibility = Visibility.Visible;
                CreateWorker();
                if (dlg.FileName.ToLower() == _currentModelFileName) //same file do nothing
                   return;
                switch (ext)
                {
                    case ".ifc": //it is an Ifc File
                    case ".ifcxml": //it is an IfcXml File
                    case ".ifczip": //it is a xip file containing xbim or ifc File
                    case ".zip": //it is a xip file containing xbim or ifc File
                        CloseAndDeleteTemporaryFiles();
                        _worker.DoWork += OpenIfcFile;
                        _worker.RunWorkerAsync(dlg.FileName);
                        break;
                    case ".xbimf":
                    case ".xbim": //it is an xbim File, just open it in the main thread
                        CloseAndDeleteTemporaryFiles();
                        _worker.DoWork += OpenXbimFile;
                        _worker.RunWorkerAsync(dlg.FileName);   
                        break;
                    default:
                        break;
                }
            }
        }

        private void CreateWorker()
        {
            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;
            _worker.WorkerSupportsCancellation = true;
            _worker.ProgressChanged += delegate(object s, ProgressChangedEventArgs args)
                                           {
                                               ProgressBar.Value = args.ProgressPercentage;
                                               StatusMsg.Text = (string) args.UserState;
                                           };

            _worker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs args)
                                              {
                                                  if (args.Result is XbimModel) //all ok
                                                  {
                                                      ModelProvider.ObjectInstance = (XbimModel)args.Result; //this Triggers the event to load the model into the views 
                                                      ModelProvider.Refresh();
                                                  }
                                                  else //we have a problem
                                                  {
                                                      string errMsg = args.Result as String;
                                                      if (!string.IsNullOrEmpty(errMsg))
                                                          MessageBox.Show(this, errMsg, "Error Opening Ifc File",
                                                                          MessageBoxButton.OK, MessageBoxImage.Error,
                                                                          MessageBoxResult.None, MessageBoxOptions.None);
                                                      if (args.Result is Exception)
                                                      {
                                                          StringBuilder sb = new StringBuilder();
                                                          Exception ex = args.Result as Exception;
                                                          String indent = "";
                                                          while (ex != null)
                                                          {
                                                              sb.AppendFormat("{0}{1}\n", indent, ex.Message);
                                                              ex = ex.InnerException;
                                                              indent += "\t";
                                                          }
                                                          MessageBox.Show(this, sb.ToString(), "Error Opening Ifc File",
                                                                          MessageBoxButton.OK, MessageBoxImage.Error,
                                                                          MessageBoxResult.None, MessageBoxOptions.None);
                                                      }
                                                  }
                                                  // StatusBar.Visibility = Visibility.Hidden;
                                              };
        }


        private void ProgressChanged(object sender, ProgressChangedEventArgs args)
        {
            ProgressBar.Value = args.ProgressPercentage;
            string msg = args.UserState as string;
            if (msg != null) StatusMsg.Text = msg;
        }

        private void SpatialControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DrawingControl.ZoomSelected();
        }

       
          


        private void dlg_FileSaveAs(object sender, CancelEventArgs e)
        {
            SaveFileDialog dlg = sender as SaveFileDialog;
            if (dlg != null)
            {
                FileInfo fInfo = new FileInfo(dlg.FileName);
                try
                {
                    if (fInfo.Exists) fInfo.Delete();

                    if (Model != null)
                    {
                        Model.SaveAs(dlg.FileName);
                       
                        if (string.Compare(Path.GetExtension(dlg.FileName),"XBIM",true)==0 && 
                            !string.IsNullOrWhiteSpace(_temporaryXbimFileName)) //we have a temp file open, it is now redundant as we have upgraded to another xbim file
                        {
                            File.Delete(_temporaryXbimFileName);
                            _temporaryXbimFileName = null;
                        }
                    }
                    else throw new Exception("Invalid Model Server");
                }
                catch (Exception except)
                {
                    MessageBox.Show(except.Message, "Error Saving as", MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
        }

       

        private void CommandBinding_SaveAs(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.DefaultExt = "ifc";
            dlg.FileName = _defaultFileName;
            dlg.Filter = "xBIM File (*.xBIM)|*.xBIM|Ifc File (*.ifc)|*.ifc|IfcXml File (*.IfcXml)|*.ifcxml|IfcZip File (*.IfcZip)|*.ifczip"; // Filter files by extension 
            dlg.Title = "Save As";
            dlg.AddExtension = true;
           
            // Show open file dialog box 
            dlg.FileOk += new CancelEventHandler(dlg_FileSaveAs);
            dlg.ShowDialog(this);
        }

        private void CommandBinding_Close(object sender, ExecutedRoutedEventArgs e)
        {
            CloseAndDeleteTemporaryFiles();
        }

        private void CommandBinding_New(object sender, ExecutedRoutedEventArgs e)
        {
            CloseAndDeleteTemporaryFiles();
            XbimModel model=   XbimModel.CreateTemporaryModel();
            model.Initialise();
            ModelProvider.ObjectInstance = model;
            ModelProvider.Refresh();

        }
        
       
        private void CommandBinding_Open(object sender, ExecutedRoutedEventArgs e)
        {
           
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Xbim Files|*.xbim;*.xbimf;*.ifc;*.ifcxml;*.ifczip"; // Filter files by extension 
            dlg.FileOk += new CancelEventHandler(dlg_OpenXbimFile);
            dlg.ShowDialog(this);
        }

        /// <summary>
        /// Tidies up any open files and closes any open models
        /// </summary>
        private void CloseAndDeleteTemporaryFiles()
        {
           
            try
            {
                if (_worker != null && _worker.IsBusy)
                    _worker.CancelAsync(); //tell it to stop
                XbimModel model = ModelProvider.ObjectInstance as XbimModel;
                _currentModelFileName = null;
                if (model != null)
                {
                    model.Dispose();
                    ModelProvider.ObjectInstance = null;
                    ModelProvider.Refresh();
                }
            }
            finally
            {
                if (!(_worker != null && _worker.IsBusy && _worker.CancellationPending)) //it is still busy but has been cancelled 
                {
             
                    if (!string.IsNullOrWhiteSpace(_temporaryXbimFileName) && File.Exists(_temporaryXbimFileName))
                        File.Delete(_temporaryXbimFileName);
                    _temporaryXbimFileName = null;
                    _defaultFileName = null;
                } //else do nothing it will be cleared up in the worker thread
            }
        }

        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (_worker != null && _worker.IsBusy)
                e.CanExecute = false;
            else
            {
                if (e.Command == ApplicationCommands.Close || e.Command == ApplicationCommands.SaveAs)
                {
                    XbimModel model = ModelProvider.ObjectInstance as XbimModel;
                    e.CanExecute = (model != null);
                }
                else
                    e.CanExecute = true; //for everything else
            }

        }


        private void MenuItem_ZoomExtents(object sender, RoutedEventArgs e)
        {
            DrawingControl.ViewHome();
        }

        private void ExportCOBieCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            string outputFile = Path.ChangeExtension(Model.DatabaseName, ".xls");

            // Build context
            COBieContext context = new COBieContext();
            context.TemplateFileName = "COBie-UK-2012-template.xls";
            context.Model = Model;
            //set filter option

            //switch (chckBtn.Name)
            //{
            //    case "rbDefault":
            //        break;
            //    case "rbPickList":
            //        context.ExcludeFromPickList = true;
            //        break;
            //    case "rbNoFilters":
            //        context.Exclude.Clear();
            //        break;
            //    default:
            //        break;
            //}
            //set the UI language to get correct resource file for template
            //if (Path.GetFileName(parameters.TemplateFile).Contains("-UK-"))
            //{
            try
            {
                System.Globalization.CultureInfo ci = new System.Globalization.CultureInfo("en-GB");
                System.Threading.Thread.CurrentThread.CurrentUICulture = ci;
            }
            catch (Exception)
            {
                //to nothing Default culture will still be used

            }

            COBieBuilder builder = new COBieBuilder(context);
            ICOBieSerialiser serialiser = new COBieXLSSerialiser(outputFile, context.TemplateFileName);
            builder.Export(serialiser);
            Process.Start(outputFile);
        }

        // CanExecuteRoutedEventHandler for the custom color command.
        private void ExportCOBieCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            XbimModel model = ModelProvider.ObjectInstance as XbimModel;
            bool canEdit = (model!=null && model.CanEdit && model.Instances.OfType<IfcBuilding>().FirstOrDefault()!=null);       
            e.CanExecute = canEdit && !(_worker != null && _worker.IsBusy);
        }

        private void InsertCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Xbim Files|*.xbim;*.ifc;*.ifcxml;*.ifczip"; // Filter files by extension 
            dlg.FileOk += new CancelEventHandler(dlg_InsertXbimFile);
            dlg.ShowDialog(this);
        }

        // CanExecuteRoutedEventHandler for the custom color command.
        private void InsertCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            XbimModel model = ModelProvider.ObjectInstance as XbimModel;
            bool canEdit = (model!=null && model.CanEdit);       
            e.CanExecute = canEdit && !(_worker != null && _worker.IsBusy);
        }

        private void EditFederationCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            FederatedModelDlg fdlg = new FederatedModelDlg();          
            fdlg.DataContext = Model;
            bool? done = fdlg.ShowDialog();
            if (done.HasValue && done.Value == true)
            {

            }
        }
        private void EditFederationCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            
            e.CanExecute = Model!=null &&  Model.IsFederation;
        }

        private void OpenFederationCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
             OpenFileDialog dlg = new OpenFileDialog();
             dlg.Filter = "Xbim Federation Files|*.xbimf"; // Filter files by extension 
             dlg.CheckFileExists = true;
            bool? done = dlg.ShowDialog(this);
            if (done.HasValue && done.Value == true)
            {
                XbimModel fedModel = new XbimModel();
                fedModel.Open(dlg.FileName,XbimDBAccess.ReadWrite);
                CloseAndDeleteTemporaryFiles();
                ModelProvider.ObjectInstance = fedModel;
                ModelProvider.Refresh();
            }
        }

       

        private void OpenFederationCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        private void CreateFederationCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            CreateFederationWindow fedwin = new CreateFederationWindow();
            bool? done = fedwin.ShowDialog();
            if (done.HasValue && done.Value == true)
            {
                if (File.Exists(fedwin.ModelFullPath))
                {
                    if (MessageBox.Show(fedwin.ModelFullPath + " Exists.\nDo you want to overwrite it?", "Overwrite file", MessageBoxButton.YesNo) == MessageBoxResult.No)
                        return;
                }
                try
                {
                    XbimModel fedModel = XbimModel.CreateModel(fedwin.ModelFullPath);

                    fedModel.Initialise(fedwin.Author, fedwin.Organisation);
                    using (var txn = fedModel.BeginTransaction())
                    {
                        fedModel.IfcProject.Name = fedwin.Project;
                        txn.Commit();
                    }
                    //FederatedModelDlg fdlg = new FederatedModelDlg();
                    //fdlg.DataContext = Model;
                    //fdlg.ShowDialog();
                    CloseAndDeleteTemporaryFiles();
                    ModelProvider.ObjectInstance = fedModel;
                    ModelProvider.Refresh();
                    //fedModel.SaveAs(Path.ChangeExtension(fedwin.ModelFullPath, ".ifc"), XbimStorageType.IFC);

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Model Creation Failed", MessageBoxButton.OK);
                }               
            }
        }
        private void CreateFederationCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void PushToBIMRLCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            XbimModel model = ModelProvider.ObjectInstance as XbimModel;

            if (String.Compare(model.IfcProject.Name, "Empty Project") == 0)
                //    CommandBinding_Open(sender, e); // Call open since there is no model loaded yet

                //if (model == null || String.Compare(model.IfcProject.Name, "Empty Project") == 0)
                return;                         // Still nothing loaded, return

            bimrlProcessModel bimrlPM = new bimrlProcessModel(model, false);    // false: for now no update
        }

        private void PushToBIMRLCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void UpdateBIMRLTransformCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            XbimModel model = ModelProvider.ObjectInstance as XbimModel;

            if (String.Compare(model.IfcProject.Name, "Empty Project") == 0)
                return;                         // Still nothing loaded, return

            string projNum = model.IfcProject.Name;
            string projName = model.IfcProject.LongName;
            BIMRLUtils.UpdateElementTransform(model, projNum, projName);    // false: for now no update
        }

        private void UpdateBIMRLTransformCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void BIMRLConfigurationCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            BIMRLConfigurations BimrlConfig = new BIMRLConfigurations();
            BimrlConfig.ShowDialog();
        }

        private void BIMRLConfigurationCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        public void BIMRLShowModelCmdExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ShowBIMRLModels showModels = new ShowBIMRLModels();
            showModels.ShowDialog();
        }

        private void BIMRLShowModelCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
    }
}
