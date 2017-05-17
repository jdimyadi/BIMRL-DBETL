using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRL.Common
{
   public class FederatedModelInfo
   {
         public int FederatedID { get; set; }
         public string ModelName { get; set; }
         public string ProjectNumber { get; set; }
         public string ProjectName { get; set; }
         public string WorldBoundingBox { get; set; }
         public int OctreeMaxDepth { get; set; }
         public DateTime? LastUpdateDate { get; set; }
         public string Owner { get; set; }
         public string DBConnection { get; set; }
   }
}
