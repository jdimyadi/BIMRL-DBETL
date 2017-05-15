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
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;

namespace BIMRLInterface
{
    public class ColumnListManager
    {
        static Dictionary<int, ColumnSpec> m_IDListDict = new Dictionary<int, ColumnSpec>();
        static int m_currIndex;

        public static bool registrationFlag
        {
            get;
            set;
        }

        public static void Init()
        {
            m_IDListDict.Clear();
            m_currIndex = 0;
            registrationFlag = true;
        }

        public static int addMember(ColumnSpec columnSpec)
        {
            return addMember(BIMRLEnum.Index.NEW, columnSpec);
        }

        public static int addMember(BIMRLEnum.Index idx, ColumnSpec columnSpec)
        {
            if (!registrationFlag)          // skip add member/registration if the flag is set to false
                return m_currIndex;

            if (idx == BIMRLEnum.Index.NEW)
                m_currIndex++;

            columnSpec.item1 = BIMRLInterfaceCommon.changeUpper(columnSpec.item1);
            columnSpec.item2 = BIMRLInterfaceCommon.changeUpper(columnSpec.item2);
            columnSpec.alias = BIMRLInterfaceCommon.changeUpper(columnSpec.alias);
            ColumnSpec memberSpec;
            if (!m_IDListDict.TryGetValue(m_currIndex, out memberSpec))
            {
                m_IDListDict.Add(m_currIndex, columnSpec);
            }
            else
            {
                // Advance the index and create a new entry. memberSpec may not be unique
                m_currIndex++;
                m_IDListDict.Add(m_currIndex, columnSpec);
            }
            return m_currIndex;
        }

        /// <summary>
        /// Check and compare column item to the ones already registered
        /// </summary>
        /// <param name="item1">item1</param>
        /// <param name="item2">item2 (can be null)</param>
        /// <param name="alias">alias (can be null)</param>
        /// <param name="idx">index output</param>
        /// <returns></returns>
        public static bool checkColumnItems (string item1, string item2, string alias, out int idx)
        {
            bool item1Same = false;
            bool item2Same = false;
            bool aliasSame = false;
            idx = -1;

            item1 = BIMRLInterfaceCommon.changeUpper(item1);
            item2 = BIMRLInterfaceCommon.changeUpper(item2);
            alias = BIMRLInterfaceCommon.changeUpper(alias);

            foreach (KeyValuePair<int, ColumnSpec> colSpec in m_IDListDict)
            {
                if (string.Compare(item1, colSpec.Value.item1, true) == 0)
                    item1Same = true;

                if (!string.IsNullOrEmpty(item2))
                {
                    if (!string.IsNullOrEmpty(colSpec.Value.item2))
                    {
                        if ((string.Compare(item2, colSpec.Value.item2, true) == 0))
                            item2Same = true;
                    }
                }
                else
                {
                    // if both null, they are considred the same
                    if (string.IsNullOrEmpty(colSpec.Value.item2))
                        item2Same = true;
                }

                if (!string.IsNullOrEmpty(alias))
                {
                    if (!string.IsNullOrEmpty(colSpec.Value.alias))
                    {
                        if ((string.Compare(alias, colSpec.Value.alias, true) == 0))
                            aliasSame = true;
                    }
                }
                else
                {
                    // if both null, they are considred the same
                    if (string.IsNullOrEmpty(colSpec.Value.alias))
                        aliasSame = true;
                }

                // In case of duplicate entry (same ColumnSpec exist in the column list), only the first index will be returned
                if (item1Same && item2Same && aliasSame)
                {
                    idx = colSpec.Key;
                    return true;
                }
                // reset flag
                item1Same = false;
                item2Same = false;
                aliasSame = false;
            }

            return item1Same && item2Same && aliasSame;
        }

        public static bool checkAndRegisterColumnItems(string item1, string item2, string alias)
        {
            int idx = -1;
            if (!checkColumnItems(item1, item2, alias, out idx))
            {
                ColumnSpec clS = new ColumnSpec { item1 = item1, item2 = item2, alias = alias };
                ColumnListManager.addMember(clS);
                return false;
            }
            // If found, return true
            return true;
        }

        public static bool checkAndRegisterColumnItems (string item1, string item2)
        {
            int idx = -1;
            if (!checkColumnItems(item1, item2, out idx))
            {
                ColumnSpec clS = new ColumnSpec { item1 = item1, item2 = item2, alias = null };
                ColumnListManager.addMember(clS);
                return false;
            }
            // If found, return true
            return true;
        }

        public static bool checkColumnItems(string item1, string item2, out int index)
        {
            bool ret = checkColumnItems(item1, item2, null, out index);
            return ret;
        }
    }
}
