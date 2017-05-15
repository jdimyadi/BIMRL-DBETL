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
using System.Threading.Tasks;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;


namespace BIMRLInterface
{
    public class Error_Listener : BaseErrorListener
    {
        public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            string stackList = null;
            IList<string> stack = ((Parser)recognizer).GetRuleInvocationStack();
            stack.Reverse();
            for (int i=0; i<stack.Count(); i++)
            {
                if (i == 0) stackList = "[";
                stackList = stackList + " " + stack[i];
            }
            stackList = stackList + "]";
            Logger.writeLog("\t\t-> rule stack: " + stackList + "\n");
            Logger.writeLog("\t\t-> line " + line + ":" + charPositionInLine + " at " + offendingSymbol + ": " + msg + "\n");
        }
    }
}
