using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Xbim.IO;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.SharedBldgElements;
using Xbim.Ifc2x3.SharedBldgServiceElements;
using Xbim.Ifc2x3.StructuralElementsDomain;
using Xbim.Ifc2x3.StructuralAnalysisDomain;
using Xbim.Ifc2x3.SharedComponentElements;
using Xbim.Ifc2x3.UtilityResource;
using Xbim.XbimExtensions;
using Xbim.XbimExtensions.Interfaces;
using Xbim.Ifc2x3.Extensions;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Xbim.Common.Exceptions;
using System.Diagnostics;
using Xbim.Ifc2x3.ActorResource;
using Xbim.Ifc2x3.PropertyResource;
using Xbim.Ifc2x3.ExternalReferenceResource;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.OctreeLib;
using Xbim.Ifc2x3.QuantityResource;
using Xbim.Ifc2x3.MeasureResource;

namespace BIMRL
{
    public class BIMRLProperties
    {
        BIMRLCommon _refBIMRLCommon;

        public BIMRLProperties(BIMRLCommon refBIMRLCommon)
        {
            _refBIMRLCommon = refBIMRLCommon;
        }

        public void processTypeProperties(IfcTypeProduct typ)
        {
            processProperties(typ.GlobalId.ToString(), typ.GetAllPropertySets(), "BIMRL_TYPEPROPERTIES");

            IEnumerable<IfcPropertySetDefinition> psets = typ.HasPropertySets;
            if (psets != null)
            {
                List<IfcPropertySetDefinition> psetDefs = new List<IfcPropertySetDefinition>();
                foreach (IfcPropertySetDefinition p in psets)
                {
                    if (p is IfcDoorLiningProperties || p is IfcDoorPanelProperties
                        || p is IfcWindowLiningProperties || p is IfcWindowPanelProperties)
                    {
                        psetDefs.Add(p);
                    }
                }
                if (psetDefs.Count > 0)
                    processPropertyDefinitions(typ.GlobalId.ToString(), psetDefs, "BIMRL_TYPEPROPERTIES");
            }
        }

        public void processElemProperties(IfcObject el)
        {
            processProperties(el.GlobalId.ToString(), el.GetAllPropertySets(), "BIMRL_ELEMENTPROPERTIES");

            // Now process Property set definitions attachd to the object via IsDefinedByProperties for special properties
            IEnumerable<IfcRelDefinesByProperties> relDProp = el.IsDefinedByProperties;
            List<IfcPropertySetDefinition> psetDefs = new List<IfcPropertySetDefinition>();
            if (relDProp != null)
            {
                foreach (IfcRelDefinesByProperties p in relDProp)
                {
                    if (p.RelatingPropertyDefinition is IfcDoorLiningProperties || p.RelatingPropertyDefinition is IfcDoorPanelProperties
                        || p.RelatingPropertyDefinition is IfcWindowLiningProperties || p.RelatingPropertyDefinition is IfcWindowPanelProperties
                        || p.RelatingPropertyDefinition is IfcElementQuantity)
                    {
                        psetDefs.Add(p.RelatingPropertyDefinition);
                    }
                }
                if (psetDefs.Count > 0)
                    processPropertyDefinitions(el.GlobalId.ToString(), psetDefs, "BIMRL_ELEMENTPROPERTIES");
            }
        }

