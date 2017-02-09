using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Xbim.Common.Exceptions;
using System.Diagnostics;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.OctreeLib;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc;

namespace BIMRL
{
    public class BIMRLProperties
    {
        BIMRLCommon _refBIMRLCommon;

        public BIMRLProperties(BIMRLCommon refBIMRLCommon)
        {
            _refBIMRLCommon = refBIMRLCommon;
        }

        public void processTypeProperties(IIfcTypeProduct typ)
        {
         IEnumerable<IIfcRelDefinesByProperties> relDProps = typ.DefinedByProperties;
         IList<IIfcPropertySet> pSets = new List<IIfcPropertySet>();
         IList<IIfcPropertySetDefinition> psetDefs = new List<IIfcPropertySetDefinition>();
         foreach (IIfcRelDefinesByProperties relDProp in relDProps)
         {
            IIfcPropertySetDefinitionSelect pDefSel = relDProp.RelatingPropertyDefinition;
            // IFC2x3:
            Xbim.Ifc2x3.Kernel.IfcPropertySetDefinition pDefSel2x3 = pDefSel as Xbim.Ifc2x3.Kernel.IfcPropertySetDefinition;
            if (pDefSel2x3 == null)
            {
               if (pDefSel is IIfcPreDefinedPropertySet || pDefSel is IIfcQuantitySet)
                  psetDefs.Add(pDefSel as IIfcPropertySetDefinition);
               if (pDefSel is IIfcPropertySet)
                  pSets.Add(pDefSel as IIfcPropertySet);
            }
            else
            {
               if (pDefSel2x3 is IIfcPropertySet)
                  pSets.Add(pDefSel2x3 as IIfcPropertySet);
               else if (pDefSel2x3 is IIfcDoorLiningProperties || pDefSel2x3 is IIfcDoorPanelProperties
                         || pDefSel2x3 is IIfcWindowLiningProperties || pDefSel2x3 is IIfcWindowPanelProperties
                         || pDefSel2x3 is IIfcElementQuantity)
                  psetDefs.Add(pDefSel2x3 as IIfcPropertySetDefinition);
            }
         }
         //processProperties(modelResource, typeResource, pSets);

         processProperties(typ.GlobalId.ToString(), pSets, "BIMRL_TYPEPROPERTIES");
         if (psetDefs.Count > 0)
            processPropertyDefinitions(typ.GlobalId.ToString(), psetDefs, "BIMRL_TYPEPROPERTIES");

         //IEnumerable<IfcPropertySetDefinition> psets = typ.HasPropertySets;
         //if (psets != null)
         //{
         //    List<IfcPropertySetDefinition> psetDefs = new List<IfcPropertySetDefinition>();
         //    foreach (IfcPropertySetDefinition p in psets)
         //    {
         //        if (p is IfcDoorLiningProperties || p is IfcDoorPanelProperties
         //            || p is IfcWindowLiningProperties || p is IfcWindowPanelProperties)
         //        {
         //            psetDefs.Add(p);
         //        }
         //    }
         //    if (psetDefs.Count > 0)
         //        processPropertyDefinitions(typ.GlobalId.ToString(), psetDefs, "BIMRL_TYPEPROPERTIES");
         //}
      }

