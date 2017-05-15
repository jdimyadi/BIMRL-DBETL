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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BIMRLInterface
{
    public static class Logger
    {
        private static MemoryStream m_mStream = null;
        public static MemoryStream loggerStream
        {
            get
            {
                if (m_mStream == null) m_mStream = new MemoryStream();
                return m_mStream;
            }
        }

        public static void resetStream()
        {
            if (m_mStream != null) 
            {
                m_mStream.Dispose();
                m_mStream = null;
            }
        }

//        public static MemoryStream mStream = new MemoryStream();

        public static void writeLog(string msgText)
        {
            if (m_mStream == null) m_mStream = new MemoryStream();
            UnicodeEncoding uniEncoding = new UnicodeEncoding();

            byte[] msgString = uniEncoding.GetBytes(msgText);
            m_mStream.Write(msgString, 0, msgString.Length);
            m_mStream.Flush();
         }

        public static char[] getmStreamContent()
        {
            char[] charArray;
            UnicodeEncoding uniEncoding = new UnicodeEncoding();

            byte[] byteArray = new byte[m_mStream.Length];
            int countC = uniEncoding.GetCharCount(byteArray);
            int countB = (int) m_mStream.Length;
            m_mStream.Seek(0, SeekOrigin.Begin);
            m_mStream.Read(byteArray, 0, countB);
            charArray = new char[countC];
            uniEncoding.GetDecoder().GetChars(byteArray, 0, countB, charArray, 0);

            return charArray;
        }
    }
}
