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
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using IErrorNode = Antlr4.Runtime.Tree.IErrorNode;
using ITerminalNode = Antlr4.Runtime.Tree.ITerminalNode;
using IToken = Antlr4.Runtime.IToken;
using ParserRuleContext = Antlr4.Runtime.ParserRuleContext;
using BIMRL;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;

namespace BIMRLInterface
{
    public class VarsInfo
    {
        public string varName { get; set; }
        public string setName { get; set; }
        public object varValue { get; set; }
        public List<string> bindVar { get; set; } 

        public VarsInfo()
        {
            bindVar = new List<string>();
        }
    }

    public static class DefinedVarsManager
    {
        public static Dictionary<string, object> VarValueList = new Dictionary<string,object>();
        private static Dictionary<string, string> VarSetList = new Dictionary<string, string>();
        private static Dictionary<string, Tuple<string, List<string>>> VarSetValueList = new Dictionary<string, Tuple<string,List<string>>>();
        private static int varSetSerialNo = 0;

        public static List<VarsInfo> listVars()
        {
            List<VarsInfo> varsInfo = new List<VarsInfo>();

            if (VarValueList.Count() > 0 || VarSetList.Count > 0)
            {
                foreach(KeyValuePair<string, object> entry in VarValueList)
                {
                    VarsInfo varsInfoItem = new VarsInfo();
                    varsInfoItem.varName = entry.Key;
                    varsInfoItem.setName = null;

                    if (entry.Value != null)
                    {
                        if (entry.Value is string)
                            varsInfoItem.varValue = entry.Value.ToString();
                        else if (entry.Value is int)
                        {
                            int? val = entry.Value as int?;
                            varsInfoItem.varValue = val.Value.ToString();
                        }
                        else if (entry.Value is double )
                        {
                            double? val = entry.Value as double?;
                            varsInfoItem.varValue = val.Value.ToString();
                        }
                        else if (entry.Value is decimal)
                        {
                            decimal? val = entry.Value as decimal?;
                            varsInfoItem.varValue = val.Value.ToString();
                        }
                        else if (entry.Value is bool)
                        {
                            bool? val = entry.Value as bool?;
                            varsInfoItem.varValue = val.Value.ToString();
                        }
                        else
                            varsInfoItem.varValue = "null";
                    } 
                    else
                        varsInfoItem.varValue = "null";

                    varsInfoItem.bindVar = new List<string>();
                    varsInfo.Add(varsInfoItem);
                }
                foreach(KeyValuePair<string, string> entry in VarSetList)
                {
                    VarsInfo varsInfoItem = new VarsInfo();
                    varsInfoItem.varName = entry.Key;
                    varsInfoItem.setName = entry.Value;

                    Tuple<string, List<string>> setVal;
                    if (VarSetValueList.TryGetValue(varsInfoItem.setName, out setVal))
                    {
                        varsInfoItem.varValue = (string) setVal.Item1;
                        // Currently the Bind Var is not displayed
                        varsInfoItem.bindVar = new List<string>();
                        foreach (string bindVar in setVal.Item2)
                        {
                            varsInfoItem.bindVar.Add(bindVar.ToUpper());
                        }
                    }
                    varsInfo.Add(varsInfoItem);
                }
            }
            return varsInfo;
        }

        static bool registrationActive = false;
        static List<string> varNameList = new List<string>();
        static List<string> bindVarList = new List<string>();
        static List<object> valueList = new List<object>();

        public static void beginRegister()
        {
            registrationActive = true;
            varNameList.Clear();
            bindVarList.Clear();
            valueList.Clear();
        }

        public static void endRegister()
        {
            // at the end of variable definition, we need to check 3 possible scenarios:
            // 1. a single variable assignment, e.g. DEFINE ?var1 := 'valuestr';
            // 2. a single/multiple variable definition using SQL Select statement, e.g. DEFINE ?v1 SELECT abc FROM tab WHERE xyz=2;
            //                                                                          or DEFINE ?v1, ?v2, ?v3 SELECT abc, def, fgh FROM tab WHERE ...
            // 3. a single/multiple variable definition with bind variable (defer SQL select execution), e.g. DEFINE ?v1, ?v2 SELECT abc, def FROM tab WHERE xyz=:bindvar1;

            // in the cases 1 and 2, what we have should be a matching var name list and the value name list
            if (varNameList.Count > 0 && varNameList.Count == valueList.Count && bindVarList.Count == 0)
            {
                addVarAndValue(varNameList, valueList);
            }

            // case 3 where there is/are bind variabl defined
            if (varNameList.Count > 0 && bindVarList.Count > 0 && valueList.Count > 0)
            {
                // We expect one value is provided through an SQL statement for the set. If somehow there is more than 1 value, we will take the first one and ignore the rest
                string setName = "S" + varSetSerialNo++;
                foreach (string var in varNameList)
                {
                    VarSetList.Add(var.ToUpper(), setName.ToUpper());
                }
                VarSetValueList.Add(setName.ToUpper(), new Tuple<string, List<string>>(valueList[0] as string, bindVarList));
            }

            registrationActive = false;
            varNameList.Clear();
            bindVarList.Clear();
            valueList.Clear();
        }

