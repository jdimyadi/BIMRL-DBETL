using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BIMRL
{
   public class BIMRLDiffOptions
   {
      public bool CheckNewAndDeletedObjects { get; set; } = true;
      public bool CheckGeometriesDiffBySignature { get; set; } = true;
      public bool CheckTypeAndTypeAssignments { get; set; } = true;
      public bool CheckContainmentRelationships { get; set; } = true;
      public bool CheckOwnerHistory { get; set; } = true;
      public bool CheckProperties { get; set; } = true;
      public bool CheckMaterials { get; set; } = true;
      public bool CheckClassificationAssignments { get; set; } = true;
      public bool CheckGroupMemberships { get; set; } = true;
      public bool CheckAggregations { get; set; } = true;
      public bool CheckConnections { get; set; } = true;
      public bool CheckElementDependencies { get; set; } = true;
      public bool CheckSpaceBoundaries { get; set; } = true;
      public double GeometryCompareTolerance { get; set; } = 0.0001;

      public static BIMRLDiffOptions SelectAllOptions()
      {
         BIMRLDiffOptions options = new BIMRLDiffOptions();
         return options;
      }

      public void WriteToJson(string outputFileName)
      {
         string json = JsonConvert.SerializeObject(this, Formatting.Indented);
         using (StreamWriter file = File.CreateText(outputFileName))
         {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(file, this);
         }
      }
   }
}
