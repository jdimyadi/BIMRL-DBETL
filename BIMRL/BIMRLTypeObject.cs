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
using NetSdoGeometry;
using BIMRL.OctreeLib;

namespace BIMRL
{
    public class BIMRLTypeObject
    {
        BIMRLCommon _refBIMRLCommon;
        IfcStore _model;

        public BIMRLTypeObject(IfcStore m, BIMRLCommon refBIMRLCommon)
        {
            _refBIMRLCommon = refBIMRLCommon;
            _model = m;
        }

        public void processTypeObject()
        {
            IEnumerable<IIfcTypeProduct> types = _model.Instances.OfType<IIfcTypeProduct>();
            foreach (IIfcTypeProduct typ in types)
            {
                OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

                string SqlStmt = "Insert into BIMRL_TYPE_" + bimrlProcessModel.currFedID.ToString("X4") + "(ElementId, IfcType, Name, Description, ApplicableOccurrence"
                                    + ", Tag, ElementType, PredefinedType, AssemblyPlace, OperationType, ConstructionType, OwnerHistoryID, ModelID)"
                                    + " Values (:1, :2, :3, :4, :5, :6, :7, :8, :9, :10, :11, :12, :13)";
                command.CommandText = SqlStmt;
                string currStep = SqlStmt;

                OracleParameter[] Param = new OracleParameter[13];
                for (int i = 0; i < 11; i++)
                {
                    Param[i] = command.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
                    Param[i].Direction = ParameterDirection.Input;
                    Param[i].Size = 1;
                }
                Param[11] = command.Parameters.Add("11", OracleDbType.Int32);
                Param[11].Direction = ParameterDirection.Input;
                Param[11].Size = 1;
                Param[12] = command.Parameters.Add("12", OracleDbType.Int32);
                Param[12].Direction = ParameterDirection.Input;
                Param[12].Size = 1;

                List<OracleParameterStatus> arrBStatN = new List<OracleParameterStatus>();
                List<OracleParameterStatus> arrBStatS = new List<OracleParameterStatus>();
                arrBStatN.Add(OracleParameterStatus.NullInsert);
                arrBStatS.Add(OracleParameterStatus.Success);

                List<string> arrGuid = new List<string>();
                List<string> IfcType = new List<string>();
                List<string> arrName = new List<string>();
                List<string> arrDesc = new List<string>();
                List<string> arrAppO = new List<string>();
                List<string> arrTag = new List<string>();
                List<string> arrETyp = new List<string>();
                List<string> arrPDTyp = new List<string>();
                List<string> arrAPl = new List<string>();
                List<string> arrOpTyp = new List<string>();
                List<string> arrConsTyp = new List<string>();
                List<int> arrOwnH = new List<int>();
                List<OracleParameterStatus> arrOwnHPS = new List<OracleParameterStatus>();
                List<int> arrModelID = new List<int>();

                arrGuid.Add(typ.GlobalId.ToString());
                Param[0].Value = arrGuid.ToArray();

                IfcType.Add(typ.GetType().Name.ToUpper());
                Param[1].Value = IfcType.ToArray();

                if (typ.Name == null)
                {
                    arrName.Add(string.Empty);
                    Param[2].ArrayBindStatus = arrBStatN.ToArray();
                }
                else
                {
                    arrName.Add(typ.Name);
                    Param[2].ArrayBindStatus = arrBStatS.ToArray();
                }
                Param[2].Value = arrName.ToArray();

                if (typ.Description == null)
                {
                    arrDesc.Add(string.Empty);
                    Param[3].ArrayBindStatus = arrBStatN.ToArray();
                }
                else
                {
                    arrDesc.Add(typ.Description);
                    Param[3].ArrayBindStatus = arrBStatS.ToArray();
                }
                Param[3].Value = arrDesc.ToArray();

                if (typ.ApplicableOccurrence == null)
                {
                    arrAppO.Add(string.Empty);
                    Param[4].ArrayBindStatus = arrBStatN.ToArray();
                }
                else
                {
                    arrAppO.Add(typ.ApplicableOccurrence);
                    Param[4].ArrayBindStatus = arrBStatS.ToArray();
                }
                Param[4].Value = arrAppO.ToArray();

                if (typ.Tag == null)
                {
                    arrTag.Add(string.Empty);
                    Param[5].ArrayBindStatus = arrBStatN.ToArray();
                }
                else
                {
                    arrTag.Add(typ.Tag);
                    Param[5].ArrayBindStatus = arrBStatS.ToArray();
                } 
                Param[5].Value = arrTag.ToArray();


                dynamic dynTyp = typ;
                if (!(typ is IIfcDoorStyle || typ is IIfcWindowStyle))
                {
                    if (dynTyp.ElementType == null)
                    {
                        arrETyp.Add(string.Empty);
                        Param[6].ArrayBindStatus = arrBStatN.ToArray();
                    }
                    else
                    {
                        arrETyp.Add(dynTyp.ElementType);
                        Param[6].ArrayBindStatus = arrBStatS.ToArray();
                    }
                    Param[6].Value = arrETyp.ToArray();
                }

                if (typ is IIfcFurnitureType)
                {
                    // these entities do not have PredefinedType
                    arrPDTyp.Add(string.Empty);
                    Param[7].Value = arrPDTyp.ToArray();
                    Param[7].ArrayBindStatus = arrBStatN.ToArray();
                    // This entity has a different attribute: AssemblyPlace. This must be placed ahead of its supertype IfcFurnishingElementType
                    IIfcFurnitureType ftyp = typ as IIfcFurnitureType;
                    arrAPl.Add(ftyp.AssemblyPlace.ToString());
                    Param[8].Value = arrAPl.ToArray();
                    if (String.IsNullOrEmpty(ftyp.AssemblyPlace.ToString()))
                        Param[8].ArrayBindStatus = arrBStatN.ToArray();
                    else
                        Param[8].ArrayBindStatus = arrBStatS.ToArray();

                    arrOpTyp.Add(string.Empty);
                    Param[9].Value = arrOpTyp.ToArray();
                    Param[9].ArrayBindStatus = arrBStatN.ToArray();
                    arrConsTyp.Add(string.Empty);
                    Param[10].Value = arrConsTyp.ToArray();
                    Param[10].ArrayBindStatus = arrBStatN.ToArray();
                }
                else if (typ is IIfcFastenerType || typ is IIfcMechanicalFastenerType || typ is IIfcFurnishingElementType || typ is IIfcSystemFurnitureElementType
                    || typ is IIfcDiscreteAccessoryType || typ is IIfcCurtainWallType)
                {
                    // these entities do not have PredefinedType. Xbim also has not implemented IfcCurtainWallType and therefore no PredefinedType yet!!
                    arrPDTyp.Add(string.Empty);
                    Param[7].Value = arrPDTyp.ToArray();
                    Param[7].ArrayBindStatus = arrBStatN.ToArray();
                    arrAPl.Add(string.Empty);
                    Param[8].Value = arrAPl.ToArray();
                    Param[8].ArrayBindStatus = arrBStatN.ToArray();
                    arrOpTyp.Add(string.Empty);
                    Param[9].Value = arrOpTyp.ToArray();
                    Param[9].ArrayBindStatus = arrBStatN.ToArray();
                    arrConsTyp.Add(string.Empty);
                    Param[10].Value = arrConsTyp.ToArray();
                    Param[10].ArrayBindStatus = arrBStatN.ToArray();
                }

                // These entities do not have predefinedtype, but OperationType and ConstructionType
                // We ignore ParameterTakesPrecedence and Sizeable are only useful for object construction
                else if (typ is IIfcDoorStyle)
                {
                    // these entities do not have PredefinedType
                    arrETyp.Add(string.Empty);
                    Param[6].Value = arrETyp.ToArray();
                    Param[6].ArrayBindStatus = arrBStatN.ToArray(); 
                    arrPDTyp.Add(string.Empty);
                    Param[7].Value = arrPDTyp.ToArray();
                    Param[7].ArrayBindStatus = arrBStatN.ToArray();
                    arrAPl.Add(string.Empty);
                    Param[8].Value = arrAPl.ToArray();
                    Param[8].ArrayBindStatus = arrBStatN.ToArray();

                    IIfcDoorStyle dst = typ as IIfcDoorStyle;
                     arrOpTyp.Add(dst.OperationType.ToString());
                     Param[9].Value = arrOpTyp.ToArray();
                     Param[9].ArrayBindStatus = arrBStatS.ToArray();

                     arrConsTyp.Add(dst.OperationType.ToString());
                     Param[10].Value = arrConsTyp.ToArray();
                     Param[10].ArrayBindStatus = arrBStatS.ToArray();
                }
                else if (typ is IIfcWindowStyle)
                {
                    // these entities do not have PredefinedType
                    arrETyp.Add(string.Empty);
                    Param[6].Value = arrETyp.ToArray();
                    Param[6].ArrayBindStatus = arrBStatN.ToArray();
                    arrPDTyp.Add(string.Empty);
                    Param[7].Value = arrPDTyp.ToArray();
                    Param[7].ArrayBindStatus = arrBStatN.ToArray();
                    arrAPl.Add(string.Empty);
                    Param[8].Value = arrAPl.ToArray();
                    Param[8].ArrayBindStatus = arrBStatN.ToArray();
                    
                    IIfcWindowStyle wst = typ as IIfcWindowStyle;
                     arrOpTyp.Add(wst.OperationType.ToString());
                     Param[9].Value = arrOpTyp.ToArray();
                     Param[9].ArrayBindStatus = arrBStatS.ToArray();

                     arrConsTyp.Add(wst.OperationType.ToString());
                     Param[10].Value = arrConsTyp.ToArray();
                     Param[10].ArrayBindStatus = arrBStatS.ToArray();
                }
                else
                {
                    arrPDTyp.Add(dynTyp.PredefinedType.ToString());
                    Param[7].Value = arrPDTyp.ToArray();
                    if (String.IsNullOrEmpty(dynTyp.PredefinedType.ToString()))
                        Param[7].ArrayBindStatus = arrBStatN.ToArray();
                    else
                        Param[7].ArrayBindStatus = arrBStatS.ToArray();
                    arrAPl.Add(string.Empty);
                    arrOpTyp.Add(string.Empty);
                    arrConsTyp.Add(string.Empty);
                    // These are specific attributes only for specific types handled below
                    Param[8].Value = arrAPl.ToArray();
                    Param[8].ArrayBindStatus = arrBStatN.ToArray();
                    Param[9].Value = arrOpTyp.ToArray();
                    Param[9].ArrayBindStatus = arrBStatN.ToArray();
                    Param[10].Value = arrConsTyp.ToArray();
                    Param[10].ArrayBindStatus = arrBStatN.ToArray();
                }

                Tuple<int, int> ownHEntry = new Tuple<int, int>(Math.Abs(typ.OwnerHistory.EntityLabel), bimrlProcessModel.currModelID);
                if (_refBIMRLCommon.OwnerHistoryExist(ownHEntry))
                {
                    arrOwnH.Add(Math.Abs(typ.OwnerHistory.EntityLabel));
                    arrOwnHPS.Add(OracleParameterStatus.Success);
                }
                else
                {
                    arrOwnH.Add(0);
                    arrOwnHPS.Add(OracleParameterStatus.NullInsert);
                }
                Param[11].Value = arrOwnH.ToArray();
                Param[11].ArrayBindStatus = arrOwnHPS.ToArray();

                arrModelID.Add(bimrlProcessModel.currModelID);
                Param[12].Value = arrModelID.ToArray();
                           
                command.ArrayBindCount = 1;
                try
                {
                    int commandStatus = command.ExecuteNonQuery();
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

                BIMRLProperties tProps = new BIMRLProperties(_refBIMRLCommon);
                tProps.processTypeProperties(typ);

            }
        }
    }
}
