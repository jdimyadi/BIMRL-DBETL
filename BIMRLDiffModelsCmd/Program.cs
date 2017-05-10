using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data;
using System.Text;
using System.Threading.Tasks;
using BIMRL;
using BIMRL.OctreeLib;
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
            Console.WriteLine("      BIMRLDiffModelCmd -o <report json file> <the IFC file (New)> <the IFC file (Reference)> [<option file>]");
            Console.WriteLine(" - The option below will load the new model to BIMRL DB and compare it with the existing reference:");
            Console.WriteLine("      BIMRLDiffModelCmd -o <report json file> -r <reference BIMRL model ID> <the IFC file (New)> [<option file>]");
            Console.WriteLine("  Supported file types: *.ifc|*.ifcxml|*.ifczip|*.xbimf");
            return;
         }

         if (!args[0].Equals("-o") && args.Length < 4)
         {
            Console.WriteLine("Usage: BIMRLDiffModelCmd - o <report json file> <the IFC file(New)> <the IFC file(Reference)>");
            Console.WriteLine("   or: BIMRLDiffModelCmd -o <report json file> -r <reference BIMRL model ID> <the IFC file (New)>");
            return;
         }

         string newModelFile = "";
         string outputFileName = args[1];
         string outputFileFullPath = Path.GetFullPath(outputFileName);
         int refModelID = -1;
         int newModelID = -1;
         BIMRLCommon bimrlCommon = new BIMRLCommon();
         DBOperation.UIMode = false;
         string optionFile = "";

         if (args[2].Equals("-r"))
         {
            if (!int.TryParse(args[3], out refModelID))
            {
               Console.WriteLine("%Error: Referenced Model ID must be an integer number: " + args[3]);
               return;
            }
            if (args.Count() < 5)
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

            newModelFile = args[4];
            if (!File.Exists(newModelFile))
            {
               Console.WriteLine("%Error: New Model file is not found!");
               return;
            }

            if (args.Count() >= 6)
               optionFile = args[5];
         }
         else
         {
            string refModelFile = args[3];
            if (!File.Exists(refModelFile))
            {
               Console.WriteLine("%Error: Referenced Model file is not found!");
               return;
            }
            newModelFile = args[2];
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

            if (args.Count() >= 5)
               optionFile = args[4];
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
