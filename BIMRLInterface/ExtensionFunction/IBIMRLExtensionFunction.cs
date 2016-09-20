using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRLInterface.ExtensionFunction
{
    interface IBIMRLExtensionFunction
    {
        /* Pair of preceedingQuery and Process with fixed inputParams are deprecated. The new BIMRL syntax should allow variable params without preceeding query */
        keywordInjection preceedingQuery(string inputParams);
        // All extension function must provide Process function accepting DataTable as an input data from the DB query and additional parameters, Each function must decide what input parameters it expect and validate them
        void Process(DataTable inputData, string inputParams);

        // The new "Process" method called "InvokeRule"
        void InvokeRule(DataTable inputData, params string[] inputParams);

        DataTable GetTableResult();
        double? GetDoubleResult(string columnName);
        int? GetIntegerResult(string columnName);
        string GetStringResult(string columnName);
        bool? GetBooleanResult(string columnName);
    }
}