        private void processPropertyDefinitions(string guid, IEnumerable<IfcPropertySetDefinition> psdefs, string tableName)
        {
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

            string SqlStmt = "Insert into " + tableName + "_" + bimrlProcessModel.currFedID.ToString("X4") + "(ElementId, PropertyGroupName, PropertyName, PropertyValue, PropertyDataType"
             + ", PropertyUnit) Values (:1, :2, :3, :4, :5, :6)";
            command.CommandText = SqlStmt;
            string currStep = SqlStmt;

            OracleParameter[] Param = new OracleParameter[6];
            for (int i = 0; i < 6; i++)
            {
                Param[i] = command.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
                Param[i].Direction = ParameterDirection.Input;
            }

            List<string> arrEleGuid = new List<string>();
            List<string> arrPGrpName = new List<string>();
            List<string> arrPropName = new List<string>();
            List<string> arrPropVal = new List<string>();
            List<OracleParameterStatus> arrPropValBS = new List<OracleParameterStatus>();
            List<string> arrPDatatyp = new List<string>();
            List<string> arrPUnit = new List<string>();
            List<OracleParameterStatus> arrPUnitBS = new List<OracleParameterStatus>();

            foreach (IfcPropertySetDefinition p in psdefs)
            {
                if (p is IfcDoorLiningProperties)
                {
                    IfcDoorLiningProperties dl = p as IfcDoorLiningProperties;

                    if (dl.LiningDepth != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("LININGDEPTH");
                        arrPropVal.Add(dl.LiningDepth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dl.LiningDepth.GetType().Name.ToUpper());
                    }
                    else if (dl.LiningThickness != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("LININGTHICKNESS");
                        arrPropVal.Add(dl.LiningThickness.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dl.LiningThickness.GetType().Name.ToUpper());
                    }
                    else if (dl.ThresholdDepth != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("THRESHOLDDEPTH");
                        arrPropVal.Add(dl.ThresholdDepth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dl.ThresholdDepth.GetType().Name.ToUpper());
                    }
                    else if (dl.ThresholdThickness != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("THRESHOLDTHICKNESS");
                        arrPropVal.Add(dl.ThresholdThickness.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dl.ThresholdThickness.GetType().Name.ToUpper());
                    }
                    else if (dl.TransomThickness != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("TRANSOMTHICKNESS");
                        arrPropVal.Add(dl.TransomThickness.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dl.TransomThickness.GetType().Name.ToUpper());
                    }
                    else if (dl.TransomOffset != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("TRANSOMOFFSET");
                        arrPropVal.Add(dl.TransomOffset.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dl.TransomOffset.GetType().Name.ToUpper());
                    }
                    else if (dl.CasingThickness != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("CASINGTHICKNESS");
                        arrPropVal.Add(dl.CasingThickness.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dl.CasingThickness.GetType().Name.ToUpper());
                    }
                    else if (dl.CasingDepth != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("CASINGDEPTH");
                        arrPropVal.Add(dl.CasingDepth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dl.CasingDepth.GetType().Name.ToUpper());
                    }
                    else if (dl.ShapeAspectStyle != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("SHAPEASPECTSTYLE");
                        arrPropVal.Add(dl.ShapeAspectStyle.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dl.ShapeAspectStyle.GetType().Name.ToUpper());
                    }
                }
                else if (p is IfcDoorPanelProperties)
                {
                    IfcDoorPanelProperties dp = p as IfcDoorPanelProperties;

                    if (dp.PanelDepth != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORPANELPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("PANELDEPTH");
                        arrPropVal.Add(dp.PanelDepth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dp.PanelDepth.GetType().Name.ToUpper());
                    }
                    if (dp.PanelWidth != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORPANELPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("PANELWIDTH");
                        arrPropVal.Add(dp.PanelWidth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dp.PanelWidth.GetType().Name.ToUpper());
                    }
                    else if (dp.ShapeAspectStyle != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCDOORPANELPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("SHAPEASPECTSTYLE");
                        arrPropVal.Add(dp.ShapeAspectStyle.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(dp.ShapeAspectStyle.GetType().Name.ToUpper());
                    }
                    arrEleGuid.Add(guid);
                    arrPGrpName.Add("IFCDOORPANELPROPERTIES");
                    arrPUnit.Add(string.Empty);
                    arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    arrPropName.Add("PANELOPERATION");
                    arrPropVal.Add(dp.PanelOperation.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPDatatyp.Add(dp.PanelOperation.GetType().Name.ToUpper());

                    arrEleGuid.Add(guid);
                    arrPGrpName.Add("IFCDOORPANELPROPERTIES");
                    arrPUnit.Add(string.Empty);
                    arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    arrPropName.Add("PANELPOSITION");
                    arrPropVal.Add(dp.PanelPosition.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPDatatyp.Add(dp.PanelPosition.GetType().Name.ToUpper());
                }
                
                if (p is IfcWindowLiningProperties)
                {
                    IfcWindowLiningProperties wl = p as IfcWindowLiningProperties;

                    if (wl.LiningDepth != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("LININGDEPTH");
                        arrPropVal.Add(wl.LiningDepth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wl.LiningDepth.GetType().Name.ToUpper());
                    }
                    else if (wl.LiningThickness != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("LININGTHICKNESS");
                        arrPropVal.Add(wl.LiningThickness.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wl.LiningThickness.GetType().Name.ToUpper());
                    }
                    else if (wl.TransomThickness != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("TRANSOMTHICKNESS");
                        arrPropVal.Add(wl.TransomThickness.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wl.TransomThickness.GetType().Name.ToUpper());
                    }
                    else if (wl.MullionThickness != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("MULLIONTHICKNESS");
                        arrPropVal.Add(wl.MullionThickness.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wl.MullionThickness.GetType().Name.ToUpper());
                    }
                    else if (wl.FirstTransomOffset != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("FIRSTTRANSOMOFFSET");
                        arrPropVal.Add(wl.FirstTransomOffset.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wl.FirstTransomOffset.GetType().Name.ToUpper());
                    }
                    else if (wl.SecondTransomOffset != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("SECONDTRANSOMOFFSET");
                        arrPropVal.Add(wl.SecondTransomOffset.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wl.SecondTransomOffset.GetType().Name.ToUpper());
                    }
                    else if (wl.FirstMullionOffset != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("FIRSTMULLIONOFFSET");
                        arrPropVal.Add(wl.FirstMullionOffset.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wl.FirstMullionOffset.GetType().Name.ToUpper());
                    }
                    else if (wl.SecondMullionOffset != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("SECONDMULLIONOFFSET");
                        arrPropVal.Add(wl.SecondMullionOffset.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wl.SecondMullionOffset.GetType().Name.ToUpper());
                    }
                    else if (wl.ShapeAspectStyle != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWLININGPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("SHAPEASPECTSTYLE");
                        arrPropVal.Add(wl.ShapeAspectStyle.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wl.ShapeAspectStyle.GetType().Name.ToUpper());
                    }
                }
                else if (p is IfcWindowPanelProperties)
                {
                    IfcWindowPanelProperties wp = p as IfcWindowPanelProperties;

                    if (wp.FrameDepth != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWPANELPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("FRAMEDEPTH");
                        arrPropVal.Add(wp.FrameDepth.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wp.FrameDepth.GetType().Name.ToUpper());
                    }
                    if (wp.FrameThickness != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWPANELPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("PANELWIDTH");
                        arrPropVal.Add(wp.FrameThickness.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wp.FrameThickness.GetType().Name.ToUpper());
                    }
                    else if (wp.ShapeAspectStyle != null)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add("IFCWINDOWPANELPROPERTIES");
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        arrPropName.Add("SHAPEASPECTSTYLE");
                        arrPropVal.Add(wp.ShapeAspectStyle.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        arrPDatatyp.Add(wp.ShapeAspectStyle.GetType().Name.ToUpper());
                    }
                    arrEleGuid.Add(guid);
                    arrPGrpName.Add("IFCWINDOWPANELPROPERTIES");
                    arrPUnit.Add(string.Empty);
                    arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    arrPropName.Add("OPERATIONTYPE");
                    arrPropVal.Add(wp.OperationType.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPDatatyp.Add(wp.OperationType.GetType().Name.ToUpper());

                    arrEleGuid.Add(guid);
                    arrPGrpName.Add("IFCWINDOWPANELPROPERTIES");
                    arrPUnit.Add(string.Empty);
                    arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                    arrPropName.Add("PANELPOSITION");
                    arrPropVal.Add(wp.PanelPosition.ToString());
                    arrPropValBS.Add(OracleParameterStatus.Success);
                    arrPDatatyp.Add(wp.PanelPosition.GetType().Name.ToUpper());
                }

                if (p is IfcElementQuantity)
                {
                    // Currently will ONLY support IfcPhysicalSimpleQuantity
                    IfcElementQuantity elq = p as IfcElementQuantity;
                    string pGrpName = "IFCELEMENTQUANTITY"; // Default name
                    if (!string.IsNullOrEmpty(elq.Name))
                        pGrpName = elq.Name;

                    foreach (IfcPhysicalQuantity pQ in elq.Quantities)
                    {
                        if (pQ is IfcPhysicalSimpleQuantity)
                        {
                            arrEleGuid.Add(guid);
                            arrPGrpName.Add(pGrpName);
                            arrPDatatyp.Add(pQ.GetType().Name.ToUpper());
                            if (!string.IsNullOrEmpty(pQ.Name))
                                arrPropName.Add(pQ.Name);
                            else
                                arrPropName.Add(pQ.GetType().Name.ToUpper());       // Set default to the type if name is not defined
                            if (((IfcPhysicalSimpleQuantity)pQ).Unit != null)
                            {
                                IfcPhysicalSimpleQuantity pQSimple = pQ as IfcPhysicalSimpleQuantity;
                                if (pQSimple.Unit is IfcContextDependentUnit)
                                {
                                    IfcContextDependentUnit unit = pQSimple.Unit as IfcContextDependentUnit;
                                    arrPUnit.Add(unit.Name);
                                }
                                else if (pQSimple.Unit is IfcConversionBasedUnit)
                                {
                                    IfcConversionBasedUnit unit = pQSimple.Unit as IfcConversionBasedUnit;
                                    arrPUnit.Add(unit.Name);
                                }
                                else if (pQSimple.Unit is IfcSIUnit)
                                {
                                    IfcSIUnit unit = pQSimple.Unit as IfcSIUnit;
                                    arrPUnit.Add(unit.Name.ToString());
                                }
                                arrPUnitBS.Add(OracleParameterStatus.Success);
                            }
                            else
                            {
                                arrPUnit.Add(string.Empty);
                                arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                            }

                            if (pQ is IfcQuantityLength)
                            {
                                IfcQuantityLength quant = pQ as IfcQuantityLength;
                                arrPropVal.Add(quant.LengthValue.ToString());
                                arrPropValBS.Add(OracleParameterStatus.Success);
                            }
                            else if (pQ is IfcQuantityArea)
                            {
                                IfcQuantityArea quant = pQ as IfcQuantityArea;
                                arrPropVal.Add(quant.AreaValue.ToString());
                                arrPropValBS.Add(OracleParameterStatus.Success);
                            }
                            else if (pQ is IfcQuantityVolume)
                            {
                                IfcQuantityVolume quant = pQ as IfcQuantityVolume;
                                arrPropVal.Add(quant.VolumeValue.ToString());
                                arrPropValBS.Add(OracleParameterStatus.Success);
                            }
                            else if (pQ is IfcQuantityCount)
                            {
                                IfcQuantityCount quant = pQ as IfcQuantityCount;
                                arrPropVal.Add(quant.CountValue.ToString());
                                arrPropValBS.Add(OracleParameterStatus.Success);
                            }
                            else if (pQ is IfcQuantityWeight)
                            {
                                IfcQuantityWeight quant = pQ as IfcQuantityWeight;
                                arrPropVal.Add(quant.WeightValue.ToString());
                                arrPropValBS.Add(OracleParameterStatus.Success);
                            }
                            else if (pQ is IfcQuantityTime)
                            {
                                IfcQuantityTime quant = pQ as IfcQuantityTime;
                                arrPropVal.Add(quant.TimeValue.ToString());
                                arrPropValBS.Add(OracleParameterStatus.Success);
                            }
                        }
                        else if (pQ is IfcPhysicalComplexQuantity)
                        {
                            // Not handled yet
                        }
                    }
                }

                if (arrEleGuid.Count >= DBOperation.commitInterval)
                {

                    Param[0].Value = arrEleGuid.ToArray();
                    Param[0].Size = arrEleGuid.Count;
                    Param[1].Value = arrPGrpName.ToArray();
                    Param[1].Size = arrPGrpName.Count;
                    Param[2].Value = arrPropName.ToArray();
                    Param[2].Size = arrPropName.Count;
                    Param[3].Value = arrPropVal.ToArray();
                    Param[3].Size = arrPropVal.Count;
                    Param[3].ArrayBindStatus = arrPropValBS.ToArray();
                    Param[4].Value = arrPDatatyp.ToArray();
                    Param[4].Size = arrPDatatyp.Count;

                    try
                    {
                        command.ArrayBindCount = arrEleGuid.Count;    // No of values in the array to be inserted
                        int commandStatus = command.ExecuteNonQuery();
                        DBOperation.commitTransaction();
                        arrEleGuid.Clear();
                        arrPGrpName.Clear();
                        arrPropName.Clear();
                        arrPropVal.Clear();
                        arrPropValBS.Clear();
                        arrPDatatyp.Clear();
                    }
                    catch (OracleException e)
                    {
                        string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + currStep;
                        _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                        // Ignore any error
                        arrEleGuid.Clear();
                        arrPGrpName.Clear();
                        arrPropName.Clear();
                        arrPropVal.Clear();
                        arrPropValBS.Clear();
                        arrPDatatyp.Clear();
                    }
                    catch (SystemException e)
                    {
                        string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                        _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                        throw;
                    }
                }
            }

            if (arrEleGuid.Count > 0)
            {

                Param[0].Value = arrEleGuid.ToArray();
                Param[0].Size = arrEleGuid.Count;
                Param[1].Value = arrPGrpName.ToArray();
                Param[1].Size = arrPGrpName.Count;
                Param[2].Value = arrPropName.ToArray();
                Param[2].Size = arrPropName.Count;
                Param[3].Value = arrPropVal.ToArray();
                Param[3].Size = arrPropVal.Count;
                Param[3].ArrayBindStatus = arrPropValBS.ToArray();
                Param[4].Value = arrPDatatyp.ToArray();
                Param[4].Size = arrPDatatyp.Count;

                try
                {
                    command.ArrayBindCount = arrEleGuid.Count;    // No of values in the array to be inserted
                    int commandStatus = command.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                }
                catch (OracleException e)
                {
                    string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + currStep;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    // Ignore any error
                }
                catch (SystemException e)
                {
                    string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    throw;
                }
            }

            DBOperation.commitTransaction();
            command.Dispose();
        }

        /// <summary>
        /// Process all properties
        /// </summary>
        /// <param name="el"></param>
        private void processProperties(string guid, IEnumerable<IfcPropertySet> elPsets, string tableName)
        {
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

            string SqlStmt = "Insert into " + tableName + "_" + bimrlProcessModel.currFedID.ToString("X4") + "(ElementId, PropertyGroupName, PropertyName, PropertyValue, PropertyDataType"
             + ", PropertyUnit) Values (:1, :2, :3, :4, :5, :6)";
            command.CommandText = SqlStmt;
            string currStep = SqlStmt;

            OracleParameter[] Param = new OracleParameter[6];
            for (int i = 0; i < 6; i++)
            {
                Param[i] = command.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
                Param[i].Direction = ParameterDirection.Input;
            }

            List<string> arrEleGuid = new List<string>();
            List<string> arrPGrpName = new List<string>();
            List<string> arrPropName = new List<string>();
            List<string> arrPropVal = new List<string>();
            List<OracleParameterStatus> arrPropValBS = new List<OracleParameterStatus>();
            List<string> arrPDatatyp = new List<string>();
            List<string> arrPUnit = new List<string>();
            List<OracleParameterStatus> arrPUnitBS = new List<OracleParameterStatus>();

            // IEnumerable<IfcPropertySet> elPsets = el.PropertySets;
            foreach (IfcPropertySet pset in elPsets)
            {
                IEnumerable<IfcProperty> props = pset.HasProperties;
                foreach (IfcProperty prop in props)
                {
                    if (prop is IfcSimpleProperty)
                    {
                        arrEleGuid.Add(guid);
                        arrPGrpName.Add(pset.Name);
                        string[] propStr = new string[4];

                        processSimpleProperty(prop, out propStr);
                        arrPropName.Add(propStr[0]); 
                        if (string.IsNullOrEmpty(propStr[1]))
                            continue;               // property not supported (only for Reference property)

                        arrPropVal.Add(propStr[1]);
                        if (string.IsNullOrEmpty(propStr[1]))
                            arrPropValBS.Add(OracleParameterStatus.NullInsert);
                        else
                            arrPropValBS.Add(OracleParameterStatus.Success);

                        arrPDatatyp.Add(propStr[2]);
                        arrPUnit.Add(propStr[3]);
                        if (string.IsNullOrEmpty(propStr[3]))
                            arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                        else
                            arrPUnitBS.Add(OracleParameterStatus.Success);
                    }
                    else if (prop is IfcComplexProperty)
                    {
                        List<string[]> compList = new List<string[]>();
                        IfcComplexProperty comP = prop as IfcComplexProperty;
                        processComplexProp(prop, out compList);

                        for (int i = 0; i < compList.Count; i++)
                        {
                            arrEleGuid.Add(guid);
                            arrPGrpName.Add(pset.Name + "." + compList[i][0]);
                            arrPropName.Add(compList[i][1]);
                            arrPropVal.Add(compList[i][2]);
                            if (string.IsNullOrEmpty(compList[i][2]))
                                arrPropValBS.Add(OracleParameterStatus.NullInsert);
                            else
                                arrPropValBS.Add(OracleParameterStatus.Success);

                            arrPDatatyp.Add(compList[i][3]);
                            arrPUnit.Add(compList[i][4]);
                            if (string.IsNullOrEmpty(compList[i][4]))
                                arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                            else
                                arrPUnitBS.Add(OracleParameterStatus.Success);
                        }
                    }
                    else
                    {
                        // Not supported IfcProperty type
                    }
                }
            }

            if (arrEleGuid.Count >= DBOperation.commitInterval)
            {

                Param[0].Value = arrEleGuid.ToArray();
                Param[0].Size = arrEleGuid.Count;
                Param[1].Value = arrPGrpName.ToArray();
                Param[1].Size = arrPGrpName.Count;
                Param[2].Value = arrPropName.ToArray();
                Param[2].Size = arrPropName.Count;
                Param[3].Value = arrPropVal.ToArray();
                Param[3].Size = arrPropVal.Count;
                Param[3].ArrayBindStatus = arrPropValBS.ToArray();
                Param[4].Value = arrPDatatyp.ToArray();
                Param[4].Size = arrPDatatyp.Count;

                try
                {
                    command.ArrayBindCount = arrEleGuid.Count;    // No of values in the array to be inserted
                    int commandStatus = command.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                    arrEleGuid.Clear();
                    arrPGrpName.Clear();
                    arrPropName.Clear();
                    arrPropVal.Clear();
                    arrPropValBS.Clear();
                    arrPDatatyp.Clear();
                }
                catch (OracleException e)
                {
                    string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + currStep;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    // Ignore any error
                    arrEleGuid.Clear();
                    arrPGrpName.Clear();
                    arrPropName.Clear();
                    arrPropVal.Clear();
                    arrPropValBS.Clear();
                    arrPDatatyp.Clear();
                }
                catch (SystemException e)
                {
                    string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    throw;
                }
            }
            

            if (arrEleGuid.Count > 0)
            {

                Param[0].Value = arrEleGuid.ToArray();
                Param[0].Size = arrEleGuid.Count;
                Param[1].Value = arrPGrpName.ToArray();
                Param[1].Size = arrPGrpName.Count;
                Param[2].Value = arrPropName.ToArray();
                Param[2].Size = arrPropName.Count;
                Param[3].Value = arrPropVal.ToArray();
                Param[3].Size = arrPropVal.Count;
                Param[3].ArrayBindStatus = arrPropValBS.ToArray();
                Param[4].Value = arrPDatatyp.ToArray();
                Param[4].Size = arrPDatatyp.Count;

                try
                {
                    command.ArrayBindCount = arrEleGuid.Count;    // No of values in the array to be inserted
                    int commandStatus = command.ExecuteNonQuery();
                    DBOperation.commitTransaction();
                }
                catch (OracleException e)
                {
                    string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + currStep;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    // Ignore any error
                }
                catch (SystemException e)
                {
                    string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                    throw;
                }
            }

            DBOperation.commitTransaction();
            command.Dispose();
        }

        /// <summary>
        /// Processing a simple property. Returning string array containing: [0] property value in string format, [1] property data type, [2] Unit
        /// Property format: all will be in string:
        /// - Property Single value: string, single unit, single datatype
        /// - Property Enumerated value: string, no unit, datatype enumeration
        /// - Property Bounded value: [ <LowerBound>, <UpperBound> ], "-" when null, single unit, single datatype
        /// - Property Table value: (<defining value>, <defined value>); ( , ); ...;, unit (<defining unit>, <defined unit>), "-" when null, similarly for datatype
        /// - Property List value: (list1); (list2); ..., single unit, single datatype
        /// - Property Reference value: not supported!
        /// </summary>
        /// <param name="prop"></param>
        /// <param name="outStr"></param>
        /// <returns></returns>
        private void processSimpleProperty(IfcProperty prop, out string[] outStr)
        {
            string[] tmpString = new string[4];

            for (int i = 0; i < 4; i++ )
                tmpString[i] = string.Empty;

            if (prop is IfcPropertySingleValue)
            {
                IfcPropertySingleValue psv = prop as IfcPropertySingleValue;
                tmpString[0] = psv.Name;
                tmpString[1] = psv.ToString();
                if (psv.NominalValue != null)
                    tmpString[2] = psv.NominalValue.GetType().Name.ToUpper();
                if (psv.Unit != null)
                    tmpString[3] = psv.Unit.ToString();
            }
            else if (prop is IfcPropertyEnumeratedValue)
            {
                IfcPropertyEnumeratedValue pev = prop as IfcPropertyEnumeratedValue;
                tmpString[0] = pev.Name;
                if (pev.EnumerationValues != null)
                {
                    for (int i = 0; i < pev.EnumerationValues.Count; i++)
                    {
                        tmpString[1] += "(" + pev.EnumerationValues[i].ToString() + "); ";
                    }
                    tmpString[2] = pev.EnumerationValues[0].GetType().Name.ToUpper();
                }
            }
            else if (prop is IfcPropertyBoundedValue)
            {
                IfcPropertyBoundedValue pbv = prop as IfcPropertyBoundedValue;
                tmpString[0] = pbv.Name;
                string lowerB;
                string upperB;
                if (pbv.LowerBoundValue == null)
                    lowerB = "-";
                else
                    lowerB = pbv.LowerBoundValue.ToString();
                if (pbv.UpperBoundValue == null)
                    upperB = "-";
                else
                    upperB = pbv.UpperBoundValue.ToString();

                tmpString[1] = "[" + lowerB + ", " + upperB + "]";

                if (pbv.LowerBoundValue != null)
                    tmpString[2] = pbv.LowerBoundValue.GetType().Name.ToUpper();
                else if (pbv.UpperBoundValue != null)
                    tmpString[2] = pbv.UpperBoundValue.GetType().Name.ToUpper();

                if (pbv.Unit != null)
                    tmpString[3] = pbv.Unit.ToString();
            }
            else if (prop is IfcPropertyTableValue)
            {
                IfcPropertyTableValue ptv = prop as IfcPropertyTableValue;
                tmpString[0] = ptv.Name;
                if (ptv.DefiningValues != null)
                {
                    for (int i = 0; i < ptv.DefiningValues.Count; i++)
                    {
                        if (ptv.DefinedValues != null)
                            tmpString[1] += "(" + ptv.DefiningValues[i].ToString() + ", " + ptv.DefinedValues[i].ToString() + "); ";
                        else
                            tmpString[1] += "(" + ptv.DefiningValues[i].ToString() + ", ); ";
                    }
                    if (ptv.DefinedValues != null)
                        tmpString[2] = "(" + ptv.DefiningValues[0].GetType().Name.ToUpper() + ", " + ptv.DefinedValues[0].GetType().Name.ToUpper() + ")";
                    else
                        tmpString[2] = "(" + ptv.DefiningValues[0].GetType().Name.ToUpper() + ", )";
                }
                if (ptv.DefiningUnit != null || ptv.DefinedUnit != null)
                {
                    tmpString[3] = "(" + ptv.DefiningUnit != null ? ptv.DefiningUnit.ToString() : "-" + ", " + ptv.DefinedUnit != null ? ptv.DefinedUnit.ToString() : "-" + ")";
                }
            }
            else if (prop is IfcPropertyReferenceValue)
            {
                // ReferenceValue is not yet supported!
                IfcPropertyReferenceValue prv = prop as IfcPropertyReferenceValue;
                tmpString[0] = prv.Name;
            }
            else if (prop is IfcPropertyListValue)
            {
                IfcPropertyListValue plv = prop as IfcPropertyListValue;
                tmpString[0] = plv.Name;
                if (plv.ListValues != null)
                {
                    for (int i = 0; i < plv.ListValues.Count; i++)
                    {
                        tmpString[1] += "(" + plv.ListValues[i].ToString() + "); ";
                    }
                    tmpString[2] = plv.ListValues[0].GetType().Name.ToUpper();
                }
                if (plv.Unit != null)
                    tmpString[3] = plv.Unit.ToString();
            }
            else
            {
                // prop not supported
            }
            tmpString[0] = BIMRLUtils.checkSingleQuote(tmpString[0]);
            tmpString[1] = BIMRLUtils.checkSingleQuote(tmpString[1]);
            
            outStr = tmpString;
        }
 
        /// <summary>
        /// Process a complex property. It allows recursive processing of complex property. What it does simply create list of the single properties
        /// The nested nature of the complex property is going to be flattened by moving the complex property as a new Pset level with name appended to the
        ///    parent property. When it is called recursively, all the single properties will be rolled up.
        /// An array of 5 strings will returned for each single property. All of them are put into a List:
        /// [0] - Name of the complex property (nested complex property will have name: <parent name>.<its name>
        /// [1] - Name of the property
        /// Array member [2] to [4] are the same as the simple property, in fact it is returned from it.
        /// [2] - Property value in a string format
        /// [3] - Property data type
        /// [4] - Property unit
        /// </summary>
        /// <param name="prop"></param>
        /// <param name="outStr"></param>
        private void processComplexProp(IfcProperty prop, out List<string[]> outStr)
        {
            List<string[]> tmpList = new List<string[]>();
            IfcComplexProperty cProp = prop as IfcComplexProperty;
            IEnumerable<IfcProperty> hasProps = cProp.HasProperties;
            foreach (IfcProperty hProp in hasProps)
            {
                if (hProp is IfcSimpleProperty)
                {
                    string[] tmpStr = new string[5];
                    tmpStr[0] = prop.Name;

                    string[] sProp = new string[4];
                    processSimpleProperty(hProp, out sProp);
                    tmpStr[1] = sProp[0]; 
                    if (string.IsNullOrEmpty(sProp[1]))
                        continue;       // not supported (reference property only)
                    tmpStr[2] = sProp[1];
                    tmpStr[3] = sProp[2];
                    tmpStr[4] = sProp[3];

                    tmpList.Add(tmpStr);
                }
                else if (hProp is IfcComplexProperty)
                {
                    List<string[]> retList = new List<string[]>();
                    processComplexProp(hProp, out retList);
                    if (retList.Count == 0)
                        continue;   // empty list, maybe all of unspported types
                    // go trhough the list now and populate own list
                    for (int i =0; i<retList.Count; i++)
                    {
                        string[] tmpStr = new string[5];
                        tmpStr[0] = prop.Name + "." + retList[i][0];
                        tmpStr[1] = retList[i][1];
                        tmpStr[2] = retList[i][2];
                        tmpStr[3] = retList[i][3];
                        tmpStr[4] = retList[i][4];

                        tmpList.Add(tmpStr);
                    }
                }
                else
                {
                    // Not supported type
                }
            }

            outStr = tmpList;
        }
    }
}

