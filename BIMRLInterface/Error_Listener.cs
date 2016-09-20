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
