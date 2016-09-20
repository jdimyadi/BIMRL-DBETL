using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL;
using BIMRLInterface;
using BIMRL.OctreeLib;

namespace BIMRLInterface.ExtensionFunction
{
    /// <summary>
    /// It is really a dummy function, serves like a stub. It does nothing except what has been provided by the default implementation in the ExtensionFunctionBase
    /// It basically will return the result DataTable identical with the input DataTable of the EVALUATE statement that may still involve Join and User Geometry
    /// </summary>
    public class Nothing : ExtensionFunctionBase, IBIMRLExtensionFunction
    {
        public Nothing()
        {

        }

        public override void InvokeRule(DataTable inputDT, params string[] inputParams)
        {
            base.InvokeRule(inputDT, inputParams);

            // Not Ideal, but for now, this column has to be added in each extension function since we cannot tell what data type it should be depending on each function
            DataColumn column;

            column = new DataColumn();
            column.DataType = System.Type.GetType("System.String");
            column.ColumnName = "OUTPUT";
            column.ReadOnly = false;
            column.Unique = false;
            try
            {
                inputDT.Columns.Add(column);    // Add column result
            }
            catch (System.Data.DuplicateNameException)
            {
                // ignore error for duplicate column and continue
            }
        }
    }
}
