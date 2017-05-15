//
// BIMRL (BIM Rule Language) Simplified Schema ETL (Extract, Transform, Load) library: this library transforms IFC data into BIMRL Simplified Schema for RDBMS. 
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
   public class BIMRLRelationship
   {
      private IfcStore _model;
      private BIMRLCommon _refBIMRLCommon;

      public BIMRLRelationship(IfcStore m, BIMRLCommon refBIMRLCommon)
      {
         _model = m;
         _refBIMRLCommon = refBIMRLCommon;
      }

      public void processRelationships()
      {
         processRelAggregation();
         processRelConnections();
         processRelSpaceBoundary();
         processRelGroup();
         processElemDependency();
      }

      private void processRelAggregation()
      {
         List<string> arrMastGuids = new List<string>();
         List<string> arrMastTypes = new List<string>();
         List<string> arrAggrGuids = new List<string>();
         List<string> arrAggrTypes = new List<string>();
         string SqlStmt;
         string currStep = string.Empty;

         DBOperation.beginTransaction();

         int commandStatus = -1;
         string parentGuid = string.Empty;
         string parentType = string.Empty;

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

         SqlStmt = "insert into BIMRL_RELAGGREGATION_" + bimrlProcessModel.currFedID.ToString("X4")
                     + " (MASTERELEMENTID, MASTERELEMENTTYPE, AGGREGATEELEMENTID, AGGREGATEELEMENTTYPE) values (:mGuids, :mType, :aGuids, :aType )";
         command.CommandText = SqlStmt;
         currStep = SqlStmt;

         OracleParameter[] Param = new OracleParameter[4];
         Param[0] = command.Parameters.Add("mGuids", OracleDbType.Varchar2);
         Param[0].Direction = ParameterDirection.Input;
         Param[1] = command.Parameters.Add("mType", OracleDbType.Varchar2);
         Param[1].Direction = ParameterDirection.Input;
         Param[2] = command.Parameters.Add("aGuids", OracleDbType.Varchar2);
         Param[2].Direction = ParameterDirection.Input;
         Param[3] = command.Parameters.Add("aType", OracleDbType.Varchar2);
         Param[3].Direction = ParameterDirection.Input;


         IEnumerable<IIfcRelAggregates> rels = _model.Instances.OfType<IIfcRelAggregates>();
         foreach (IIfcRelAggregates aggr in rels)
         {
               string aggrGuid = aggr.RelatingObject.GlobalId.ToString();
               string aggrType = aggr.RelatingObject.GetType().Name.ToUpper();
               if (_refBIMRLCommon.getLineNoFromMapping(aggrGuid) == null)
                  continue;   // skip relationship that involves "non" element Guids

               IEnumerable<IIfcObjectDefinition> relObjects = aggr.RelatedObjects;
               foreach (IIfcObjectDefinition relObj in relObjects)
               {
                  string relObjGuid = relObj.GlobalId.ToString();
                  string relObjType = relObj.GetType().Name.ToUpper();
                  arrMastGuids.Add(aggrGuid);
                  arrMastTypes.Add(aggrType);
                  arrAggrGuids.Add(relObjGuid);
                  arrAggrTypes.Add(relObjType);
               }

               if (arrMastGuids.Count >= DBOperation.commitInterval)
               {
                  Param[0].Size = arrMastGuids.Count();
                  Param[0].Value = arrMastGuids.ToArray();
                  Param[1].Size = arrMastTypes.Count();
                  Param[1].Value = arrMastTypes.ToArray();
                  Param[2].Size = arrAggrGuids.Count();
                  Param[2].Value = arrAggrGuids.ToArray();
                  Param[3].Size = arrAggrTypes.Count();
                  Param[3].Value = arrAggrTypes.ToArray();

                  try
                  {
                     command.ArrayBindCount = arrMastGuids.Count;    // No of values in the array to be inserted
                     commandStatus = command.ExecuteNonQuery();
                     //Do commit at interval but keep the long transaction (reopen)
                     DBOperation.commitTransaction();
                     arrMastGuids.Clear();
                     arrMastTypes.Clear();
                     arrAggrGuids.Clear();
                     arrAggrTypes.Clear();
                  }
                  catch (OracleException e)
                  {
                     string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                     _refBIMRLCommon.StackPushIgnorableError(excStr);
                     // Ignore any error
                     arrMastGuids.Clear();
                     arrMastTypes.Clear();
                     arrAggrGuids.Clear();
                     arrAggrTypes.Clear();
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

         if (arrMastGuids.Count > 0)
         {
               Param[0].Size = arrMastGuids.Count();
               Param[0].Value = arrMastGuids.ToArray();
               Param[1].Size = arrMastTypes.Count();
               Param[1].Value = arrMastTypes.ToArray();
               Param[2].Size = arrAggrGuids.Count();
               Param[2].Value = arrAggrGuids.ToArray();
               Param[3].Size = arrAggrTypes.Count();
               Param[3].Value = arrAggrTypes.ToArray();

               try
               {
                  command.ArrayBindCount = arrMastGuids.Count;    // No of values in the array to be inserted
                  commandStatus = command.ExecuteNonQuery();
                  //Do commit at interval but keep the long transaction (reopen)
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
      }

      private void processRelConnections()
      {
         string SqlStmt;
         string currStep = string.Empty;

         DBOperation.beginTransaction();

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

         SqlStmt = "insert into BIMRL_RELCONNECTION_" + bimrlProcessModel.currFedID.ToString("X4")
                     + " (CONNECTINGELEMENTID, CONNECTINGELEMENTTYPE, CONNECTINGELEMENTATTRNAME, CONNECTINGELEMENTATTRVALUE, "
                     + "CONNECTEDELEMENTID, CONNECTEDELEMENTTYPE, CONNECTEDELEMENTATTRNAME, CONNECTEDELEMENTATTRVALUE, "
                     + "CONNECTIONATTRNAME, CONNECTIONATTRVALUE, REALIZINGELEMENTID, REALIZINGELEMENTTYPE, RELATIONSHIPTYPE) "
                     + "VALUES (:1, :2, :3, :4, :5, :6, :7, :8, :9, :10, :11, :12, :13)";
         command.CommandText = SqlStmt;
         currStep = SqlStmt;

         OracleParameter[] Param = new OracleParameter[13];
         for (int i = 0; i < 13; i++)
         {
               Param[i] = command.Parameters.Add((i + 1).ToString(), OracleDbType.Varchar2);
               Param[i].Direction = ParameterDirection.Input;
         }

         int commandStatus = -1;

         List<string> cIngEle = new List<string>();
         List<string> cIngEleTyp = new List<string>();
         List<string> cIngAttrN = new List<string>();
         List<string> cIngAttrV = new List<string>();
         List<string> cEdEle = new List<string>();
         List<string> cEdEleTyp = new List<string>();
         List<string> cEdAttrN = new List<string>();
         List<string> cEdAttrV = new List<string>();
         List<string> cAttrN = new List<string>();
         List<string> cAttrV = new List<string>();
         List<string> realEl = new List<string>();
         List<string> realElTyp = new List<string>();
         List<string> relTyp = new List<string>();
         List<OracleParameterStatus> cIngAttrNBS = new List<OracleParameterStatus>();
         List<OracleParameterStatus> cIngAttrVBS = new List<OracleParameterStatus>();
         List<OracleParameterStatus> cEdAttrNBS = new List<OracleParameterStatus>();
         List<OracleParameterStatus> cEdAttrVBS = new List<OracleParameterStatus>();
         List<OracleParameterStatus> cAttrNBS = new List<OracleParameterStatus>();
         List<OracleParameterStatus> cAttrVBS = new List<OracleParameterStatus>();
         List<OracleParameterStatus> realElBS = new List<OracleParameterStatus>();
         List<OracleParameterStatus> realElTBS = new List<OracleParameterStatus>();

         // Do speacial step first by processing the IfcRelConnecsPortToElement to match FccDistributionPort and IfcElement for MEP connectivity
         IEnumerable<IIfcRelConnectsPortToElement> ptes = _model.Instances.OfType<IIfcRelConnectsPortToElement>();
         foreach (IIfcRelConnectsPortToElement pte in ptes)
         {
               Dictionary<string, string> portElemVal;
               portElemVal = _refBIMRLCommon.PortToElem_GetValue(pte.RelatingPort.GlobalId.ToString());
               if (portElemVal == null)
                  portElemVal = new Dictionary<string, string>();
               portElemVal.Add("RELATEDELEMENT", pte.RelatedElement.GlobalId.ToString());
               portElemVal.Add("RELATEDELEMENTTYPE", pte.RelatedElement.GetType().Name.ToUpper());
         }

         IEnumerable<IIfcRelConnects> rels = _model.Instances.OfType<IIfcRelConnects>().Where
                  (re => !(re is IIfcRelConnectsPortToElement || re is IIfcRelContainedInSpatialStructure || re is IIfcRelConnectsStructuralActivity || re is IIfcRelConnectsStructuralMember
                           || re is IIfcRelFillsElement || re is IIfcRelVoidsElement || re is IIfcRelSequence || re is IIfcRelSpaceBoundary));
         foreach (IIfcRelConnects conn in rels)
         {
            if (conn is IIfcRelConnectsPathElements)
            {
               IIfcRelConnectsPathElements connPE = conn as IIfcRelConnectsPathElements;
               if (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingElement.GlobalId.ToString()) == null)
                  continue;       // skip "non" element guid in the relationship object

               cIngEle.Add(connPE.RelatingElement.GlobalId.ToString());
               cIngEleTyp.Add(connPE.RelatingElement.GetType().Name.ToUpper());
               cEdEle.Add(connPE.RelatedElement.GlobalId.ToString());
               cEdEleTyp.Add(connPE.RelatedElement.GetType().Name.ToUpper());
               cIngAttrN.Add("RELATINGCONNECTIONTYPE");
               cIngAttrNBS.Add(OracleParameterStatus.Success);
               cIngAttrV.Add(connPE.RelatingConnectionType.ToString());
               cIngAttrVBS.Add(OracleParameterStatus.Success);
               cEdAttrN.Add("RELATEDCONNECTIONTYPE");
               cEdAttrNBS.Add(OracleParameterStatus.Success);
               cEdAttrV.Add(connPE.RelatedConnectionType.ToString());
               cEdAttrVBS.Add(OracleParameterStatus.Success);
               cAttrN.Add(string.Empty);
               cAttrNBS.Add(OracleParameterStatus.NullInsert);
               cAttrV.Add(string.Empty);
               cAttrVBS.Add(OracleParameterStatus.NullInsert);
               realEl.Add(string.Empty);
               realElTyp.Add(string.Empty);
               realElTBS.Add(OracleParameterStatus.NullInsert);
               realElBS.Add(OracleParameterStatus.NullInsert);
               relTyp.Add(connPE.GetType().Name.ToUpper());
            }
            else if (conn is IIfcRelConnectsWithRealizingElements)
            {
               IIfcRelConnectsWithRealizingElements connPE = conn as IIfcRelConnectsWithRealizingElements;
               if (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingElement.GlobalId.ToString()) == null)
                  continue;       // skip "non" element guid in the relationship object

               //Iterate for each Realizing element. One record for each realizing element
               foreach (IIfcElement realElem in connPE.RealizingElements)
               {
                  cIngEle.Add(connPE.RelatingElement.GlobalId.ToString());
                  cIngEleTyp.Add(connPE.RelatingElement.GetType().Name.ToUpper());
                  cEdEle.Add(connPE.RelatedElement.GlobalId.ToString());
                  cEdEleTyp.Add(connPE.RelatedElement.GetType().Name.ToUpper());
                  cIngAttrN.Add(string.Empty);
                  cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cIngAttrV.Add(string.Empty);
                  cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrN.Add(string.Empty);
                  cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrV.Add(string.Empty);
                  cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
                  if (connPE.ConnectionType == null)
                  {
                        cAttrN.Add(string.Empty);
                        cAttrNBS.Add(OracleParameterStatus.NullInsert);
                        cAttrV.Add(string.Empty);
                        cAttrVBS.Add(OracleParameterStatus.NullInsert);
                  }
                  else
                  {
                        cAttrN.Add("CONNECTIONTYPE");
                        cAttrNBS.Add(OracleParameterStatus.Success);
                        cAttrV.Add(connPE.ConnectionType.ToString());
                        cAttrVBS.Add(OracleParameterStatus.Success);
                  }
                  realEl.Add(realElem.GlobalId.ToString());
                  realElBS.Add(OracleParameterStatus.Success);
                  realElTyp.Add(realElem.GetType().Name.ToUpper());
                  realElTBS.Add(OracleParameterStatus.Success);
                  relTyp.Add(connPE.GetType().Name.ToUpper());
               }
            }
            else if (conn is IIfcRelConnectsElements)
            {
               IIfcRelConnectsElements connPE = conn as IIfcRelConnectsElements;
               if (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingElement.GlobalId.ToString()) == null)
                  continue;       // skip "non" element guid in the relationship object

               cIngEle.Add(connPE.RelatingElement.GlobalId.ToString());
               cIngEleTyp.Add(connPE.RelatingElement.GetType().Name.ToUpper());
               cEdEle.Add(connPE.RelatedElement.GlobalId.ToString());
               cEdEleTyp.Add(connPE.RelatedElement.GetType().Name.ToUpper());
               cIngAttrN.Add(string.Empty);
               cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
               cIngAttrV.Add(string.Empty);
               cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
               cEdAttrN.Add(string.Empty);
               cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
               cEdAttrV.Add(string.Empty);
               cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
               cAttrN.Add(string.Empty);
               cAttrNBS.Add(OracleParameterStatus.NullInsert);
               cAttrV.Add(string.Empty);
               cAttrVBS.Add(OracleParameterStatus.NullInsert);
               realEl.Add(string.Empty);
               realElBS.Add(OracleParameterStatus.NullInsert);
               realElTyp.Add(string.Empty);
               realElTBS.Add(OracleParameterStatus.NullInsert);
               relTyp.Add(connPE.GetType().Name.ToUpper());
            }
            else if (conn is IIfcRelConnectsPorts)
            {
               // Handle MEP connections through Port connections IfcRelConnectsPorts (Port itself won't be created)
               // From Port connection, we will find both ends of the connection (we will get ports) and
               // from there through IfcRelConnectsPortToElement, we will get both ends of the element connected

               IIfcRelConnectsPorts connPE = conn as IIfcRelConnectsPorts;

               IIfcDistributionPort port1 = connPE.RelatingPort as IIfcDistributionPort;
               IIfcDistributionPort port2 = connPE.RelatedPort as IIfcDistributionPort;

               Dictionary<string, string> portElemVal1;
               Dictionary<string, string> portElemVal2;

               portElemVal1 = _refBIMRLCommon.PortToElem_GetValue(port1.GlobalId.ToString());
               portElemVal2 = _refBIMRLCommon.PortToElem_GetValue(port2.GlobalId.ToString());
               if (portElemVal1 == null || portElemVal2 == null)
               {
                  // This should not happen. If somehow happen, skip such a hanging port
                  continue;
               }

               string eleGuid1;
               string eleType1;
               string eleGuid2;
               string eleType2;
               string attrName1;
               string attrVal1;
               string attrName2;
               string attrVal2;

               portElemVal1.TryGetValue("RELATEDELEMENT", out eleGuid1);
               portElemVal2.TryGetValue("RELATEDELEMENT", out eleGuid2);
               if (String.IsNullOrEmpty(eleGuid1) || String.IsNullOrEmpty(eleGuid2))
                  continue;   // Should not happen!


               // We will insert 2 record for each relationship to represent the both directional of the relationship
               portElemVal1.TryGetValue("RELATEDELEMENTTYPE", out eleType1);
               portElemVal2.TryGetValue("RELATEDELEMENTTYPE", out eleType2);
               portElemVal1.TryGetValue("ATTRIBUTENAME", out attrName1);
               portElemVal2.TryGetValue("ATTRIBUTENAME", out attrName2);
               portElemVal1.TryGetValue("ATTRIBUTEVALUE", out attrVal1);
               portElemVal2.TryGetValue("ATTRIBUTEVALUE", out attrVal2);

               cIngEle.Add(eleGuid1);
               cIngEle.Add(eleGuid2);

               cIngEleTyp.Add(eleType1);
               cIngEleTyp.Add(eleType2);

               cEdEle.Add(eleGuid2);
               cEdEle.Add(eleGuid1);

               cEdEleTyp.Add(eleType2);
               cEdEleTyp.Add(eleType1);

               // RealizingElement if any
               if (connPE.RealizingElement != null)
               {
                  realEl.Add(connPE.RealizingElement.GlobalId.ToString());
                  realEl.Add(connPE.RealizingElement.GlobalId.ToString());
                  realElBS.Add(OracleParameterStatus.Success);
                  realElBS.Add(OracleParameterStatus.Success);
                  realElTyp.Add(connPE.RealizingElement.GetType().Name.ToUpper());
                  realElTyp.Add(connPE.RealizingElement.GetType().Name.ToUpper());
                  realElTBS.Add(OracleParameterStatus.Success);
                  realElTBS.Add(OracleParameterStatus.Success);
               }
               else
               {
                  realEl.Add(string.Empty);
                  realEl.Add(string.Empty);
                  realElBS.Add(OracleParameterStatus.NullInsert);
                  realElBS.Add(OracleParameterStatus.NullInsert);
                  realElTyp.Add(string.Empty);
                  realElTyp.Add(string.Empty);
                  realElTBS.Add(OracleParameterStatus.NullInsert);
                  realElTBS.Add(OracleParameterStatus.NullInsert);
               }

               // First rel
               if (!String.IsNullOrEmpty(attrName1))
               {
                  cIngAttrN.Add(attrName1);
                  cIngAttrNBS.Add(OracleParameterStatus.Success);
                  cIngAttrV.Add(attrVal1);
                  cIngAttrVBS.Add(OracleParameterStatus.Success);
               }
               else
               {
                  cIngAttrN.Add(string.Empty);
                  cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cIngAttrV.Add(string.Empty);
                  cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
               }
               if (!String.IsNullOrEmpty(attrName2))
               {
                  cEdAttrN.Add(attrName2);
                  cEdAttrNBS.Add(OracleParameterStatus.Success);
                  cEdAttrV.Add(attrVal2);
                  cEdAttrVBS.Add(OracleParameterStatus.Success);
               }
               else
               {
                  cEdAttrN.Add(string.Empty);
                  cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrV.Add(string.Empty);
                  cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
               }

               // Second rel
               if (!String.IsNullOrEmpty(attrName2))
               {
                  cIngAttrN.Add(attrName2);
                  cIngAttrNBS.Add(OracleParameterStatus.Success);
                  cIngAttrV.Add(attrVal2);
                  cIngAttrVBS.Add(OracleParameterStatus.Success);
               }
               else
               {
                  cIngAttrN.Add(string.Empty);
                  cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cIngAttrV.Add(string.Empty);
                  cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
               }
               if (!String.IsNullOrEmpty(attrName1))
               {
                  cEdAttrN.Add(attrName1);
                  cEdAttrNBS.Add(OracleParameterStatus.Success);
                  cEdAttrV.Add(attrVal1);
                  cEdAttrVBS.Add(OracleParameterStatus.Success);
               }
               else
               {
                  cEdAttrN.Add(string.Empty);
                  cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrV.Add(string.Empty);
                  cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
               }

               cAttrN.Add(string.Empty);
               cAttrN.Add(string.Empty);
               cAttrNBS.Add(OracleParameterStatus.NullInsert);
               cAttrNBS.Add(OracleParameterStatus.NullInsert);
               cAttrV.Add(string.Empty);
               cAttrV.Add(string.Empty);
               cAttrVBS.Add(OracleParameterStatus.NullInsert);
               cAttrVBS.Add(OracleParameterStatus.NullInsert);
               relTyp.Add(connPE.GetType().Name.ToUpper());
               relTyp.Add(connPE.GetType().Name.ToUpper());
            }

               // Handle covering for both covering for spaces and building element
            else if (conn is IIfcRelCoversSpaces)
            {
               IIfcRelCoversSpaces covS = conn as IIfcRelCoversSpaces;
               Xbim.Ifc2x3.Interfaces.IIfcRelCoversSpaces covS2x3 = conn as Xbim.Ifc2x3.Interfaces.IIfcRelCoversSpaces;
               string guid;
               string relType;
               if (covS2x3 != null)
               {
                  guid = covS2x3.RelatedSpace.GlobalId.ToString();
                  relType = covS2x3.RelatedSpace.GetType().Name.ToUpper();
               }
               else
               {
                  guid = covS.RelatingSpace.GlobalId.ToString();
                  relType = covS.RelatingSpace.GetType().Name.ToUpper();
               }
               if (_refBIMRLCommon.getLineNoFromMapping(guid) == null)
                  continue;       // skip "non" element guid in the relationship object

               IEnumerable<IIfcCovering> relCovs = covS.RelatedCoverings;
               foreach (IIfcCovering cov in relCovs)
               {
                  cIngEle.Add(guid);
                  cIngEleTyp.Add(relType);
                  cEdEle.Add(cov.GlobalId.ToString());
                  cEdEleTyp.Add(cov.GetType().Name.ToUpper());
                  cIngAttrN.Add(string.Empty);
                  cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cIngAttrV.Add(string.Empty);
                  cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrN.Add(string.Empty);
                  cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrV.Add(string.Empty);
                  cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cAttrN.Add(string.Empty);
                  cAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cAttrV.Add(string.Empty);
                  cAttrVBS.Add(OracleParameterStatus.NullInsert);
                  realEl.Add(string.Empty);
                  realElBS.Add(OracleParameterStatus.NullInsert);
                  realElTyp.Add(string.Empty);
                  realElTBS.Add(OracleParameterStatus.NullInsert);
               }
               relTyp.Add(covS.GetType().Name.ToUpper());
            }

            else if (conn is IIfcRelCoversBldgElements)
            {
               IIfcRelCoversBldgElements covE = conn as IIfcRelCoversBldgElements;
               if (_refBIMRLCommon.getLineNoFromMapping(covE.RelatingBuildingElement.GlobalId.ToString()) == null)
                  continue;       // skip "non" element guid in the relationship object

               IEnumerable<IIfcCovering> relCovs = covE.RelatedCoverings;
               foreach (IIfcCovering cov in relCovs)
               {
                  cIngEle.Add(covE.RelatingBuildingElement.GlobalId.ToString());
                  cIngEleTyp.Add(covE.RelatingBuildingElement.GetType().Name.ToUpper());
                  cEdEle.Add(cov.GlobalId.ToString());
                  cEdEleTyp.Add(cov.GetType().Name.ToUpper());
                  cIngAttrN.Add(string.Empty);
                  cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cIngAttrV.Add(string.Empty);
                  cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrN.Add(string.Empty);
                  cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrV.Add(string.Empty);
                  cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cAttrN.Add(string.Empty);
                  cAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cAttrV.Add(string.Empty);
                  cAttrVBS.Add(OracleParameterStatus.NullInsert);
                  realEl.Add(string.Empty);
                  realElBS.Add(OracleParameterStatus.NullInsert);
                  realElTyp.Add(string.Empty);
                  realElTBS.Add(OracleParameterStatus.NullInsert);
               }
               relTyp.Add(covE.GetType().Name.ToUpper());
            }

            else if (conn is IIfcRelFlowControlElements)
            {
               // Handle Flow Control
               IIfcRelFlowControlElements connPE = conn as IIfcRelFlowControlElements;
               if (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingFlowElement.GlobalId.ToString()) == null)
                  continue;       // skip "non" element guid in the relationship object

               foreach (IIfcDistributionControlElement dist in connPE.RelatedControlElements)
               {
                  cIngEle.Add(connPE.RelatingFlowElement.GlobalId.ToString());
                  cIngEleTyp.Add(connPE.RelatingFlowElement.GetType().Name.ToUpper());
                  cEdEle.Add(dist.GlobalId.ToString());
                  cEdEleTyp.Add(dist.GetType().Name.ToUpper());
                  cIngAttrN.Add(string.Empty);
                  cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cIngAttrV.Add(string.Empty);
                  cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrN.Add(string.Empty);
                  cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrV.Add(string.Empty);
                  cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cAttrN.Add(string.Empty);
                  cAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cAttrV.Add(string.Empty);
                  cAttrVBS.Add(OracleParameterStatus.NullInsert);
                  realEl.Add(string.Empty);
                  realElBS.Add(OracleParameterStatus.NullInsert);
                  realElTyp.Add(string.Empty);
                  realElTBS.Add(OracleParameterStatus.NullInsert);
               }
               relTyp.Add(connPE.GetType().Name.ToUpper());
            }

            else if (conn is IIfcRelInterferesElements)
            {
               // Handle Interference
               IIfcRelInterferesElements connPE = conn as IIfcRelInterferesElements;
               if (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingElement.GlobalId.ToString()) == null)
                  continue;       // skip "non" element guid in the relationship object

               cIngEle.Add(connPE.RelatingElement.GlobalId.ToString());
               cIngEleTyp.Add(connPE.RelatingElement.GetType().Name.ToUpper());
               cEdEle.Add(connPE.RelatedElement.GlobalId.ToString());
               cEdEleTyp.Add(connPE.RelatedElement.GetType().Name.ToUpper());
               cIngAttrN.Add(string.Empty);
               cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
               cIngAttrV.Add(string.Empty);
               cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
               cEdAttrN.Add(string.Empty);
               cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
               cEdAttrV.Add(string.Empty);
               cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
               if (connPE.InterferenceType != null)
               {
                  cAttrN.Add("INTERFERENCETYPE");
                  cAttrNBS.Add(OracleParameterStatus.Success);
                  cAttrV.Add(connPE.InterferenceType.ToString());
                  cAttrVBS.Add(OracleParameterStatus.Success);
               }
               else
               {
                  cAttrN.Add(string.Empty);
                  cAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cAttrV.Add(string.Empty);
                  cAttrVBS.Add(OracleParameterStatus.NullInsert);
               }
               realEl.Add(string.Empty);
               realElBS.Add(OracleParameterStatus.NullInsert);
               realElTyp.Add(string.Empty);
               realElTBS.Add(OracleParameterStatus.NullInsert);
               relTyp.Add(connPE.GetType().Name.ToUpper());

            }

            else if (conn is IIfcRelReferencedInSpatialStructure)
            {
               // Handle referenced by Spatial Structure
               IIfcRelReferencedInSpatialStructure connPE = conn as IIfcRelReferencedInSpatialStructure;
               if (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingStructure.GlobalId.ToString()) == null)
                  continue;       // skip "non" element guid in the relationship object

               foreach (IIfcProduct elem in connPE.RelatedElements)
               {
                  cIngEle.Add(connPE.RelatingStructure.GlobalId.ToString());
                  cIngEleTyp.Add(connPE.RelatingStructure.GetType().Name.ToUpper());
                  cEdEle.Add(elem.GlobalId.ToString());
                  cEdEleTyp.Add(elem.GetType().Name.ToUpper());
                  cIngAttrN.Add(string.Empty);
                  cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cIngAttrV.Add(string.Empty);
                  cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrN.Add(string.Empty);
                  cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrV.Add(string.Empty);
                  cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cAttrN.Add(string.Empty);
                  cAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cAttrV.Add(string.Empty);
                  cAttrVBS.Add(OracleParameterStatus.NullInsert);
                  realEl.Add(string.Empty);
                  realElBS.Add(OracleParameterStatus.NullInsert);
                  realElTyp.Add(string.Empty);
                  realElTBS.Add(OracleParameterStatus.NullInsert);
                  relTyp.Add(connPE.GetType().Name.ToUpper());
               }
            }

            else if (conn is IIfcRelServicesBuildings)
            {
               // Handle MEP connections through IfcDistributionPorts (Port will not be captured)
               IIfcRelServicesBuildings connPE = conn as IIfcRelServicesBuildings;
               if (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingSystem.GlobalId.ToString()) == null)
                  continue;       // skip "non" element guid in the relationship object

               foreach (IIfcSpatialStructureElement bldg in connPE.RelatedBuildings)
               {
                  cIngEle.Add(connPE.RelatingSystem.GlobalId.ToString());
                  cIngEleTyp.Add(connPE.RelatingSystem.GetType().Name.ToUpper());
                  cEdEle.Add(bldg.GlobalId.ToString());
                  cEdEleTyp.Add(bldg.GetType().Name.ToUpper());
                  cIngAttrN.Add(string.Empty);
                  cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cIngAttrV.Add(string.Empty);
                  cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrN.Add(string.Empty);
                  cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cEdAttrV.Add(string.Empty);
                  cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
                  cAttrN.Add(string.Empty);
                  cAttrNBS.Add(OracleParameterStatus.NullInsert);
                  cAttrV.Add(string.Empty);
                  cAttrVBS.Add(OracleParameterStatus.NullInsert);
                  realEl.Add(string.Empty);
                  realElBS.Add(OracleParameterStatus.NullInsert);
                  realElTyp.Add(string.Empty);
                  realElTBS.Add(OracleParameterStatus.NullInsert);
                  relTyp.Add(connPE.GetType().Name.ToUpper());
               }
            }
            else if (conn is Xbim.Ifc2x3.StructuralAnalysisDomain.IfcRelConnectsStructuralElement)
            {
            Xbim.Ifc2x3.Interfaces.IIfcRelConnectsStructuralElement connPE = conn as Xbim.Ifc2x3.Interfaces.IIfcRelConnectsStructuralElement;
            if (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingElement.GlobalId.ToString()) == null)
                  continue;       // skip "non" element guid in the relationship object

               IIfcStructuralMember stru = connPE.RelatedStructuralMember as IIfcStructuralMember;
               cIngEle.Add(connPE.RelatingElement.GlobalId.ToString());
               cIngEleTyp.Add(connPE.RelatingElement.GetType().Name.ToUpper());
               cEdEle.Add(stru.GlobalId.ToString());
               cEdEleTyp.Add(stru.GetType().Name.ToUpper());
               cIngAttrN.Add(string.Empty);
               cIngAttrNBS.Add(OracleParameterStatus.NullInsert);
               cIngAttrV.Add(string.Empty);
               cIngAttrVBS.Add(OracleParameterStatus.NullInsert);
               cEdAttrN.Add(string.Empty);
               cEdAttrNBS.Add(OracleParameterStatus.NullInsert);
               cEdAttrV.Add(string.Empty);
               cEdAttrVBS.Add(OracleParameterStatus.NullInsert);
               cAttrN.Add(string.Empty);
               cAttrNBS.Add(OracleParameterStatus.NullInsert);
               cAttrV.Add(string.Empty);
               cAttrVBS.Add(OracleParameterStatus.NullInsert);
               realEl.Add(string.Empty);
               realElBS.Add(OracleParameterStatus.NullInsert);
               realElTyp.Add(string.Empty);
               realElTBS.Add(OracleParameterStatus.NullInsert);
               relTyp.Add(connPE.GetType().Name.ToUpper());
            }

            else
            {
               // Unsupported type!
            }

            if (cIngEle.Count >= DBOperation.commitInterval)
            {
               Param[0].Value = cIngEle.ToArray();
               Param[0].Size = cIngEle.Count;
               Param[1].Value = cIngEleTyp.ToArray();
               Param[1].Size = cIngEleTyp.Count;
               Param[2].Value = cIngAttrN.ToArray();
               Param[2].Size = cIngAttrN.Count;
               Param[2].ArrayBindStatus = cIngAttrNBS.ToArray();
               Param[3].Value = cIngAttrV.ToArray();
               Param[3].Size = cIngAttrV.Count;
               Param[3].ArrayBindStatus = cIngAttrVBS.ToArray();
               Param[4].Value = cEdEle.ToArray();
               Param[4].Size = cEdEle.Count;
               Param[5].Value = cEdEleTyp.ToArray();
               Param[5].Size = cEdEleTyp.Count;
               Param[6].Value = cEdAttrN.ToArray();
               Param[6].Size = cEdAttrN.Count;
               Param[6].ArrayBindStatus = cEdAttrNBS.ToArray();
               Param[7].Value = cEdAttrV.ToArray();
               Param[7].Size = cEdAttrV.Count;
               Param[7].ArrayBindStatus = cEdAttrVBS.ToArray();
               Param[8].Value = cAttrN.ToArray();
               Param[8].Size = cAttrN.Count;
               Param[8].ArrayBindStatus = cAttrNBS.ToArray();
               Param[9].Value = cAttrV.ToArray();
               Param[9].Size = cAttrV.Count;
               Param[9].ArrayBindStatus = cAttrVBS.ToArray();
               Param[10].Value = realEl.ToArray();
               Param[10].Size = realEl.Count;
               Param[10].ArrayBindStatus = realElBS.ToArray();
               Param[11].Value = realElTyp.ToArray();
               Param[11].Size = realElTyp.Count;
               Param[11].ArrayBindStatus = realElTBS.ToArray();
               Param[12].Value = relTyp.ToArray();
               Param[12].Size = relTyp.Count;
               try
               {
                  command.ArrayBindCount = cIngEle.Count;    // No of values in the array to be inserted
                  commandStatus = command.ExecuteNonQuery();
                  DBOperation.commitTransaction();

                  cIngEle.Clear();
                  cIngEleTyp.Clear();
                  cIngAttrN.Clear();
                  cIngAttrV.Clear();
                  cEdEle.Clear();
                  cEdEleTyp.Clear();
                  cEdAttrN.Clear();
                  cEdAttrV.Clear();
                  cAttrN.Clear();
                  cAttrV.Clear();
                  realEl.Clear();
                  realElTyp.Clear();
                  cIngAttrNBS.Clear();
                  cIngAttrVBS.Clear();
                  cEdAttrNBS.Clear();
                  cEdAttrVBS.Clear();
                  cAttrNBS.Clear();
                  cAttrVBS.Clear();
                  realElBS.Clear();
                  realElTBS.Clear();
                  relTyp.Clear();
               }
               catch (OracleException e)
               {
                  string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                  _refBIMRLCommon.StackPushIgnorableError(excStr);
                  // Ignore any error
                  cIngEle.Clear();
                  cIngEleTyp.Clear();
                  cIngAttrN.Clear();
                  cIngAttrV.Clear();
                  cEdEle.Clear();
                  cEdEleTyp.Clear();
                  cEdAttrN.Clear();
                  cEdAttrV.Clear();
                  cAttrN.Clear();
                  cAttrV.Clear();
                  realEl.Clear();
                  realElTyp.Clear();
                  cIngAttrNBS.Clear();
                  cIngAttrVBS.Clear();
                  cEdAttrNBS.Clear();
                  cEdAttrVBS.Clear();
                  cAttrNBS.Clear();
                  cAttrVBS.Clear();
                  realElBS.Clear();
                  realElTBS.Clear();
                  relTyp.Clear();
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

         if (cIngEle.Count > 0)
         {
               Param[0].Value = cIngEle.ToArray();
               Param[0].Size = cIngEle.Count;
               Param[1].Value = cIngEleTyp.ToArray();
               Param[1].Size = cIngEleTyp.Count;
               Param[2].Value = cIngAttrN.ToArray();
               Param[2].Size = cIngAttrN.Count;
               Param[2].ArrayBindStatus = cIngAttrNBS.ToArray();
               Param[3].Value = cIngAttrV.ToArray();
               Param[3].Size = cIngAttrV.Count;
               Param[3].ArrayBindStatus = cIngAttrVBS.ToArray();
               Param[4].Value = cEdEle.ToArray();
               Param[4].Size = cEdEle.Count;
               Param[5].Value = cEdEleTyp.ToArray();
               Param[5].Size = cEdEleTyp.Count;
               Param[6].Value = cEdAttrN.ToArray();
               Param[6].Size = cEdAttrN.Count;
               Param[6].ArrayBindStatus = cEdAttrNBS.ToArray();
               Param[7].Value = cEdAttrV.ToArray();
               Param[7].Size = cEdAttrV.Count;
               Param[7].ArrayBindStatus = cEdAttrVBS.ToArray();
               Param[8].Value = cAttrN.ToArray();
               Param[8].Size = cAttrN.Count;
               Param[8].ArrayBindStatus = cAttrNBS.ToArray();
               Param[9].Value = cAttrV.ToArray();
               Param[9].Size = cAttrV.Count;
               Param[9].ArrayBindStatus = cAttrVBS.ToArray();
               Param[10].Value = realEl.ToArray();
               Param[10].Size = realEl.Count;
               Param[10].ArrayBindStatus = realElBS.ToArray();
               Param[11].Value = realElTyp.ToArray();
               Param[11].Size = realElTyp.Count;
               Param[11].ArrayBindStatus = realElTBS.ToArray();
               Param[12].Value = relTyp.ToArray();
               Param[12].Size = relTyp.Count;

               try
               {
                  command.ArrayBindCount = cIngEle.Count;    // No of values in the array to be inserted
                  commandStatus = command.ExecuteNonQuery();
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
      }

      /// <summary>
      /// Special treatment on Space boundary:
      /// 1. No duplicate of space, boundary is inserted. the pair is checked first from the local dictionary before value is setup
      /// 2. Virtual element is resolved to become Space1 - Space2 space and boundary relationship 
      /// </summary>
      private void processRelSpaceBoundary()
      {
         string SqlStmt;
         string currStep = string.Empty;

         DBOperation.beginTransaction();

         int commandStatus = -1;

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

         SqlStmt = "insert into BIMRL_RELSPACEBOUNDARY_" + bimrlProcessModel.currFedID.ToString("X4")
                     + " (SPACEELEMENTID, BOUNDARYELEMENTID, BOUNDARYELEMENTTYPE, BOUNDARYTYPE, INTERNALOREXTERNAL) values (:1, :2, :3, :4, :5)";
         command.CommandText = SqlStmt;
         currStep = SqlStmt;

         OracleParameter[] Param = new OracleParameter[5];
         for (int i = 0; i < 5; i++)
         {
               Param[i] = command.Parameters.Add((i + 1).ToString(), OracleDbType.Varchar2);
               Param[i].Direction = ParameterDirection.Input;
         }

         var spBIndex = new Dictionary<Tuple<string, string>, int>();    // Keep the index pair in the dictionary to avoid duplicate

         List<string> arrSpaceGuids = new List<string>();
         List<string> arrBoundGuids = new List<string>();
         List<string> arrBoundEleTypes = new List<string>();
         List<string> arrBoundTypes = new List<string>();
         List<string> arrIntOrExt = new List<string>();

         IEnumerable<IIfcRelSpaceBoundary> rels = _model.Instances.OfType<IIfcRelSpaceBoundary>();
         foreach (IIfcRelSpaceBoundary spb in rels)
         {
            string spaceGuid;
            if (spb.RelatingSpace is IIfcSpace)
               spaceGuid = (spb.RelatingSpace as IIfcSpace).GlobalId.ToString();
            else
               spaceGuid = (spb.RelatingSpace as IIfcExternalSpatialElement).GlobalId.ToString();

            if ((_refBIMRLCommon.getLineNoFromMapping(spaceGuid) == null) || (spb.RelatedBuildingElement == null))
               continue;   // skip relationship that involves "non" element Guids

            if (spb.RelatedBuildingElement is IIfcVirtualElement)
            {
               // We will ignore the virtual element and instead get the conencted space as the related boundary
               IIfcVirtualElement ve = spb.RelatedBuildingElement as IIfcVirtualElement;
               // get the element this IfcVirtualElement is connected to
               IEnumerable<IIfcRelSpaceBoundary> veSpaces = ve.ProvidesBoundaries;
               foreach (IIfcRelSpaceBoundary veSp in veSpaces)
               {
                  int lineNo;
                  string space2Guid;
                  if (veSp.RelatingSpace is IIfcSpace)
                     space2Guid = (veSp.RelatingSpace as IIfcSpace).GlobalId.ToString();
                  else
                     space2Guid = (veSp.RelatingSpace as IIfcExternalSpatialElement).GlobalId.ToString();

                  string space2Type = veSp.RelatingSpace.GetType().Name.ToUpper();
                  if (spb.RelatingSpace == veSp.RelatingSpace)
                        continue;       // It points aback to itself, skip
                  if (spBIndex.TryGetValue(Tuple.Create(spaceGuid, space2Guid), out lineNo))
                        continue;       // existing pair found, skip this pair

                  arrSpaceGuids.Add(spaceGuid);
                  arrBoundGuids.Add(space2Guid);
                  arrBoundEleTypes.Add(space2Type);
                  arrBoundTypes.Add(spb.PhysicalOrVirtualBoundary.ToString());
                  arrIntOrExt.Add(spb.InternalOrExternalBoundary.ToString());

                  spBIndex.Add(Tuple.Create(spaceGuid, space2Guid), spb.EntityLabel);
               }
            }
            else
            {
               int lineNo;
               string boundGuid = spb.RelatedBuildingElement.GlobalId.ToString();
               string boundType = spb.RelatedBuildingElement.GetType().Name.ToUpper();

               if (spBIndex.TryGetValue(Tuple.Create(spaceGuid, boundGuid), out lineNo))
                  continue;       // existing pair found, skip this pair

               arrSpaceGuids.Add(spaceGuid);
               arrBoundGuids.Add(boundGuid);
               arrBoundEleTypes.Add(boundType);
               arrBoundTypes.Add(spb.PhysicalOrVirtualBoundary.ToString());
               arrIntOrExt.Add(spb.InternalOrExternalBoundary.ToString());

               spBIndex.Add(Tuple.Create(spaceGuid, boundGuid), spb.EntityLabel);
            }

            if (arrSpaceGuids.Count >= DBOperation.commitInterval)
            {
               Param[0].Size = arrSpaceGuids.Count();
               Param[0].Value = arrSpaceGuids.ToArray();
               Param[1].Size = arrBoundGuids.Count();
               Param[1].Value = arrBoundGuids.ToArray();
               Param[2].Size = arrBoundEleTypes.Count();
               Param[2].Value = arrBoundEleTypes.ToArray();
               Param[3].Size = arrBoundTypes.Count();
               Param[3].Value = arrBoundTypes.ToArray();
               Param[4].Size = arrIntOrExt.Count();
               Param[4].Value = arrIntOrExt.ToArray();
               try
               {
                  command.ArrayBindCount = arrSpaceGuids.Count;    // No of values in the array to be inserted
                  commandStatus = command.ExecuteNonQuery();
                  //Do commit at interval but keep the long transaction (reopen)
                  DBOperation.commitTransaction();
                  arrSpaceGuids.Clear();
                  arrBoundGuids.Clear();
                  arrBoundEleTypes.Clear();
                  arrBoundTypes.Clear();
                  arrIntOrExt.Clear();
               }
               catch (OracleException e)
               {
                  string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                  _refBIMRLCommon.StackPushIgnorableError(excStr);
                  // Ignore any error
                  arrSpaceGuids.Clear();
                  arrBoundGuids.Clear();
                  arrBoundEleTypes.Clear();
                  arrBoundTypes.Clear();
                  arrIntOrExt.Clear();
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

         if (arrSpaceGuids.Count > 0)
         {
               Param[0].Size = arrSpaceGuids.Count();
               Param[0].Value = arrSpaceGuids.ToArray();
               Param[1].Size = arrBoundGuids.Count();
               Param[1].Value = arrBoundGuids.ToArray();
               Param[2].Size = arrBoundEleTypes.Count();
               Param[2].Value = arrBoundEleTypes.ToArray();
               Param[3].Size = arrBoundTypes.Count();
               Param[3].Value = arrBoundTypes.ToArray();
               Param[4].Size = arrIntOrExt.Count();
               Param[4].Value = arrIntOrExt.ToArray();

               try
               {
                  command.ArrayBindCount = arrSpaceGuids.Count;    // No of values in the array to be inserted
                  commandStatus = command.ExecuteNonQuery();
                  //Do commit at interval but keep the long transaction (reopen)
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
      }

      private void processRelGroup()
      {
         string SqlStmt;
         string currStep = string.Empty;

         DBOperation.beginTransaction();

         int commandStatus = -1;

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

         SqlStmt = "insert into BIMRL_RELGROUP_" + bimrlProcessModel.currFedID.ToString("X4")
                     + " (GROUPELEMENTID, GROUPELEMENTTYPE, MEMBERELEMENTID, MEMBERELEMENTTYPE) values (:1, :2, :3, :4)";
         command.CommandText = SqlStmt;
         currStep = SqlStmt;

         OracleParameter[] Param = new OracleParameter[4];
         for (int i = 0; i < 4; i++)
         {
               Param[i] = command.Parameters.Add((i + 1).ToString(), OracleDbType.Varchar2);
               Param[i].Direction = ParameterDirection.Input;
         }

         List<string> arrGroupGuids = new List<string>();
         List<string> arrGroupTypes = new List<string>();
         List<string> arrMemberGuids = new List<string>();
         List<string> arrMemberTypes = new List<string>();

         // IEnumerable<IfcRelAssignsToGroup> rels = _model.InstancesLocal.OfType<IfcRelAssignsToGroup>(true).Where(gr => gr.RelatingGroup is IfcSystem || gr.RelatingGroup is IfcZone);
         // Handle other types of Group too
         IEnumerable<IIfcRelAssignsToGroup> rels = _model.Instances.OfType<IIfcRelAssignsToGroup>();
         foreach (IIfcRelAssignsToGroup rGr in rels)
         {
               string grpGuid = rGr.RelatingGroup.GlobalId.ToString();
               if (_refBIMRLCommon.getLineNoFromMapping(grpGuid) == null)
                  continue;   // skip relationship if the Group GUID does not exist

               string grType = rGr.RelatingGroup.GetType().Name.ToUpper();

               IEnumerable<IIfcObjectDefinition> members = rGr.RelatedObjects;

               foreach (IIfcObjectDefinition oDef in members)
               {
                  string memberGuid = oDef.GlobalId.ToString();
                  string memberType = oDef.GetType().Name.ToUpper();
                  if (_refBIMRLCommon.getLineNoFromMapping(memberGuid) == null)
                     continue;       // Skip if member is not loaded into BIMRL_ELEMENT already

                  arrGroupGuids.Add(grpGuid);
                  arrGroupTypes.Add(grType);
                  arrMemberGuids.Add(memberGuid);
                  arrMemberTypes.Add(memberType);
               }

               if (arrGroupGuids.Count >= DBOperation.commitInterval)
               {
                  Param[0].Size = arrGroupGuids.Count();
                  Param[0].Value = arrGroupGuids.ToArray();
                  Param[1].Size = arrGroupTypes.Count();
                  Param[1].Value = arrGroupTypes.ToArray();
                  Param[2].Size = arrMemberGuids.Count();
                  Param[2].Value = arrMemberGuids.ToArray();
                  Param[3].Size = arrMemberTypes.Count();
                  Param[3].Value = arrMemberTypes.ToArray();
                  try
                  {
                     command.ArrayBindCount = arrGroupGuids.Count;    // No of values in the array to be inserted
                     commandStatus = command.ExecuteNonQuery();
                     //Do commit at interval but keep the long transaction (reopen)
                     DBOperation.commitTransaction();
                     arrGroupGuids.Clear();
                     arrGroupTypes.Clear();
                     arrMemberGuids.Clear();
                     arrMemberTypes.Clear();
                  }
                  catch (OracleException e)
                  {
                     string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                     _refBIMRLCommon.StackPushIgnorableError(excStr);
                     // Ignore any error
                     arrGroupGuids.Clear();
                     arrGroupTypes.Clear();
                     arrMemberGuids.Clear();
                     arrMemberTypes.Clear();
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

         if (arrGroupGuids.Count > 0)
         {
               Param[0].Size = arrGroupGuids.Count();
               Param[0].Value = arrGroupGuids.ToArray();
               Param[1].Size = arrGroupTypes.Count();
               Param[1].Value = arrGroupTypes.ToArray();
               Param[2].Size = arrMemberGuids.Count();
               Param[2].Value = arrMemberGuids.ToArray();
               Param[3].Size = arrMemberTypes.Count();
               Param[3].Value = arrMemberTypes.ToArray();

               try
               {
                  command.ArrayBindCount = arrGroupGuids.Count;    // No of values in the array to be inserted
                  commandStatus = command.ExecuteNonQuery();
                  //Do commit at interval but keep the long transaction (reopen)
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
      }

      private void processElemDependency()
      {
         List<string> cEleId = new List<string>();
         List<string> cEleTyp = new List<string>();
         List<string> cDepend = new List<string>();
         List<string> cDependTyp = new List<string>();
         List<string> cDepTyp = new List<string>();

         IEnumerable<IIfcRelVoidsElement> relVoids = _model.Instances.OfType<IIfcRelVoidsElement>(true);
         foreach (IIfcRelVoidsElement connPE in relVoids)
         {
            if ((_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingBuildingElement.GlobalId.ToString()) == null)
               || (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatedOpeningElement.GlobalId.ToString()) == null))
               continue;       // skip "non" element guid in the relationship object

            cEleId.Add(connPE.RelatingBuildingElement.GlobalId.ToString());
            cEleTyp.Add(connPE.RelatingBuildingElement.GetType().Name.ToUpper());
            cDepend.Add(connPE.RelatedOpeningElement.GlobalId.ToString());
            cDependTyp.Add(connPE.RelatedOpeningElement.GetType().Name.ToUpper());
            cDepTyp.Add(connPE.GetType().Name.ToUpper());
         }
         InsertDependencyRecords(cEleId, cEleTyp, cDepend, cDependTyp, cDepTyp);

         IEnumerable<IIfcRelProjectsElement> relProjects = _model.Instances.OfType<IIfcRelProjectsElement>(true);
         foreach (IIfcRelProjectsElement connPE in relProjects)
         {
            if ((_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingElement.GlobalId.ToString()) == null)
               || (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatedFeatureElement.GlobalId.ToString()) == null))
               continue;       // skip "non" element guid in the relationship object

            cEleId.Add(connPE.RelatingElement.GlobalId.ToString());
            cEleTyp.Add(connPE.RelatingElement.GetType().Name.ToUpper());
            cDepend.Add(connPE.RelatedFeatureElement.GlobalId.ToString());
            cDependTyp.Add(connPE.RelatedFeatureElement.GetType().Name.ToUpper());
            cDepTyp.Add(connPE.GetType().Name.ToUpper());
         }
         InsertDependencyRecords(cEleId, cEleTyp, cDepend, cDependTyp, cDepTyp);

         IEnumerable<IIfcRelFillsElement> relFills = _model.Instances.OfType<IIfcRelFillsElement>(true);
         foreach (IIfcRelFillsElement connPE in relFills)
         {
            if ((_refBIMRLCommon.getLineNoFromMapping(connPE.RelatingOpeningElement.GlobalId.ToString()) == null)
               || (_refBIMRLCommon.getLineNoFromMapping(connPE.RelatedBuildingElement.GlobalId.ToString()) == null))
               continue;       // skip "non" element guid in the relationship object

            cEleId.Add(connPE.RelatingOpeningElement.GlobalId.ToString());
            cEleTyp.Add(connPE.RelatingOpeningElement.GetType().Name.ToUpper());
            cDepend.Add(connPE.RelatedBuildingElement.GlobalId.ToString());
            cDependTyp.Add(connPE.RelatedBuildingElement.GetType().Name.ToUpper());
            cDepTyp.Add(connPE.GetType().Name.ToUpper());
         }
         InsertDependencyRecords(cEleId, cEleTyp, cDepend, cDependTyp, cDepTyp);

         IEnumerable<IIfcRelNests> relNests = _model.Instances.OfType<IIfcRelNests>(true);
         foreach (IIfcRelNests connPE in relNests)
         {
            string relatingObject = connPE.RelatingObject.GlobalId.ToString();
            if (_refBIMRLCommon.getLineNoFromMapping(relatingObject) == null)
               continue;
            string relatingType = connPE.RelatingObject.GetType().Name.ToUpper();
            string depTyp = connPE.GetType().Name.ToUpper();

            foreach (IIfcObjectDefinition relatedObj in connPE.RelatedObjects)
            {
               string relatedObjGUID = relatedObj.GlobalId.ToString();
               if (_refBIMRLCommon.getLineNoFromMapping(relatedObjGUID) == null)
                  continue;       // skip "non" element guid in the relationship object

               cEleId.Add(relatingObject);
               cEleTyp.Add(relatingType);
               cDepend.Add(relatedObjGUID);
               cDependTyp.Add(relatedObj.GetType().Name.ToUpper());
               cDepTyp.Add(depTyp);
            }
         }
         InsertDependencyRecords(cEleId, cEleTyp, cDepend, cDependTyp, cDepTyp);
      }

      private void InsertDependencyRecords(List<string> cEleId, List<string> cEleTyp, List<string> cDepend, List<string> cDependTyp, List<string> cDepTyp)
      {
         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

         string SqlStmt = "insert into BIMRL_ELEMENTDEPENDENCY_" + bimrlProcessModel.currFedID.ToString("X4")
                     + " (ELEMENTID, ELEMENTTYPE, DEPENDENTELEMENTID, DEPENDENTELEMENTTYPE, DEPENDENCYTYPE) "
                     + "VALUES (:1, :2, :3, :4, :5)";
         command.CommandText = SqlStmt;
         string currStep = SqlStmt;
         int commandStatus = -1;

         DBOperation.beginTransaction();

         OracleParameter[] Param = new OracleParameter[5];
         for (int i = 0; i < 5; i++)
         {
            Param[i] = command.Parameters.Add((i + 1).ToString(), OracleDbType.Varchar2);
            Param[i].Direction = ParameterDirection.Input;
         }

         if (cEleId.Count >= DBOperation.commitInterval)
         {
            Param[0].Size = cEleId.Count();
            Param[0].Value = cEleId.ToArray();
            Param[1].Size = cEleTyp.Count();
            Param[1].Value = cEleTyp.ToArray();
            Param[2].Size = cDepend.Count();
            Param[2].Value = cDepend.ToArray();
            Param[3].Size = cDependTyp.Count();
            Param[3].Value = cDependTyp.ToArray();
            Param[4].Size = cDepTyp.Count();
            Param[4].Value = cDepTyp.ToArray();
            try
            {
               command.ArrayBindCount = cDepTyp.Count;    // No of values in the array to be inserted
               commandStatus = command.ExecuteNonQuery();
               DBOperation.commitTransaction();
            }
            catch (OracleException e)
            {
               string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
               _refBIMRLCommon.StackPushIgnorableError(excStr);
               // Ignore any error
            }
            catch (SystemException e)
            {
               string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
               _refBIMRLCommon.StackPushError(excStr);
               throw;
            }

            DBOperation.commitTransaction();
            command.Dispose();
         }
      }
   }
}
