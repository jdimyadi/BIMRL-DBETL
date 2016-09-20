using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Data;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using IErrorNode = Antlr4.Runtime.Tree.IErrorNode;
using ITerminalNode = Antlr4.Runtime.Tree.ITerminalNode;
using IToken = Antlr4.Runtime.IToken;
using ParserRuleContext = Antlr4.Runtime.ParserRuleContext;
using TokenStreamRewriter = Antlr4.Runtime.TokenStreamRewriter;
using BIMRL;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;

namespace BIMRLInterface
{
    public class ExprHandler
    {
        static string[] exceptionList = { "CAST", "MEMBEROF" };

        #region Validate
        public static void validateExpr(bimrlParser.ExprContext context)
        {
            // Validate Expr for adhering to certain limitations
            // 1. Check whether there is function inside a function: not supported
            if (context.GetChild(0) is bimrlParser.Ext_id_dot_notationContext)
            {
                bimrlParser.Ext_id_dot_notationContext extId = context.GetChild(0) as bimrlParser.Ext_id_dot_notationContext;
                if (extId.GetChild(0) is bimrlParser.FunctionContext)
                {
                    bimrlParser.FunctionContext fn = extId.GetChild(0) as bimrlParser.FunctionContext;
                    string hostFnName = fn.function_name().GetText(); 
                    IReadOnlyCollection<bimrlParser.ExprContext> fnExprs = fn.expr();
                    foreach (bimrlParser.ExprContext fnExpr in fnExprs)
                    {
                        string fnName;
                        if (functionInFunction(fnExpr, out fnName))
                        {
                            throw new BIMRLInterfaceRuntimeException("%Parsing Error: Function " + fnName + " inside function " + hostFnName + " is not allowed!");
                        }
                    }
                }
            }
        }

        static bool functionInFunction(bimrlParser.ExprContext context, out string fnName)
        {
            string retFnName = null;
            fnName = retFnName;

            if (context.expr() != null)
            {
                IReadOnlyCollection<bimrlParser.ExprContext> exprs = context.expr();
                foreach (bimrlParser.ExprContext expr in exprs)
                {
                    if (functionInFunction(expr, out retFnName))
                    {
                        bool matchesException = false;
                        foreach (string fn in exceptionList)
                        {
                            if (string.Compare(fnName, fn, true) == 0)
                                matchesException = true;
                        }
                        if (!matchesException)
                        {
                            fnName = retFnName;
                            return true;
                        }
                    }
                }
            }
            if (context.GetChild(0) is bimrlParser.Ext_id_dot_notationContext)
            {
                bimrlParser.Ext_id_dot_notationContext extId = context.GetChild(0) as bimrlParser.Ext_id_dot_notationContext;
                if (extId.function() != null)
                {
                    bimrlParser.FunctionContext fnCtx = extId.function();
                    fnName = fnCtx.function_name().GetText();
                    return true;
                }
            }

            return false;
        }

        public static void validateCastType(string theType, string castType)
        {
            string sqlStmt = "Select ElementType, ElementSubType from BIMRL_OBJECTHIERARCHY where ElementType='" + theType.ToUpper() + "' and ElementSubType='" + castType.ToUpper() + "'";
            DBQueryManager dbQ = new DBQueryManager();
            DataTable dt = dbQ.querySingleRow(sqlStmt);
            if (dt.Rows.Count == 0)
                throw new BIMRLInterfaceRuntimeException("%%Error - Invalid Cast. " + castType + " is not subtype of " + theType);
        }

        public static void validateBetween(bimrlParser.Between_conditionContext betweenCtx, nodeProperty expr1, nodeProperty expr2)
        {
            // For Between to work properly, we need to make sure that both expressions are valid
            if (betweenCtx.expr().Count < 2
                || string.IsNullOrEmpty(expr1.forExpr)
                || string.IsNullOrEmpty(expr2.forExpr))
                throw new BIMRLInterfaceRuntimeException("%Error: Missing expression in the BETWEEN clause");
        }

        #endregion

        #region ResolveExpression

        public static nodeProperty resolveExprValue(bimrlParser.ValueContext valueCtx)
        {
            nodeProperty ret = new nodeProperty();
            ret.forExpr = valueCtx.GetText();
            ret.exprSingleItem = true;
            return ret;
        }

        public static nodeProperty resolveExprExtIdDotNotation(bimrlParser.Ext_id_dot_notationContext extIdCtx, nodeProperty childNodeProp)
        {
            nodeProperty ret = new nodeProperty();
            if (childNodeProp != null)
                ret = childNodeProp;    // rollup the node Property
            ret.exprSingleItem = true;
            return ret;
        }

