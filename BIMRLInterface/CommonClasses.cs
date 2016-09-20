using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRLInterface
{
    public static class BIMRLInterfaceCommon
    {
        public static void appendToString(string stringToAppend, string joiningKeyword, ref string originalString)
        {
            if (string.IsNullOrEmpty(stringToAppend))
                return;

            if (!string.IsNullOrEmpty(originalString))
            {
                if (originalString.Length > 0)
                    originalString += joiningKeyword;
            }
            originalString += stringToAppend;
        }

        public static void appendToStringInFront(string stringToAppend, string joiningKeyword, ref string originalString)
        {
            if (string.IsNullOrEmpty(stringToAppend))
                return;

            if (!string.IsNullOrEmpty(originalString))
            {
                if (originalString.Length > 0)
                    originalString = joiningKeyword + originalString;
            }
            originalString = stringToAppend + originalString;
        }


        public static string changeUpper(string inputStr)
        {
            string tmpStr = null;
            // Check if the string starts with a doublequote, we should leave the string as it is, otherwise convert it to uppercase
            if (!string.IsNullOrEmpty(inputStr))
            {
                if (!inputStr.TrimStart().StartsWith("\""))
                    tmpStr = inputStr.ToUpper();
                else
                    tmpStr = inputStr;
            }
            return tmpStr;
        }
    }

    public static class BIMRLConstant
    {
        static string m_tempOp = " <t_Op> ";
        static string m_notOp = " <nOt> ";

        public static string tempOp
        { get { return m_tempOp; } }

        public static string notOp
        { get { return m_notOp; } }
    }

    //public struct Ext_ID
    //{
    //    public string name;
    //    public BIMRLEnum.IDMemberType type;
    //}
    public struct FunctionDesc
    {
        public string name;
        public IList<string> exprs;
        public string where;
    }
    public struct CurrObjectDesc
    {
        public bool increment;   // default: increment
        public IList<string> exprs;
    }
    public struct CurrValueDesc
    {
        public int incr;
        public string varname;
    }
    public class ColumnSpec
    {
        public ColumnSpec()
        {
            //item1 = new Ext_ID { name = null, type = BIMRLEnum.IDMemberType.Undefined };
            //item2 = new Ext_ID { name = null, type = BIMRLEnum.IDMemberType.Undefined };
            item1 = null;
            item2 = null;
            alias = null;
        }

        public string item1;
        public string item2;
        public string alias;
        //public FunctionDesc? functionDesc;
        //public CurrObjectDesc? currObjectDesc;
        //public CurrValueDesc? currValueDesc;
    }

    public class TableSpec
    {
        public string tableName;
        public string alias;
        public string originalName;

        public TableSpec()
        {
            tableName = null;
            alias = null;
            originalName = null;
        }
    }

    /// <summary>
    /// Return value from processign a function. Depending on the type of the function, different fields may be filled in:
    /// scope and name will be returned for both EXTENSION and EXTERNAL
    /// keywordInjection will be returned for BUILTIN
    /// inlineString will return the string to be included in the SQL statement. This is for INLINE function or undefined
    /// </summary>
    public class functionRet
    {
        public BIMRLEnum.FunctionType type;
        // 3 attributes below are for use with the extension and external functions
        public int scope;
        public int subScope;
        public string functionName;
        // 2 attributes below are for use with the buildin functions. forExpr is also used for inline function string.
        // forExpr is basically the main expression string that will capture the expression tree. Keyword injection will complement/add to it
        public string forExpr;
        public keywordInjection keywordInjection;

        public functionRet()
        {
            type = BIMRLEnum.FunctionType.UNDEFINED;
            scope = -1;
            subScope = -1;
            functionName = null;
            forExpr = null;
            keywordInjection = new keywordInjection { tabProjInjection = "", colProjInjection = "", whereClInjection = ""};
        }
    }

    public class nodeProperty
    {
        public string forExpr;
        public bool exprSingleItem;
        public BIMRLEnum.FunctionType fnType;
        public List<string> fnExprList;
        public string fnName;
        public keywordInjection keywInj;
        public List<string> genList;
        public ActionInfo actionCommandInfo;
        public object originalValue { get; set; }

        public nodeProperty()
        {
            forExpr = "";
            exprSingleItem = false;
            fnType = BIMRLEnum.FunctionType.UNDEFINED;
            keywInj = new keywordInjection();
            genList = new List<string>();
            fnExprList = new List<string>();
            actionCommandInfo = null;
            originalValue = null;
        }
    }

    public class keywordInjection
    {
        public string colProjInjection; // Column list injection into the SQL statement
        public string tabProjInjection; // Table list injection into the SQL statement
        public string whereClInjection; // Where clause injection into the SQL statement
        public string suffixInjection;  // subquery that must be appended behind (e.g. group by)
   
        public keywordInjection()
        {
            colProjInjection = "";
            tabProjInjection = "";
            whereClInjection = "";
            suffixInjection = "";
        }

        public void Clear()
        {
            colProjInjection = "";
            tabProjInjection = "";
            whereClInjection = "";
            suffixInjection = "";
        }
    }

}
