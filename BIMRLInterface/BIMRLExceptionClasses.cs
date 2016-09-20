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