        public static nodeProperty resolveExprTerminalVarName(ITerminalNode term)
        {
            nodeProperty ret = new nodeProperty();
            string name = term.GetText();
            VarsInfo vInfo = new VarsInfo();
            if (string.Compare(name, 0, "?", 0, 1) == 0)
            {
                // resolve the variable
                name = name.Replace("?", "").Trim();

                bool regularVar = true;
                if (ExecListener.isActionStmt)
                {
                    // if It is inside the ACTION statement, we will check the variable name to be a correct/valid one and then replace it with OUTPUT field name
                    if (ExecListener.evalVariables.Contains(name.ToUpper()))
                    {
                        ret.forExpr = "OUTPUT";         // replace variable name for ACTION statement with OUTPUT column name
                        ret.genList.Add(name.ToUpper());          // Keep the variable name for use later
                        regularVar = false;
                    }
                }

                if (regularVar)
                {
                    // get the regular variable first, if it does not exist and it is EVALUATE statement, we check for the special eval variable
                    vInfo = DefinedVarsManager.getDefinedVar(name.ToUpper());
                    if (string.IsNullOrEmpty(vInfo.varName))
                    {
                        throw new BIMRLInterfaceRuntimeException("%Error: Variable ?" + name + " undefined.");
                    }

                    if (vInfo.bindVar.Count > 0)
                    {
                        // This is a deferred SQL query. It should not appear here but at varname_with_bind
                        throw new BIMRLInterfaceRuntimeException("%Error: Binding required for ?" + name + ". Use ?variable BIND (expr, ...).");
                    }
                    else
                    {
                        if (vInfo.varValue is String)
                            ret.forExpr = "'" + vInfo.varValue + "'";
                        else
                            ret.forExpr = vInfo.varValue.ToString();
                    }
                }
            }
            else
                ret.forExpr = name;     // if it is a bindvar, do nothing and return the bindvar as it is

            ret.exprSingleItem = true;
            return ret;
        }

        public static nodeProperty resolveExprVarnameWBind(string varName, IList<string> exprs)
        {
            nodeProperty ret = new nodeProperty();

            VarsInfo vInfo = DefinedVarsManager.getDefinedVar(varName);
            if (string.IsNullOrEmpty(vInfo.varName))
                throw new BIMRLInterfaceRuntimeException("%Error: Variable ?" + varName + " undefined.");

            // IReadOnlyCollection<bimrlParser.ExprContext> exprs = varCtx.expr();
            if (vInfo.bindVar.Count != exprs.Count)
                throw new BIMRLInterfaceRuntimeException("%Error: Mismatch number of bind variables for ?" + varName + " - found "
                                                        + exprs.Count.ToString() + " instead of required " + vInfo.bindVar.Count.ToString());

            string sqlStmt = vInfo.varValue.ToString();
            int ii = 0;
            //foreach (bimrlParser.ExprContext expr in exprs)
            // substitute all the bind variables with the expressions, e.g. "SELECT a from tab WHERE tab.c = :x", with ?var BIND (e.propertyvalue) -> :x replaced with e.propertyvalue
            foreach (string expr in exprs)
            {
                // string exprStr = expr.GetText();
                string bindVar = ":" + vInfo.bindVar[ii];
                // sqlStmt = sqlStmt.Replace(bindVar, exprStr);
                sqlStmt = sqlStmt.Replace(bindVar, expr);
                ii++;
            }

            ret.forExpr = "(" + sqlStmt + ")";

            //OracleCommand cmd = new OracleCommand(sqlStmt, DBOperation.DBConn);
            //OracleDataReader reader = cmd.ExecuteReader();
            //if (!reader.Read())  // we will read only the first record found
            //    throw new BIMRLInterfaceRuntimeException("%Error: Bind values do not return any value to the variable ?" + varName);

            //object value = reader.GetValue(0);
            //if (value is int)
            //    ret.forExpr = reader.GetInt32(0).ToString();
            //else if (value is double)
            //    ret.forExpr = reader.GetDouble(0).ToString();
            //else if (value is string)
            //    ret.forExpr = "'" + reader.GetString(0) + "'";
            //else
            //    ret.forExpr = "'" + value.ToString() + "'";         // Everything else convert it to String

            ret.exprSingleItem = false;
            return ret;
        }