        public static bool registerVar(string varName, bool skipRegistration = false)
        {
            // skip registration if set
            if (skipRegistration)
            {
                return true;
            }

            if (!registrationActive)
                throw new BIMRLInconsistentParsingException(@"[There is no registration flag active at this point]");
            
            // Check existence of variable in both var dictionary and set dictionary
            if (VarValueList.ContainsKey(varName.ToUpper()) || VarSetList.ContainsKey(varName.ToUpper()))
                throw new BIMRLInterfaceRuntimeException(@"%%Error - A Variable with the same name alreadey defined! [" + varName + "]");

            // Check existence varname in the temp list
            if (varNameList.Contains(varName.ToUpper()))
                throw new BIMRLInterfaceRuntimeException(@"%%Error - A Variable with the same name alreadey defined! [" + varName + "]");

            varNameList.Add(varName.ToUpper());
            return true;
        }

        public static bool registerBindVar(string varName, bool skipRegistration =  false)
        {
            // skip registration if set
            if (skipRegistration)
            {
                return true;
            }

            if (!registrationActive)
                throw new BIMRLInconsistentParsingException(@"[There is no registration flag active at this point]");
            if (bindVarList.Contains(varName.ToUpper()))
                return false;
            bindVarList.Add(varName.ToUpper());
            return true;
        }

        public static void registerValue(object value, bool skipRegistration = false)
        {
            // skip registration if set
            if (skipRegistration)
            {
                return;
            }

            if (!registrationActive)
                throw new BIMRLInconsistentParsingException(@"[There is no registration flag active at this point]");
            valueList.Add(value);
        }

        public static void addVarAndValue(string varName, object varValue)
        {
            if (VarValueList.ContainsKey(varName.ToUpper()))
            {
                // Error, but do nothing for now
            }
            else
            {
                VarValueList.Add(varName.ToUpper(), varValue);
            }
        }

        public static void addVarAndValue(List<string> varName, List<object> varValue)
        {
            int count = 0;
            foreach (string var in varName)
            {
                if (VarValueList.ContainsKey(var.ToUpper()))
                {
                    // Error, but do nothing for now
                }
                else
                {
                    VarValueList.Add(var.ToUpper(), varValue[count]);
                }
                count++;
            }
        }

        public static void addVarSet(List<string> varNames, string sqlStmt, List<string> bindVars)
        {
            foreach (string varname in varNames)
            {
                if (VarValueList.ContainsKey(varname.ToUpper()))
                {
                    // Error: duplicate variable name in a set, but do nothing for now
                    return;
                }
            }
            string setname = "S" + varSetSerialNo++.ToString();

            foreach (string varname in varNames)
            {
                VarSetList.Add(varname.ToUpper(), setname.ToUpper());
                VarSetValueList.Add(setname.ToUpper(), new Tuple<string, List<string>>(sqlStmt, bindVars));
            }
        }

        public static void deleteVar(string varName)
        {
            string setname = null;
            if (VarSetList.TryGetValue(varName.ToUpper(), out setname))
            {
                IEnumerable<KeyValuePair<string,string>> items = VarSetList.Where(x => x.Value == setname);
                foreach(KeyValuePair<string,string> item in items)
                {
                    VarValueList.Remove(item.Key);
                }
                VarSetValueList.Remove(setname);
            }
            else
                if (VarValueList.ContainsKey(varName.ToUpper()))
                    VarValueList.Remove(varName.ToUpper());
        }

        public static void deleteAll()
        {
            VarValueList.Clear();
            VarSetList.Clear();
            VarSetValueList.Clear();
        }

        public static List<string> getVars()
        {
            return varNameList;
        }

        public static List<string> getBindVars()
        {
            return bindVarList;
        }

        public static List<object> getVarValues()
        {
            return valueList;
        }

        public static VarsInfo getDefinedVar(string varName)
        {
            VarsInfo vInfo = new VarsInfo();
            object varVal;
            if (VarValueList.TryGetValue(varName.ToUpper(), out varVal))
            {
                // try to get value from the individual var definition first
                vInfo.varName = varName.ToUpper();
                vInfo.varValue = varVal;
                vInfo.setName = null;
                vInfo.bindVar = new List<string>();
            }
            else
            {
                string setName; 
                if (VarSetList.TryGetValue(varName.ToUpper(), out setName))
                {
                    vInfo.varName = varName.ToUpper();
                    vInfo.setName = setName.ToUpper();

                    Tuple<string, List<string>> setVal;
                    if (VarSetValueList.TryGetValue(setName, out setVal))
                    {
                        vInfo.varValue = (string)setVal.Item1;
                        // Currently the Bind Var is not displayed
                        vInfo.bindVar = new List<string>();
                        foreach (string bindVar in setVal.Item2)
                        {
                            vInfo.bindVar.Add(bindVar.ToUpper());
                        }
                    }
                }
            }

            return vInfo;
        }

        public static void updateValue(string varname, object value)
        {
            if (value == null)
                value = "null";

            if (VarValueList.ContainsKey(varname.ToUpper()))
            {
                VarValueList[varname.ToUpper()] = value;
            }
            else
            {
                // It is not yet defined, add instead
                addVarAndValue(varname.ToUpper(), value);
            }
        }

        public static string ToString()
        {
            string variableList = null ;
            List<string> vars = getVars();
            foreach (string var in vars)
            {
                variableList += var;
                if (var != vars.Last())
                    variableList += ", ";
            }
            return variableList;
        }
    }
}
