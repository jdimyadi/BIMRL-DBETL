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