        public static nodeProperty resolveExprUnaryOperator(string unaryOp, nodeProperty exprNProp)
        {
            nodeProperty ret = new nodeProperty();
            if (!string.IsNullOrEmpty(exprNProp.forExpr))
                ret.forExpr = unaryOp + " " + exprNProp.forExpr;

            BIMRLInterfaceCommon.appendToString(exprNProp.keywInj.tabProjInjection, ", ", ref ret.keywInj.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(exprNProp.keywInj.colProjInjection, ", ", ref ret.keywInj.colProjInjection);
            BIMRLInterfaceCommon.appendToString(exprNProp.keywInj.whereClInjection, " AND ", ref ret.keywInj.whereClInjection);
            BIMRLInterfaceCommon.appendToString(exprNProp.keywInj.suffixInjection, " AND ", ref ret.keywInj.suffixInjection);
            ret.exprSingleItem = false;
            if (exprNProp.genList != null)
                ret.genList.AddRange(exprNProp.genList);
            return ret;
        }

        public static nodeProperty resolveExprBracket(nodeProperty exprNProp)
        {
            nodeProperty ret = new nodeProperty();
            ret = exprNProp;
            if (!string.IsNullOrEmpty(ret.forExpr))
                ret.forExpr = "(" +  ret.forExpr + ")";
            if (exprNProp.genList != null)
                ret.genList.AddRange(exprNProp.genList);
            return ret;
        }

        public static nodeProperty resolveExprOpsExpr(bimrlParser.ExprContext expr1Ctx, nodeProperty expr1, bimrlParser.OpsContext ops, nodeProperty expr2, out string opRepl, out string notRepl)
        {
            opRepl = null;
            notRepl = null;
            nodeProperty ret = new nodeProperty();
            string opStr = " " + ops.GetChild(0).GetText() + " ";
            // Hack to separate the NOT that somehow is joint
            if (opStr.Equals(" NOTLIKE "))
                opStr = " NOT LIKE ";
            if (opStr.Equals(" NOTREGEXP_LIKE "))
                opStr = " NOT REGEXP_LIKE ";

            if (ops.GetChild(0) is bimrlParser.Logical_opsContext || ops.GetChild(0) is bimrlParser.Arithmetic_opsContext)
            {
                // for logical operator, we will just combine the expr values with the logical operator and add it to the where clause, seperating them with brackets
                BIMRLInterfaceCommon.appendToString(expr1.forExpr, "", ref ret.forExpr);
                BIMRLInterfaceCommon.appendToString(expr2.forExpr, opStr, ref ret.forExpr);
                //ret.forExpr = expr1.forExpr + " " + opStr + " " + expr2.forExpr; 

            }
            else if (ops.GetChild(0) is bimrlParser.Comparison_opsContext)
            {
                bimrlParser.Comparison_opsContext compCtx = ops.GetChild(0) as bimrlParser.Comparison_opsContext; 
                // If the expr is a function and the value of expr2 is boolean type (.T./.F.), return a replacement chars for the operator
                if (expr1Ctx.GetChild(0) is bimrlParser.Ext_id_dot_notationContext)
                {
                    if (expr1Ctx.GetChild(0).GetChild(0) is bimrlParser.FunctionContext)
                    {
                        if (string.Compare(expr2.forExpr, 0, ".TRUE.", 0, 2, true) == 0)
                        {
                            opRepl = " = ";
                            notRepl = "";
                        }
                        else if (string.Compare(expr2.forExpr, 0, ".FALSE.", 0, 2, true) == 0)
                        {
                            opRepl = " != ";
                            notRepl = " NOT ";
                        }
                        else if (compCtx.LIKE() != null)
                        {
                            opRepl = " = ";         // always "=" for use in the WHERE condition in this case
                            ret.forExpr = expr1.forExpr + opStr + expr2.forExpr;
                        }
                        else if (compCtx.REGEXP_LIKE() != null)
                        {
                            string regex = "REGEXP_LIKE";

                            if (compCtx.NOT() != null)
                                regex = "NOT REGEXP_LIKE";

                            ret.forExpr = regex + " (" + expr1.forExpr + ", " + expr2.forExpr + ")";
                        }
                        else
                        {

                            opRepl = opStr;
                            //notRepl = opStr;   // Not sure about this!

                            BIMRLInterfaceCommon.appendToString(expr1.forExpr, "", ref ret.forExpr);
                            BIMRLInterfaceCommon.appendToString(expr2.forExpr, opStr, ref ret.forExpr);
                        }

                        if (!string.IsNullOrEmpty(expr1.keywInj.whereClInjection))
                        {
                            expr1.keywInj.whereClInjection = expr1.keywInj.whereClInjection.Replace(BIMRLConstant.tempOp, opRepl);
                            expr1.keywInj.whereClInjection = expr1.keywInj.whereClInjection.Replace(BIMRLConstant.notOp, notRepl);
                        }
                        if (!string.IsNullOrEmpty(expr1.keywInj.suffixInjection))
                        {
                            expr1.keywInj.suffixInjection = expr1.keywInj.suffixInjection.Replace(BIMRLConstant.tempOp, opRepl);
                            expr1.keywInj.suffixInjection = expr1.keywInj.suffixInjection.Replace(BIMRLConstant.notOp, notRepl);
                        }
                    }
                    else
                    {
                        BIMRLInterfaceCommon.appendToString(expr1.forExpr, "", ref ret.forExpr);
                        BIMRLInterfaceCommon.appendToString(expr2.forExpr, opStr, ref ret.forExpr);
                        //ret.forExpr = expr1.forExpr + " " + opStr + " " + expr2.forExpr; 
                    }
                }
                else
                {
                    BIMRLInterfaceCommon.appendToString(expr1.forExpr, "", ref ret.forExpr);
                    BIMRLInterfaceCommon.appendToString(expr2.forExpr, opStr, ref ret.forExpr);
                    //ret.forExpr = expr1.forExpr + " " + opStr + " " + expr2.forExpr; 
                }

                // We check again here in case the comparison "escaped" earlier check, to make sure all template operations is appropriately changed for boolean value

                bool boolVal = false;
                if (string.Compare(expr2.forExpr, 0, ".TRUE.", 0, 2, true) == 0)
                {
                    boolVal = true;
                    opRepl = " = ";
                    notRepl = "";
                }
                else if (string.Compare(expr2.forExpr, 0, ".FALSE.", 0, 2, true) == 0)
                {
                    boolVal = true;
                    opRepl = " != ";
                    notRepl = " NOT ";
                }

                if (boolVal)
                {
                    if (!string.IsNullOrEmpty(expr1.keywInj.whereClInjection))
                    {
                        expr1.keywInj.whereClInjection = expr1.keywInj.whereClInjection.Replace(BIMRLConstant.tempOp, opRepl);
                        expr1.keywInj.whereClInjection = expr1.keywInj.whereClInjection.Replace(BIMRLConstant.notOp, notRepl);
                    }
                    if (!string.IsNullOrEmpty(expr1.keywInj.suffixInjection))
                    {
                        expr1.keywInj.suffixInjection = expr1.keywInj.suffixInjection.Replace(BIMRLConstant.tempOp, opRepl);
                        expr1.keywInj.suffixInjection = expr1.keywInj.suffixInjection.Replace(BIMRLConstant.notOp, notRepl);
                    }
                }
            }

            BIMRLInterfaceCommon.appendToString(expr1.keywInj.whereClInjection, " AND ", ref ret.keywInj.whereClInjection);
            BIMRLInterfaceCommon.appendToString(expr2.keywInj.whereClInjection, " AND ", ref ret.keywInj.whereClInjection);

            BIMRLInterfaceCommon.appendToString(expr1.keywInj.suffixInjection, " ", ref ret.keywInj.suffixInjection);
            BIMRLInterfaceCommon.appendToString(expr2.keywInj.suffixInjection, " ", ref ret.keywInj.suffixInjection);
                
            BIMRLInterfaceCommon.appendToString(expr1.keywInj.tabProjInjection, ", ", ref ret.keywInj.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(expr1.keywInj.colProjInjection, ", ", ref ret.keywInj.colProjInjection);

            BIMRLInterfaceCommon.appendToString(expr2.keywInj.tabProjInjection, ", ", ref ret.keywInj.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(expr2.keywInj.colProjInjection, ", ", ref ret.keywInj.colProjInjection);

            ret.exprSingleItem = false;
            if (expr1.genList != null)
                ret.genList.AddRange(expr1.genList);
            if (expr2.genList != null)
                ret.genList.AddRange(expr2.genList);
            return ret;
        }

        public static nodeProperty resolveConditionalExpr(nodeProperty expr, nodeProperty condExpr)
        {
            nodeProperty ret = new nodeProperty();
            ret.forExpr = expr.forExpr + " " + condExpr.forExpr;
            ret.keywInj = expr.keywInj;
            BIMRLInterfaceCommon.appendToString(condExpr.keywInj.colProjInjection, ", ", ref ret.keywInj.colProjInjection);
            BIMRLInterfaceCommon.appendToString(condExpr.keywInj.tabProjInjection, ", ", ref ret.keywInj.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(condExpr.keywInj.whereClInjection, " AND ", ref ret.keywInj.whereClInjection);
            BIMRLInterfaceCommon.appendToString(condExpr.keywInj.suffixInjection, " AND ", ref ret.keywInj.suffixInjection);
            ret.exprSingleItem = false;
            if (expr.genList != null)
                ret.genList.AddRange(expr.genList);
            return ret;
        }

        public static nodeProperty resolveExists(nodeProperty existsNProp)
        {
            nodeProperty ret = new nodeProperty();
            ret = existsNProp;
            ret.exprSingleItem = false;
            return ret;
        }

        #endregion
    }
}
