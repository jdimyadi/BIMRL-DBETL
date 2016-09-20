using System;
using System.Data;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BIMRLInterface.ExtensionFunction;

namespace BIMRLInterface
{
    public class FunctionManager
    {
        static Dictionary<string, Tuple<IList<string>, BIMRLEnum.deferredExecutionMode>> processedFunction = new Dictionary<string, Tuple<IList<string>, BIMRLEnum.deferredExecutionMode>>();
        static Dictionary<string, string> processedBuiltinFunction = new Dictionary<string, string>();
        static List<string> loadedAssembly = new List<string>();

        public FunctionManager()
        {
            processedFunction.Clear();
            processedBuiltinFunction.Clear();
        }

        public static void Init()
        {
            processedFunction.Clear();
            processedBuiltinFunction.Clear();
        }

        public static bool checkBuiltinFnReg (string functionSpec, out string forExpr)
        {
            forExpr = null;
            bool ret = processedBuiltinFunction.TryGetValue(functionSpec.ToUpper(), out forExpr);
            return ret;
        }

        public static void regBuiltinFn (string functionSpec, string forExpr)
        {
            // we will register the uppercase verion of the fn spec that is good enough just for the purpose of making sure we know that the same fn has been defined and processed before
            if (!processedBuiltinFunction.ContainsKey(functionSpec.ToUpper()))
                processedBuiltinFunction.Add(functionSpec.ToUpper(), forExpr);
        }

        public static Dictionary<string,Tuple<IList<string>, BIMRLEnum.deferredExecutionMode>> DeferredExecutionFn
        {
            get { return processedFunction; }
        }

        public static bool hasDeferredFunction()
        {
            return (processedFunction.Count > 0 ? true : false);
        }

