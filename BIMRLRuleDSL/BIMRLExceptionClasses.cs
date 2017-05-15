//
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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BIMRLInterface
{
    public class BIMRLInconsistentParsingException : ApplicationException
    {
        public BIMRLInconsistentParsingException()
        {
        }

        public BIMRLInconsistentParsingException(string message)
            : base(message)
        { 
        }

        public BIMRLInconsistentParsingException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }

    public class BIMRLInterfaceRuntimeException : ApplicationException
    {
        public BIMRLInterfaceRuntimeException() : base() { }
        public BIMRLInterfaceRuntimeException(string msg) : base(msg) { }
    }

}
