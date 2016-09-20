using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data;
using System.Threading.Tasks;
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
using BIMRL.OctreeLib;
using NetSdoGeometry;

namespace BIMRLInterface
{
    class ExecListener:bimrlBaseListener
    {
        Antlr4.Runtime.Tree.ParseTreeProperty<nodeProperty> nodeProp = new Antlr4.Runtime.Tree.ParseTreeProperty<nodeProperty>();
        Dictionary<string, string> deferredExecution = new Dictionary<string, string>();
        BIMRLCommon m_BIMRLRefCommon = new BIMRLCommon();
        static string checkResultTabName = "BIMRLCK$";
        static string evalResultTabName = "BIMRLEV$";
        static string evalProcessTabName = "BIMRLEP$";
        bool createTmpTable = true;
        bool debugSQLStmt = false;
        public static bool isActionStmt = false;
        public static HashSet<string> evalVariables = new HashSet<string>();

        TokenStreamRewriter rewriter;
        bimrlParser parser;
        BufferedTokenStream tokens;

        string m_parsedSQLStmt = null;
        IList<DataTable> m_queryResults = new List<DataTable>();
        static Stack<string> m_queryConsoleOutput = new Stack<string>();

        int startSQLStmt = 0;
        int stopSQLStmt = 0;

        bool putBackWS = false;
        bool collectBindVar = false;
        List<string> elementIdColAdd = new List<string>();

        delegate bool registerVarDelegate(string varName, bool skipRegistration = false);
        delegate void registerValueDelegate(object value, bool skipRegistration = false);
        delegate bool registerBindVarDelegate(string bindVar, bool skipRegistration = false);
        delegate List<string> getVarListDelegate();
        delegate List<string> getBindVarListDelegate();
        delegate List<object> getValueListDelegate();

        // Declare delegate functions
        registerVarDelegate registerVar;
        registerValueDelegate registerValue;
        registerBindVarDelegate registerBindVar;
        getVarListDelegate getVarList;
        getBindVarListDelegate getBindVarList;
        getValueListDelegate getValueList;

        string m_LastQueryString=null;
        StringBuilder m_queryString = new StringBuilder();
        keywordInjection currSetInj;
        Dictionary<string, keywordInjection> bimrlQuerySets;
        Dictionary<string, DataTable> evaluateQueryResults;
        Dictionary<string, string> internalTableMapping;

        FunctionDesc functionDesc = new FunctionDesc { name = null, exprs = new List<string>(), where = null };
        CurrObjectDesc currObjectDesc = new CurrObjectDesc { exprs = new List<string>(), increment = true };
        CurrValueDesc currValueDesc = new CurrValueDesc { incr = 0, varname = null };

        bool savedTabRegistrationFlag = false;
        bool savedColRegistrationFlag = false;
        bool idListByCheck = false;

        struct evalItems
        {
            public string targetTab { get; set; }                   // Evaluate result target table
            public string resultTab { get; set; }                   // Evaluate final result table
            public string varName { get; set; }                     // variable name to hold inidivdual status of the set in the evaluate result
            public string sourceTab { get; set; }                   // source table (from check stmt)
            public string setName { get; set; }                     // set name as defined in the check stmt
            public string joinClause { get; set; }                  // join clause the specifies the second set (from check stmt) in the join including the join condition
            public string groupFilterCond { get; set; }             // list of column(s) that is used as a criteria to group evaluate
            public List<string> groupFilterItems { get; set; }      // itemized columns from the groupFilterCond above 
            public string evalExpr { get; set; }                    // expression from EVALUATE stmt
            public keywordInjection keywInj { get; set; }           // keyword injection for the actual SQL statement to create/insert eveluate stmt into a target table
            public string externFnName { get; set; }                // external function name, this is an additional processing/logic for evaluate stmt
            public List<string> externFnExpr { get; set; }          // external function expression
            public bool aggregateResult { get; set; }               // generate aggregated result from evaluate result using the same column as the groupfilterItems?
        }

        public ExecListener(bimrlParser parser)
        {
            this.parser = parser;
            bimrlQuerySets = new Dictionary<string,keywordInjection>();
            evaluateQueryResults = new Dictionary<string, DataTable>();
        }

        public ExecListener(BufferedTokenStream tokens)
        {
            this.tokens = tokens;
            rewriter = new TokenStreamRewriter(tokens);
            bimrlQuerySets = new Dictionary<string, keywordInjection>();
            evaluateQueryResults = new Dictionary<string, DataTable>();
        }

        public string visitMsg
        {
            set;
            get;
        }

        public void setNodePropertyValue (Antlr4.Runtime.Tree.IParseTree node, nodeProperty value)
        {
            nodeProp.Put(node, value);
        }

        public nodeProperty getNodePropertyValue (Antlr4.Runtime.Tree.IParseTree node)
        {
            return nodeProp.Get(node);
        }

        public string lastQueryString
        {
            get { return m_LastQueryString; }
        }

        public void clearQueryResults()
        {
            m_queryResults.Clear();
        }

        public IList<DataTable> queryResults
        {
            get { return m_queryResults; }
        }
        
        public void clearQueryConsoleOutput()
        {
            if (m_queryConsoleOutput != null)
                m_queryConsoleOutput.Clear();
        }

        public Stack<string> queryConsoleOutput
        {
            get { return m_queryConsoleOutput; }
        }

        void clearQueryStrings()
        {
            // Clear all these items as they are only used within a single statement
            m_parsedSQLStmt = null;

            m_queryString.Clear();
            bimrlQuerySets.Clear();
            evaluateQueryResults.Clear();
        }

        public override void VisitTerminal(ITerminalNode node)
        {
            string nodeName = node.Symbol.Text;
            if (putBackWS && whiteSpaceOnRight(node.Symbol.TokenIndex))
                rewriter.InsertAfter(node.Symbol.StopIndex, " ");
        }


        public override void EnterBimrl_rule(bimrlParser.Bimrl_ruleContext context)
        {
            base.EnterBimrl_rule(context);
            ColumnListManager.Init();
            TableListManager.Init();
            m_BIMRLRefCommon.BIMRlErrorStack.Clear();
        }

        public override void ExitBimrl_rule(bimrlParser.Bimrl_ruleContext context)
        {
            base.ExitBimrl_rule(context);
            clearQueryStrings();    // make sure we clear this at the end of every bimrl rule
        }

        public override void EnterAssignment_stmt(bimrlParser.Assignment_stmtContext context)
        {
            base.EnterAssignment_stmt(context);
            DefinedVarsManager.beginRegister();
            clearQueryStrings();

            // Assign the appropriate methods into delegate funtions
            registerVar = DefinedVarsManager.registerVar;
            registerValue = DefinedVarsManager.registerValue;
            registerBindVar = DefinedVarsManager.registerBindVar;
            getVarList = DefinedVarsManager.getVars;
            getBindVarList = DefinedVarsManager.getBindVars;
            getValueList = DefinedVarsManager.getVarValues;
            currSetInj = new keywordInjection();
        }

        public override void EnterAction_stmt(bimrlParser.Action_stmtContext context)
        {
            base.EnterAction_stmt(context);

            isActionStmt = true;
        }