      public void processElemProperties(IIfcObject el)
      {
         // Now process Property set definitions attachd to the object via IsDefinedByProperties for special properties
         IEnumerable<IIfcRelDefinesByProperties> relDProps = el.IsDefinedBy;
         IList<IIfcPropertySet> pSets = new List<IIfcPropertySet>();
         IList<IIfcPropertySetDefinition> psetDefs = new List<IIfcPropertySetDefinition>();
         foreach (IIfcRelDefinesByProperties relDProp in relDProps)
         {
         IIfcPropertySetDefinitionSelect pDefSel = relDProp.RelatingPropertyDefinition;
         // IFC2x3:
         Xbim.Ifc2x3.Interfaces.IIfcPropertySetDefinition pDefSel2x3 = pDefSel as Xbim.Ifc2x3.Interfaces.IIfcPropertySetDefinition;
         if (pDefSel2x3 == null)
         {
            if (pDefSel is IIfcPreDefinedPropertySet || pDefSel is IIfcQuantitySet)
               psetDefs.Add(pDefSel as IIfcPropertySetDefinition);
            if (pDefSel is IIfcPropertySet)
               pSets.Add(pDefSel as IIfcPropertySet);
         }
         else
         {
            if (pDefSel2x3 is IIfcPropertySet)
               pSets.Add(pDefSel2x3 as IIfcPropertySet);
            else if (pDefSel2x3 is IIfcDoorLiningProperties || pDefSel2x3 is IIfcDoorPanelProperties
                        || pDefSel2x3 is IIfcWindowLiningProperties || pDefSel2x3 is IIfcWindowPanelProperties
                        || pDefSel2x3 is IIfcElementQuantity)
               psetDefs.Add(pDefSel2x3 as IIfcPropertySetDefinition);
         }
         //if (p.RelatingPropertyDefinition is IIfcDoorLiningProperties || p.RelatingPropertyDefinition is IIfcDoorPanelProperties
         //      || p.RelatingPropertyDefinition is IIfcWindowLiningProperties || p.RelatingPropertyDefinition is IIfcWindowPanelProperties
         //      || p.RelatingPropertyDefinition is IIfcElementQuantity)
         //   {
         //      psetDefs.Add(p.RelatingPropertyDefinition);
         //   }
         }
         processProperties(el.GlobalId.ToString(), pSets, "BIMRL_ELEMENTPROPERTIES");

         if (psetDefs.Count > 0)
               processPropertyDefinitions(el.GlobalId.ToString(), psetDefs, "BIMRL_ELEMENTPROPERTIES");
      }

      private void processPropertyDefinitions(string guid, IEnumerable<IIfcPropertySetDefinition> psdefs, string tableName)
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

