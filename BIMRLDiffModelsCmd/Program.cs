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
using System.IO;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using BIMRL;
using BIMRL.Common;
using Xbim.Ifc;
using Newtonsoft.Json;

namespace BIMRLDiffModelsCmd
{
   class Program
   {
      static void Main(string[] args)
      {
         if (args.Count() == 0 || args[0].Equals("-h"))
         {
            Console.WriteLine("Usage:");
            Console.WriteLine(" - The option below will load both model to BIMRL DB and compare them afterward:");
            Console.WriteLine("      BIMRLDiffModelCmd <DB connect string> -o <report json file> <the IFC file (New)> <the IFC file (Reference)> [<option file>]");
            Console.WriteLine(" - The option below will load the new model to BIMRL DB and compare it with the existing reference:");
            Console.WriteLine("      BIMRLDiffModelCmd <DB connect string> -o <report json file> -r <reference BIMRL model ID> <the IFC file (New)> [<option file>]");
            Console.WriteLine("  Supported file types: *.ifc|*.ifcxml|*.ifczip|*.xbimf");
            return;
         }

         if (!args[1].Equals("-o") && args.Length < 5)
         {
            Console.WriteLine("Usage: BIMRLDiffModelCmd <DB connect string> -o <report json file> <the IFC file(New)> <the IFC file(Reference)>");
            Console.WriteLine("   or: BIMRLDiffModelCmd <DB connect string> -o <report json file> -r <reference BIMRL model ID> <the IFC file (New)>");
            return;
         }

         string dbConnectStr = args[0];
         string[] conn = dbConnectStr.Split(new char[] { '/', '@' });
         if (conn.Count() < 3)
         {
            Console.WriteLine("%Error: Connection string is not in the right format. Use: <username>/<password>@<db>. For example: bimrl/bimrlpwd@pdborcl");
            return;
         }
         try
         {
            DBOperation.ConnectToDB(conn[0], conn[1], conn[2]);
         }
         catch
         {
            Console.WriteLine("%Error: Connection to DB Error");
            return;
         }

         string newModelFile = "";
         string outputFileName = args[2];
         string outputFileFullPath = Path.GetFullPath(outputFileName);
         int refModelID = -1;
         int newModelID = -1;
         BIMRLCommon bimrlCommon = new BIMRLCommon();
         DBOperation.UIMode = false;
         string optionFile = "";

         if (args[3].Equals("-r"))
         {
            if (!int.TryParse(args[4], out refModelID))
            {
               Console.WriteLine("%Error: Referenced Model ID must be an integer number: " + args[4]);
               return;
            }
            if (args.Count() < 6)
            {
               Console.WriteLine("%Error: Missing IFC file name (New)!");
               return;
            }

            // Check ID is a valid model ID in the DB
            BIMRLQueryModel bQM = new BIMRLQueryModel(bimrlCommon);
            DataTable modelInfo = bQM.checkModelExists(refModelID);
            if (modelInfo.Rows.Count == 0)
            {
               Console.WriteLine("%Error: Referenced Model ID " + refModelID.ToString() + " does not exist in the DB, load it first!");
               return;
            }

            newModelFile = args[5];
            if (!File.Exists(newModelFile))
            {
               Console.WriteLine("%Error: New Model file is not found!");
               return;
            }

            if (args.Count() >= 7)
               optionFile = args[6];
         }
         else
         {
            string refModelFile = args[4];
            if (!File.Exists(refModelFile))
            {
               Console.WriteLine("%Error: Referenced Model file is not found!");
               return;
            }
            newModelFile = args[3];
            if (!File.Exists(newModelFile))
            {
               Console.WriteLine("%Error: New Model file is not found!");
               return;
            }
            
            // Load the referenced Model
            IfcStore refModel = LoadModel.OpenModel(refModelFile);
            if (refModel != null)
               refModelID = LoadModel.LoadModelToBIMRL(refModel);

            if (refModel == null || refModelID == -1)
            {
               Console.WriteLine("%Error: Load referenced Model " + refModelFile + " failed!");
               return;
            }

            if (args.Count() >= 6)
               optionFile = args[5];
         }

         // Load the new model
         IfcStore newModel = LoadModel.OpenModel(newModelFile);
         if (newModel != null)
            newModelID = LoadModel.LoadModelToBIMRL(newModel);

         if (newModel == null || newModelID == -1)
         {
            Console.WriteLine("%Error: Load referenced Model " + newModelFile + " failed!");
            return;
         }

         // Compare
         BIMRLDiffOptions options = new BIMRLDiffOptions();
         if (File.Exists(optionFile))
         {
            options = JsonConvert.DeserializeObject<BIMRLDiffOptions>(File.ReadAllText(optionFile));
            if (options == null)
               options = BIMRLDiffOptions.SelectAllOptions();
         }
         else
            options = BIMRLDiffOptions.SelectAllOptions();

         // For the purpose of Model Diff, no enhanced BIMRL data processing is needed. This saves time and space.
         //   If the model requires the enhanced data, it can be done either load the model beforehand, or run the enhancement using BIMRL_ETLMain.XplorerPlugin UI
         DBOperation.OnepushETL = false;

         BIMRLDiffModels diffModels = new BIMRLDiffModels(newModelID, refModelID, bimrlCommon);
         diffModels.RunDiff(outputFileFullPath, options: options);
      }
   }
}
