using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xbim.ModelGeometry;
using Xbim.Common.Geometry;

namespace BIMRL.Common
{
    public class BIMRLCommon
    {
        private Dictionary<int, string> LineNoToGuidMapping = new Dictionary<int, string>();
        private Dictionary<string, int> GuidToLineNoMapping = new Dictionary<string, int>();
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


        public void resetAll()
        {
            LineNoToGuidMapping.Clear();
            GuidToLineNoMapping.Clear();
            PortToElem.Clear();
            OwnerHistory.Clear();
            ClassificationSet.Clear();
            EntityLabelList.Clear();

            _LLB.X = float.MaxValue;
            _LLB.Y = float.MaxValue;
            _LLB.Z = float.MaxValue;
            _URT.X = float.MinValue;
            _URT.Y = float.MinValue;
            _URT.Z = float.MinValue;

            _bimrlErrorStack.Clear();

        }

        public void guidLineNoMappingAdd (int lineNo, string guid)
        {
            if (LineNoToGuidMapping.ContainsKey(lineNo))
                return;
            LineNoToGuidMapping.Add(lineNo, guid);
            GuidToLineNoMapping.Add(guid, lineNo);
        }

        public string guidLineNoMapping_Getguid(int lineNo)
        {
            string tmpGuid;
            if (!(LineNoToGuidMapping.TryGetValue(lineNo, out tmpGuid)))
                return null;
            else
                return tmpGuid;
        }

        public int? getLineNoFromMapping(string guid)
        {
            int lineNo;
            if (!(GuidToLineNoMapping.TryGetValue(guid, out lineNo)))
                return null;
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

        private XbimPoint3D _LLB = new XbimPoint3D(float.MaxValue, float.MaxValue, float.MaxValue);
        private XbimPoint3D _URT = new XbimPoint3D(float.MinValue, float.MinValue, float.MinValue);
        public XbimPoint3D LLB
        {
            get { return _LLB; }
            set { _LLB = value; }
        }
        public float LLB_X
        {
            get { return _LLB.X; }
            set { _LLB.X = value; }
        }
        public float LLB_Y
        {
            get { return _LLB.Y; }
            set { _LLB.Y = value; }
        }
        public float LLB_Z
        {
            get { return _LLB.Z; }
            set { _LLB.Z = value; }
        }
        public XbimPoint3D URT
        {
            get { return _URT; }
            set { _URT = value; }
        }
        public float URT_X
        {
            get { return _URT.X; }
            set { _URT.X = value; }
        }
        public float URT_Y
        {
            get { return _URT.Y; }
            set { _URT.Y = value; }
        }
        public float URT_Z
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
    }
}
