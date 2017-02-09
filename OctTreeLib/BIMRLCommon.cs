using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BIMRL.OctreeLib
{
    public class BIMRLCommon
    {
        private Dictionary<Tuple<int,int>, string> LineNoToGuidMapping = new Dictionary<Tuple<int,int>,string>();
        private Dictionary<string, Tuple<int, int>> GuidToLineNoMapping = new Dictionary<string, Tuple<int, int>>();
        private Dictionary<string, Dictionary<string, string>> PortToElem = new Dictionary<string, Dictionary<string,string>>();
        private HashSet<Tuple<int,int>> OwnerHistory = new HashSet<Tuple<int,int>>();
        private HashSet<Tuple<string, string>> ClassificationSet = new HashSet<Tuple<string, string>>();
        private HashSet<int> EntityLabelList = new HashSet<int>();

        private Stack<string> _bimrlErrorStack = new Stack<string>();

        public BIMRLCommon()
        {
            resetAll();
        }

        public Stack<string> BIMRlErrorStack
        {
            get { return _bimrlErrorStack; }
        }

        public string ErrorMessages
        {
            get
            {
                string errMsg = "";
                foreach (string msg in _bimrlErrorStack)
                {
                    errMsg += msg + "\n";
                }
                return errMsg;
            }
        }

        public void resetAll()
        {
            ClearDicts();
            ClassificationSet.Clear();

            _LLB.X = float.MaxValue;
            _LLB.Y = float.MaxValue;
            _LLB.Z = float.MaxValue;
            _URT.X = float.MinValue;
            _URT.Y = float.MinValue;
            _URT.Z = float.MinValue;

            _bimrlErrorStack.Clear();

        }

      public void ClearDicts()
      {
         LineNoToGuidMapping.Clear();
         GuidToLineNoMapping.Clear();
         PortToElem.Clear();
         OwnerHistory.Clear();
         EntityLabelList.Clear();
      }

        public void guidLineNoMappingAdd (int modelId, int lineNo, string guid)
        {
            Tuple<int,int> lineNoKey = new Tuple<int,int>(modelId,lineNo);
            if (LineNoToGuidMapping.ContainsKey(lineNoKey))
                return;
            LineNoToGuidMapping.Add(lineNoKey, guid);
            GuidToLineNoMapping.Add(guid, lineNoKey);
        }

        public string guidLineNoMapping_Getguid(int modelId, int lineNo)
        {
            Tuple<int, int> lineNoKey = new Tuple<int, int>(modelId, lineNo);
            string tmpGuid;
            if (!(LineNoToGuidMapping.TryGetValue(lineNoKey, out tmpGuid)))
                return null;
            else
                return tmpGuid;
        }

        /// <summary>
        /// GUID is unique anyway and therefore the Tuple will also be unique even though the ModelId may not be unique, the line no is good enough for the return value
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public int? getLineNoFromMapping(string guid)
        {
            Tuple<int, int> lineNoKey;
            if (!(GuidToLineNoMapping.TryGetValue(guid, out lineNoKey)))
                return null;
            int modelId = lineNoKey.Item1;
            int lineNo = lineNoKey.Item2;
            return lineNo;
        }

        public void PortToElemAdd(string portGuid, Dictionary<string,string> portElemdata)
        {
            if (PortToElem.ContainsKey(portGuid))
                return;
            PortToElem.Add(portGuid, portElemdata);
        }

        public Dictionary<string,string> PortToElem_GetValue(string portGuid)
        {
            Dictionary<string,string> portElemVal = new Dictionary<string,string>();
            if (!(PortToElem.TryGetValue(portGuid, out portElemVal)))
                return null;
            return portElemVal;
        }

        private Point3D _LLB = new Point3D(float.MaxValue, float.MaxValue, float.MaxValue);
        private Point3D _URT = new Point3D(float.MinValue, float.MinValue, float.MinValue);
        public Point3D LLB
        {
            get { return _LLB; }
            set { _LLB = value; }
        }
        public double LLB_X
        {
            get { return _LLB.X; }
            set { _LLB.X = value; }
        }
        public double LLB_Y
        {
            get { return _LLB.Y; }
            set { _LLB.Y = value; }
        }
        public double LLB_Z
        {
            get { return _LLB.Z; }
            set { _LLB.Z = value; }
        }
        public Point3D URT
        {
            get { return _URT; }
            set { _URT = value; }
        }
        public double URT_X
        {
            get { return _URT.X; }
            set { _URT.X = value; }
        }
        public double URT_Y
        {
            get { return _URT.Y; }
            set { _URT.Y = value; }
        }
        public double URT_Z
        {
            get { return _URT.Z; }
            set { _URT.Z = value; }
        }

        public void OwnerHistoryAdd(Tuple<int,int> id)
        {
            if (!OwnerHistory.Contains(id))
               OwnerHistory.Add(id);
        }

        public bool OwnerHistoryExist(Tuple<int,int> id)
        {
            return OwnerHistory.Contains(id);
        }

        public void ClassificationSetAdd(Tuple<string, string> id)
        {
            if (!ClassificationSet.Contains(id))
                ClassificationSet.Add(id);
        }

        public bool ClassificationSetExist(Tuple<string, string> id)
        {
            return ClassificationSet.Contains(id);
        }

        public HashSet<int> insEntityLabelList
        {
            get { return EntityLabelList; }
        }

        public void insEntityLabelListAdd(int label)
        {
            EntityLabelList.Add(label);
        }

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
    }
}
