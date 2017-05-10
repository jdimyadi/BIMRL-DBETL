using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using BIMRL.OctreeLib;

namespace BIMRL
{
   public class BIMRLMaterial
   {
      private IfcStore _model;
      private BIMRLCommon _refBIMRLCommon;

      public BIMRLMaterial(IfcStore m, BIMRLCommon refBIMRLCommon)
      {
         _model = m;
         _refBIMRLCommon = refBIMRLCommon;
      }

      public void processMaterials()
      {
         List<string> insTGuid = new List<string>();
         List<string> insTMatName = new List<string>();
         List<string> insTSetName = new List<string>();
         List<OracleParameterStatus> insTSetNPS = new List<OracleParameterStatus>();
         List<int> insTMatSeq = new List<int>();
         List<OracleParameterStatus> insTMatSPS = new List<OracleParameterStatus>();
         List<double> insTMatThick = new List<double>();
         List<OracleParameterStatus> insTMatTPS = new List<OracleParameterStatus>();
         List<string> insTIsVentilated = new List<string>();
         List<OracleParameterStatus> insTIsVPS = new List<OracleParameterStatus>();

         List<string> insGuid = new List<string>();
         List<string> insMatName = new List<string>();
         List<string> insSetName = new List<string>();
         List<OracleParameterStatus> insSetNPS = new List<OracleParameterStatus>();
         List<int> insMatSeq = new List<int>();
         List<OracleParameterStatus> insMatSPS = new List<OracleParameterStatus>();
         List<double> insMatThick = new List<double>();
         List<OracleParameterStatus> insMatTPS = new List<OracleParameterStatus>();
         List<string> insIsVentilated = new List<string>();
         List<OracleParameterStatus> insIsVPS = new List<OracleParameterStatus>();

         string currStep = "Processing Materials";

         DBOperation.beginTransaction();

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
         OracleCommand command2 = new OracleCommand(" ", DBOperation.DBConn);

         // 2 columsn are not used: Category, ForProfile is only used in IFC4
         string sqlStmt = "insert into BIMRL_TYPEMATERIAL_" + bimrlProcessModel.currFedID.ToString("X4") + " (ElementID, MaterialName, SetName, IsVentilated, MaterialSequence, MaterialThickness) "
                           + " values (:1, :2, :3, :4, :5, :6)";
         command.CommandText = sqlStmt;
         OracleParameter[] Param = new OracleParameter[6];
         for (int i = 0; i < 4; i++)
         {
               Param[i] = command.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
               Param[i].Direction = ParameterDirection.Input;
         }
         Param[4] = command.Parameters.Add("3", OracleDbType.Int16);
         Param[4].Direction = ParameterDirection.Input;
         Param[5] = command.Parameters.Add("4", OracleDbType.Double);
         Param[5].Direction = ParameterDirection.Input;
                
         string sqlStmt2 = "insert into BIMRL_ELEMENTMATERIAL_" + bimrlProcessModel.currFedID.ToString("X4") + " (ElementID, MaterialName, SetName, IsVentilated, MaterialSequence, MaterialThickness) "
                           + " values (:1, :2, :3, :4, :5, :6)";
         command2.CommandText = sqlStmt2;
         OracleParameter[] Param2 = new OracleParameter[6];
         for (int i = 0; i < 4; i++)
         {
               Param2[i] = command2.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
               Param2[i].Direction = ParameterDirection.Input;
         }
         Param2[4] = command2.Parameters.Add("3", OracleDbType.Int16);
         Param2[4].Direction = ParameterDirection.Input;
         Param2[5] = command2.Parameters.Add("4", OracleDbType.Double);
         Param2[5].Direction = ParameterDirection.Input;

         IEnumerable<IIfcRelAssociatesMaterial> relMaterials = _model.Instances.OfType<IIfcRelAssociatesMaterial>();
         foreach (IIfcRelAssociatesMaterial relMat in relMaterials)
         {
            // reset Relating material data at the start
            List<string> arrMatName = new List<string>();
            List<string> arrSetName = new List<string>();
            List<OracleParameterStatus> arrSetNPS = new List<OracleParameterStatus>();
            List<int> arrMatSeq = new List<int>();
            List<OracleParameterStatus> arrMatSPS = new List<OracleParameterStatus>();
            List<double> arrMatThick = new List<double>();
            List<OracleParameterStatus> arrMatTPS = new List<OracleParameterStatus>();
            List<string> arrIsVentilated = new List<string>();
            List<OracleParameterStatus> arrIsVPS = new List<OracleParameterStatus>();

            // Handle various IfcMaterialSelect
            if (relMat.RelatingMaterial is IIfcMaterial)
            {
               IIfcMaterial m = relMat.RelatingMaterial as IIfcMaterial;
               arrMatName.Add(m.Name);
               arrSetName.Add(string.Empty);
               arrSetNPS.Add(OracleParameterStatus.NullInsert);
               arrMatSeq.Add(0);
               arrMatSPS.Add(OracleParameterStatus.NullInsert);
               arrMatThick.Add(0.0);
               arrMatTPS.Add(OracleParameterStatus.NullInsert);
               arrIsVentilated.Add(string.Empty);
               arrIsVPS.Add(OracleParameterStatus.NullInsert);
            }
            else if (relMat.RelatingMaterial is IIfcMaterialConstituent)
            {
               IIfcMaterialConstituent m = relMat.RelatingMaterial as IIfcMaterialConstituent;
               arrMatName.Add(m.Material.Name);
               arrSetName.Add(string.Empty);
               arrSetNPS.Add(OracleParameterStatus.NullInsert);
               arrMatSeq.Add(0);
               arrMatSPS.Add(OracleParameterStatus.NullInsert);
               arrMatThick.Add(0.0);
               arrMatTPS.Add(OracleParameterStatus.NullInsert);
               arrIsVentilated.Add(string.Empty);
               arrIsVPS.Add(OracleParameterStatus.NullInsert);
            }
            else if (relMat.RelatingMaterial is IIfcMaterialList)
            {
               IIfcMaterialList mList = relMat.RelatingMaterial as IIfcMaterialList;
               foreach (IIfcMaterial m in mList.Materials)
               {
                  arrMatName.Add(m.Name);
                  arrSetName.Add(string.Empty);
                  arrSetNPS.Add(OracleParameterStatus.NullInsert);
                  arrMatSeq.Add(0);
                  arrMatSPS.Add(OracleParameterStatus.NullInsert);
                  arrMatThick.Add(0.0);
                  arrMatTPS.Add(OracleParameterStatus.NullInsert);
                  arrIsVentilated.Add(string.Empty);
                  arrIsVPS.Add(OracleParameterStatus.NullInsert);
               }
            }
            else if (relMat.RelatingMaterial is IIfcMaterialConstituentSet)
            {
               IIfcMaterialConstituentSet mConstSet = relMat.RelatingMaterial as IIfcMaterialConstituentSet;
               foreach (IIfcMaterialConstituent mConst in mConstSet.MaterialConstituents)
               {
                  arrMatName.Add(mConst.Material.Name);
                  arrSetName.Add(mConstSet.Name);
                  arrSetNPS.Add(OracleParameterStatus.Success);
                  arrMatSeq.Add(0);
                  arrMatSPS.Add(OracleParameterStatus.NullInsert);
                  arrMatThick.Add(0.0);
                  arrMatTPS.Add(OracleParameterStatus.NullInsert);
                  arrIsVentilated.Add(string.Empty);
                  arrIsVPS.Add(OracleParameterStatus.NullInsert);
               }
            }
            else if (relMat.RelatingMaterial is IIfcMaterialLayer)
            {
               IIfcMaterialLayer mLayer = relMat.RelatingMaterial as IIfcMaterialLayer;
               if (mLayer.Material != null)
                  arrMatName.Add(mLayer.Material.Name);
               else
                  arrMatName.Add("-");
               arrSetName.Add(string.Empty);
               arrSetNPS.Add(OracleParameterStatus.NullInsert);
               arrMatSeq.Add(0);
               arrMatSPS.Add(OracleParameterStatus.NullInsert);
               arrMatThick.Add((double) mLayer.LayerThickness.Value);
               arrMatTPS.Add(OracleParameterStatus.Success);
               if (mLayer.IsVentilated != null)
               {
                  arrIsVentilated.Add("TRUE");
                  arrIsVPS.Add(OracleParameterStatus.Success);
               }
               else
               {
                  arrIsVentilated.Add(string.Empty);
                  arrIsVPS.Add(OracleParameterStatus.NullInsert);
               }
            }
            else if (relMat.RelatingMaterial is IIfcMaterialLayerSet || relMat.RelatingMaterial is IIfcMaterialLayerSetUsage)
            {
               IIfcMaterialLayerSet mLayerSet;
               if (relMat.RelatingMaterial is IIfcMaterialLayerSetUsage)
               {
                  // We do not handle LayerSetDirection, DirectionSense, OffserFromReference as they are mainly important for drawing construction
                  IIfcMaterialLayerSetUsage mLSU = relMat.RelatingMaterial as IIfcMaterialLayerSetUsage;
                  mLayerSet = mLSU.ForLayerSet;
               }
               else
                  mLayerSet = relMat.RelatingMaterial as IIfcMaterialLayerSet;

               Int16 seqNo = 1;
               foreach (IIfcMaterialLayer mLayer in mLayerSet.MaterialLayers)
               {
                  if (mLayerSet.LayerSetName != null)
                  {
                        arrSetName.Add(mLayerSet.LayerSetName);
                        arrSetNPS.Add(OracleParameterStatus.Success);
                  }
                  else
                  {
                        arrSetName.Add(string.Empty);
                        arrSetNPS.Add(OracleParameterStatus.NullInsert);                      
                  }

                  if (mLayer.Material != null)
                        arrMatName.Add(mLayer.Material.Name);
                  else
                        arrMatName.Add("-");
                  arrMatSeq.Add(seqNo++);
                  arrMatSPS.Add(OracleParameterStatus.NullInsert);
                  arrMatThick.Add((double) mLayer.LayerThickness.Value);
                  arrMatTPS.Add(OracleParameterStatus.Success);
                  if (mLayer.IsVentilated != null)
                  {
                        arrIsVentilated.Add("TRUE");
                        arrIsVPS.Add(OracleParameterStatus.Success);
                  }
                  else
                  {
                        arrIsVentilated.Add(string.Empty);
                        arrIsVPS.Add(OracleParameterStatus.NullInsert);
                  }
               }
            }
            else if (relMat.RelatingMaterial is IIfcMaterialProfile)
            {
               IIfcMaterialProfile mProfile = relMat.RelatingMaterial as IIfcMaterialProfile;
               string profileName = "-";
               string material;
               if (mProfile.Material != null)
                  material = "(" + mProfile.Material.Name + ", ";
               else
                  material = "(-, ";
               if (mProfile.Profile.ProfileName.HasValue)
                  profileName = mProfile.Profile.ProfileName.ToString();

               material += profileName + ")";
               arrMatName.Add(material);

               arrSetName.Add(string.Empty);
               arrSetNPS.Add(OracleParameterStatus.NullInsert);
               arrMatSeq.Add(0);
               arrMatSPS.Add(OracleParameterStatus.NullInsert);
               arrMatThick.Add(0);
               arrMatTPS.Add(OracleParameterStatus.NullInsert);
               arrIsVentilated.Add(string.Empty);
               arrIsVPS.Add(OracleParameterStatus.NullInsert);
            }
            else if (relMat.RelatingMaterial is IIfcMaterialProfileSet
                     || relMat.RelatingMaterial is IIfcMaterialProfileSetUsage
                     || relMat.RelatingMaterial is IIfcMaterialProfileSetUsageTapering)
            {
               IIfcMaterialProfileSet mProfileSet;
               IIfcMaterialProfileSet mProfileSetEnd = null;
               if (relMat.RelatingMaterial is IIfcMaterialProfileSetUsage)
               {
                  // We do not handle other information, except the material name and the profile name
                  IIfcMaterialProfileSetUsage mPSU = relMat.RelatingMaterial as IIfcMaterialProfileSetUsage;
                  mProfileSet = mPSU.ForProfileSet;

                  if (relMat.RelatingMaterial is IIfcMaterialProfileSetUsageTapering)
                     mProfileSetEnd = (relMat.RelatingMaterial as IIfcMaterialProfileSetUsageTapering).ForProfileEndSet;
               }
               else
                  mProfileSet = relMat.RelatingMaterial as IIfcMaterialProfileSet;

               string material = getMaterialProfileSetString(mProfileSet);
               if (mProfileSetEnd != null)
                  BIMRLCommon.appendToString(getMaterialProfileSetString(mProfileSetEnd), " | ", ref material);

               Int16 seqNo = 1;
               foreach (IIfcMaterialProfile mProf in mProfileSet.MaterialProfiles)
               {
                  if (mProfileSet.Name != null)
                  {
                     arrSetName.Add(mProfileSet.Name);
                     arrSetNPS.Add(OracleParameterStatus.Success);
                  }
                  else
                  {
                     arrSetName.Add(string.Empty);
                     arrSetNPS.Add(OracleParameterStatus.NullInsert);
                  }

                  arrMatName.Add(material);

                  arrMatSeq.Add(seqNo++);
                  arrMatSPS.Add(OracleParameterStatus.Success);
                  arrMatThick.Add(0);
                  arrMatTPS.Add(OracleParameterStatus.Success);
                  arrIsVentilated.Add(string.Empty);
                  arrIsVPS.Add(OracleParameterStatus.NullInsert);
               }
            }
            else
            {
               // Not supported type
            }

            IEnumerable<IIfcDefinitionSelect> relObjects = relMat.RelatedObjects;
            foreach (IIfcDefinitionSelect relObjSel in relObjects)
            {
               IIfcObjectDefinition relObj = relObjSel as IIfcObjectDefinition;
               if (!(relObj is IIfcProduct) && !(relObj is IIfcTypeProduct))
                     continue;

               string guid = relObj.GlobalId.ToString();

               for (int i = 0; i < arrMatName.Count; i++)
               {
                  if (relObj is IIfcProduct)
                  {
                        insGuid.Add(guid);
                        insMatName.Add(arrMatName[i]);
                        insSetName.Add(arrSetName[i]);
                        insSetNPS.Add(arrSetNPS[i]);
                        insIsVentilated.Add(arrIsVentilated[i]);
                        insIsVPS.Add(arrIsVPS[i]);
                        insMatSeq.Add(arrMatSeq[i]);
                        insMatSPS.Add(arrMatSPS[i]);
                        insMatThick.Add(arrMatThick[i]);
                        insMatTPS.Add(arrMatTPS[i]);
                  }
                  else
                  {
                        insTGuid.Add(guid);
                        insTMatName.Add(arrMatName[i]);
                        insTSetName.Add(arrSetName[i]);
                        insTSetNPS.Add(arrSetNPS[i]);
                        insTIsVentilated.Add(arrIsVentilated[i]);
                        insTIsVPS.Add(arrIsVPS[i]);
                        insTMatSeq.Add(arrMatSeq[i]);
                        insTMatSPS.Add(arrMatSPS[i]);
                        insTMatThick.Add(arrMatThick[i]);
                        insTMatTPS.Add(arrMatTPS[i]);
                  }
               }
            }

            if ((insGuid.Count + insTGuid.Count) >= DBOperation.commitInterval)
            {
               int commandStatus;
               try
               {
                  if (insTGuid.Count > 0)
                  {
                        currStep = "Processing Type Materials";
                        Param[0].Value = insTGuid.ToArray();
                        Param[1].Value = insTMatName.ToArray();
                        Param[2].Value = insTSetName.ToArray();
                        Param[2].ArrayBindStatus = insTSetNPS.ToArray();
                        Param[3].Value = insTIsVentilated.ToArray();
                        Param[3].ArrayBindStatus = insTIsVPS.ToArray();
                        Param[4].Value = insTMatSeq.ToArray();
                        Param[4].ArrayBindStatus = insTMatSPS.ToArray();
                        Param[5].Value = insTMatThick.ToArray();
                        Param[5].ArrayBindStatus = insTMatTPS.ToArray();
                        for (int i = 0; i < 6; i++)
                           Param[i].Size = insTGuid.Count;
                        command.ArrayBindCount = insTGuid.Count;

                        commandStatus = command.ExecuteNonQuery();
                  }

                  if (insGuid.Count > 0)
                  {
                        currStep = "Processing Element Materials";

                        Param2[0].Value = insGuid.ToArray();
                        Param2[1].Value = insMatName.ToArray();
                        Param2[2].Value = insSetName.ToArray();
                        Param2[2].ArrayBindStatus = insSetNPS.ToArray();
                        Param2[3].Value = insIsVentilated.ToArray();
                        Param2[3].ArrayBindStatus = insIsVPS.ToArray();
                        Param2[4].Value = insMatSeq.ToArray();
                        Param2[4].ArrayBindStatus = insMatSPS.ToArray();
                        Param2[5].Value = insMatThick.ToArray();
                        Param2[5].ArrayBindStatus = insMatTPS.ToArray();
                        for (int i = 0; i < 6; i++)
                           Param2[i].Size = insGuid.Count;
                        command2.ArrayBindCount = insGuid.Count;

                        commandStatus = command2.ExecuteNonQuery();
                  }

                  DBOperation.commitTransaction();
                        
                  insTGuid.Clear();
                  insTMatName.Clear();
                  insTSetName.Clear();
                  insTSetNPS.Clear();
                  insTIsVentilated.Clear();
                  insTIsVPS.Clear();
                  insTMatSeq.Clear();
                  insTMatSPS.Clear();
                  insTMatThick.Clear();
                  insTMatTPS.Clear();

                  insGuid.Clear();
                  insMatName.Clear();
                  insSetName.Clear();
                  insSetNPS.Clear();
                  insIsVentilated.Clear();
                  insIsVPS.Clear();
                  insMatSeq.Clear();
                  insMatSPS.Clear();
                  insMatThick.Clear();
                  insMatTPS.Clear();
               }
               catch (OracleException e)
               {
                  string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                  _refBIMRLCommon.StackPushIgnorableError(excStr);

                  arrMatName.Clear();
                  arrSetName.Clear();
                  arrSetNPS.Clear();
                  arrIsVentilated.Clear();
                  arrIsVPS.Clear();
                  arrMatSeq.Clear();
                  arrMatSPS.Clear();
                  arrMatThick.Clear();
                  arrMatTPS.Clear();

                  insTGuid.Clear();
                  insTMatName.Clear();
                  insTSetName.Clear();
                  insTSetNPS.Clear();
                  insTIsVentilated.Clear();
                  insTIsVPS.Clear();
                  insTMatSeq.Clear();
                  insTMatSPS.Clear();
                  insTMatThick.Clear();
                  insTMatTPS.Clear();

                  insGuid.Clear();
                  insMatName.Clear();
                  insSetName.Clear();
                  insSetNPS.Clear();
                  insIsVentilated.Clear();
                  insIsVPS.Clear();
                  insMatSeq.Clear();
                  insMatSPS.Clear();
                  insMatThick.Clear();
                  insMatTPS.Clear();

                  continue;
               }
               catch (SystemException e)
               {
                  string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                  _refBIMRLCommon.StackPushError(excStr);
                  throw;
               }
            }
         }

         if ((insGuid.Count + insTGuid.Count) > 0)
         {
               int commandStatus;
               try
               {
                  if (insTGuid.Count > 0)
                  {
                     currStep = "Processing Type Materials";
                     Param[0].Value = insTGuid.ToArray();
                     Param[1].Value = insTMatName.ToArray();
                     Param[2].Value = insTSetName.ToArray();
                     Param[2].ArrayBindStatus = insTSetNPS.ToArray();
                     Param[3].Value = insTIsVentilated.ToArray();
                     Param[3].ArrayBindStatus = insTIsVPS.ToArray();
                     Param[4].Value = insTMatSeq.ToArray();
                     Param[4].ArrayBindStatus = insTMatSPS.ToArray();
                     Param[5].Value = insTMatThick.ToArray();
                     Param[5].ArrayBindStatus = insTMatTPS.ToArray();
                     for (int i = 0; i < 6; i++)
                           Param[i].Size = insTGuid.Count;
                     command.ArrayBindCount = insTGuid.Count;

                     commandStatus = command.ExecuteNonQuery();
                  }

                  if (insGuid.Count > 0)
                  {
                     currStep = "Processing Element Materials";

                     Param2[0].Value = insGuid.ToArray();
                     Param2[1].Value = insMatName.ToArray();
                     Param2[2].Value = insSetName.ToArray();
                     Param2[2].ArrayBindStatus = insSetNPS.ToArray();
                     Param2[3].Value = insIsVentilated.ToArray();
                     Param2[3].ArrayBindStatus = insIsVPS.ToArray();
                     Param2[4].Value = insMatSeq.ToArray();
                     Param2[4].ArrayBindStatus = insMatSPS.ToArray();
                     Param2[5].Value = insMatThick.ToArray();
                     Param2[5].ArrayBindStatus = insMatTPS.ToArray();
                     for (int i = 0; i < 6; i++)
                           Param2[i].Size = insGuid.Count;
                     command2.ArrayBindCount = insGuid.Count;

                     commandStatus = command2.ExecuteNonQuery();
                  }

                  DBOperation.commitTransaction();
               }
               catch (OracleException e)
               {
                  string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                  _refBIMRLCommon.StackPushIgnorableError(excStr);
               }
               catch (SystemException e)
               {
                  string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                  _refBIMRLCommon.StackPushError(excStr);
                  throw;
               }
         }

         DBOperation.commitTransaction();
         command.Dispose();
         command2.Dispose();
      }

      string getMaterialProfileSetString(IIfcMaterialProfileSet mProfileSet)
      {
         string material = string.Empty;

         foreach (IIfcMaterialProfile mProfile in mProfileSet.MaterialProfiles)
         {
            string profileName = string.Empty;
            string materialProfile = "-";
            if (mProfile.Material != null)
               materialProfile = "(" + mProfile.Material.Name + ", ";
            else
               materialProfile = "(-, ";

            if (mProfile.Profile.ProfileName.HasValue)
               profileName = mProfile.Profile.ProfileName.ToString();

            materialProfile += profileName + ")";

            BIMRLCommon.appendToString(materialProfile, ", ", ref material);
         }

         return material;
      }

   }
}
