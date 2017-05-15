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
// This file incorporates work covered by the following copyright and permission notice:
//   Xbim License (Common Development and Distribution License (CDDL)
// the license details can be found at: https://github.com/xBimTeam/XbimWindowsUI/blob/master/LICENCE.md
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Xbim.Ifc;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.IO.Esent;
using Xbim.ModelGeometry.Scene;
using Xbim.Presentation.FederatedModel;
using Newtonsoft.Json;
using BIMRL;
using BIMRL.Common;


namespace BIMRLDiffModelsCmd
{
   class LoadModel
   {
      static public int LoadModelToBIMRL(IfcStore model)
      {
         bimrlProcessModel bimrlPM = new bimrlProcessModel(model, false);
         if (bimrlPM != null)
            return bimrlProcessModel.currFedID;
         return -1;
      }

      static public IfcStore OpenModel(string fileName)
      {
         IfcStore model = null;
         try
         {
            string extName = Path.GetExtension(fileName);
            if (extName.Equals(".xbimf"))
            {
               model = LoadFederatedModelDef(fileName);
            }
            else
            {
               model = IfcStore.Open(fileName, null, null, null, XbimDBAccess.Read);
               if (model.GeometryStore.IsEmpty)
               {
                  var context = new Xbim3DModelContext(model);
                  //upgrade to new geometry representation, uses the default 3D model
                  //context.CreateContext(progDelegate: _worker.ReportProgress);
                  context.CreateContext();
               }
               foreach (var modelReference in model.ReferencedModels)
               {
                  // creates federation geometry contexts if needed
                  if (modelReference.Model == null)
                     continue;
                  if (!modelReference.Model.GeometryStore.IsEmpty)
                     continue;
                  var context = new Xbim3DModelContext(modelReference.Model);
                  //upgrade to new geometry representation, uses the default 3D model
                  context.CreateContext();
               }
            }
            return model;
         }
         catch (Exception ex)
         {
            var sb = new StringBuilder();
            sb.AppendLine($"Error opening '{fileName}' {ex.StackTrace}.");

            throw new Exception(sb.ToString(), ex);
         }
      }

      static private IfcStore LoadFederatedModelDef(string fileName)
      {
         XBimFederation xFedModel = JsonConvert.DeserializeObject<XBimFederation>(File.ReadAllText(fileName));
         xFedModel.fedFilePath = fileName;
         IfcStore fedModel = ProcessModels(xFedModel);
         return fedModel;
      }

      static private IfcStore ProcessModels(XBimFederation xFedModel)
      {
         IfcStore fedModel = IfcStore.Create(null, IfcSchemaVersion.Ifc4, XbimStoreType.InMemoryModel);
         using (var txn = fedModel.BeginTransaction())
         {
            var project = fedModel.Instances.New<Xbim.Ifc4.Kernel.IfcProject>();
            //project.Name = "Default Project Name";
            project.Name = xFedModel.FederationName;
            project.LongName = xFedModel.FederationName + " - Federation";
            project.Description = xFedModel.FederationDescription;
            project.Initialize(ProjectUnits.SIUnitsUK);
            txn.Commit();
         }

         var informUser = true;
         for (var i = 0; i < xFedModel.GetMemberModelList().Count; i++)
         {
            var fileName = xFedModel.GetMemberModelList()[i].ModelFileName;
            if (!addReferencedModel(fileName, fedModel, i, informUser, out informUser))
               break;      // The process is being cancelled by user
         }
         return fedModel;
      }

      static bool addReferencedModel(string fileName, IfcStore fedModel, int counter, bool informUser, out bool userRespOnInfo)
      {
         userRespOnInfo = true;
         var temporaryReference = new XbimReferencedModelViewModel
         {
            Name = fileName,
            OrganisationName = "OrganisationName " + counter,
            OrganisationRole = "Undefined"
         };

         var buildRes = false;
         Exception exception = null;
         try
         {
            buildRes = temporaryReference.TryBuildAndAddTo(fedModel);
         }
         catch (Exception ex)
         {
            //usually an EsentDatabaseSharingViolationException, user needs to close db first
            exception = ex;
            Console.WriteLine("%Error. Unable to open the .xbim cache file (" + fileName + "). Check source file exists or the cache file is being used or corrupted!");
            return false;
         }
         return true;
      }

   }
}
