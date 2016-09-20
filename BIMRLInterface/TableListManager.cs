using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRLInterface
{
    public class TableListManager
    {
        static Dictionary<int, TableSpec> m_IDListDict = new Dictionary<int, TableSpec>();
        static Dictionary<string, TableSpec> m_RegisteredAliasDict = new Dictionary<string, TableSpec>();
        static int m_currIndex;

        public static bool registratioFlag
        {
            get;
            set;
        }

        public static void Init()
        {
            m_IDListDict.Clear();
            m_RegisteredAliasDict.Clear();
            m_currIndex = 0;
            registratioFlag = true;
        }

        public static void addOrUpdateMember(TableSpec tableSpec)
        {
            addOrUpdateMember(BIMRLEnum.Index.NEW, tableSpec);
        }

        public static void addOrUpdateMember(BIMRLEnum.Index idx, TableSpec tableSpec)
        {
            if (!registratioFlag)           // skip registration if set to false
                return;

            if (idx == BIMRLEnum.Index.NEW)
                m_currIndex++;
            int memberIdx = m_currIndex;

            tableSpec.tableName = BIMRLInterfaceCommon.changeUpper(tableSpec.tableName);
            tableSpec.alias = BIMRLInterfaceCommon.changeUpper(tableSpec.alias);
            tableSpec.originalName = BIMRLInterfaceCommon.changeUpper(tableSpec.originalName);

            TableSpec tmpSpec;
            if (!m_IDListDict.TryGetValue(memberIdx, out tmpSpec))
            {
                m_IDListDict.Add(memberIdx, tableSpec);
                if (!string.IsNullOrEmpty(tableSpec.alias))
                    registerAliasOnly(tableSpec.alias);           // Register alias so that we avoid conflicting/duplicate alias
            }
            else
            {
                tmpSpec.tableName = tableSpec.tableName;
                tmpSpec.alias = tableSpec.alias;
                tmpSpec.originalName = tableSpec.originalName;
            }
        }

        public static void addOrUpdateMember(string tableName, string alias, string originalName)
        {
            addOrUpdateMember(BIMRLEnum.Index.NEW, tableName, alias, originalName);
        }

        public static void addOrUpdateMember(BIMRLEnum.Index idx, string tableName, string alias, string originalName)
        {
            if (!registratioFlag)           // skip registration if set to false
                return;

            if (idx == BIMRLEnum.Index.NEW)
                m_currIndex++;
            int memberIdx = m_currIndex;

            tableName = BIMRLInterfaceCommon.changeUpper(tableName);
            alias = BIMRLInterfaceCommon.changeUpper(alias);
            originalName = BIMRLInterfaceCommon.changeUpper(originalName);

            TableSpec tmpSpec;
            if (!m_IDListDict.TryGetValue(memberIdx, out tmpSpec))
            {
                tmpSpec = new TableSpec { tableName = tableName, alias = alias, originalName = originalName };
                m_IDListDict.Add(memberIdx, tmpSpec);
                if (!string.IsNullOrEmpty(tmpSpec.alias))
                    registerAliasOnly(tmpSpec.alias);           // Register alias so that we avoid conflicting/duplicate alias
            }
            else
            {
                if (!string.IsNullOrEmpty(tableName))
                    tmpSpec.tableName = tableName;
                if (!string.IsNullOrEmpty(alias))
                {
                    tmpSpec.alias = alias;
                    registerAliasOnly(alias);           // Register alias so that we avoid conflicting/duplicate alias
                }
                if (!string.IsNullOrEmpty(originalName))
                    tmpSpec.originalName = originalName;
            }
        }

        public static bool checkTableAndAlias(string tableName, string alias, out int index)
        {
            bool tableFound = false;
            bool aliasFound = false;
            index = -1;

            tableName = BIMRLInterfaceCommon.changeUpper(tableName);
            alias = BIMRLInterfaceCommon.changeUpper(alias);

            foreach (KeyValuePair<int, TableSpec> entry in m_IDListDict)
            {
                if (string.Compare(entry.Value.tableName, tableName, true) == 0)
                    tableFound = true;
                if (!string.IsNullOrEmpty(alias))
                {
                    if (!string.IsNullOrEmpty(entry.Value.alias))
                        if (string.Compare(entry.Value.alias, alias, true) == 0)
                            aliasFound = true;
                }
                else
                {
                    if (string.IsNullOrEmpty(entry.Value.alias))
                        aliasFound = true;
                }

                // In case the same TableSpec exists more than once, this will return only the first index
                if (tableFound && aliasFound)
                {
                    index = entry.Key;
                    return tableFound && aliasFound;
                }
                // reset flags
                tableFound = false;
                aliasFound = false;
            }

            return tableFound && aliasFound;
        }

        public static void registerAliasOnly(string alias)
        {
            if (!registratioFlag)           // skip registration if set to false
                return;

            TableSpec tmpSpec = new TableSpec();
            tmpSpec.alias = BIMRLInterfaceCommon.changeUpper(alias);
            if (!m_RegisteredAliasDict.ContainsKey(alias))      // register alias and reference to the IdSpec here. The alias is global within the statement
                m_RegisteredAliasDict.Add(alias, tmpSpec);
        }

        /// <summary>
        /// This function is mainly useful for the explicitly defined aliases in the id_list of the CHECK statement, or aliases used in EVALUATE and ACTION statements 
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public static bool aliasRegistered(string alias)
        {
            return m_RegisteredAliasDict.ContainsKey(alias);
        }
    }
}