        public static functionRet resolveFunction (string functionName, IList<string> exprStrList, bool byTable = false)
        {
            functionRet fnRet = new functionRet();
            nodeProperty nodePRet = new nodeProperty();

            BIMRLEnum.BuiltinFunction res = BIMRLEnum.BuiltinFunction.UNDEFINED;
            if (Enum.TryParse(functionName.ToUpper(), out res))
            {
                fnRet.type = BIMRLEnum.FunctionType.BUILTIN;
                switch (res)
                {
                    case BIMRLEnum.BuiltinFunction.AGGREGATEOF:
                        nodePRet = BIMRLKeywordMapping.functionAGGREGATEOF(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.AGGREGATEMASTER:
                        nodePRet = BIMRLKeywordMapping.functionAGGREGATEMASTER(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.BOUNDEDSPACE:
                        nodePRet = BIMRLKeywordMapping.functionBOUNDEDSPACE(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.CLASSIFICATIONOF:
                        nodePRet = BIMRLKeywordMapping.functionCLASSIFICATIONOF(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.CONNECTEDTO:
                        nodePRet = BIMRLKeywordMapping.functionCONNECTEDTO(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.CONTAINER:
                        nodePRet = BIMRLKeywordMapping.functionCONTAINER(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.CONTAINS:
                        nodePRet = BIMRLKeywordMapping.functionCONTAINS(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.DEPENDENCY:
                        nodePRet = BIMRLKeywordMapping.functionDEPENDENCY(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.DEPENDENTTO:
                        nodePRet = BIMRLKeywordMapping.functionDEPENDENTTO(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.ELEMENTTYPEOF:
                        nodePRet = BIMRLKeywordMapping.functionELEMENTTYPEOF(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.GROUPOF:
                        nodePRet = BIMRLKeywordMapping.functionGROUPOF(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.HASPROPERTY:
                        nodePRet = BIMRLKeywordMapping.functionHASPROPERTY(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.HASCLASSIFICATION:
                        nodePRet = BIMRLKeywordMapping.functionHASCLASSIFICATION(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.MATERIALOF:
                        nodePRet = BIMRLKeywordMapping.functionMATERIALOF(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.MODELINFO:
                        nodePRet = BIMRLKeywordMapping.functionMODELINFO(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.OWNERHISTORY:
                        nodePRet = BIMRLKeywordMapping.functionOWNERHISTORY(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.PROPERTY:
                        nodePRet = BIMRLKeywordMapping.functionPROPERTY(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.PROPERTYOF:
                        nodePRet = BIMRLKeywordMapping.functionPROPERTYOF(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.SPACEBOUNDARY:
                        nodePRet = BIMRLKeywordMapping.functionSPACEBOUNDARY(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.SYSTEMOF:
                        nodePRet = BIMRLKeywordMapping.functionSYSTEMOF(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.TYPEOF:
                        nodePRet = BIMRLKeywordMapping.functionTYPEOF(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.UNIQUEVALUE:
                        nodePRet = BIMRLKeywordMapping.functionUNIQUEVALUE(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.ZONEOF:
                        nodePRet = BIMRLKeywordMapping.functionZONEOF(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.BOUNDARYINFO:
                        nodePRet = BIMRLKeywordMapping.functionBOUNDARYINFO(exprStrList, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.DOORLEAF:
                        nodePRet = BIMRLKeywordMapping.functionDOORLEAF(exprStrList, BIMRLEnum.functionQualifier.UNDERSIDE, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.TOP:
                        nodePRet = BIMRLKeywordMapping.faceOrientation(exprStrList, BIMRLEnum.functionQualifier.TOP, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.BOTTOM:
                        nodePRet = BIMRLKeywordMapping.faceOrientation(exprStrList, BIMRLEnum.functionQualifier.BOTTOM, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.SIDE:
                        nodePRet = BIMRLKeywordMapping.faceOrientation(exprStrList, BIMRLEnum.functionQualifier.SIDE, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.TOPSIDE:
                        nodePRet = BIMRLKeywordMapping.faceOrientation(exprStrList, BIMRLEnum.functionQualifier.TOPSIDE, byTable);
                        break;
                    case BIMRLEnum.BuiltinFunction.UNDERSIDE:
                        nodePRet = BIMRLKeywordMapping.faceOrientation(exprStrList, BIMRLEnum.functionQualifier.UNDERSIDE, byTable);
                        break;
                    default:
                        fnRet.type = BIMRLEnum.FunctionType.UNDEFINED;
                        break;
                }
                if (fnRet.type != BIMRLEnum.FunctionType.UNDEFINED)
                {
                    fnRet.keywordInjection = nodePRet.keywInj;
                    fnRet.forExpr = nodePRet.forExpr;
                }
            }
            else if (ExtensionFunctionRegister.checkExtensionFunction(functionName))
            {
                fnRet.type = BIMRLEnum.FunctionType.EXTENSION;
                fnRet.functionName = functionName.ToUpper();
                string regKey = fnRet.functionName;

                if (!processedFunction.ContainsKey(regKey))
                    processedFunction.Add(regKey, new Tuple<IList<string>,BIMRLEnum.deferredExecutionMode>(exprStrList, BIMRLEnum.deferredExecutionMode.PARALLEL));    // For now we will handle PARALLEL mode only

                //fnRet.keywordInjection = getQueryFilter(functionName, exprStrList);
            }
            else if (isExternalFunction(functionName))
            {
                fnRet.type = BIMRLEnum.FunctionType.EXTERNAL;
                fnRet.functionName = functionName.ToUpper();
                string regKey = fnRet.functionName;

                if (!processedFunction.ContainsKey(regKey))
                    processedFunction.Add(regKey, new Tuple<IList<string>, BIMRLEnum.deferredExecutionMode>(exprStrList, BIMRLEnum.deferredExecutionMode.PARALLEL));

                //fnRet.keywordInjection = getQueryFilter(functionName, exprStrList);
            }
            else
            {
                fnRet.type = BIMRLEnum.FunctionType.INLINE;
                // This is probably inline function: put back the function spec into the colProjection
                string parList = "";
                foreach (string par in exprStrList)
                {
                    if (parList.Length > 0)
                        parList += ", ";
                    parList += par;
                }
                fnRet.forExpr = functionName + "(" + parList + ")";
            }
            return fnRet;
        }

        static bool isExtensionFunction(string functionName)
        {
            //TODO: check the registry of internal function. It should return the ref to the function and other info such as parameter list
            return false;
        }

        static bool isExternalFunction(string functionName)
        {
            //TODO: check the registry of external function. It should return the ref to the function and other info such as parameter list
            // AND it should also load the relevant assembly into memory and keep track of which assembly already loaded
            return false;
        }

        public static DataTable executeDeferredFunction(DataTable inputTable)
        {
            DataTable finalResult = new DataTable();
            DataTable result = new DataTable();
            DataTable inputT = inputTable;
            foreach(KeyValuePair<string, Tuple<IList<string>, BIMRLEnum.deferredExecutionMode>> defFn in processedFunction)
            {
                if (defFn.Value.Item2 == BIMRLEnum.deferredExecutionMode.PARALLEL)
                    inputT = inputTable;
                else
                    inputT = result;

                Type deferredFn = Type.GetType("BIMRLInterface.ExtensionFunction." + defFn.Key, true, true);
                object obj = Activator.CreateInstance(deferredFn);
                object[] args = new object[2];
                // set the first argument to be the inputTable, followed by other arguments if any
                args.SetValue(inputT, 0);
                // combined the parameter list into one string as I do not seem to find a way for the Invoke to recognize variable params
                string addPar = null;
                for (int i = 0; i < defFn.Value.Item1.Count; ++i)
                    BIMRLInterfaceCommon.appendToString(defFn.Value.Item1[i], "| ", ref addPar);
                //args.SetValue(defFn.Value.Item1[i], i + 1);
                if (defFn.Value.Item1.Count > 0)
                    args.SetValue(addPar, 1);

                //result = (DataTable) deferredFn.InvokeMember("Process", BindingFlags.InvokeMethod, null, obj, args);
                MethodInfo mi = deferredFn.GetMethod("Process");
                result = (DataTable)mi.Invoke(obj, args);

                if (defFn.Value.Item2 == BIMRLEnum.deferredExecutionMode.PARALLEL)
                {
                    finalResult.Merge(result);
                }
                else
                {
                    finalResult = result;
                }
            }

            return finalResult;
        }

        public static DataTable executeEvalFunction(string fnName, DataTable inputDT, params string[] fnParams)
        {
            DataTable result = new DataTable();

            Type evalFn = Type.GetType("BIMRLInterface.ExtensionFunction." + fnName, true, true);
            object obj = Activator.CreateInstance(evalFn);
            PropertyInfo piWhereCond = evalFn.GetProperty("WhereCondition");
            //PropertyInfo piKeyFields = evalFn.GetProperty("KeyFields");
            PropertyInfo piExcFields = evalFn.GetProperty("ExceptionFields");
            PropertyInfo piQualifiers = evalFn.GetProperty("Qualifiers");
            PropertyInfo piAggrFields = evalFn.GetProperty("AggregateFields");
            MethodInfo mi = evalFn.GetMethod("InvokeRule");

            BIMRLKeywordMapping.ParsedParams parsedFnParams = BIMRLKeywordMapping.parseParams(0, fnParams.ToList());
            if (!string.IsNullOrEmpty(parsedFnParams.Where.Text))
                piWhereCond.GetSetMethod().Invoke(obj, new object[] { parsedFnParams.Where.Text });
            //if (parsedFnParams.KeyFields.pars != null)
            //    piKeyFields.GetSetMethod().Invoke(obj, new object[] { parsedFnParams.KeyFields.pars.ToArray() });
            if (parsedFnParams.ExceptionFields.pars != null)
                piExcFields.GetSetMethod().Invoke(obj, new object[] { parsedFnParams.ExceptionFields.pars });
            if (parsedFnParams.Qualifiers != null)
            {
                List<BIMRLEnum.functionQualifier> qualifiers = new List<BIMRLEnum.functionQualifier>();
                foreach (BIMRLKeywordMapping.QualifierParam qPar in parsedFnParams.Qualifiers)
                {
                    qualifiers.Add(qPar.qEnum);
                }
                piQualifiers.GetSetMethod().Invoke(obj, new object[] { qualifiers });
            }
            if (parsedFnParams.AggregateFields != null)
                piAggrFields.GetSetMethod().Invoke(obj, new object[] { parsedFnParams.AggregateFields.pars });

            object[] args = new object[2];
            args[0] = inputDT;
            args[1] = parsedFnParams.unparsedParams;
            
            mi.Invoke(obj, args);

            MethodInfo miGetTableResult = evalFn.GetMethod("GetTableResult");
            result = (DataTable)miGetTableResult.Invoke(obj, null);

            return result;
        }

        public static DataTable executeSingleFunction(string fnName, string tableName, List<string> whereCond)
        {
            DataTable finalResult = new DataTable();
            DataTable result = new DataTable();
            Tuple<IList<string>, BIMRLEnum.deferredExecutionMode> defFn;
            if (!processedFunction.TryGetValue(fnName, out defFn))
                return finalResult;

            Type deferredFn = Type.GetType("BIMRLInterface.ExtensionFunction." + fnName, true, true);
            object obj = Activator.CreateInstance(deferredFn);
            object[] args = new object[2];
            string addPar = null;
            // combined the parameter list into one string as I do not seem to find a way for the Invoke to recognize variable params
            for (int i = 0; i < defFn.Item1.Count; ++i)
                BIMRLInterfaceCommon.appendToString(defFn.Item1[i], "| ", ref addPar);
            //args.SetValue(defFn.Value.Item1[i], i + 1);
            if (defFn.Item1.Count > 0)
                args.SetValue(addPar, 1);

            DBQueryManager dbQ = new DBQueryManager();

            foreach (string whereC in whereCond)
            {
                string sqlStmt = " SELECT * FROM " + tableName + " WHERE " + whereC;
                DataTable inputT = dbQ.queryMultipleRows(sqlStmt);
                args.SetValue(inputT, 0);
                MethodInfo mi = deferredFn.GetMethod("Process");
                result = (DataTable)mi.Invoke(obj, args);

                if (defFn.Item2 == BIMRLEnum.deferredExecutionMode.PARALLEL)
                {
                    finalResult.Merge(result);
                }
                else
                {
                    finalResult = result;
                }
            }

            return finalResult;
        }

        public static keywordInjection getQueryFilter(string functionName, IList<string> exprStrList)
        {
            Type extFn = Type.GetType("BIMRLInterface.ExtensionFunction." + functionName, true, true);
            object obj = Activator.CreateInstance(extFn);
            string pars = null;
            // combined the parameter list into one string as I do not seem to find a way for the Invoke to recognize variable params
            foreach (string expr in exprStrList)
                BIMRLInterfaceCommon.appendToString(expr, "| ", ref pars);
            object[] args = new object[]{pars};
            keywordInjection res = new keywordInjection();
            //res = (keywordInjection) extFn.InvokeMember("preceedingQuery", BindingFlags.InvokeMethod, null, obj, args);
            MethodInfo mi = extFn.GetMethod("preceedingQuery");
            res = (keywordInjection)mi.Invoke(obj, args);
            return res;
        }
    }
}