         foreach (IIfcPropertySetDefinition p in psdefs)
         {
            if (p is IIfcDoorLiningProperties)
            {
               IIfcDoorLiningProperties dl = p as IIfcDoorLiningProperties;

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
            else if (p is IIfcDoorPanelProperties)
            {
               IIfcDoorPanelProperties dp = p as IIfcDoorPanelProperties;

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
                
            if (p is IIfcWindowLiningProperties)
            {
               IIfcWindowLiningProperties wl = p as IIfcWindowLiningProperties;

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
            else if (p is IIfcWindowPanelProperties)
            {
               IIfcWindowPanelProperties wp = p as IIfcWindowPanelProperties;

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

            if (p is IIfcElementQuantity)
            {
               // Currently will ONLY support IfcPhysicalSimpleQuantity
               IIfcElementQuantity elq = p as IIfcElementQuantity;
               string pGrpName = "IFCELEMENTQUANTITY"; // Default name
               if (!string.IsNullOrEmpty(elq.Name))
                  pGrpName = elq.Name;

               foreach (IIfcPhysicalQuantity pQ in elq.Quantities)
               {
                  string unitOfMeasure = string.Empty;
                  if (pQ is IIfcPhysicalSimpleQuantity)
                  {
                     arrEleGuid.Add(guid);
                     arrPGrpName.Add(pGrpName);
                     arrPDatatyp.Add(pQ.GetType().Name.ToUpper());
                     if (!string.IsNullOrEmpty(pQ.Name))
                        arrPropName.Add(pQ.Name);
                     else
                        arrPropName.Add(pQ.GetType().Name.ToUpper());       // Set default to the type if name is not defined
                     if (((IIfcPhysicalSimpleQuantity)pQ).Unit != null)
                     {
                        IIfcPhysicalSimpleQuantity pQSimple = pQ as IIfcPhysicalSimpleQuantity;
                        unitOfMeasure = BIMRLUtils.getIfcUnitStr(pQSimple.Unit);
                     }

                     if (pQ is IIfcQuantityLength)
                     {
                        IIfcQuantityLength quant = pQ as IIfcQuantityLength;
                        arrPropVal.Add(quant.LengthValue.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        if (string.IsNullOrEmpty(unitOfMeasure))
                           unitOfMeasure = BIMRLUtils.getDefaultIfcUnitStr(quant.LengthValue);
                     }
                     else if (pQ is IIfcQuantityArea)
                     {
                        IIfcQuantityArea quant = pQ as IIfcQuantityArea;
                        arrPropVal.Add(quant.AreaValue.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        if (string.IsNullOrEmpty(unitOfMeasure))
                           unitOfMeasure = BIMRLUtils.getDefaultIfcUnitStr(quant.AreaValue);
                     }
                     else if (pQ is IIfcQuantityVolume)
                     {
                        IIfcQuantityVolume quant = pQ as IIfcQuantityVolume;
                        arrPropVal.Add(quant.VolumeValue.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        if (string.IsNullOrEmpty(unitOfMeasure))
                           unitOfMeasure = BIMRLUtils.getDefaultIfcUnitStr(quant.VolumeValue);
                     }
                     else if (pQ is IIfcQuantityCount)
                     {
                        IIfcQuantityCount quant = pQ as IIfcQuantityCount;
                        arrPropVal.Add(quant.CountValue.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        if (string.IsNullOrEmpty(unitOfMeasure))
                           unitOfMeasure = BIMRLUtils.getDefaultIfcUnitStr(quant.CountValue);
                     }
                     else if (pQ is IIfcQuantityWeight)
                     {
                        IIfcQuantityWeight quant = pQ as IIfcQuantityWeight;
                        arrPropVal.Add(quant.WeightValue.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        if (string.IsNullOrEmpty(unitOfMeasure))
                           unitOfMeasure = BIMRLUtils.getDefaultIfcUnitStr(quant.WeightValue);
                     }
                     else if (pQ is IIfcQuantityTime)
                     {
                        IIfcQuantityTime quant = pQ as IIfcQuantityTime;
                        arrPropVal.Add(quant.TimeValue.ToString());
                        arrPropValBS.Add(OracleParameterStatus.Success);
                        if (string.IsNullOrEmpty(unitOfMeasure))
                           unitOfMeasure = BIMRLUtils.getDefaultIfcUnitStr(quant.TimeValue);
                     }

                     if (!string.IsNullOrEmpty(unitOfMeasure))
                     {
                        arrPUnit.Add(unitOfMeasure);
                        arrPUnitBS.Add(OracleParameterStatus.Success);
                     }
                     else
                     {
                        arrPUnit.Add(string.Empty);
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                     }

                  }
                  else if (pQ is IIfcPhysicalComplexQuantity)
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
               Param[5].Value = arrPUnit.ToArray();
               Param[5].Size = arrPUnitBS.Count;
               Param[5].ArrayBindStatus = arrPUnitBS.ToArray();

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
                  arrPUnit.Clear();
                  arrPUnitBS.Clear();
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
                  arrPUnit.Clear();
                  arrPUnitBS.Clear();
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
            Param[5].Value = arrPUnit.ToArray();
            Param[5].Size = arrPUnitBS.Count;
            Param[5].ArrayBindStatus = arrPUnitBS.ToArray();

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
      private void processProperties(string guid, IEnumerable<IIfcPropertySet> elPsets, string tableName)
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
         foreach (IIfcPropertySet pset in elPsets)
         {
            IEnumerable<IIfcProperty> props = pset.HasProperties;
            foreach (IIfcProperty prop in props)
            {
               if (prop is IIfcSimpleProperty)
               {
                  arrEleGuid.Add(guid);
                  arrPGrpName.Add(pset.Name);
                  //string[] propStr = new string[4];

                  //processSimpleProperty(prop, out propStr);
                  //arrPropName.Add(propStr[0]); 
                  //if (string.IsNullOrEmpty(propStr[1]))
                  //      continue;               // property not supported (only for Reference property)
                  Tuple<string, string, string, string> propVal = processSimpleProperty(prop);
                  if (string.IsNullOrEmpty(propVal.Item1))
                     continue;               // property not supported (only for Reference property)

                  arrPropName.Add(propVal.Item1);
                  arrPropVal.Add(propVal.Item2);
                  if (string.IsNullOrEmpty(propVal.Item2))
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                  else
                        arrPropValBS.Add(OracleParameterStatus.Success);

                  arrPDatatyp.Add(propVal.Item3);
                  arrPUnit.Add(propVal.Item4);
                  if (string.IsNullOrEmpty(propVal.Item4))
                        arrPUnitBS.Add(OracleParameterStatus.NullInsert);
                  else
                        arrPUnitBS.Add(OracleParameterStatus.Success);
               }
               else if (prop is IIfcComplexProperty)
               {
                  IIfcComplexProperty comP = prop as IIfcComplexProperty;
                  List<Tuple<string, Tuple<string, string, string, string>>> compList = processComplexProp(prop);

                  for (int i = 0; i < compList.Count; i++)
                  {
                     arrEleGuid.Add(guid);
                     arrPGrpName.Add(pset.Name + "." + compList[i].Item1);
                     arrPropName.Add(compList[i].Item2.Item1);
                     arrPropVal.Add(compList[i].Item2.Item2);
                     if (string.IsNullOrEmpty(compList[i].Item2.Item2))
                        arrPropValBS.Add(OracleParameterStatus.NullInsert);
                     else
                        arrPropValBS.Add(OracleParameterStatus.Success);

                     arrPDatatyp.Add(compList[i].Item2.Item3);
                     arrPUnit.Add(compList[i].Item2.Item4);
                     if (string.IsNullOrEmpty(compList[i].Item2.Item4))
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
            Param[5].Value = arrPUnit.ToArray();
            Param[5].Size = arrPUnit.Count;
            Param[5].ArrayBindStatus = arrPUnitBS.ToArray();

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
               arrPUnit.Clear();
               arrPUnitBS.Clear();
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
               arrPUnit.Clear();
               arrPUnitBS.Clear();
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
            Param[5].Value = arrPUnit.ToArray();
            Param[5].Size = arrPUnit.Count;
            Param[5].ArrayBindStatus = arrPUnitBS.ToArray();

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
      private Tuple<string, string, string, string> processSimpleProperty(IIfcProperty prop)
      {
         //private void processSimpleProperty(IIfcProperty prop, out string[] outStr)
         //{
         //string[] tmpString = new string[4];

         //for (int i = 0; i < 4; i++ )
         //      tmpString[i] = string.Empty;

         string propName = string.Empty;
         string propValue = string.Empty;
         string propDataType = string.Empty;
         string propUnit = string.Empty;

         if (prop is IIfcPropertySingleValue)
         {
            IIfcPropertySingleValue psv = prop as IIfcPropertySingleValue;
            propName = psv.Name;
            if (psv.NominalValue != null)
            {
               if (psv.NominalValue.Value != null)
               {
                  propValue = psv.NominalValue.Value.ToString();
                  propDataType = psv.NominalValue.Value.GetType().Name.ToUpper();      // This will give the primitive datatype, e.g. Integer, Double, String
               }
            }

            if (psv.Unit != null)
            {
               propUnit = BIMRLUtils.getIfcUnitStr(psv.Unit);
            }
            else
            {
               propUnit = BIMRLUtils.getDefaultIfcUnitStr(psv.NominalValue);
            }
         }
         else if (prop is IIfcPropertyEnumeratedValue)
         {
            IIfcPropertyEnumeratedValue pev = prop as IIfcPropertyEnumeratedValue;
            propName = pev.Name;
            if (pev.EnumerationValues != null)
            {
               string tmpStr = string.Empty;
               for (int i = 0; i < pev.EnumerationValues.Count; i++)
               {
                  tmpStr += "(" + pev.EnumerationValues[i].ToString() + "); ";
               }
               propValue = tmpStr;
               propDataType = pev.EnumerationValues[0].GetType().Name.ToUpper();
            }
         }
         else if (prop is IIfcPropertyBoundedValue)
         {
            IIfcPropertyBoundedValue pbv = prop as IIfcPropertyBoundedValue;
            propName = pbv.Name;
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

            string tmpStr = "[" + lowerB + ", " + upperB + "]";

            if (pbv.LowerBoundValue != null)
               propDataType = pbv.LowerBoundValue.GetType().Name.ToUpper();
            else if (pbv.UpperBoundValue != null)
               propDataType = pbv.UpperBoundValue.GetType().Name.ToUpper();

            // We will always assign the property unit by its explicit unit, or by the IfcProject default unit if not specified
            if (pbv.Unit != null)
            {
               propUnit = BIMRLUtils.getIfcUnitStr(pbv.Unit);
            }
            else
            {
               propUnit = BIMRLUtils.getDefaultIfcUnitStr(pbv.LowerBoundValue);
            }
         }
         else if (prop is IIfcPropertyTableValue)
         {
            IIfcPropertyTableValue ptv = prop as IIfcPropertyTableValue;
            propName = ptv.Name;
            if (ptv.DefiningValues != null)
            {
               string tmpStr = string.Empty;
               for (int i = 0; i < ptv.DefiningValues.Count; i++)
               {
                  if (ptv.DefinedValues != null)
                        tmpStr += "(" + ptv.DefiningValues[i].ToString() + ", " + ptv.DefinedValues[i].ToString() + "); ";
                  else
                        tmpStr += "(" + ptv.DefiningValues[i].ToString() + ", ); ";
               }
               propValue = tmpStr;
               if (ptv.DefinedValues != null)
                  propDataType = "(" + ptv.DefiningValues[0].GetType().Name.ToUpper() + ", " + ptv.DefinedValues[0].GetType().Name.ToUpper() + ")";
               else
                  propDataType = "(" + ptv.DefiningValues[0].GetType().Name.ToUpper() + ", )";
            }
            string definingUnitStr = "-";
            string definedUnitStr = "-";
            if (ptv.DefiningUnit != null)
               definingUnitStr = BIMRLUtils.getIfcUnitStr(ptv.DefiningUnit);
            else
               definingUnitStr = BIMRLUtils.getDefaultIfcUnitStr(ptv.DefiningValues[0]);

            if (ptv.DefinedUnit != null)
               definedUnitStr = BIMRLUtils.getIfcUnitStr(ptv.DefinedUnit);
            else
               if (ptv.DefinedValues != null)
               definedUnitStr = BIMRLUtils.getDefaultIfcUnitStr(ptv.DefinedValues[0]);

         }
         else if (prop is IIfcPropertyReferenceValue)
         {
            // ReferenceValue is not yet supported!
            IIfcPropertyReferenceValue prv = prop as IIfcPropertyReferenceValue;
            propName = prv.Name;
         }
         else if (prop is IIfcPropertyListValue)
         {
            IIfcPropertyListValue plv = prop as IIfcPropertyListValue;
            propName = plv.Name;
            if (plv.ListValues != null)
            {
               string tmpStr = string.Empty;
               for (int i = 0; i < plv.ListValues.Count; i++)
               {
                  tmpStr += "(" + plv.ListValues[i].ToString() + "); ";
               }
               propValue = tmpStr;
               propDataType = plv.ListValues[0].GetType().Name.ToUpper();
            }
            if (plv.Unit != null)
            {
               propUnit = BIMRLUtils.getIfcUnitStr(plv.Unit);
            }
            else
            {
               propUnit = BIMRLUtils.getDefaultIfcUnitStr(plv.ListValues[0]);
            }
         }
         else
         {
               // prop not supported
         }
         propName = BIMRLUtils.checkSingleQuote(propName);
         if (propValue is string)
            propValue = BIMRLUtils.checkSingleQuote(propValue as string);

         return new Tuple<string, string, string, string>(propName, propValue, propDataType, propUnit);
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
      //private void processComplexProp(IIfcProperty prop, out List<string[]> outStr)
      private List<Tuple<string, Tuple<string, string, string, string>>> processComplexProp(IIfcProperty prop)
      {
         //List<string[]> tmpList = new List<string[]>();
         List<Tuple<string, Tuple<string, string, string, string>>> tmpList = new List<Tuple<string, Tuple<string, string, string, string>>>();

         IIfcComplexProperty cProp = prop as IIfcComplexProperty;
         IEnumerable<IIfcProperty> hasProps = cProp.HasProperties;
         foreach (IIfcProperty hProp in hasProps)
         {
            if (hProp is IIfcSimpleProperty)
            {
               string complexPropName = prop.Name;
               Tuple<string, string, string, string> propVal = processSimpleProperty(hProp);
               if (propVal.Item2 == null)
                  continue;       // not supported (reference property only)

               tmpList.Add(new Tuple<string, Tuple<string, string, string, string>>(complexPropName, propVal));
            }
            else if (hProp is IIfcComplexProperty)
            {
               List<Tuple<string, Tuple<string, string, string, string>>> compPropValList = processComplexProp(hProp);
               if (compPropValList.Count == 0)
                  continue;   // empty list, maybe all of unspported types

               // go through the list now and populate own list
               for (int i = 0; i < compPropValList.Count; i++)
               {
                  tmpList.AddRange(compPropValList);
               }
            }
            else
            {
               // Not supported type
            }
         }

         return tmpList;
      }
   }
}