        public override void ExitAction_stmt(bimrlParser.Action_stmtContext context)
        {
            base.ExitAction_stmt(context);
            List<nodeProperty> actionInfoList = new List<nodeProperty>();
            List<DataTable> actionQueryResults = new List<DataTable>();
            //bool saveUsergeom = false;

            if (context.one_action() != null)
            {
                nodeProperty nodeP = getNodePropertyValue(context.one_action());
                nodeP.actionCommandInfo.evalTabName = evalResultTabName + "VAR1"; //
                actionInfoList.Add(nodeP);
            }
            else if (context.multi_action().Count > 0)
            {
                foreach(bimrlParser.Multi_actionContext maCtx in context.multi_action())
                {
                    nodeProperty oneActNP = getNodePropertyValue(maCtx.one_action());
                    string varN = string.Empty;
                    if (getNodePropertyValue(maCtx.when_clause().expr()).genList.Count == 0)
                        varN = "VAR1";  // use default if EVALUATE does not assign result into a variable
                    else
                        varN = getNodePropertyValue(maCtx.when_clause().expr()).genList[0];

                    oneActNP.actionCommandInfo.evalTabName = evalResultTabName + varN.ToUpper();

                    nodeProperty whenCl = getNodePropertyValue(maCtx.when_clause().expr());
                    // For WHEN clause, it will be simply an expression of the EVAL variables with operations. This will be added as WHERE clause in the SQL statement
                    // This is done only when the expr is not a single item without any operators/conditions, e.g. just the variable name: ?LineOfSight
                    if (!whenCl.exprSingleItem)
                        BIMRLInterfaceCommon.appendToString(whenCl.forExpr, " AND ", ref oneActNP.keywInj.whereClInjection);
                    actionInfoList.Add(oneActNP);
                }
            }

            int itemNo = 0;
            DBQueryManager dbQ = new DBQueryManager();
            Dictionary<string, BIMRLExportSDOToX3D> x3dFiles = new Dictionary<string, BIMRLExportSDOToX3D>();

            // do the execution here based on the individual information from the action sub clauses
            foreach(nodeProperty action in actionInfoList)
            {
                ActionInfo cmdInfo = action.actionCommandInfo;
                DataTable actQ = null;
                string actionWhereCond = "";
                if (!string.IsNullOrEmpty(action.keywInj.whereClInjection))
                    actionWhereCond = "WHERE " + action.keywInj.whereClInjection;

                foreach (ActionInfo.ActionCommandEnum cmd in cmdInfo.ActionCommandSequence)
                {
                    switch (cmd)
                    {
                        case ActionInfo.ActionCommandEnum.PRINTACTION:
                            string colList = "";
                            if (action.genList.Count > 0)
                            {
                                foreach (string col in action.genList)
                                    BIMRLInterfaceCommon.appendToString(col, ", ", ref colList);
                            }
                            else
                                colList = "*";
                            
                            string sqlStmt = "SELECT " + colList + " FROM " + cmdInfo.evalTabName + " " + actionWhereCond;
                            actQ = dbQ.queryMultipleRows(sqlStmt);
                            actionQueryResults.Add(actQ);
                            if (!string.IsNullOrEmpty(DBQueryManager.errorMsg))
                            {
                                m_queryConsoleOutput.Push(DBQueryManager.errorMsg + "\n");
                                DBQueryManager.errorMsg = string.Empty;
                            }
                            break;
                        case ActionInfo.ActionCommandEnum.DRAWACTION:
                            BIMRLExportSDOToX3D x3dExp = null;
                            if (!x3dFiles.TryGetValue(cmdInfo.X3DFileName, out x3dExp))
                            {
                                x3dExp = new BIMRLExportSDOToX3D(m_BIMRLRefCommon, cmdInfo.X3DFileName);    // Initialize first
                                x3dFiles.Add(cmdInfo.X3DFileName, x3dExp);
                            }

                            if (cmdInfo.highlightItems.Count > 0)
                            {
                                if (!cmdInfo.highlightItemIsValue)
                                {
                                    string hItems = "";
                                    bool outputdetails = false;
                                    foreach (string val in cmdInfo.highlightItems)
                                    {
                                        if (string.Compare(val, "OUTPUTDETAILS") == 0)
                                            outputdetails = true;
                                        else
                                            BIMRLInterfaceCommon.appendToString(val, ", ", ref hItems);
                                    }
                                    string hColStmt = "SELECT " + hItems + " FROM " + cmdInfo.evalTabName + " " + actionWhereCond;
                                    DataTable hCol = dbQ.queryMultipleRows(hColStmt);
                                    int noCol = hCol.Columns.Count;
                                    if (hCol.Rows != null)
                                    {
                                        foreach (DataRow row in hCol.Rows)
                                        {
                                            for (int i = 0; i < noCol; ++i)
                                                x3dExp.IDsToHighlight.Add(row[i].ToString());
                                        }
                                    }

                                    // if outputdetails is specified, we will perform separate query to highlight the result by EVALUATE
                                    if (outputdetails)
                                    {
                                        hColStmt = "SELECT OUTPUTDETAILS FROM USERGEOM_OUTPUTDETAILS ";
                                        DataTable hCol2 = dbQ.queryMultipleRows(hColStmt);
                                        if (hCol2.Rows != null)
                                        {
                                            foreach (DataRow row in hCol2.Rows)
                                            {
                                                x3dExp.IDsToHighlight.Add(row[0].ToString());
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    x3dExp.IDsToHighlight = cmdInfo.highlightItems;
                                }
                            }

                            // Override the background model transparency. It can also be set by variable BackgroundTransparency
                            if (backgTransparency >= 0)
                                x3dExp.transparencyOverride = backgTransparency;

                            // Skip if there is no export condition (for background specified), e.g. append cases
                            x3dExp.highlightColor = cmdInfo.highlightColor;
                            if (!string.IsNullOrEmpty(cmdInfo.exportWhereCond))
                            {
                                string whereCondElemGeom = "ELEMENTID IN (SELECT ELEMENTID FROM BIMRL_ELEMENT_" + DBQueryManager.FedModelID.ToString("X4") + " WHERE " + cmdInfo.exportWhereCond + ")";
                                whereCondElemGeom = BIMRLKeywordMapping.expandBIMRLTables(whereCondElemGeom);
                                x3dExp.exportElemGeomToX3D(DBQueryManager.FedModelID, whereCondElemGeom);
                            }

                            // User geom
                            x3dExp.userGeomColor = cmdInfo.userGeomColor;
                            string whereCondUserGeom = "ELEMENTID IN (SELECT ELEMENTID FROM " + cmdInfo.evalTabName + " " + actionWhereCond + ")";
                            x3dExp.exportUserGeomToX3D(whereCondUserGeom);

                            break;
                        case ActionInfo.ActionCommandEnum.SAVEINTOTABLE:
                            if (itemNo == 0)
                            {
                                dbQ.createTableFromDataTable(cmdInfo.TableName, actQ, false, false);        // The first one must not be APPEND, it must create the Table first
                                saveUserGeomTables(dbQ, cmdInfo.TableName, false, whenCond: actionWhereCond);  // Have to defer this until all ACTIONs are completed because there may be append cases
                            }
                            else
                            {
                                dbQ.createTableFromDataTable(cmdInfo.TableName, actQ, cmdInfo.AppendToTable, false);
                                saveUserGeomTables(dbQ, cmdInfo.TableName, cmdInfo.AppendToTable, whenCond: actionWhereCond);
                            }
                            break;
                        case ActionInfo.ActionCommandEnum.SAVEINTOBCFFILE:
                            // TO DO
                            break;
                        default:
                            break;
                    }
                }
                itemNo++;
            }

            // close the X3D file if it is opened
            foreach (KeyValuePair<string, BIMRLExportSDOToX3D> x3dToClose in x3dFiles)
            {
                x3dToClose.Value.endExportToX3D();
            }

            isActionStmt = false;
            m_queryResults = actionQueryResults;
        }

        public override void ExitOne_action(bimrlParser.One_actionContext context)
        {
 	        base.ExitOne_action(context);
            nodeProperty nodeP = new nodeProperty();
            ActionInfo actionInfo = new ActionInfo();

            if (context.print_action() != null)
            {
                bimrlParser.Print_actionContext printAcCtx = context.print_action() as bimrlParser.Print_actionContext;

                if (printAcCtx.simple_id_list() != null)
                {
                    nodeP.genList = getNodePropertyValue(printAcCtx.simple_id_list()).genList;
                    if (!nodeP.genList.Contains("ELEMENTID"))
                        nodeP.genList.Insert(0, "ELEMENTID");              // add ELEMENTID column if not specified
                    if (!nodeP.genList.Contains("OUTPUT"))
                        nodeP.genList.Insert(0, "OUTPUT");              // add OUTPUT column if not specified
                }
                else
                    nodeP.genList.Add("*");

                actionInfo.addCommandSequence(ActionInfo.ActionCommandEnum.PRINTACTION);

                if (printAcCtx.save_action() != null)
                {
                    for (int cnt = 0; cnt < printAcCtx.save_action().save_destination().Count; ++cnt)
                    {
                        if (printAcCtx.save_action().save_destination()[cnt].TABLE() != null)
                        {
                            // save to table
                            actionInfo.TableName = printAcCtx.save_action().save_destination()[cnt].table_name().GetText();
                            actionInfo.addCommandSequence(ActionInfo.ActionCommandEnum.SAVEINTOTABLE);
                            if (printAcCtx.save_action().save_destination()[cnt].APPEND() != null)
                                actionInfo.AppendToTable = true;
                        }
                        //if (printAcCtx.save_action().save_destination()[cnt].BCFFILE() != null)
                        //{
                        //    // create BCF file
                        //    actionInfo.BCFFileName = printAcCtx.save_action().save_destination()[cnt].bcffilename().stringliteral().GetText().Replace("'", "");
                        //    actionInfo.addCommandSequence(ActionInfo.ActionCommandEnum.SAVEINTOBCFFILE);
                        //}
                        // X3D option does not make sense on this context, ignore even if it exists
                    }
                }
            }

            if (context.draw_action() != null)
            {
                bimrlParser.Draw_actionContext drawAcCtx = context.draw_action() as bimrlParser.Draw_actionContext;
                if (drawAcCtx.COLOR() != null)
                {
                    ColorSpec colorSpec = new ColorSpec();

                    // default transparency value is 0 and color is RED for the transient geometry
                    setRGB(255, 0, 0, ref colorSpec);
                    colorSpec.transparency = 0.0;
                   if (drawAcCtx.color_spec().transparency() != null)
                        colorSpec.transparency = double.Parse(drawAcCtx.color_spec().transparency().NUMBER().GetText());

                    if (drawAcCtx.color_spec().RED() != null)
                        setRGB(255,0,0, ref colorSpec);
                    else if (drawAcCtx.color_spec().GREEN() != null)
                        setRGB(0,255,0, ref colorSpec);
                    else if (drawAcCtx.color_spec().BLUE() != null)
                        setRGB(0,0,255, ref colorSpec);
                    else if (drawAcCtx.color_spec().CYAN() != null)
                        setRGB(0,255,255, ref colorSpec);
                    else if (drawAcCtx.color_spec().MAGENTA() != null)
                        setRGB(255,0,255, ref colorSpec);
                    else if (drawAcCtx.color_spec().YELLOW() != null)
                        setRGB(255,255,0, ref colorSpec);
                    else if (drawAcCtx.color_spec().WHITE() != null)
                        setRGB(255,255,255, ref colorSpec);
                    else if (drawAcCtx.color_spec().BLACK() != null)
                        setRGB(0,0,0, ref colorSpec);
                    else
                    {
                        int redCol = int.Parse(drawAcCtx.color_spec().NUMBER()[0].GetText());
                        int greenCol = int.Parse(drawAcCtx.color_spec().NUMBER()[1].GetText());
                        int blueCol = int.Parse(drawAcCtx.color_spec().NUMBER()[2].GetText());
                        setRGB(redCol, greenCol, blueCol, ref colorSpec);
                    }

                    actionInfo.userGeomColor = colorSpec;
                }

                if (drawAcCtx.background_model() != null)
                {
                    nodeProperty bgMod = getNodePropertyValue(drawAcCtx.background_model());
                    actionInfo.exportWhereCond = bgMod.keywInj.whereClInjection;
                }

                if (drawAcCtx.highlight_object() != null)
                {
                    if (drawAcCtx.highlight_object().simple_id_list() != null)
                    {
                        foreach (string simpleID in getNodePropertyValue(drawAcCtx.highlight_object().simple_id_list()).genList)
                            actionInfo.highlightItems.Add(simpleID);

                        actionInfo.highlightItemIsValue = false;
                    }
                    else if (drawAcCtx.highlight_object().value_list() != null)
                    {
                        foreach (string val in getNodePropertyValue(drawAcCtx.highlight_object().value_list()).genList)
                            actionInfo.highlightItems.Add(val);

                        actionInfo.highlightItemIsValue = true;
                    }

                    // default transparency value is 0 and color RED
                    ColorSpec highlight = new ColorSpec();
                    highlight.transparency = 0.0;
                    setRGB(255, 0, 0, ref highlight);

                    if (drawAcCtx.highlight_object().color_spec() != null)
                    {
                        if (drawAcCtx.highlight_object().color_spec().transparency() != null)
                            highlight.transparency = double.Parse(drawAcCtx.highlight_object().color_spec().transparency().NUMBER().GetText());

                        if (drawAcCtx.highlight_object().color_spec().RED() != null)
                            setRGB(255, 0, 0, ref highlight);
                        else if (drawAcCtx.highlight_object().color_spec().GREEN() != null)
                            setRGB(0, 255, 0, ref highlight);
                        else if (drawAcCtx.highlight_object().color_spec().BLUE() != null)
                            setRGB(0, 0, 255, ref highlight);
                        else if (drawAcCtx.highlight_object().color_spec().CYAN() != null)
                            setRGB(0, 255, 255, ref highlight);
                        else if (drawAcCtx.highlight_object().color_spec().MAGENTA() != null)
                            setRGB(255, 0, 255, ref highlight);
                        else if (drawAcCtx.highlight_object().color_spec().YELLOW() != null)
                            setRGB(255, 255, 0, ref highlight);
                        else if (drawAcCtx.highlight_object().color_spec().WHITE() != null)
                            setRGB(255, 255, 255, ref highlight);
                        else if (drawAcCtx.highlight_object().color_spec().BLACK() != null)
                            setRGB(0, 0, 0, ref highlight);
                        else
                        {
                            int redCol = int.Parse(drawAcCtx.highlight_object().color_spec().NUMBER()[0].GetText());
                            int greenCol = int.Parse(drawAcCtx.highlight_object().color_spec().NUMBER()[1].GetText());
                            int blueCol = int.Parse(drawAcCtx.highlight_object().color_spec().NUMBER()[2].GetText());
                            setRGB(redCol, greenCol, blueCol, ref highlight);
                        }
                    }
                    actionInfo.highlightColor = highlight;
                }

                if (drawAcCtx.save_action() != null)
                {
                    // We do not combine DRAW SAVE command with others as they do not make sense. We will ignore the rest if any
                    if (drawAcCtx.save_action().save_destination()[0].X3D() != null)
                    {
                        // create X3D file
                        actionInfo.X3DFileName = drawAcCtx.save_action().save_destination()[0].x3dfilename().stringliteral().GetText().Replace("'", "");
                    }
                    // The other options do not make sense on this context, ignore even if it exists

                }
                actionInfo.addCommandSequence(ActionInfo.ActionCommandEnum.DRAWACTION);
            }

            nodeP.actionCommandInfo = actionInfo;
            setNodePropertyValue(context, nodeP);
        }

        double backgTransparency = 0.5; //default set to 50%

        public override void ExitBackground_model(bimrlParser.Background_modelContext context)
        {
            base.ExitBackground_model(context);
            keywordInjection keywInj = new keywordInjection();

            nodeProperty idListNP = getNodePropertyValue(context.id_list());
            BIMRLInterfaceCommon.appendToString(idListNP.keywInj.colProjInjection, ", ", ref keywInj.colProjInjection);
            BIMRLInterfaceCommon.appendToString(idListNP.keywInj.tabProjInjection, ", ", ref keywInj.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(idListNP.keywInj.whereClInjection, " AND ", ref keywInj.whereClInjection);
            // Add ForExpr to where clause if it comes from expression
            BIMRLInterfaceCommon.appendToString(idListNP.forExpr, " AND ", ref keywInj.whereClInjection);
            BIMRLInterfaceCommon.appendToString(idListNP.keywInj.suffixInjection, ", ", ref keywInj.suffixInjection);

            if (context.where_clause() != null)
            {
                nodeProperty whereClNP = getNodePropertyValue(context.where_clause().expr());
                BIMRLInterfaceCommon.appendToString(whereClNP.keywInj.colProjInjection, ", ", ref keywInj.colProjInjection);
                BIMRLInterfaceCommon.appendToString(whereClNP.keywInj.tabProjInjection, ", ", ref keywInj.tabProjInjection);
                BIMRLInterfaceCommon.appendToString(whereClNP.keywInj.whereClInjection, " AND ", ref keywInj.whereClInjection);
                // Add ForExpr to where clause if it comes from expression
                BIMRLInterfaceCommon.appendToString(whereClNP.forExpr, " AND ", ref keywInj.whereClInjection);
                BIMRLInterfaceCommon.appendToString(whereClNP.keywInj.suffixInjection, ", ", ref keywInj.suffixInjection);
            }

            VarsInfo tmpTab = DefinedVarsManager.getDefinedVar("BackgroundTransparency");       // can be set by a variable BackgroundTransparency
            if (tmpTab.varValue != null)
                double.TryParse(tmpTab.varValue.ToString(), out backgTransparency);

            if (context.transparent() != null)
            {
                if (context.transparent().NUMBER() != null)
                    double.TryParse(context.transparent().NUMBER().GetText(), out backgTransparency);
            }

            nodeProperty nodeProp = new nodeProperty();
            nodeProp.keywInj = keywInj;
            setNodePropertyValue(context, nodeProp);
        }

        public override void ExitValue_list(bimrlParser.Value_listContext context)
        {
            base.ExitValue_list(context);
            nodeProperty nodeP = new nodeProperty();

            foreach (bimrlParser.ValueContext val in context.value())
            {
                nodeP.genList.Add(val.GetText());
            }

            setNodePropertyValue(context, nodeP);
        }

        public override void ExitAssignment_stmt(bimrlParser.Assignment_stmtContext context)
        {
            base.ExitAssignment_stmt(context);

            if (context.varnamelist() != null)
            {
                if (context.select_expr() != null)
                {
                    // It is select expression
                    m_parsedSQLStmt = getNodePropertyValue(context.select_expr()).forExpr;
                    m_parsedSQLStmt = BIMRLKeywordMapping.expandBIMRLTables(m_parsedSQLStmt);

                    List<string> bindVars = DefinedVarsManager.getBindVars();
                    if (bindVars.Count() > 0)
                        registerValue(m_parsedSQLStmt);     // register a single value for delayed execution
                    else
                    {
                        List<string> varnames = new List<string>();
                        foreach (bimrlParser.VarnameContext varnm in context.varnamelist().varname())
                            varnames.Add(varnm.GetText());

                        // we will execute the SQL statement to get values and assign the values to the variables
                        DBQueryManager query = new DBQueryManager();
                        DataTable qRet = query.querySingleRow(m_parsedSQLStmt);
                        if (qRet != null)
                        {
                            // foreach (object val in qRet.Rows[0].ItemArray)
                            for (int i = 0; i < varnames.Count; ++i)    // use the var name list as the guide
                            {
                                object val = null;
                                if (qRet.Rows.Count > 0)
                                    val = qRet.Rows[0][i];
                                //DefinedVarsManager.registerVar(varnames[i].ToUpper().Replace('?', ' ').Trim());
                                //registerValue(val);
                                DefinedVarsManager.updateValue(varnames[i].ToUpper().Replace('?', ' ').Trim(), val);
                            }
                        }
                    }
                }
            }
            if (context.varname() != null)
            {
                // It is a simple variable assigment
                string varname = getNodePropertyValue(context.varname()).forExpr;
                object value = getNodePropertyValue(context.value()).originalValue;

                DefinedVarsManager.updateValue(varname.ToUpper(), value);
            }

                //DefinedVarsManager.registerVar(varname.ToUpper());
                //DefinedVarsManager.registerValue(value);
            
            DefinedVarsManager.endRegister();
        }
        
        public override void ExitVarname(bimrlParser.VarnameContext context)
        {
            base.ExitVarname(context);
            nodeProperty nodeP = new nodeProperty();
            string varname = context.GetChild(0).GetText();
            varname = varname.Replace('?', ' ').Trim();
            nodeP.forExpr = varname;

            //registerVar(varname);
            setNodePropertyValue(context, nodeP);
        }

        public override void ExitValue(bimrlParser.ValueContext context)
        {
            base.ExitValue(context);
            object value = null;

            // This means we get the terminal node that can be NULL or BOOLEAN
            if (context.NULL() != null)
            {
                // value is NULL
                // Don't know how to do with it yet. Do it later
            }
            else if (context.BOOLEAN() != null)
            {
                // value is boolean
                string valueStr = context.BOOLEAN().GetText();
                if (string.Compare(valueStr, ".true.", true) == 0 || string.Compare(valueStr, ".t.", true) == 0)
                    value = (bool)true;
                if (string.Compare(valueStr, ".false.", true) == 0 || string.Compare(valueStr, ".f.", true) == 0)
                    value = (bool)false;
            }
            else
            {
                string valueStr = context.GetChild(0).GetText();
                if (context.GetChild(0) is bimrlParser.StringliteralContext)
                {
                    valueStr = valueStr.Replace('\'', ' ').Trim();
                    value = (string)valueStr;
                }
                else if (context.GetChild(0) is bimrlParser.RealliteralContext)
                {
                    int valueInt;
                    if (Int32.TryParse(valueStr, out valueInt))
                        value = (int)valueInt;
                    else
                    {
                        double valueDbl;
                        if (Double.TryParse(valueStr, out valueDbl))
                            value = (double)valueDbl;
                    }
                }
            }
            //if (context.parent.RuleIndex == bimrlParser.RULE_assignment_stmt)
            //{
            //    registerValue(value);
            //}

            nodeProperty nodeP = new nodeProperty();
            nodeP.originalValue = value;
            nodeP.forExpr = value.ToString();
            setNodePropertyValue(context, nodeP);
        }

        public override void ExitReset_all_var(bimrlParser.Reset_all_varContext context)
        {
            base.ExitReset_all_var(context);

            if (context.RESET() != null && context.VARS() != null)
            {
                DefinedVarsManager.deleteAll();
            }
        }

        public override void ExitDelete_var(bimrlParser.Delete_varContext context)
        {
            base.ExitDelete_var(context);

            if (context.DELETE() != null && context.VARNAME() != null)
            {
                string varname = context.VARNAME().GetText().Replace('?', ' ').Trim();
                DefinedVarsManager.deleteVar(varname);
            }
            
        }

     
        public override void EnterCheck_stmt(bimrlParser.Check_stmtContext context)
        {
            base.EnterCheck_stmt(context);
        }

        public override void EnterSingle_check_stmt(bimrlParser.Single_check_stmtContext context)
        {
            base.EnterSingle_check_stmt(context);
            TableListManager.Init();
            ColumnListManager.Init();

            // If the parent context is Check_stmt directly. It is a single set statement, set a default set to "SET1"
            if (context.Parent is bimrlParser.Check_stmtContext)
            {
                if (!bimrlQuerySets.TryGetValue("SET1", out currSetInj))
                {
                    keywordInjection keywInj = new keywordInjection();
                    bimrlQuerySets.Add("SET1", keywInj);
                    internalTableMapping.Add("SET1", checkResultTabName + "SET1");
                }
                currSetInj = bimrlQuerySets["SET1"];
            }
        }

        public override void EnterMulti_check_stmt(bimrlParser.Multi_check_stmtContext context)
        {
            base.EnterMulti_check_stmt(context);

            string setName = context.setname().GetText().ToUpper();
            if (!bimrlQuerySets.TryGetValue(setName, out currSetInj))
            {
                keywordInjection keywInj = new keywordInjection();
                bimrlQuerySets.Add(setName, keywInj);
                internalTableMapping.Add(setName, checkResultTabName + setName);
            }
            currSetInj = bimrlQuerySets[setName];
        }

        public override void EnterEvaluate_stmt(bimrlParser.Evaluate_stmtContext context)
        {
            base.EnterEvaluate_stmt(context);

            DefinedVarsManager.beginRegister();
            // Assign the appropriate methods into delegate funtions
            registerVar = DefinedVarsManager.registerVar;
            registerValue = DefinedVarsManager.registerValue;
            registerBindVar = DefinedVarsManager.registerBindVar;
            getVarList = DefinedVarsManager.getVars;
            getBindVarList = DefinedVarsManager.getBindVars;
            getValueList = DefinedVarsManager.getVarValues;
            evalVariables.Clear();
        }

        public override void ExitSet_clause(bimrlParser.Set_clauseContext context)
        {
            base.ExitSet_clause(context);
            nodeProperty nodeP = new nodeProperty();

            // setName needs to be expanded if it belongs to either the CHECK set or EVALUATE varname (stored in internalTableMapping Dict)
            if (context.setname() != null)
            {
                string expTabName;
                if (internalTableMapping.TryGetValue(context.setname().GetText().ToUpper(), out expTabName))
                    nodeP.forExpr = expTabName;
                else
                    nodeP.forExpr = context.setname().GetText().ToUpper();
            }
            else if (context.select_stmt() != null)
            {
                // for Select_stmt, no need further expansion. Tha table name (if it belongs to CHECK or EVALUATE would have been expanded in the select statement id_member)
                nodeProperty selStmtNodeP = getNodePropertyValue(context.select_stmt());
                nodeP.forExpr = "(" + selStmtNodeP.forExpr + ')';
            }
             setNodePropertyValue(context, nodeP);
        }

        public override void ExitOutput(bimrlParser.OutputContext context)
        {
            base.ExitOutput(context);

            string varname = context.varname().GetText().Replace('?', ' ').Trim();
            // we will keep the var name into a global list. The list should be reset at entering EVALUATE
            evalVariables.Add(varname.ToUpper());

            // We will also add this to the internal mapping table for mapping the varname to an actual table name created in the process
            internalTableMapping.Add(varname.ToUpper(), evalResultTabName + varname.ToUpper());
        }

        public override void ExitEvaluate_stmt(bimrlParser.Evaluate_stmtContext context)
        {
            evaluateQueryResults.Clear();
            base.ExitEvaluate_stmt(context);

            OracleCommand cmd = new OracleCommand("", DBOperation.DBConn);
            OracleCommand cmd2 = new OracleCommand("", DBOperation.DBConn);
            DBQueryManager dbQEval = new DBQueryManager();
            string currStmt = "";
            string varname = "VAR1";
            bool singleEval = false;

            HashSet<string> constructAliases = new HashSet<string>();

            List<evalItems> evalStmtItems = new List<evalItems>();
            if (context.foreach_fnexpr().Count > 0)
            {
                int counter = 0;
                // the second branch with one or multiple set
                foreach (bimrlParser.Foreach_fnexprContext foreachCl in context.foreach_fnexpr())
                {
                    // TODO: To support WHEN expr !!!

                    evalItems evalItem = new evalItems();
                    keywordInjection keywInj = new keywordInjection();

                    // expr is th only mandatory item in EVALUATE
                    nodeProperty nodePexpr = getNodePropertyValue(foreachCl.expr());
                    if (nodePexpr.fnType == BIMRLEnum.FunctionType.EXTENSION)                   // for EVALUATE that is based on the extension function
                    {
                        nodePexpr.forExpr = nodePexpr.forExpr.Replace(BIMRLConstant.tempOp, " = ");
                        nodePexpr.forExpr = nodePexpr.forExpr.Replace(BIMRLConstant.notOp, "");
                        evalItem.externFnExpr = nodePexpr.fnExprList;
                        evalItem.externFnName = nodePexpr.fnName;
                    }
                    else
                    {                                                                           // for EVALUATE that uses purely BIMRL statement (?)
                        if (!string.IsNullOrEmpty(nodePexpr.forExpr))
                        {
                            nodePexpr.forExpr = nodePexpr.forExpr.Replace(BIMRLConstant.tempOp, " = ");
                            nodePexpr.forExpr = nodePexpr.forExpr.Replace(BIMRLConstant.notOp, "");
                        }
                        BIMRLInterfaceCommon.appendToString(nodePexpr.forExpr, " AND ", ref keywInj.whereClInjection);

                        // append other stuffs if any
                        BIMRLInterfaceCommon.appendToString(nodePexpr.keywInj.colProjInjection, ", ", ref keywInj.colProjInjection);
                        BIMRLInterfaceCommon.appendToString(nodePexpr.keywInj.tabProjInjection, ", ", ref keywInj.tabProjInjection);
                        // At this point, function does not have any value/expr, replace the template op with the default one " = "

                        if (!string.IsNullOrEmpty(nodePexpr.keywInj.whereClInjection))
                        {
                            nodePexpr.keywInj.whereClInjection = nodePexpr.keywInj.whereClInjection.Replace(BIMRLConstant.tempOp, " = ");
                            nodePexpr.keywInj.whereClInjection = nodePexpr.keywInj.whereClInjection.Replace(BIMRLConstant.notOp, "");
                        }
                        if (!string.IsNullOrEmpty(nodePexpr.keywInj.suffixInjection))
                        {
                            nodePexpr.keywInj.suffixInjection = nodePexpr.keywInj.suffixInjection.Replace(BIMRLConstant.tempOp, " = ");
                            nodePexpr.keywInj.suffixInjection = nodePexpr.keywInj.suffixInjection.Replace(BIMRLConstant.notOp, "");
                        }
                        BIMRLInterfaceCommon.appendToString(nodePexpr.keywInj.whereClInjection, " AND ", ref keywInj.whereClInjection);
                        BIMRLInterfaceCommon.appendToString(nodePexpr.keywInj.suffixInjection, " ", ref keywInj.suffixInjection);
                        evalItem.keywInj = keywInj;
                    }

                    varname = counter.ToString();   // default name if not provided
                    if (foreachCl.output() != null)
                    {
                        varname = foreachCl.output().varname().GetText().Replace('?', ' ').Trim();
                        // we will keep the var name into a global list. The list should be reset at entering EVALUATE
                        //evalVariables.Add(varname.ToUpper());
                    }

                    if (foreachCl.AGGREGATE() != null)
                        evalItem.aggregateResult = true;
                    else
                        evalItem.aggregateResult = false;

                    if (foreachCl.simple_id_list() != null)
                    {
                        nodeProperty nodeP = getNodePropertyValue(foreachCl.simple_id_list());
                        evalItem.groupFilterCond = foreachCl.simple_id_list().GetText();           // The entire list of simple_id_list in a single string
                        evalItem.groupFilterItems = nodeP.genList;                                  // A list of individual items from simple_id_list
                    }

                    string evalTab = evalProcessTabName + varname.ToUpper();                               // Eval result table is formed by a fixed prefix + the varname
                    string setName = "SET1";
                    if (foreachCl.set_clause() != null)
                    {
                        //setName = foreachCl.setname().GetText();                                // The first table (from check stmt) to join (default is SET1)
                        nodeProperty nodeProp = getNodePropertyValue(foreachCl.set_clause());
                        setName = nodeProp.forExpr;
                    }

                    evalItem.targetTab = evalTab;
                    evalItem.resultTab = evalResultTabName + varname.ToUpper();

                    //evalItem.setName = setName;
                    //evalItem.sourceTab = checkResultTabName + setName.ToUpper();
                    //evalItem.sourceTab = internalTableMapping[setName.ToUpper()];
                    evalItem.sourceTab = setName;
                    foreach (KeyValuePair<string, string> intTM in internalTableMapping)
                    {
                        if (string.Compare(setName, intTM.Value, true) == 0)
                        {
                            evalItem.setName = intTM.Key;
                            break;
                        }
                    }

                    // JOIN should be evaluated ater varname
                    string joinCl = null;
                    if (foreachCl.join_clause() != null)
                    {
                        nodeProperty nodePJ = getNodePropertyValue(foreachCl.join_clause());        // The second table to join that includes the join condition from the join clause
                        joinCl = nodePJ.genList[0];
                    }

                    evalItem.joinClause = joinCl;
                    evalItem.varName = varname;
                    evalItem.keywInj = keywInj;

                    evalStmtItems.Add(evalItem);
                }
            }
            else
            {
                singleEval = true;
                evalItems evalItem = new evalItems();
                keywordInjection keywInj = new keywordInjection();

                // expr is the only mandatory item in EVALUATE
                nodeProperty nodePexpr = getNodePropertyValue(context.expr());
                if (nodePexpr.fnType == BIMRLEnum.FunctionType.EXTENSION)
                {
                    nodePexpr.forExpr = nodePexpr.forExpr.Replace(BIMRLConstant.tempOp, " = ");
                    nodePexpr.forExpr = nodePexpr.forExpr.Replace(BIMRLConstant.notOp, "");
                    evalItem.externFnExpr = nodePexpr.fnExprList;
                    evalItem.externFnName = nodePexpr.fnName;
                }
                else
                {
                    if (!string.IsNullOrEmpty(nodePexpr.forExpr))
                    {
                        nodePexpr.forExpr = nodePexpr.forExpr.Replace(BIMRLConstant.tempOp, " = ");
                        nodePexpr.forExpr = nodePexpr.forExpr.Replace(BIMRLConstant.notOp, "");
                    }
                    // Add 'expr' into col projection instead for an expression on columns (without using function as the 'expr')
                    //BIMRLInterfaceCommon.appendToString(nodePexpr.forExpr, " AND ", ref keywInj.whereClInjection);
                    string evalExpr = nodePexpr.forExpr + " OUTPUT";          // Give an alias to the expression for the column name
                    BIMRLInterfaceCommon.appendToString(evalExpr, " AND ", ref keywInj.colProjInjection);

                    // append other stuffs if any
                    BIMRLInterfaceCommon.appendToString(nodePexpr.keywInj.colProjInjection, ", ", ref keywInj.colProjInjection);
                    BIMRLInterfaceCommon.appendToString(nodePexpr.keywInj.tabProjInjection, ", ", ref keywInj.tabProjInjection);
                    // At this point, function does not have any value/expr, replace the template op with the default one " = "

                    if (!string.IsNullOrEmpty(nodePexpr.keywInj.whereClInjection))
                    {
                        nodePexpr.keywInj.whereClInjection = nodePexpr.keywInj.whereClInjection.Replace(BIMRLConstant.tempOp, " = ");
                        nodePexpr.keywInj.whereClInjection = nodePexpr.keywInj.whereClInjection.Replace(BIMRLConstant.notOp, "");
                    }
                    if (!string.IsNullOrEmpty(nodePexpr.keywInj.suffixInjection))
                    {
                        nodePexpr.keywInj.suffixInjection = nodePexpr.keywInj.suffixInjection.Replace(BIMRLConstant.tempOp, " = ");
                        nodePexpr.keywInj.suffixInjection = nodePexpr.keywInj.suffixInjection.Replace(BIMRLConstant.notOp, "");
                    }
                    BIMRLInterfaceCommon.appendToString(nodePexpr.keywInj.whereClInjection, " AND ", ref keywInj.whereClInjection);
                    BIMRLInterfaceCommon.appendToString(nodePexpr.keywInj.suffixInjection, " ", ref keywInj.suffixInjection);
                    evalItem.keywInj = keywInj;
                }

                if (context.output() != null)
                {
                    varname = context.output().varname().GetText().Replace('?', ' ').TrimStart().TrimEnd();
                    // we will keep the var name into a global list. The list should be reset at entering EVALUATE
                    //evalVariables.Add(varname.ToUpper());
                }

                if (context.AGGREGATE() != null)
                    evalItem.aggregateResult = true;
                else
                    evalItem.aggregateResult = false;

                if (context.simple_id_list() != null)
                {
                    nodeProperty nodeP = getNodePropertyValue(context.simple_id_list());
                    evalItem.groupFilterCond = context.simple_id_list().GetText();
                    evalItem.groupFilterItems = nodeP.genList;
                }

                string evalTab = evalProcessTabName + varname.ToUpper();
                string setName = "SET1";
                if (context.set_clause() != null)
                {
                    //setName = context.setname().GetText();
                    nodeProperty nodeProp = getNodePropertyValue(context.set_clause());
                    setName = nodeProp.forExpr;
                }
                else
                {
                    // The EVALUATE statement does not have any set_clause defined, default will use the default setname and get the actual table from the mapping
                    setName = internalTableMapping[setName];
                }

                string joinCl = null;
                if (context.join_clause() != null)
                {
                    nodeProperty nodePJ = getNodePropertyValue(context.join_clause());
                    joinCl = nodePJ.genList[0];
                }

                evalItem.targetTab = evalTab;
                evalItem.resultTab = evalResultTabName + varname.ToUpper();

                //evalItem.setName = setName;
                //evalItem.sourceTab = checkResultTabName + setName.ToUpper();
                //evalItem.sourceTab = internalTableMapping[setName.ToUpper()];
                evalItem.sourceTab = setName;
                foreach (KeyValuePair<string, string> intTM in internalTableMapping)
                {
                    if (string.Compare(setName, intTM.Value, true) == 0)
                    {
                        evalItem.setName = intTM.Key;
                        break;
                    }
                }
                evalItem.joinClause = joinCl;
                evalItem.varName = varname;
                evalItem.keywInj = keywInj;

                evalStmtItems.Add(evalItem);
            }

            // after collecting all the relevant information for the EVALUATE statement, regardless whether it is a single EVALUATE or multiple, 
            //  perform the actual operation by inserting results into table(s)
            int foreachCnt = 0;
            foreach (evalItems evalIt in evalStmtItems)
            {
                m_queryString.Clear();

                try
                {
                    // Create eval table (all in one) without groupUniqueValues for the filter where condition to be handled by the execute fn
                    string sqlStmt = "SELECT * FROM " + evalIt.sourceTab + " " + evalIt.joinClause;
                    cmd2.CommandText = sqlStmt;
                    currStmt = sqlStmt;
                    DBQueryManager dbQ = new DBQueryManager();
                    dbQ.queryIntoTable(evalIt.targetTab, sqlStmt, createTmpTable);
                    if (!string.IsNullOrEmpty(DBQueryManager.errorMsg))
                    {
                        m_queryConsoleOutput.Push(DBQueryManager.errorMsg + "\n");
                        DBQueryManager.errorMsg = string.Empty;
                    }

                    if (debugSQLStmt)
                    {
                        m_queryConsoleOutput.Push("Evaluate Statement (Join): \n" + sqlStmt);
                    }

                    List<string> groupUniqueValues = new List<string>();

                    if (!string.IsNullOrEmpty(evalIt.groupFilterCond))
                    {
                        // Collect unique values for the grouping defined via FOREACH GROUP OF ... for use in the actual evaluate queries
                        // we need to group data from the CHECK statement into groups by the list of IDs specified here

                        sqlStmt = "SELECT UNIQUE " + evalIt.groupFilterCond + " FROM " + evalIt.sourceTab + " " + evalIt.joinClause + " ORDER BY " + evalIt.groupFilterCond;
                        cmd.CommandText = sqlStmt;
                        currStmt = sqlStmt;
                        OracleDataReader reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            string whereCond = "";
                            // Get the unique values based on the column(s) defined. These values are needed to group the rows later on
                            for (int i = 0; i < reader.FieldCount; ++i)
                            {
                                string withQuote = "";
                                object rowCol = reader.GetValue(i);
                                if (rowCol is string)
                                    withQuote = "'";
                                if (!string.IsNullOrEmpty(whereCond))
                                    whereCond += " AND ";
                                whereCond += evalIt.groupFilterItems[i] + " = " + withQuote + rowCol.ToString() + withQuote;
                            }
                            groupUniqueValues.Add(whereCond);
                        }
                        reader.Dispose();
                    }
                    else
                        groupUniqueValues.Add("");
                    // We will create the evaluate set conditions into data table using the join statement as specified against the CHECK filter set(s)
                    // We will also perform user (transient) geometry creation here as defined in the CONSTRUCT rule 
                    DataTable result = new DataTable();
                    BIMRLInterfaceCommon.appendToStringInFront("\"evT\".*", ", ", ref evalIt.keywInj.colProjInjection);
                    BIMRLInterfaceCommon.appendToStringInFront(evalIt.targetTab + " \"evT\"", ", ", ref evalIt.keywInj.tabProjInjection);

                    foreach (string groupCond in groupUniqueValues)
                    {
                        string whereCond = "";
                        BIMRLInterfaceCommon.appendToStringInFront(evalIt.keywInj.whereClInjection, " AND ", ref whereCond);
                        BIMRLInterfaceCommon.appendToStringInFront(groupCond, " AND ", ref whereCond);

                        if (!string.IsNullOrEmpty(whereCond))
                            whereCond = "WHERE " + whereCond;
                        sqlStmt = "SELECT TO_CHAR(SEQ_GEOMID.NEXTVAL) ELEMENTID," + evalIt.keywInj.colProjInjection + " FROM " + evalIt.keywInj.tabProjInjection 
                                + whereCond + " " + evalIt.keywInj.suffixInjection;
                        DataTable evalGroupDT = dbQEval.queryMultipleRows(sqlStmt);
                        if (!string.IsNullOrEmpty(DBQueryManager.errorMsg))
                        {
                            m_queryConsoleOutput.Push(DBQueryManager.errorMsg + "\n");
                            DBQueryManager.errorMsg = string.Empty;
                        }

                        if (debugSQLStmt)
                        {
                            m_queryConsoleOutput.Push("Evaluate Statement: \n" + sqlStmt);
                        }

                        // Do CONSTRUCT geometries here
                        bimrlParser.ConstructContext constrCtx = null;
                        bimrlParser.Geometry_typeContext geomCtx = null;
                        //bimrlParser.AlignmentContext alignCtx = null;
                        //bimrlParser.PlacementContext placementCtx = null;

                        bool hasConstruct = false;
                        if (singleEval && context.construct() != null)
                        {
                            constrCtx = context.construct() as bimrlParser.ConstructContext;
                            geomCtx = constrCtx.geometry_type() as bimrlParser.Geometry_typeContext;
                            hasConstruct = true;
                            constructAliases.Add(constrCtx.alias().GetText());
                        }
                        else if (!singleEval)
                        {
                            bimrlParser.Foreach_fnexprContext ffexpr = context.foreach_fnexpr().ElementAt(foreachCnt);
                            if (ffexpr.construct() != null)
                            {
                                hasConstruct = true;
                                constrCtx = ffexpr.construct() as bimrlParser.ConstructContext;
                                geomCtx = constrCtx.geometry_type() as bimrlParser.Geometry_typeContext;
                                constructAliases.Add(constrCtx.alias().GetText());
                            }
                        }

                        if (hasConstruct)
                        {
                            string constructAlias = constrCtx.alias().GetText();
                            string geomID = null;

                            // handle geomertry creation
                            if (geomCtx.LINE() != null)
                            {
                                if (geomCtx.three_position().Count > 0)
                                {
                                    List<Point3D> pList = getThreePositions(geomCtx.three_position());
                                    geomID = UserGeometryUtils.createLine(null, pList, false);
                                }
                                else
                                {
                                    string alias1 = geomCtx.alias(0).GetText();
                                    string alias2 = geomCtx.alias(1).GetText();
                                    foreach (DataRow dataRow in evalGroupDT.Rows)
                                    {
                                        string elemID = dataRow["ELEMENTID"].ToString();
                                        SdoGeometry geomAlias1 = dataRow[geomCtx.alias(0).GetText()] as SdoGeometry;
                                        SdoGeometry geomAlias2 = dataRow[geomCtx.alias(1).GetText()] as SdoGeometry;
                                        if (geomAlias1 == null || geomAlias2 == null)
                                        {
                                            // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                            throw new BIMRLInterfaceRuntimeException("Alias(es) specified for the LINE creation is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                        }
                                        Point3D p1 = new Point3D(geomAlias1.SdoPoint.XD.Value, geomAlias1.SdoPoint.YD.Value, geomAlias1.SdoPoint.ZD.Value);
                                        Point3D p2 = new Point3D(geomAlias2.SdoPoint.XD.Value, geomAlias2.SdoPoint.YD.Value, geomAlias2.SdoPoint.ZD.Value);
                                        List<Point3D> pList = new List<Point3D>();
                                        pList.Add(p1);
                                        pList.Add(p2);
                                        geomID = UserGeometryUtils.createLine(elemID, pList, false);
                                    }
                                }
                            }
                            else if (geomCtx.BOX() != null)
                            {
                                List<Point3D> pList = getThreePositions(geomCtx.three_position());
                                geomID = UserGeometryUtils.createBox3D(null, pList[0], pList[1], false);
                            }
                            else if (geomCtx.EXTRUSION() != null)
                            {
                                bimrlParser.Face_specContext faceSpecCtx = geomCtx.face_spec(0) as bimrlParser.Face_specContext;
                                Face3D theFace = null;
                                string faceAlias = null;
                                string offsetAlias = null;
                                string offsetNormalAlias = null;
                                double offsetVal = 0.0;
                                Vector3D offsetVector = null;

                                if (faceSpecCtx.DEFFACE() != null)
                                {
                                    if (faceSpecCtx.alias() != null)
                                    {
                                        faceAlias = faceSpecCtx.alias().GetText();
                                    }
                                    else // three_position
                                    {
                                        List<Point3D> pList = getThreePositions(faceSpecCtx.three_position());
                                        theFace = new Face3D(pList);
                                    }

                                    if (faceSpecCtx.offset() != null)
                                    {
                                        // The aliases specified for the creation of the EXTRUSION must be part of the CHECK-COLLECT projection
                                        if (faceSpecCtx.offset().three_position() != null)
                                        {
                                            offsetVector = getVectorThreePosition(faceSpecCtx.offset().three_position());
                                        }
                                        else if (faceSpecCtx.offset().alias() != null)
                                        {
                                            offsetAlias = faceSpecCtx.offset().alias().GetText();
                                            offsetVal = double.Parse(faceSpecCtx.offset().signed_number().GetText());
                                        }
                                        else if (faceSpecCtx.offset().normal() != null)
                                        {
                                            offsetNormalAlias = faceSpecCtx.offset().normal().alias().GetText();
                                            offsetVal = double.Parse(faceSpecCtx.offset().signed_number().GetText());
                                        }
                                    }

                                    // Ignore extend for extrusion
                                }
                                else if (faceSpecCtx.DEFPOINT() != null)
                                {
                                    throw new BIMRLInterfaceRuntimeException("A Face must be specified as the extrusion base!");
                                }

                                // Process direction
                                int signVal = 1;
                                Vector3D extrDir = null;
                                string extrDirNormalAlias = null;
                                if (geomCtx.direction().sign() != null)
                                {
                                    if (geomCtx.direction().sign().GetText() == "-")
                                        signVal = -1;
                                }
                                if (geomCtx.direction().XAXIS() != null)
                                {
                                    extrDir = new Vector3D(1, 0, 0);
                                }
                                else if (geomCtx.direction().YAXIS() != null)
                                {
                                    extrDir = new Vector3D(0, 1, 0);
                                }
                                else if (geomCtx.direction().ZAXIS() != null)
                                {
                                    extrDir = new Vector3D(0, 0, 1);
                                }
                                else if (geomCtx.direction().normal() != null)
                                {
                                    extrDirNormalAlias = geomCtx.direction().normal().alias().GetText();
                                }
                                else if (geomCtx.direction().VECTOR() != null)
                                {
                                    extrDir = getVectorThreePosition(geomCtx.direction().three_position());
                                }

                                // process extrusion extent
                                double extrusionExtent = 0.0;

                                if (evalGroupDT.Rows.Count > 0)
                                {
                                    foreach (DataRow dataRow in evalGroupDT.Rows)
                                    {
                                        string elemID = dataRow["ELEMENTID"].ToString();

                                        // process extrusion extent
                                        if (geomCtx.extrusion().arithmetic_expr() != null)
                                        {
                                            string alias1 = geomCtx.extrusion().arithmetic_expr().alias(0).GetText();
                                            double a1 = double.Parse(dataRow[alias1].ToString());
                                            string alias2 = geomCtx.extrusion().arithmetic_expr().alias(1).GetText();
                                            double a2 = double.Parse(dataRow[alias2].ToString());
                                            if (geomCtx.extrusion().arithmetic_expr().arithmetic_ops().ADDITION() != null)
                                                extrusionExtent = a1 + a2;
                                            else if (geomCtx.extrusion().arithmetic_expr().arithmetic_ops().SUBTRACT() != null)
                                                extrusionExtent = a1 - a2;
                                            else if (geomCtx.extrusion().arithmetic_expr().arithmetic_ops().DIVIDE() != null)
                                                extrusionExtent = a1 / a2;
                                            else if (geomCtx.extrusion().arithmetic_expr().arithmetic_ops().MULTIPLY() != null)
                                                extrusionExtent = a1 * a2;
                                        }
                                        else
                                            extrusionExtent = double.Parse(geomCtx.extrusion().signed_number().GetText());

                                        if (!string.IsNullOrEmpty(faceAlias))
                                        {
                                            SdoGeometry faceAliasGeom = dataRow[faceAlias] as SdoGeometry;
                                            if (faceAliasGeom == null)
                                            {
                                                // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                throw new BIMRLInterfaceRuntimeException("The alias specified for the EXTRUSION BASE creation is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                            }
                                            SDOGeomUtils.generate_Face3D(faceAliasGeom, out theFace);
                                        }
                                        if (!string.IsNullOrEmpty(offsetAlias))
                                        {
                                            SdoGeometry offsetVecGeom = dataRow[offsetAlias] as SdoGeometry;
                                            if (offsetVecGeom == null)
                                            {
                                                // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                throw new BIMRLInterfaceRuntimeException("The alias specified for the EXTRUSION BASE offset is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                            }
                                            offsetVector = new Vector3D(offsetVecGeom.SdoPoint.XD.Value, offsetVecGeom.SdoPoint.YD.Value, offsetVecGeom.SdoPoint.ZD.Value);
                                            if (offsetVal != 0.0)
                                                offsetVector = offsetVal * offsetVector;
                                            theFace = Face3D.offsetFace(theFace, offsetVector);
                                        }
                                        if (!string.IsNullOrEmpty(offsetNormalAlias))
                                        {
                                            SdoGeometry offsetNormalGeom = dataRow[offsetNormalAlias] as SdoGeometry;
                                            if (offsetNormalGeom == null)
                                            {
                                                // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                throw new BIMRLInterfaceRuntimeException("The alias specified for the EXTRUSION BASE offset is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                            }
                                            Face3D fNorm = null;
                                            SDOGeomUtils.generate_Face3D(offsetNormalGeom, out fNorm);
                                            offsetVector = fNorm.basePlane.normalVector;
                                            if (offsetVal != 0.0)
                                                offsetVector = offsetVal * offsetVector;
                                            theFace = Face3D.offsetFace(theFace, offsetVector);
                                        }
                                        if (!string.IsNullOrEmpty(extrDirNormalAlias))
                                        {
                                            SdoGeometry dirNormalGeom = dataRow[extrDirNormalAlias] as SdoGeometry;
                                            if (dirNormalGeom == null)
                                            {
                                                // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                throw new BIMRLInterfaceRuntimeException("The alias specified for the EXTRUSION Direction is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                            }
                                            Face3D fNorm = null;
                                            SDOGeomUtils.generate_Face3D(dirNormalGeom, out fNorm);
                                            extrDir = fNorm.basePlane.normalVector;
                                        }
                                        extrDir = signVal * extrDir;

                                        geomID = UserGeometryUtils.createExtrusion(elemID, theFace, extrDir, extrusionExtent, false);
                                    }
                                }
                            }
                            else if (geomCtx.BREP() != null)
                            {
                                if (geomCtx.VERTICES() != null)
                                {
                                    List<Point3D> listP = getThreePositions(geomCtx.three_position());
                                    List<List<int>> faceIdxList = new List<List<int>>();
                                    int noVertFace = 3;                             // default for triangulated faces
                                    if (geomCtx.noVertInFace() != null)
                                        noVertFace = int.Parse(geomCtx.noVertInFace().GetText());
                                    for (int i = 0; i < geomCtx.face_indexes().INT().Count; i+=noVertFace)
                                    {
                                        List<int> faceIdx = new List<int>();
                                        for (int j = 0; j < noVertFace; ++j)
                                            faceIdx.Add(int.Parse(geomCtx.face_indexes().INT(i+j).GetText()));
                                        faceIdxList.Add(faceIdx);
                                    }
                                    geomID = UserGeometryUtils.createBrep(null, listP, faceIdxList, noVertFace, false);
                                }
                                else if (geomCtx.STARTENDFACES() != null)
                                {
                                    bool[] isFace = new bool[2];
                                    object[] faceOrPoint = new object[2];
                                    string[] fOrPAlias = new string[2];
                                    string[] offsetAlias = new string[2];
                                    Vector3D[] offsetVector = new Vector3D[2];
                                    double[] offsetVal = new double[2];
                                    string[] offsetNormalAlias = new string[2];
                                    double[] extend = new double[2];
                                    int[] extendMod = new int[2];

                                    for (int i = 0; i < 2; ++i)
                                    {
                                        offsetVector[i] = new Vector3D(0, 0, 0);
                                        offsetVal[i] = 0;
                                        if (geomCtx.face_spec(i).DEFFACE() != null)
                                        {
                                            isFace[i] = true;
                                            if (geomCtx.face_spec(i).alias() != null)
                                            {
                                                fOrPAlias[i] = geomCtx.face_spec(i).alias().GetText();
                                            }
                                            else
                                            {
                                                List<Point3D> pList = getThreePositions(geomCtx.face_spec(i).three_position());
                                                faceOrPoint[i] = new Face3D(pList);
                                            }
                                        }
                                        else if (geomCtx.face_spec(i).DEFPOINT() != null)
                                        {
                                            isFace[i] = false;
                                            if (geomCtx.face_spec(i).alias() != null)
                                            {
                                                fOrPAlias[i] = geomCtx.face_spec(i).alias().GetText();
                                            }
                                            else
                                            {
                                                Point3D p = getPointThreePosition(geomCtx.face_spec(i).three_position(0));
                                                faceOrPoint[i] = p;
                                            }
                                        }

                                        if (geomCtx.face_spec(i).offset() != null)
                                        {
                                            // The aliases specified for the creation of the EXTRUSION must be part of the CHECK-COLLECT projection
                                            if (geomCtx.face_spec(i).offset().three_position() != null)
                                            {
                                                offsetVector[i] = getVectorThreePosition(geomCtx.face_spec(i).offset().three_position());
                                            }
                                            else if (geomCtx.face_spec(i).offset().alias() != null)
                                            {
                                                offsetAlias[i] = geomCtx.face_spec(i).offset().alias().GetText();
                                                offsetVal[i] = double.Parse(geomCtx.face_spec(i).offset().signed_number().GetText());
                                            }
                                            else if (geomCtx.face_spec(i).offset().normal() != null)
                                            {
                                                offsetNormalAlias[i] = geomCtx.face_spec(i).offset().normal().alias().GetText();
                                                offsetVal[i] = double.Parse(geomCtx.face_spec(i).offset().signed_number().GetText());
                                            }
                                        }

                                        if (geomCtx.face_spec(i).extend() != null)
                                        {
                                            extend[i] = int.Parse(geomCtx.face_spec(i).extend().signed_number().GetText());
                                            if (geomCtx.face_spec(i).extend().XEDGE() != null)
                                                extendMod[i] = 1;
                                            else if (geomCtx.face_spec(i).extend().YEDGE() != null)
                                                extendMod[i] = 2;
                                            else if (geomCtx.face_spec(i).extend().BOTHDIRECTION() != null)
                                                extendMod[i] = 3;
                                            else
                                                extendMod[i] = 0;
                                        }
                                    }

                                    if (evalGroupDT.Rows.Count > 0)
                                    {
                                        foreach (DataRow dataRow in evalGroupDT.Rows)
                                        {
                                            string elemID = dataRow["ELEMENTID"].ToString();
                                            for (int i = 0; i < 2; ++i)
                                            {
                                                if (!string.IsNullOrEmpty(fOrPAlias[i]))
                                                {
                                                    SdoGeometry faceAliasGeom = dataRow[fOrPAlias[i]] as SdoGeometry;
                                                    if (faceAliasGeom == null)
                                                    {
                                                        // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                        throw new BIMRLInterfaceRuntimeException("The alias specified for the EXTRUSION BASE creation is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                                    }
                                                    if (isFace[i])
                                                    {
                                                        Face3D face = null;
                                                        SDOGeomUtils.generate_Face3D(faceAliasGeom, out face);
                                                        faceOrPoint[i] = face;
                                                    }
                                                    else
                                                    {
                                                        // It is a point
                                                        Point3D point = new Point3D(faceAliasGeom.SdoPoint.XD.Value, faceAliasGeom.SdoPoint.YD.Value, faceAliasGeom.SdoPoint.ZD.Value);
                                                        faceOrPoint[i] = point;
                                                    }
                                                }

                                                // Now evaluate offset, if any
                                                if (!string.IsNullOrEmpty(offsetAlias[i]))
                                                {
                                                    SdoGeometry offsetVecGeom = dataRow[offsetAlias[i]] as SdoGeometry;
                                                    if (offsetVecGeom == null)
                                                    {
                                                        // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                        throw new BIMRLInterfaceRuntimeException("The alias specified for the BREP offset is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                                    }
                                                    offsetVector[i] = new Vector3D(offsetVecGeom.SdoPoint.XD.Value, offsetVecGeom.SdoPoint.YD.Value, offsetVecGeom.SdoPoint.ZD.Value);
                                                    if (offsetVal[i] != 0.0)
                                                        offsetVector[i] = offsetVal[i] * offsetVector[i];
                                                    if (isFace[i])
                                                        faceOrPoint[i] = Face3D.offsetFace(faceOrPoint[i] as Face3D, offsetVector[i]);
                                                    else
                                                        faceOrPoint[i] = faceOrPoint[i] as Point3D + offsetVector[i];
                                                }
                                                if (!string.IsNullOrEmpty(offsetNormalAlias[i]))
                                                {
                                                    SdoGeometry offsetNormalGeom = dataRow[offsetNormalAlias[i]] as SdoGeometry;
                                                    if (offsetNormalGeom == null)
                                                    {
                                                        // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                        throw new BIMRLInterfaceRuntimeException("The alias specified for the EXTRUSION BASE offset is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                                    }
                                                    Face3D fNorm = null;
                                                    SDOGeomUtils.generate_Face3D(offsetNormalGeom, out fNorm);
                                                    offsetVector[i] = fNorm.basePlane.normalVector;
                                                    if (offsetVal[i] != 0.0)
                                                        offsetVector[i] = offsetVal[i] * offsetVector[i];
                                                    if (isFace[i])
                                                        faceOrPoint[i] = Face3D.offsetFace(faceOrPoint[i] as Face3D, offsetVector[i]);
                                                    else
                                                        faceOrPoint[i] = faceOrPoint[i] as Point3D + offsetVector[i];
                                                }
                                            }

                                            if (faceOrPoint[0] is Face3D && faceOrPoint[1] is Face3D)
                                                geomID = UserGeometryUtils.createBrep(elemID, faceOrPoint[0] as Face3D, faceOrPoint[1] as Face3D, extend, extendMod, false);
                                            else if (faceOrPoint[0] is Point3D && faceOrPoint[1] is Face3D)
                                                geomID = UserGeometryUtils.createBrep(elemID, faceOrPoint[0] as Point3D, faceOrPoint[1] as Face3D, extend, extendMod, false);
                                            else if (faceOrPoint[0] is Face3D && faceOrPoint[1] is Point3D)
                                                geomID = UserGeometryUtils.createBrep(elemID, faceOrPoint[1] as Point3D, faceOrPoint[0] as Face3D, extend, extendMod, false);
                                            else
                                                throw new BIMRLInterfaceRuntimeException("Invalid option. createBrep() requires at least one input as a Face3D!");
                                        }
                                    }
                                }
                                else if (geomCtx.FACESET() != null)
                                {
                                    List<object> fAliases = new List<object>();
                                    List<object> offsetInfo = new List<object>();
                                    List<double> offsetVals = new List<double>();
                                    List<bool> isNormalAlias = new List<bool>();

                                    foreach (bimrlParser.Face_specContext fspec in geomCtx.face_spec())
                                    {
                                        if (fspec.DEFFACE() != null)
                                        {
                                            if (fspec.alias() != null)
                                            {
                                                fAliases.Add(fspec.alias().GetText());
                                            }
                                            else
                                            {
                                                List<Point3D> pList = getThreePositions(fspec.three_position());
                                                fAliases.Add(new Face3D(pList));
                                            }
                                        }
                                        else if (fspec.DEFPOINT() != null)
                                        {
                                            // not supported. Only collection of faces are supported in this statement
                                            // skip
                                            continue;
                                        }

                                        if (fspec.offset() != null)
                                        {
                                            // The aliases specified for the creation of the BREP must be part of the CHECK-COLLECT projection
                                            if (fspec.offset().three_position() != null)
                                            {
                                                Vector3D offsetVector = getVectorThreePosition(fspec.offset().three_position());
                                                offsetInfo.Add(offsetVector);
                                                offsetVals.Add(0.0);
                                                isNormalAlias.Add(false);
                                            }
                                            else if (fspec.offset().alias() != null)
                                            {
                                                offsetInfo.Add(fspec.offset().alias().GetText());
                                                offsetVals.Add(double.Parse(fspec.offset().signed_number().GetText()));
                                                isNormalAlias.Add(false);
                                            }
                                            else if (fspec.offset().normal() != null)
                                            {
                                                offsetInfo.Add(fspec.offset().normal().alias().GetText());
                                                offsetVals.Add(double.Parse(fspec.offset().signed_number().GetText()));
                                                isNormalAlias.Add(true);
                                            }
                                        }
                                        else
                                        {
                                            offsetInfo.Add(null);
                                            offsetVals.Add(0.0);
                                            isNormalAlias.Add(false);
                                        }

                                        // ignore extend for Brep by list of faces
                                    }

                                    if (evalGroupDT.Rows.Count > 0)
                                    {
                                        foreach (DataRow dataRow in evalGroupDT.Rows)
                                        {
                                            List<Face3D> faceList = new List<Face3D>();
                                            Face3D theFace;
                                            Vector3D theOffset;

                                            string elemID = dataRow["ELEMENTID"].ToString();

                                            for (int i = 0; i < fAliases.Count; ++i)
                                            {
                                                if (fAliases[i] is Face3D)
                                                    theFace = fAliases[i] as Face3D;
                                                else
                                                {
                                                    SdoGeometry faceAliasGeom = dataRow[fAliases[i] as string] as SdoGeometry;
                                                    if (faceAliasGeom == null)
                                                    {
                                                        // The aliases specified for the creation of the FACE must be part of the CHECK-COLLECT projection
                                                        throw new BIMRLInterfaceRuntimeException("The alias specified for the BREP creation is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                                    }
                                                    SDOGeomUtils.generate_Face3D(faceAliasGeom, out theFace);
                                                }

                                                if (offsetInfo[i] != null)
                                                {
                                                    if (offsetInfo[i] is Vector3D)
                                                        theOffset = offsetInfo[i] as Vector3D;
                                                    else
                                                    {
                                                        string offsetAlias = offsetInfo[i] as string;
                                                        SdoGeometry offset = dataRow[offsetAlias] as SdoGeometry;
                                                        if (offset == null)
                                                        {
                                                            // The aliases specified for the creation of the OFFSET must be part of the CHECK-COLLECT projection
                                                            throw new BIMRLInterfaceRuntimeException("The alias specified for the offset for the BREP creation is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                                        }

                                                        if (isNormalAlias[i])
                                                        {
                                                            // It is for normal alias
                                                            Face3D fNorm = null;
                                                            SDOGeomUtils.generate_Face3D(offset, out fNorm);
                                                            theOffset = fNorm.basePlane.normalVector;
                                                        }
                                                        else
                                                        {
                                                            // It is for the offset alias
                                                            theOffset = new Vector3D(offset.SdoPoint.XD.Value, offset.SdoPoint.YD.Value, offset.SdoPoint.ZD.Value);
                                                        }
                                                        theOffset = offsetVals[i] * theOffset;
                                                    }
                                                }
                                                else
                                                {
                                                    theOffset = new Vector3D(0, 0, 0);
                                                }

                                                theFace = Face3D.offsetFace(theFace, theOffset);
                                                faceList.Add(theFace);
                                            }
                                            geomID = UserGeometryUtils.createBrep(elemID, faceList, false);
                                        }
                                    }
                                }
                            }
                            else if (geomCtx.BREPFROMEDGE() != null)
                            {
                                bimrlParser.Face_specContext faceSpecCtx = geomCtx.face_spec(0) as bimrlParser.Face_specContext;
                                Face3D theFace = null;
                                string faceAlias = null;
                                string offsetAlias = null;
                                string offsetNormalAlias = null;
                                double offsetVal = 0.0;
                                Vector3D offsetVector = null;

                                if (faceSpecCtx.DEFFACE() != null)
                                {
                                    if (faceSpecCtx.alias() != null)
                                    {
                                        faceAlias = faceSpecCtx.alias().GetText();
                                    }
                                    else // three_position
                                    {
                                        List<Point3D> pList = getThreePositions(faceSpecCtx.three_position());
                                        theFace = new Face3D(pList);
                                    }

                                    if (faceSpecCtx.offset() != null)
                                    {
                                        // The aliases specified for the creation of the EXTRUSION must be part of the CHECK-COLLECT projection
                                        if (faceSpecCtx.offset().three_position() != null)
                                        {
                                            offsetVector = getVectorThreePosition(faceSpecCtx.offset().three_position());
                                        }
                                        else if (faceSpecCtx.offset().alias() != null)
                                        {
                                            offsetAlias = faceSpecCtx.offset().alias().GetText();
                                            offsetVal = double.Parse(faceSpecCtx.offset().signed_number().GetText());
                                        }
                                        else if (faceSpecCtx.offset().normal() != null)
                                        {
                                            offsetNormalAlias = faceSpecCtx.offset().normal().alias().GetText();
                                            offsetVal = double.Parse(faceSpecCtx.offset().signed_number().GetText());
                                        }
                                    }

                                    // Ignore extend for extrusion
                                }
                                else if (faceSpecCtx.DEFPOINT() != null)
                                {
                                    throw new BIMRLInterfaceRuntimeException("A Face must be specified as the extrusion base!");
                                }

                                // Process depth
                                double depthExtent = double.Parse(geomCtx.depth().signed_number().GetText());
                                // process extrusion extent
                                double extrusionExtent = double.Parse(geomCtx.extrusion().signed_number().GetText());

                                if (evalGroupDT.Rows.Count > 0)
                                {
                                    foreach (DataRow dataRow in evalGroupDT.Rows)
                                    {
                                        string elemID = dataRow["ELEMENTID"].ToString();
                                        if (!string.IsNullOrEmpty(faceAlias))
                                        {
                                            SdoGeometry faceAliasGeom = dataRow[faceAlias] as SdoGeometry;
                                            if (faceAliasGeom == null)
                                            {
                                                // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                throw new BIMRLInterfaceRuntimeException("The alias specified for the EXTRUSION BASE creation is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                            }
                                            SDOGeomUtils.generate_Face3D(faceAliasGeom, out theFace);
                                        }
                                        if (!string.IsNullOrEmpty(offsetAlias))
                                        {
                                            SdoGeometry offsetVecGeom = dataRow[offsetAlias] as SdoGeometry;
                                            if (offsetVecGeom == null)
                                            {
                                                // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                throw new BIMRLInterfaceRuntimeException("The alias specified for the EXTRUSION BASE offset is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                            }
                                            offsetVector = new Vector3D(offsetVecGeom.SdoPoint.XD.Value, offsetVecGeom.SdoPoint.YD.Value, offsetVecGeom.SdoPoint.ZD.Value);
                                            if (offsetVal != 0.0)
                                                offsetVector = offsetVal * offsetVector;
                                            theFace = Face3D.offsetFace(theFace, offsetVector);
                                        }
                                        if (!string.IsNullOrEmpty(offsetNormalAlias))
                                        {
                                            SdoGeometry offsetNormalGeom = dataRow[offsetNormalAlias] as SdoGeometry;
                                            if (offsetNormalGeom == null)
                                            {
                                                // The aliases specified for the creation of the LINE must be part of the CHECK-COLLECT projection
                                                throw new BIMRLInterfaceRuntimeException("The alias specified for the EXTRUSION BASE offset is not found in the CHECK - COLLECT projection or of an incorrect type!");
                                            }
                                            Face3D fNorm = null;
                                            SDOGeomUtils.generate_Face3D(offsetNormalGeom, out fNorm);
                                            offsetVector = fNorm.basePlane.normalVector;
                                            if (offsetVal != 0.0)
                                                offsetVector = offsetVal * offsetVector;
                                            theFace = Face3D.offsetFace(theFace, offsetVector);
                                        }

                                        // process segmentize option (if any)
                                        if (geomCtx.segmentize() != null)
                                        {
                                            double segementExtent = double.Parse(geomCtx.segmentize().GetText());
                                            geomID = UserGeometryUtils.createBrepFromEdge(elemID, theFace, depthExtent, extrusionExtent, segementExtent, false);
                                        }
                                        else
                                            geomID = UserGeometryUtils.createBrepFromEdge(elemID, theFace, depthExtent, extrusionExtent, null, false);
                                    }
                                }
                            }

                            // handle alignment
                            //if (constrCtx.alignment() != null)
                            //{

                            //}

                            //// handle placement
                            //if (constrCtx.placement() != null)
                            //{

                            //}
                        }

                        if (evalGroupDT != null)
                            result.Merge(evalGroupDT);      // merge result from each of the individual group into one table
                    }

                    if (!string.IsNullOrEmpty(evalIt.externFnName))
                    {
                        for (int i = 0; i < evalIt.externFnExpr.Count; ++i)
                        {
                            if (constructAliases.Contains(evalIt.externFnExpr[i]))
                                evalIt.externFnExpr[i] = "USERGEOM:ELEMENTID";
                        }
                        if (evalIt.aggregateResult)
                            evalIt.externFnExpr.Add("AGGREGATE(" + evalIt.groupFilterCond + ")");

                        if (result.Rows.Count == 0)
                            throw new BIMRLInterfaceRuntimeException("Query does not return any value for EVALUATE function!");

                        DataTable endResult = FunctionManager.executeEvalFunction(evalIt.externFnName, result, evalIt.externFnExpr.ToArray());
                        evaluateQueryResults.Add(evalIt.varName, endResult);
                        dbQEval.createTableFromDataTable(evalIt.resultTab, endResult, false, createTmpTable);
                        if (!string.IsNullOrEmpty(DBQueryManager.errorMsg))
                        {
                            m_queryConsoleOutput.Push(DBQueryManager.errorMsg + "\n");
                            DBQueryManager.errorMsg = string.Empty;
                        }
                    }
                    else 
                    {
                        evaluateQueryResults.Add(evalIt.varName, result);
                        dbQEval.createTableFromDataTable(evalIt.resultTab, result, false, createTmpTable);
                        if (!string.IsNullOrEmpty(DBQueryManager.errorMsg))
                        {
                            m_queryConsoleOutput.Push(DBQueryManager.errorMsg + "\n");
                            DBQueryManager.errorMsg = string.Empty;
                        }
                    }
                }
                catch (OracleException e)
                {
                    string excStr = "%%Error - " + e.Message + "\n" + currStmt;
                    m_BIMRLRefCommon.BIMRlErrorStack.Push(excStr);
                    BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLRefCommon);
                    erroDlg.ShowDialog();
                    cmd.Dispose();
                    cmd2.Dispose();
                    //throw;
                }

                foreachCnt++;
            }
        }

        public override void ExitJoin_clause(bimrlParser.Join_clauseContext context)
        {
            base.ExitJoin_clause(context);
            nodeProperty nodeP = new nodeProperty();

            //string setNameTab = checkResultTabName + context.setname().GetText().ToUpper();      // get the second table name from the check stmt for the join
            string joinCl = rewriter.GetText(context.SourceInterval);
            nodeProperty nodePSetCl = getNodePropertyValue(context.set_clause());

            joinCl = joinCl.Replace(" " + context.set_clause().GetText() + " ", " " + nodePSetCl.forExpr + " ");       // replace set name using " setname " to " newsetname "
            nodeP.genList.Add(joinCl);
            setNodePropertyValue(context, nodeP);
        }

        public override void ExitPrint_action(bimrlParser.Print_actionContext context)
        {
            base.ExitPrint_action(context);

        }

        public override void EnterBimrl_triplets(bimrlParser.Bimrl_tripletsContext context)
        {
            base.EnterBimrl_triplets(context);
            putBackWS = true;       // set this flag to true so that we can get back the oiriginal statement with a whitespace put back (maybe needed for all?)

            // We need to clear all these reusable items for each BIMRL_triplets
            elementIdColAdd.Clear();
            FunctionManager.Init();
            TableListManager.Init();
            ColumnListManager.Init();
            bimrlQuerySets.Clear();
            currSetInj = null;
            UserGeometryUtils.truncateUserGeomTables();
            VarsInfo tmpTab = DefinedVarsManager.getDefinedVar("CreateTempTable");
            if (tmpTab.varValue != null)
                bool.TryParse(tmpTab.varValue.ToString(), out createTmpTable);
            VarsInfo sqlDebug = DefinedVarsManager.getDefinedVar("DebugSQL");
            if (sqlDebug.varValue != null)
            {
                bool.TryParse(sqlDebug.varValue.ToString(), out debugSQLStmt);
            }
            if (internalTableMapping != null)
                internalTableMapping.Clear();
            else
                internalTableMapping = new Dictionary<string, string>();
        }

        public override void ExitBimrl_triplets(bimrlParser.Bimrl_tripletsContext context)
        {
            base.ExitBimrl_triplets(context);
            DBOperation.commitTransaction();    // Make sure we commit at the end
        }

        public override void ExitFunction(bimrlParser.FunctionContext context)
        {
            base.ExitFunction(context);

            string functionName = context.function_name().GetText();
            bool countFn = string.Compare(functionName, "COUNT", true) == 0;
            IList<string> exprStrList = new List<string>();
            if (context.expr().Count > 0 && !countFn)
            {
                IReadOnlyCollection<bimrlParser.ExprContext> exprs = context.expr();
                foreach (bimrlParser.ExprContext expr in exprs)
                {
                    string fnExpr = getNodePropertyValue(expr).forExpr;
                    DomainTableColumn dtc = new DomainTableColumn(fnExpr);
                    if (!string.IsNullOrEmpty(dtc.tableName))
                    {
                        //if (bimrlQuerySets.ContainsKey(dtc.tableName.ToUpper()))
                        //{
                        //    dtc.tableName = checkResultTabName + dtc.tableName.ToUpper();   // expand table name if it is found to be one of the query sets
                        string expTabName;
                        if (internalTableMapping.TryGetValue(dtc.tableName.ToUpper(), out expTabName))
                        {
                            dtc.tableName = expTabName;
                            // Make sure the domain is set correctly too
                            if (string.IsNullOrEmpty(dtc.domain))
                                dtc.domain = "BIMRL";   // default value set
                            fnExpr = dtc.ToString();
                        }
                        //else if (evaluateQueryResults.ContainsKey(dtc.tableName.ToUpper()))
                        //{
                        //    dtc.tableName = evalResultTabName + dtc.tableName.ToUpper();   // expand table name if it is found to be one of the query sets
                        //    // Make sure the domain is set correctly too
                        //    if (string.IsNullOrEmpty(dtc.domain))
                        //        dtc.domain = "BIMRL";   // default value set
                        //    fnExpr = dtc.ToString();

                        //}
                    }
                    exprStrList.Add(fnExpr);
                }
            }
            else 
            {
                // Handle COUNT() function specially here. We expect to have only 1 expr inside COOUNT
                if (countFn)
                {
                    string countExpr = "*";
                    if (context.expr(0) != null)
                        countExpr = context.expr(0).GetText();
                    if (context.UNIQUE() != null)
                        countExpr = "UNIQUE " + countExpr;
                    exprStrList.Add(countExpr);
                }
                //if (context.ChildCount >= 3)
                //    if (string.Compare(context.GetChild(2).GetText(), "*") == 0)
                //        exprStrList.Add(context.GetChild(2).GetText());
            }

            //functionRet ret = FunctionManager.resolveFunction(functionName, exprStrList, idListByCheck);  // will not use this flag as the interpretation does not work well with this
            functionRet ret = FunctionManager.resolveFunction(functionName, exprStrList);
            nodeProperty nodeP = new nodeProperty();
            nodeP.fnType = ret.type;
            switch (ret.type)
            {
                case BIMRLEnum.FunctionType.BUILTIN:
                    string cachedForExpr = null;
                    if (FunctionManager.checkBuiltinFnReg(context.GetText(), out cachedForExpr))
                    {
                        // The same function has been registered before (called more than once), use the cached information and do not add any keyword injection which will be repeated
                        // e.g. CLASSIFICATIONOF(E).ClassificationItemCode, CLASSIFICATIOF(E).ClassificationName. The cached data must be exactly the same function spec
                        nodeP.forExpr = cachedForExpr;
                    }
                    else
                    { 
                        nodeP.forExpr = ret.forExpr;
                        nodeP.keywInj = ret.keywordInjection;
                        FunctionManager.regBuiltinFn(context.GetText(), nodeP.forExpr);     // Register function to avoid repeating the same keyword injection if the function is called more than once
                    }
                    setNodePropertyValue(context, nodeP);
                    break;
                case BIMRLEnum.FunctionType.EXTENSION:
                    foreach (string exprStr in exprStrList)
                        BIMRLInterfaceCommon.appendToString(exprStr, ", ", ref nodeP.forExpr);
                    nodeP.keywInj = ret.keywordInjection;
                    nodeP.fnName = ret.functionName;
                    nodeP.fnExprList = exprStrList.ToList();
                    setNodePropertyValue(context, nodeP);
                    break;
                case BIMRLEnum.FunctionType.EXTERNAL:
                    foreach (string exprStr in exprStrList)
                        BIMRLInterfaceCommon.appendToString(exprStr, ", ", ref nodeP.forExpr);
                    nodeP.keywInj = ret.keywordInjection;
                    nodeP.fnName = ret.functionName;
                    nodeP.fnExprList = exprStrList.ToList();
                    setNodePropertyValue(context, nodeP);
                    break;
                case BIMRLEnum.FunctionType.INLINE:
                    // for inline function, there may be function inside the expressions
                    foreach (bimrlParser.ExprContext exprCtx in context.expr())
                    {
                        nodeProperty exprNP = getNodePropertyValue(exprCtx);
                        if (exprNP != null)
                        {
                            BIMRLInterfaceCommon.appendToString(exprNP.keywInj.colProjInjection, ", ", ref nodeP.keywInj.colProjInjection);
                            BIMRLInterfaceCommon.appendToString(exprNP.keywInj.tabProjInjection, ", ", ref nodeP.keywInj.tabProjInjection);
                            BIMRLInterfaceCommon.appendToString(exprNP.keywInj.whereClInjection, " AND ", ref nodeP.keywInj.whereClInjection);
                            BIMRLInterfaceCommon.appendToString(exprNP.keywInj.suffixInjection, " ", ref nodeP.keywInj.suffixInjection);
                        }
                    }
                    nodeP.forExpr = ret.forExpr;
                    setNodePropertyValue(context, nodeP);
                    break;
                default:
                    break;
            }
        }

        public override void EnterId_list(bimrlParser.Id_listContext context)
        {
            base.EnterId_list(context);
            if (context.parent is bimrlParser.Single_check_stmtContext)
            {
                idListByCheck = true;       // set a flag to tell that the id_list is for CHECK statement
            }
        }

        public override void ExitId_list(bimrlParser.Id_listContext context)
        {
            base.ExitId_list(context);
            nodeProperty nodeP = new nodeProperty();
            foreach (bimrlParser.Id_memberContext member in context.id_member())
            {
                nodeProperty memberNode = getNodePropertyValue(member);

                BIMRLInterfaceCommon.appendToString(memberNode.forExpr, " ", ref nodeP.forExpr);
                BIMRLInterfaceCommon.appendToString(memberNode.keywInj.tabProjInjection, ", ", ref nodeP.keywInj.tabProjInjection);
                BIMRLInterfaceCommon.appendToString(memberNode.keywInj.colProjInjection, ", ", ref nodeP.keywInj.colProjInjection);
                BIMRLInterfaceCommon.appendToString(memberNode.keywInj.whereClInjection, " AND ", ref nodeP.keywInj.whereClInjection);
                BIMRLInterfaceCommon.appendToString(memberNode.keywInj.suffixInjection, " ", ref nodeP.keywInj.suffixInjection);
            }
            setNodePropertyValue(context, nodeP);
            idListByCheck = false;      // always set this flag to false (default) at the end of id_list
        }

        public override void ExitId_member(bimrlParser.Id_memberContext context)
        {
            base.ExitId_member(context);

            nodeProperty nodeP = new nodeProperty();
            
            bool skipAlias = false;
            if (context.Parent.Parent is bimrlParser.Background_modelContext)
                skipAlias = true;

            if (context.stringliteral() != null)
            {
                bimrlParser.StringliteralContext strLit = context.stringliteral() as bimrlParser.StringliteralContext;

                // for this, we will just put the token to the appropriate projection
                if (context.Parent.Parent is bimrlParser.Check_stmtContext || context.Parent.Parent is bimrlParser.Table_listContext)
                {
                    // When it is a table name, we will check against registered "internal BIMRL" tables from CHECK statement and replace the table with the actual name
                    string tableName = strLit.GetText().ToUpper();
                    BIMRLInterfaceCommon.appendToString(tableName, ", ", ref nodeP.keywInj.tabProjInjection);
                    if (context.alias() != null)
                        BIMRLInterfaceCommon.appendToString(context.alias().GetText(), " ", ref nodeP.keywInj.tabProjInjection);
                }
                else
                {
                    BIMRLInterfaceCommon.appendToString(strLit.GetText(), ", ", ref nodeP.keywInj.colProjInjection);
                    if (context.alias() != null)
                        BIMRLInterfaceCommon.appendToString(context.alias().GetText(), " ", ref nodeP.keywInj.colProjInjection);
                }
            }
            else if (context.id_array() != null)
            {
                // currently id_array is only intended for IFC objects, it won't work with any others
                string alias = null;
                if (context.alias() != null)
                    alias = context.alias().GetText();
                string ifcIdArray = context.id_array().GetText().ToUpper(); // get the list of IFCobjects
                string ifcIdArrayMod = null;
                ifcIdArray = ifcIdArray.Replace("(", "").Replace(")", "");  // remove the brackets
                string[] ifcIdArrayStr = ifcIdArray.Split(',');
                foreach (string IfcItem in ifcIdArrayStr)
                {
                    string tmpStr;
                    tmpStr = "'" + IfcItem.Trim() + "'";
                    BIMRLInterfaceCommon.appendToString(tmpStr, ",", ref ifcIdArrayMod);
                }
                keywordInjection keywInj = processIFCElementArray(ifcIdArrayMod, alias, skipAlias);
                BIMRLInterfaceCommon.appendToString(keywInj.colProjInjection, ", ", ref nodeP.keywInj.colProjInjection);
                BIMRLInterfaceCommon.appendToString(keywInj.tabProjInjection, ", ", ref nodeP.keywInj.tabProjInjection);
                BIMRLInterfaceCommon.appendToString(keywInj.whereClInjection, " AND ", ref nodeP.keywInj.whereClInjection);
                BIMRLInterfaceCommon.appendToString(keywInj.suffixInjection, " ", ref nodeP.keywInj.suffixInjection);
            }
            else if (context.ext_id_dot_notation() != null)
            {
                keywordInjection keywInj;
                nodeProperty nodePr = getNodePropertyValue(context.ext_id_dot_notation());
                string item1 = null;
                string item2 = null;
                string[] items;

                if (!string.IsNullOrEmpty(nodePr.forExpr))
                {
                    items = nodePr.forExpr.Split('.');
                    item1 = items[0];
                    if (items.Count() > 1)
                        item2 = items[1];
                }


                if (!string.IsNullOrEmpty(nodePr.forExpr) &&
                    (context.Parent.Parent is bimrlParser.Single_check_stmtContext 
                        || context.Parent.Parent is bimrlParser.Table_listContext
                        || context.Parent.Parent is bimrlParser.Background_modelContext))
                {
                    string alias = null;

                    if (context.alias() != null)
                        alias = context.alias().GetText();

                    if (string.Compare(item1.ToUpper(), 0, "IFC", 0, 3) == 0)
                    {
                        keywInj = processIFCElement(item1, alias, skipAlias);
                        BIMRLInterfaceCommon.appendToString(keywInj.colProjInjection, ", ", ref nodeP.keywInj.colProjInjection);
                        BIMRLInterfaceCommon.appendToString(keywInj.tabProjInjection, ", ", ref nodeP.keywInj.tabProjInjection);
                        BIMRLInterfaceCommon.appendToString(keywInj.whereClInjection, " AND ", ref nodeP.keywInj.whereClInjection);
                        BIMRLInterfaceCommon.appendToString(keywInj.suffixInjection, " ", ref nodeP.keywInj.suffixInjection);
                        // table registration is done inside processIFCElement
                    }
                    else
                    {
                        TableSpec tab = new TableSpec();
                        tab.tableName = item1;
                        tab.alias = alias;
                        int idx = -1;
                        bool tabRenamed = false;

                        if (!TableListManager.checkTableAndAlias(tab.tableName, tab.alias, out idx))
                        {
                            if (internalTableMapping != null)
                            {
                                string expTabName;
                                if (internalTableMapping.TryGetValue(tab.tableName.ToUpper(), out expTabName))
                                {
                                    tab.tableName = expTabName;
                                    tabRenamed = true;
                                }
                            }
                            TableListManager.addOrUpdateMember(BIMRLEnum.Index.NEW, tab);

                            // append to projection what we get from the context below (from a function if any)
                            if (tabRenamed)
                                BIMRLInterfaceCommon.appendToString(tab.tableName, ", ", ref nodeP.keywInj.tabProjInjection);
                            else
                                BIMRLInterfaceCommon.appendToString(nodePr.forExpr, ", ", ref nodeP.keywInj.tabProjInjection);

                            if (context.alias() != null)
                                BIMRLInterfaceCommon.appendToString(context.alias().GetText(), " ", ref nodeP.keywInj.tabProjInjection);
                        }
                    }

                    BIMRLInterfaceCommon.appendToString(nodePr.keywInj.tabProjInjection, ", ", ref nodeP.keywInj.tabProjInjection);
                    BIMRLInterfaceCommon.appendToString(nodePr.keywInj.colProjInjection, ", ", ref nodeP.keywInj.colProjInjection);
                }
                else
                {
                    ColumnSpec clS = new ColumnSpec();
                    clS.item1 = item1;
                    clS.item2 = item2;
                    if (context.alias() != null)
                        clS.alias = context.alias().GetText();

                    // Add only of the column items are not yet in registration
                    if (!ColumnListManager.checkAndRegisterColumnItems(item1, item2, clS.alias))
                    {
                        // if the underlying member is a function, value will be empty and colProjection is not
                        if (!string.IsNullOrEmpty(nodePr.keywInj.colProjInjection))
                            BIMRLInterfaceCommon.appendToString(nodePr.keywInj.colProjInjection, ", ", ref nodeP.keywInj.colProjInjection);
                        else
                            BIMRLInterfaceCommon.appendToString(nodePr.forExpr, ", ", ref nodeP.keywInj.colProjInjection);

                        BIMRLInterfaceCommon.appendToString(clS.alias, " ", ref nodeP.keywInj.colProjInjection);
                    }
                    else
                    {
                        BIMRLInterfaceCommon.appendToString(nodePr.forExpr, ", ", ref nodeP.keywInj.colProjInjection);
                    }
                    BIMRLInterfaceCommon.appendToString(nodePr.keywInj.tabProjInjection, ", ", ref nodeP.keywInj.tabProjInjection);
                }

                // append to projection what we get from the context below (from a function if any)

                if (!string.IsNullOrEmpty(nodePr.keywInj.whereClInjection))
                    nodePr.keywInj.whereClInjection = nodePr.keywInj.whereClInjection.Replace(BIMRLConstant.tempOp, " = ");
                BIMRLInterfaceCommon.appendToString(nodePr.keywInj.whereClInjection, " AND ", ref nodeP.keywInj.whereClInjection);
                if (!string.IsNullOrEmpty(nodePr.keywInj.suffixInjection))
                    nodePr.keywInj.suffixInjection = nodePr.keywInj.suffixInjection.Replace(BIMRLConstant.tempOp, " = ");
                BIMRLInterfaceCommon.appendToString(nodePr.keywInj.suffixInjection, " ", ref nodeP.keywInj.suffixInjection);
            }
            setNodePropertyValue(context, nodeP);
        }
        
        public override void EnterSelect_stmt(bimrlParser.Select_stmtContext context)
        {
            base.EnterSelect_stmt(context);
            m_parsedSQLStmt = null;     // reset the string first
            collectBindVar = true;
            putBackWS = true;

            startSQLStmt = context.Start.TokenIndex;
            stopSQLStmt = context.Stop.TokenIndex;
            savedTabRegistrationFlag = TableListManager.registratioFlag;
            TableListManager.registratioFlag = false;
            savedColRegistrationFlag = ColumnListManager.registrationFlag;                   // set it to false SELECT statement
            ColumnListManager.registrationFlag = false;
        }

        public override void ExitWhere_clause(bimrlParser.Where_clauseContext context)
        {
            base.ExitWhere_clause(context);
            nodeProperty nodeP = getNodePropertyValue(context.expr());
            //nodeP.keywInj.whereClInjection = nodeP.forExpr;         // assign the expr into where clause injection since it is a where clause
            BIMRLInterfaceCommon.appendToString(nodeP.forExpr, " AND ", ref currSetInj.whereClInjection);
            BIMRLInterfaceCommon.appendToString(nodeP.keywInj.whereClInjection, " AND ", ref currSetInj.whereClInjection);
            BIMRLInterfaceCommon.appendToString(nodeP.keywInj.suffixInjection, " AND ", ref currSetInj.suffixInjection);
            BIMRLInterfaceCommon.appendToString(nodeP.keywInj.tabProjInjection, " AND ", ref currSetInj.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(nodeP.keywInj.colProjInjection, " AND ", ref currSetInj.colProjInjection);     
            setNodePropertyValue(context, nodeP);
        }

        public override void EnterExpr(bimrlParser.ExprContext context)
        {
            base.EnterExpr(context);
            //ExprHandler.validateExpr(context);
        }

        public override void ExitExpr(bimrlParser.ExprContext context)
        {
            base.ExitExpr(context);
            nodeProperty retExpr = new nodeProperty();

            if (collectBindVar)
            {
                if (context.BINDNAME() != null)
                {
                    string bindName = context.BINDNAME().GetText();
                    bindName = bindName.Replace(':', ' ').Trim();
                    registerBindVar(bindName);
                }
            }
            
            // Handling various expr "expressions"
            // : value
            if (context.GetChild(0) is bimrlParser.ValueContext && context.children.Count == 1)
                retExpr = ExprHandler.resolveExprValue(context.GetChild(0) as bimrlParser.ValueContext);
            // | ext_id_dot_notation
            else if (context.GetChild(0) is bimrlParser.Ext_id_dot_notationContext && context.children.Count == 1)
                retExpr = ExprHandler.resolveExprExtIdDotNotation(context.GetChild(0) as bimrlParser.Ext_id_dot_notationContext, getNodePropertyValue(context.GetChild(0)));
            // | VARNAME
            // | BINDNAME
            else if (context.GetChild(0) is ITerminalNode && context.children.Count == 1)
                retExpr = ExprHandler.resolveExprTerminalVarName(context.GetChild(0) as ITerminalNode);
            // | varname_with_bind
            else if (context.GetChild(0) is bimrlParser.Varname_with_bindContext && context.children.Count == 1)
                setNodePropertyValue(context, getNodePropertyValue(context.GetChild(0)));    // set value to expr node
            // | unary_operator expr
            else if (context.GetChild(0) is bimrlParser.Unary_operatorContext && context.children.Count == 2)
                retExpr = ExprHandler.resolveExprUnaryOperator(context.GetChild(0).GetText(), getNodePropertyValue(context.GetChild(1)));
            // | '(' expr ')'
            else if (context.GetChild(0) is ITerminalNode && context.children.Count == 3)
                retExpr = ExprHandler.resolveExprBracket(getNodePropertyValue(context.GetChild(1)));
            // | expr ops expr
            else if (context.GetChild(1) is bimrlParser.OpsContext && context.children.Count == 3)
            {
                string opRepl = null;
                string notRepl = null;
                retExpr = ExprHandler.resolveExprOpsExpr(context.GetChild(0) as bimrlParser.ExprContext, getNodePropertyValue(context.GetChild(0)),
                                                context.GetChild(1) as bimrlParser.OpsContext,
                                                getNodePropertyValue(context.GetChild(2)), out opRepl, out notRepl);
            }
            else if (context.GetChild(1) is bimrlParser.Conditional_exprContext && context.children.Count == 2)
                retExpr = ExprHandler.resolveConditionalExpr(getNodePropertyValue(context.GetChild(0)), getNodePropertyValue(context.GetChild(1)));
            else if (context.GetChild(0) is bimrlParser.ExistsContext && context.children.Count == 1)
                retExpr = ExprHandler.resolveExists(getNodePropertyValue(context.GetChild(0)));
            else
            {
                // Not valid rule for expr, do nothing
            }

            setNodePropertyValue(context, retExpr);
        }

        public override void ExitSingle_check_stmt(bimrlParser.Single_check_stmtContext context)
        {
 	        base.ExitSingle_check_stmt(context);

            nodeProperty nodeP = getNodePropertyValue(context.id_list());
            
            BIMRLInterfaceCommon.appendToString(nodeP.keywInj.tabProjInjection, ", ", ref currSetInj.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(nodeP.keywInj.colProjInjection, ", ", ref currSetInj.colProjInjection);
            BIMRLInterfaceCommon.appendToString(nodeP.keywInj.whereClInjection, " AND ", ref currSetInj.whereClInjection);
            BIMRLInterfaceCommon.appendToString(nodeP.keywInj.suffixInjection, " ", ref currSetInj.suffixInjection);
        
            // Check if there is COLLECT sub-clause
            if (context.collect_stmt() != null)
            {
                bimrlParser.Collect_stmtContext collectCtx = context.collect_stmt() as bimrlParser.Collect_stmtContext;
                nodeProperty nodeP2 = getNodePropertyValue(collectCtx.id_list());
                BIMRLInterfaceCommon.appendToString(nodeP2.keywInj.tabProjInjection, ", ", ref currSetInj.tabProjInjection);
                BIMRLInterfaceCommon.appendToString(nodeP2.keywInj.colProjInjection, ", ", ref currSetInj.colProjInjection);
                BIMRLInterfaceCommon.appendToString(nodeP2.keywInj.whereClInjection, " AND ", ref currSetInj.whereClInjection);
                BIMRLInterfaceCommon.appendToString(nodeP2.keywInj.suffixInjection, " ", ref currSetInj.suffixInjection);
                
                if (context.collect_stmt().group_clause() != null)
                {
                    foreach (bimrlParser.Group_clauseContext grpCl in context.collect_stmt().group_clause())
                    {
                        if (grpCl.GROUP() != null)
                        {
                            nodeProperty grNP = getNodePropertyValue(grpCl.simple_id_list());
                            string simpleIdList = "";
                            if (grNP.genList != null)
                                foreach (string id in grNP.genList)
                                    BIMRLInterfaceCommon.appendToString(id, ", ", ref simpleIdList);
                            BIMRLInterfaceCommon.appendToStringInFront("GROUP BY", " ", ref simpleIdList);
                            BIMRLInterfaceCommon.appendToString(simpleIdList, " ", ref currSetInj.suffixInjection);
                        }
                        if (grpCl.ORDER() != null)
                        {
                            nodeProperty grNP = getNodePropertyValue(grpCl.simple_id_list());
                            string simpleIdList = "";
                            if (grNP.genList != null)
                                foreach (string id in grNP.genList)
                                    BIMRLInterfaceCommon.appendToString(id, ", ", ref simpleIdList);
                            BIMRLInterfaceCommon.appendToStringInFront("ORDER BY", " ", ref simpleIdList);
                            BIMRLInterfaceCommon.appendToString(simpleIdList, " ", ref currSetInj.suffixInjection);
                        }
                    }
                }
            }

        }

        public override void ExitCheck_stmt(bimrlParser.Check_stmtContext context)
        {
            base.ExitCheck_stmt(context);

            int retCount = -1;
            // Execute queries for all check statements at the end of Check clause. The query(ies) will be inserted into tables according to the set name
            if (context.single_check_stmt() != null)
            {
                m_queryString.Clear();
                keywordInjection keywInj = bimrlQuerySets["SET1"];
                if (!string.IsNullOrEmpty(keywInj.whereClInjection))
                {
                    keywInj.whereClInjection = keywInj.whereClInjection.Replace(BIMRLConstant.tempOp, " = ");
                    keywInj.whereClInjection = keywInj.whereClInjection.Replace(BIMRLConstant.notOp, "");
                }
                if (!string.IsNullOrEmpty(keywInj.suffixInjection))
                {
                    keywInj.suffixInjection = keywInj.suffixInjection.Replace(BIMRLConstant.tempOp, " = ");
                    keywInj.suffixInjection = keywInj.suffixInjection.Replace(BIMRLConstant.notOp, "");
                }

                m_queryString.AppendFormat("SELECT {0} FROM {1} WHERE {2} {3}", keywInj.colProjInjection, keywInj.tabProjInjection, keywInj.whereClInjection, keywInj.suffixInjection);
                string sqlStmt = BIMRLKeywordMapping.expandBIMRLTables(m_queryString.ToString());       // in case some tables are not yet expanded

                DBQueryManager dbQ = new DBQueryManager();
//                retCount = dbQ.queryIntoTable(checkResultTabName + "SET1", sqlStmt, createTmpTable); 
                retCount = dbQ.queryIntoTable(internalTableMapping["SET1"], sqlStmt, createTmpTable);
                if (!string.IsNullOrEmpty(DBQueryManager.errorMsg))
                {
                    m_queryConsoleOutput.Push(DBQueryManager.errorMsg + "\n");
                    DBQueryManager.errorMsg = string.Empty;   // Reset the error message after pushing it to the console error stack
                }

                if (debugSQLStmt)
                {
                    m_queryConsoleOutput.Push("Check Statement: \n" + sqlStmt + "\n");
                }
            }
            else if (context.multi_check_stmt() != null)
            {
                
                foreach (KeyValuePair<string, keywordInjection> dictKeywInj in bimrlQuerySets)
                {
                    m_queryString.Clear();
                    keywordInjection keywInj = dictKeywInj.Value;
                    if (!string.IsNullOrEmpty(keywInj.whereClInjection))
                    {
                        keywInj.whereClInjection = keywInj.whereClInjection.Replace(BIMRLConstant.tempOp, " = ");
                        keywInj.whereClInjection = keywInj.whereClInjection.Replace(BIMRLConstant.notOp, "");
                    }
                    if (!string.IsNullOrEmpty(keywInj.suffixInjection))
                    {
                        keywInj.suffixInjection = keywInj.suffixInjection.Replace(BIMRLConstant.tempOp, " = ");
                        keywInj.suffixInjection = keywInj.suffixInjection.Replace(BIMRLConstant.notOp, "");
                    }

                    m_queryString.AppendFormat("SELECT {0} FROM {1} WHERE {2} {3}", keywInj.colProjInjection, keywInj.tabProjInjection, keywInj.whereClInjection, keywInj.suffixInjection);
                    string sqlStmt = BIMRLKeywordMapping.expandBIMRLTables(m_queryString.ToString());       // in case some tables are not yet expanded

                    DBQueryManager dbQ = new DBQueryManager();
                    //string tabName = checkResultTabName + dictKeywInj.Key ;
                    string tabName = internalTableMapping[dictKeywInj.Key];
                    retCount += dbQ.queryIntoTable(tabName, sqlStmt, createTmpTable);
                    if (!string.IsNullOrEmpty(DBQueryManager.errorMsg))
                    {
                        m_queryConsoleOutput.Push(DBQueryManager.errorMsg + "\n");
                        DBQueryManager.errorMsg = string.Empty;
                    }

                    if (debugSQLStmt)
                    {
                        m_queryConsoleOutput.Push("Check Statement: \n" + sqlStmt);
                    }
                }
            }
            if (retCount <= 0)
                throw new BIMRLInterfaceRuntimeException("At least one of the CHECK query does not find any value!");
        }

        public override void ExitConditional_expr(bimrlParser.Conditional_exprContext context)
        {
            base.ExitConditional_expr(context);
            setNodePropertyValue(context, getNodePropertyValue(context.GetChild(0)));
        }

        public override void ExitNull_condition(bimrlParser.Null_conditionContext context)
        {
            base.ExitNull_condition(context);

            nodeProperty retExpr = new nodeProperty();
            retExpr.forExpr = "IS " + (context.NOT() == null ? "" : "NOT ") + "NULL";
           
            setNodePropertyValue(context, retExpr);
        }


        public override void ExitBetween_condition(bimrlParser.Between_conditionContext context)
        {
            base.ExitBetween_condition(context);        

            nodeProperty retExpr = new nodeProperty();
            
            string notStr = null;
            int index = 0;
            if (context.NOT() != null)
            {
                notStr = "NOT ";
                index++;
            }

            nodeProperty expr1 = getNodePropertyValue(context.GetChild(index + 1));
            nodeProperty expr2 = getNodePropertyValue(context.GetChild(index + 3));

            ExprHandler.validateBetween(context, expr1, expr2);

            retExpr.forExpr = notStr + "BETWEEN " + expr1.forExpr + " AND " + expr2.forExpr;

            BIMRLInterfaceCommon.appendToString(expr1.keywInj.whereClInjection, " AND ", ref retExpr.keywInj.whereClInjection);
            BIMRLInterfaceCommon.appendToString(expr2.keywInj.whereClInjection, " AND ", ref retExpr.keywInj.whereClInjection);
            
            BIMRLInterfaceCommon.appendToString(expr1.keywInj.tabProjInjection, ", ", ref retExpr.keywInj.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(expr2.keywInj.tabProjInjection, ", ", ref retExpr.keywInj.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(expr1.keywInj.colProjInjection, ", ", ref retExpr.keywInj.colProjInjection);
            BIMRLInterfaceCommon.appendToString(expr2.keywInj.colProjInjection, ", ", ref retExpr.keywInj.colProjInjection);
            setNodePropertyValue(context, retExpr);
        }

        public override void ExitIn_condition(bimrlParser.In_conditionContext context)
        {
            base.ExitIn_condition(context);

            nodeProperty retExpr = new nodeProperty();

            string notStr = null;
            int index = 0;
            if (context.NOT() != null)
            {
                notStr = "NOT ";
                index++;
            }

            if (context.select_expr() != null)
            {
                // select_stmt rule already consolidates the complete statement in .value
                nodeProperty nodeP = getNodePropertyValue(context.select_expr());
                retExpr.forExpr = notStr + "IN (" + nodeP.forExpr + ")";
            }
            else
            {
                // expr list ONLY support values or ids at this time. No function!! 
                retExpr.forExpr = notStr + "IN (";
                IReadOnlyCollection<bimrlParser.ExprContext> exprs = context.expr();
                bool first = true;
                foreach (bimrlParser.ExprContext exprCtx in exprs)
                {
                    if (!first)
                        retExpr.forExpr += ", ";
                    retExpr.forExpr += getNodePropertyValue(exprCtx).forExpr;
                    first = false;
                }
                retExpr.forExpr += ")";
            }

            setNodePropertyValue(context, retExpr);
        }

        public override void ExitVarname_with_bind(bimrlParser.Varname_with_bindContext context)
        {
            base.ExitVarname_with_bind(context);
            string varName = context.VARNAME().GetText();
            varName = varName.Replace('?', ' ').Trim().ToUpper();
            IList<string> exprStrList = new List<string>();
            IReadOnlyCollection<bimrlParser.ExprContext> exprs = context.expr();
            nodeProperty retExpr = new nodeProperty();
            foreach (bimrlParser.ExprContext exprCtx in exprs)
            {
                nodeProperty nodeP = getNodePropertyValue(exprCtx);
                exprStrList.Add(nodeP.forExpr);
            }
            retExpr = ExprHandler.resolveExprVarnameWBind(varName, exprStrList);
            setNodePropertyValue(context, retExpr);     // set property to the node
        }

        public override void ExitSelect_stmt(bimrlParser.Select_stmtContext context)
        {
            base.ExitSelect_stmt(context);

            string distinct = null;
            if (context.UNIQUE() != null)
                distinct = "UNIQUE ";
            else if (context.DISTINCT() != null)
                distinct= "DISTINCT ";
            else if (context.ALL() != null)
                distinct = "ALL ";

            nodeProperty tabNP = getNodePropertyValue(context.table_list().id_list());
            string tabProj = tabNP.keywInj.tabProjInjection;
            string whereCl = tabNP.keywInj.whereClInjection;
            string colProj = "";

            bimrlParser.Column_listContext colCtx = context.column_list() as bimrlParser.Column_listContext;
            if (colCtx.id_list() != null)
            {
                nodeProperty colNP = getNodePropertyValue(context.column_list().id_list());
                BIMRLInterfaceCommon.appendToString(colNP.keywInj.whereClInjection, " AND ", ref whereCl);
                colProj = colNP.keywInj.colProjInjection;
            }
            else if (colCtx.all_columns() != null)
            {
                colProj = "*";
            }

            // WHERE clause may add into COLUMN and TABLE projections
            if (context.where_clause() != null)
            {
                nodeProperty whereNP = getNodePropertyValue(context.where_clause());
                BIMRLInterfaceCommon.appendToString(whereNP.keywInj.tabProjInjection, ", ", ref tabProj);
                BIMRLInterfaceCommon.appendToString(whereNP.keywInj.colProjInjection, ", ", ref colProj);
                BIMRLInterfaceCommon.appendToString(whereNP.keywInj.whereClInjection, " AND ", ref whereCl);
                BIMRLInterfaceCommon.appendToString(whereNP.forExpr, " AND ", ref whereCl);
            }

            string sqlStmt = "SELECT " + distinct + colProj + " FROM " + tabProj;
            BIMRLInterfaceCommon.appendToString(whereCl, " WHERE ", ref sqlStmt);

            nodeProperty nodeP = new nodeProperty();
            nodeP.forExpr = sqlStmt;
            setNodePropertyValue(context, nodeP);

            //m_parsedSQLStmt = sqlStmt;

            TableListManager.registratioFlag = savedTabRegistrationFlag;
            ColumnListManager.registrationFlag = savedColRegistrationFlag;

        }

        public override void ExitSelect_expr(bimrlParser.Select_exprContext context)
        {
            if (context.select_stmt() != null)
            {
                setNodePropertyValue(context, getNodePropertyValue(context.select_stmt()));
            }
            // | '(' select_expr ')'
            else if (context.GetChild(0) is ITerminalNode && context.children.Count == 3)
            {
                nodeProperty nodeP = new nodeProperty();
                nodeP.forExpr = "(" + getNodePropertyValue(context.select_expr()[0]).forExpr + ")";
                setNodePropertyValue(context, nodeP);
            }
                // select_expr (UNION (ALL)?|INTERSECT|MINUS) select_expr
            else if (context.GetChild(0) is bimrlParser.Select_exprContext && context.GetChild(1) is bimrlParser.Select_booleanContext 
                && context.GetChild(2) is bimrlParser.Select_exprContext && context.children.Count == 3)
            {
                nodeProperty nodeP = new nodeProperty();
                string boolKeyw = null;
                if (context.select_boolean().UNION() != null && context.select_boolean().ALL() != null)
                    boolKeyw = "UNION ALL";
                else if (context.select_boolean().UNION() != null)
                    boolKeyw = "UNION";                         // important!! THis has to be after UNION ALL
                else if (context.select_boolean().INTERSECT() != null)
                    boolKeyw = "INTERSECT";
                else if (context.select_boolean().MINUS() != null)
                    boolKeyw = "MINUS";

                nodeP.forExpr = getNodePropertyValue(context.select_expr()[0]).forExpr + " " + boolKeyw + " " + getNodePropertyValue(context.select_expr()[1]).forExpr;
                setNodePropertyValue(context, nodeP);
            }
            else
            {
                // Unhandled
            }
        }

        public override void ExitSimple_id_list(bimrlParser.Simple_id_listContext context)
        {
            base.ExitSimple_id_list(context);
            nodeProperty nodeProp = new nodeProperty();
            foreach (bimrlParser.Id_dotContext idD in context.id_dot())
            {
                nodeProp.genList.Add(idD.GetText());
            }
            setNodePropertyValue(context, nodeProp);
        }

        public override void ExitExt_id_dot_notation(bimrlParser.Ext_id_dot_notationContext context)
        {
            base.ExitExt_id_dot_notation(context);

            if (context.function() != null)
            {
                // at this point, function is already resolved at function rule. We just need to set additional projection if property
                nodeProperty fnOut = getNodePropertyValue(context.function());
                nodeProperty nodeP = new nodeProperty();
                string idPrefix = "";
                if (context.id(0) != null)
                    idPrefix = context.id(0).GetText();

                if (context.property() != null)
                {
                    if (!string.IsNullOrEmpty(fnOut.forExpr))    // for Builtin and inline function
                    {
                        nodeP.forExpr = fnOut.forExpr + "." + context.property().GetText();
                        BIMRLCommon.appendToStringInFront(idPrefix, ".", ref nodeP.forExpr);        // prefix the function if there is an id in front defined
                        nodeP.keywInj = fnOut.keywInj;

                        setNodePropertyValue(context, nodeP);
                    }
                    else
                    {
                        // can't find property to assign to the .property from function
                        throw new BIMRLInconsistentParsingException("%Error: Invalid property '" + context.property().GetText() + "' assignment to '" + context.GetChild(0).GetText() + "'");
                    }
                }
                else
                {
                    // when .property does not exist, rollup the information from function
                    nodeP = fnOut;
                    BIMRLCommon.appendToStringInFront(idPrefix, ".", ref nodeP.forExpr);        // prefix the function if there is an id in front defined
                    setNodePropertyValue(context, nodeP);
                }
            }
//            else if (context.GetChild(0) is bimrlParser.IdContext)
            else
            {
                // set node property to rollup (this contains the complete ext_id_dot_notation, e.g. id1.id2
                nodeProperty nodeP = new nodeProperty();
                nodeP.forExpr = context.GetText();
                setNodePropertyValue(context, nodeP);
            }
            //else
            //{
            //    nodeProperty nodeP = new nodeProperty();
            //    nodeP.forExpr = context.GetText();
            //    setNodePropertyValue(context, nodeP);
            //}
        }

        public override void EnterSql_stmt(bimrlParser.Sql_stmtContext context)
        {
            base.EnterSql_stmt(context);
            m_parsedSQLStmt = null;
        }

        public override void ExitSql_stmt(bimrlParser.Sql_stmtContext context)
        {
            base.ExitSql_stmt(context);

            m_parsedSQLStmt = context.Start.Text;   // Since the entire rule is a token, get the text directly
            m_parsedSQLStmt = m_parsedSQLStmt.Remove(0, 4);     // remove the beginning SQL
            if (m_parsedSQLStmt[m_parsedSQLStmt.Length - 1] == ';')
                m_parsedSQLStmt = m_parsedSQLStmt.Substring(0, m_parsedSQLStmt.Length - 1);

            m_parsedSQLStmt = BIMRLKeywordMapping.expandBIMRLTables(m_parsedSQLStmt);

            DBQueryManager query = new DBQueryManager();
            //if (context.SELECT() != null)
            if (string.Compare(m_parsedSQLStmt.Substring(0, 6).Trim(), "SELECT", true) == 0)
            {
                m_queryResults.Add(query.queryMultipleRows(m_parsedSQLStmt));
            }
            else
            {
                string tmpStr = null;
                int res = query.runNonQuery(m_parsedSQLStmt);
                if (string.Compare(m_parsedSQLStmt.Substring(0, 6).Trim(), "CREATE", true) == 0)
                    tmpStr = " item created.\n";
                else if (string.Compare(m_parsedSQLStmt.Substring(0, 6).Trim(), "UPDATE", true) == 0)
                    tmpStr = " rows updated.\n";
                else if (string.Compare(m_parsedSQLStmt.Substring(0, 6).Trim(), "INSERT", true) == 0)
                    tmpStr = " rows inserted.\n";
                else if (string.Compare(m_parsedSQLStmt.Substring(0, 6).Trim(), "DELETE", true) == 0)
                    tmpStr = " rows deleted.\n";
                else
                    tmpStr = " item affected\n";
                m_queryConsoleOutput.Push(res.ToString() + tmpStr);
            }
        }

        public override void ExitDelete_model(bimrlParser.Delete_modelContext context)
        {
            base.ExitDelete_model(context);

            DataTable modelInfo = null;
            BIMRLQueryModel qModel = new BIMRLQueryModel(new BIMRLCommon());

            if (context.FEDID() != null)
            {
                int fID;
                if (int.TryParse(context.INT().GetText(), out fID))
                    modelInfo = qModel.checkModelExists(fID);
            }
            else if (context.project_name_number() != null)
            {
                IReadOnlyList<bimrlParser.Project_name_numberContext> pNameNums = context.project_name_number();
                string projName = null;
                string projNumber = null;
                foreach (bimrlParser.Project_name_numberContext pNameNum in pNameNums)
                {
                    if (pNameNum.PROJECTNAME() != null)
                    {
                        projName = pNameNum.stringliteral().GetText();
                    }
                    else if (pNameNum.PROJECTNUMBER() != null)
                    {
                        projNumber = pNameNum.stringliteral().GetText();
                    }
                }
                if (string.IsNullOrEmpty(projName) || string.IsNullOrEmpty(projNumber))
                {
                    throw new BIMRLInterfaceRuntimeException("Both PROJECTNAME and PROJECNUMBER parameters must be supplied!");
                }

                modelInfo = qModel.checkModelExists(projName, projNumber);
            }

            if (modelInfo.Rows.Count == 0)
                throw new BIMRLInterfaceRuntimeException("Nothing deleted! There is no model matches the condition specified");
            if (modelInfo.Rows.Count > 1)
                throw new BIMRLInterfaceRuntimeException("Nothing deleted! There are more than one models that match the condition specified");

            foreach (DataRow row in modelInfo.Rows)
            {
                object rowdata = row["FEDERATEDID"];

                if (rowdata is int)
                {
                    qModel.deleteModel((int) rowdata);
                    break;
                }
            }
        }

        /// <summary>
        /// Checking whether in the original statement there is a whitespace on the right hand of a token. If there is returns true
        /// - useful to restore the whitespace to get the original statement (or modified one) back
        /// </summary>
        /// <param name="idx">Token index</param>
        /// <returns>true if there is a whitespace</returns>
        bool whiteSpaceOnRight(int idx)
        {
            try
            {
                IList<IToken> WSChannel = this.tokens.GetHiddenTokensToRight(idx, bimrlLexer.WHITESPACE);
                if (WSChannel != null)
                {
                    IToken ws = WSChannel.First();
                    if (ws != null)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Ignore?
                return false;
            }
        }

        //void appendToProjection(string stringToAppend, string joiningKeyword, ref StringBuilder originalString)
        //{
        //    string origStr = originalString.ToString();
        //    BIMRLInterfaceCommon.appendToString(stringToAppend, joiningKeyword, ref origStr);
        //    originalString.Clear();
        //    originalString.Append(origStr);
        //}

        //void appendToProjectionInFront(string stringToAppend, string joiningKeyword, ref StringBuilder originalString)
        //{
        //    string origStr = originalString.ToString();
        //    BIMRLInterfaceCommon.appendToStringInFront(stringToAppend, joiningKeyword, ref origStr);
        //    originalString.Clear();
        //    originalString.Append(origStr);
        //}

        keywordInjection processIFCElement(string tabName, string alias, bool skipAlias)
        {
            keywordInjection keywInj = new keywordInjection();

            if (string.Compare(tabName.ToUpper(), 0, "IFC", 0, 3) == 0)
            {
                string elColAdd = null;

                TableSpec tab = new TableSpec { tableName = "BIMRL_ELEMENT" , alias = alias, originalName = tabName };
                int tabIdx;
                if (!TableListManager.checkTableAndAlias(tab.tableName, tab.alias, out tabIdx))
                {
                    BIMRLInterfaceCommon.appendToString(tab.tableName, ", ", ref keywInj.tabProjInjection);
                    BIMRLInterfaceCommon.appendToString(tab.alias, " ", ref keywInj.tabProjInjection);
                    TableListManager.addOrUpdateMember(BIMRLEnum.Index.NEW, tab);
                }

                string item1 = "ELEMENTID";
                string item2 = null;
                int colIdx;
                if (!ColumnListManager.checkColumnItems(item1, item2, out colIdx))
                {
                    if (!string.IsNullOrEmpty(tab.alias))
                        elColAdd += tab.alias.Trim() + ".";
                    elColAdd += "ELEMENTID";
                    //if (!elementIdColAdd.Contains(elColAdd))
                    //    elementIdColAdd.Add(elColAdd);  // defer it until we know there is no GROUP BY clause
                }

                keywordInjection expEnt = new keywordInjection { colProjInjection = null, tabProjInjection = null, whereClInjection = null };
                expEnt = BIMRLKeywordMapping.expandEntity(skipAlias, tab.originalName, tab.alias);

                if (tab.alias != null)
                    TableListManager.registerAliasOnly(tab.alias);
 
                BIMRLInterfaceCommon.appendToString(expEnt.whereClInjection, " AND ", ref keywInj.whereClInjection);
            }
            else
            {
                BIMRLInterfaceCommon.appendToString(tabName, ", ", ref keywInj.tabProjInjection);
            }

            return keywInj;
        }

        keywordInjection processIFCElementArray(string IFCarray, string alias, bool skipAlias)
        {
            keywordInjection keywInj = new keywordInjection();

            {
                string elColAdd = null;

                TableSpec tab = new TableSpec { tableName = "BIMRL_ELEMENT" , alias = alias, originalName = null };
                int tabIdx;
                if (!TableListManager.checkTableAndAlias(tab.tableName, tab.alias, out tabIdx))
                {
                    BIMRLInterfaceCommon.appendToString(tab.tableName, ", ", ref keywInj.tabProjInjection);
                    BIMRLInterfaceCommon.appendToString(tab.alias, " ", ref keywInj.tabProjInjection);
                    TableListManager.addOrUpdateMember(BIMRLEnum.Index.NEW, tab);
                }

                string item1 = "ELEMENTID";
                string item2 = null;
                int colIdx;
                if (!ColumnListManager.checkColumnItems(item1, item2, out colIdx))
                {
                    if (!string.IsNullOrEmpty(tab.alias))
                        elColAdd += tab.alias.Trim() + ".";
                    elColAdd += "ELEMENTID";
                    //if (!elementIdColAdd.Contains(elColAdd))
                    //    elementIdColAdd.Add(elColAdd);  // defer it until we know there is no GROUP BY clause
                }

                keywordInjection expEnt = new keywordInjection { colProjInjection = null, tabProjInjection = null, whereClInjection = null };
                expEnt = BIMRLKeywordMapping.expandEntityArray(skipAlias, IFCarray, tab.alias);

                if (tab.alias != null)
                    TableListManager.registerAliasOnly(tab.alias);

                BIMRLInterfaceCommon.appendToString(expEnt.whereClInjection, " AND ", ref keywInj.whereClInjection);
            }

            return keywInj;
        }

        void appendKeywordInjection(keywordInjection input, ref keywordInjection original)
        {
            BIMRLInterfaceCommon.appendToString(input.colProjInjection, ", ", ref original.colProjInjection);
            BIMRLInterfaceCommon.appendToString(input.tabProjInjection, ", ", ref original.tabProjInjection);
            BIMRLInterfaceCommon.appendToString(input.whereClInjection, ", ", ref original.whereClInjection);
            BIMRLInterfaceCommon.appendToString(input.suffixInjection, " ", ref original.suffixInjection);
        }

        bool getEvaluateTables (string tabName, string fromSetname, string joinClause, string orderBy)
        {
            //string sourceTab1 = checkResultTabName + fromSetname;
            string sourceTab1 = internalTableMapping[fromSetname];

            string sqlStmt = "SELECT * FROM " + sourceTab1;
            if (!string.IsNullOrEmpty(joinClause))
                sqlStmt += " " + joinClause;
            if (!string.IsNullOrEmpty(orderBy))
                sqlStmt += " ORDER BY " + orderBy;

            DBQueryManager dbQ = new DBQueryManager();
            int stat = dbQ.queryIntoTable(tabName, sqlStmt, createTmpTable);
            if (stat >= 0)
                return true;
            else
                return false;
        }

        Point3D getPointThreePosition(bimrlParser.Three_positionContext tpCtx)
        {
            double x = double.Parse(tpCtx.signed_number(0).GetText());
            double y = double.Parse(tpCtx.signed_number(1).GetText());
            double z = double.Parse(tpCtx.signed_number(2).GetText());

            Point3D p = new Point3D(x, y, z);
            return p;
        }

        Vector3D getVectorThreePosition(bimrlParser.Three_positionContext tpCtx)
        {
            double x = double.Parse(tpCtx.signed_number(0).GetText());
            double y = double.Parse(tpCtx.signed_number(1).GetText());
            double z = double.Parse(tpCtx.signed_number(2).GetText());

            Vector3D p = new Vector3D(x, y, z);
            return p;
        }

        List<Point3D> getThreePositions(IReadOnlyCollection<bimrlParser.Three_positionContext> tpCtxs)
        {
            List<Point3D> pList = new List<Point3D>();
            // It will only process pairs and ignore the remaining if any
            for (int i = 0; i < Math.Floor(tpCtxs.Count / 2.0); ++i )
            {
                double x = double.Parse(tpCtxs.ElementAt(i).signed_number(0).GetText());
                double y = double.Parse(tpCtxs.ElementAt(i).signed_number(1).GetText());
                double z = double.Parse(tpCtxs.ElementAt(i).signed_number(2).GetText());
                Point3D p = new Point3D(x, y, z);

                pList.Add(p);
            }
            return pList;
        }

        void setRGB(int red, int green, int blue, ref ColorSpec colorSpec)
        {
            colorSpec.emissiveColorRed = red;
            colorSpec.emissiveColorGreen = green;
            colorSpec.emissiveColorBlue = blue;
        }

        void saveUserGeomTables(DBQueryManager dbQ, string targetTable, bool appendMode, string whenCond=null)
        {
            string sqlStmt = null;
            string actionWhereCond = "";
            if (!string.IsNullOrEmpty(whenCond))
                actionWhereCond = whenCond;

            string whereCondition = " WHERE ELEMENTID IN (SELECT ELEMENTID FROM " + targetTable + " " + actionWhereCond + ")";

            if (!appendMode)
            {
                sqlStmt = "DROP TABLE " + targetTable + "_GEOMETRY";
                dbQ.runNonQuery(sqlStmt, true);
                sqlStmt = "DROP TABLE " + targetTable + "_SPATIALINDEX";
                dbQ.runNonQuery(sqlStmt, true);
                sqlStmt = "DROP TABLE " + targetTable + "_TOPO_FACE";
                dbQ.runNonQuery(sqlStmt, true);
                sqlStmt = "DROP TABLE " + targetTable + "_OUTPUTDETAILS";
                dbQ.runNonQuery(sqlStmt, true);

                sqlStmt = "CREATE TABLE " + targetTable + "_GEOMETRY AS SELECT * FROM USERGEOM_GEOMETRY" + whereCondition;
                dbQ.runNonQuery(sqlStmt);
                sqlStmt = "CREATE TABLE " + targetTable + "_SPATIALINDEX AS SELECT * FROM USERGEOM_SPATIALINDEX" + whereCondition;
                dbQ.runNonQuery(sqlStmt);
                sqlStmt = "CREATE TABLE " + targetTable + "_TOPO_FACE AS SELECT * FROM USERGEOM_TOPO_FACE" + whereCondition;
                dbQ.runNonQuery(sqlStmt);
                sqlStmt = "CREATE TABLE " + targetTable + "_OUTPUTDETAILS AS SELECT * FROM USERGEOM_OUTPUTDETAILS" + whereCondition;
                dbQ.runNonQuery(sqlStmt);
            }
            else 
            {
                sqlStmt = "INSERT INTO " + targetTable + "_GEOMETRY SELECT * FROM USERGEOM_GEOMETRY" + whereCondition;
                dbQ.runNonQuery(sqlStmt);
                sqlStmt = "INSERT INTO " + targetTable + "_SPATIALINDEX SELECT * FROM USERGEOM_SPATIALINDEX" + whereCondition;
                dbQ.runNonQuery(sqlStmt);
                sqlStmt = "INSERT INTO " + targetTable + "_TOPO_FACE SELECT * FROM USERGEOM_TOPO_FACE" + whereCondition;
                dbQ.runNonQuery(sqlStmt);
                sqlStmt = "INSERT INTO " + targetTable + "_OUTPUTDETAILS SELECT * FROM USERGEOM_OUTPUTDETAILS" + whereCondition;
                dbQ.runNonQuery(sqlStmt);
            }
            DBOperation.commitTransaction();
        }

        /// <summary>
        /// Parsing the spacial format <Domain>:<TableName>.<ColumnName>
        /// </summary>
        public class DomainTableColumn
        {
            public string domain { get; set; }
            public string tableName { get; set; }
            public string columnName { get; set; }

            public DomainTableColumn(string exprStr)
            {
                string strTab = exprStr;
                string[] domainComp = exprStr.Split(':');
                if (domainComp.Count() > 1)
                {
                    domain = domainComp[0];
                    strTab = domainComp[1];
                }

                string[] tabCol = strTab.Split('.');
                if (tabCol.Count() > 1)
                {
                    tableName = tabCol[0];
                    columnName = tabCol[1];
                }
                else
                {
                    tableName = tabCol[0];
                }
            }

            public override string ToString()
            {
                string ret = "";
                if (!string.IsNullOrEmpty(domain))
                    ret += domain + ":";
                if (!string.IsNullOrEmpty(tableName))
                    ret += tableName;
                if (!string.IsNullOrEmpty(columnName))
                    ret += "." + columnName;

                return ret;
            }
        }
    }
}
