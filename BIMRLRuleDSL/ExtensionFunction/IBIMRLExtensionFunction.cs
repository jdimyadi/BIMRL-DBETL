﻿//
// BIMRL (BIM Rule Language) library: this library performs DSL for rule checking using BIM Rule Language that works on BIMRL Simplified Schema on RDBMS. 
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