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
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using BIMRL.Common;

namespace BIMRL
{
   class BIMRLSpatialStructure
   {
      IfcStore _model;
      BIMRLCommon _refBIMRLCommon;

      public BIMRLSpatialStructure(IfcStore m, BIMRLCommon refBIMRLCommon)
      {
         _model = m;
         _refBIMRLCommon = refBIMRLCommon;
      }

      public void processElementStructure()
      {
         processSpatialStructureData();
         processSpatialStructureRel();
      }

      private void processSpatialStructureData()
      {
         string currStep = string.Empty;
         DBOperation.beginTransaction();
         string container = string.Empty;
         int commandStatus = -1;
         int currInsertCount = 0;

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

         try
         {
            IEnumerable<IIfcSpatialStructureElement> spatialStructure = _model.Instances.OfType<IIfcSpatialStructureElement>();
            foreach (IIfcSpatialStructureElement sse in spatialStructure)
            {
               string SqlStmt;

               // do something
               string guid = sse.GlobalId.ToString();
               int IfcLineNo = sse.EntityLabel;

               string elementtype = sse.GetType().Name.ToUpper();
               string typeID = String.Empty;
               int typeLineNo = 0;
               IEnumerable<IIfcRelDefinesByType> relTyp = sse.IsTypedBy;
               if (relTyp != null || relTyp.Count() > 0)
               {
                  IIfcRelDefinesByType typ = relTyp.FirstOrDefault();
                  if (typ != null)
                  {
                     typeID = typ.RelatingType.GlobalId.ToString();
                     typeLineNo = typ.RelatingType.EntityLabel;
                  }
               }
               //if (sse.GetDefiningType() != null)
               //  {
               //      typeID = sse.GetDefiningType().GlobalId;
               //      typeLineNo = sse.GetDefiningType().EntityLabel;
               //  }
               string sseName = BIMRLUtils.checkSingleQuote(sse.Name);
               string sseDescription = BIMRLUtils.checkSingleQuote(sse.Description);
               string sseObjectType = BIMRLUtils.checkSingleQuote(sse.ObjectType);
               string sseLongName = BIMRLUtils.checkSingleQuote(sse.LongName);
               IIfcRelAggregates relContainer = sse.Decomposes.FirstOrDefault();
               if (relContainer == null)
                  container = string.Empty;
               else
                  container = relContainer.RelatingObject.GlobalId.ToString();

               // Keep a mapping between IFC guid used as a key in BIMRL and the IFC line no of the entity
               _refBIMRLCommon.guidLineNoMappingAdd(bimrlProcessModel.currModelID, IfcLineNo, guid);

               SqlStmt = "Insert into BIMRL_Element_" + bimrlProcessModel.currFedID.ToString("X4") + "(Elementid, LineNo, ElementType, ModelID, Name, LongName, Description, ObjectType, Container, TypeID) Values ('"
                           + guid + "'," + IfcLineNo + ", '" + elementtype + "', " + bimrlProcessModel.currModelID.ToString() + ", '" + sseName + "', '" + sseLongName + "','" + sseDescription + "', '" + sseObjectType 
                           + "', '" + container + "', '" + typeID + "')";
               // status = DBOperation.insertRow(SqlStmt);
               command.CommandText = SqlStmt;
               currStep = SqlStmt;
               commandStatus = command.ExecuteNonQuery();

               // Add intormation of the product label (LineNo into a List for the use later to update the Geometry 
               _refBIMRLCommon.insEntityLabelListAdd(Math.Abs(IfcLineNo));
               currInsertCount++;

               if (currInsertCount % DBOperation.commitInterval == 0)
               {
                  //Do commit at interval but keep the long transaction (reopen)
                  DBOperation.commitTransaction();
               }
            }
         }
         catch (OracleException e)
         {
            string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
            _refBIMRLCommon.StackPushError(excStr);
            command.Dispose();
            throw;
         }

         DBOperation.commitTransaction();
         command.Dispose();
      }

      private void processSpatialStructureRel()
      {
         string SqlStmt;
         string currStep = string.Empty;

         DBOperation.beginTransaction();

         int commandStatus = -1;
         int currInsertCount = 0;
         string parentGuid = string.Empty;
         string parentType = string.Empty;

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

         try
         {
            IEnumerable<IIfcSpatialStructureElement> spatialStructure = _model.Instances.OfType<IIfcSpatialStructureElement>();
            foreach (IIfcSpatialStructureElement sse in spatialStructure)
            {
               // Insert itself at levelremoved=0
               int levelRemoved = 0;
               SqlStmt = "insert into BIMRL_SPATIALSTRUCTURE_" + bimrlProcessModel.currFedID.ToString("X4") + "(SPATIALELEMENTID, SPATIALELEMENTTYPE, PARENTID, PARENTTYPE, LEVELREMOVED)"
                  + " values ('" + sse.GlobalId.ToString() + "','" + sse.GetType().Name.ToUpper() + "','"
                  + sse.GlobalId.ToString() + "','" + sse.GetType().Name.ToUpper() + "'," + levelRemoved + ")";
               command.CommandText = SqlStmt;
               currStep = SqlStmt;
               commandStatus = command.ExecuteNonQuery();

               currInsertCount++;

               levelRemoved = 1;
               // insert aggregation relationship with the parent
               IEnumerable<IIfcRelAggregates> decomposes = sse.Decomposes;
               foreach (IIfcRelAggregates relAgg in decomposes)
               {
                  IIfcSpatialStructureElement parent = relAgg.RelatingObject as IIfcSpatialStructureElement;
                  if (parent == null)
                  {
                     parentGuid = string.Empty;
                     parentType = string.Empty;
                     levelRemoved = 0;
                  }
                  else
                  {
                     parentGuid = parent.GlobalId.ToString();
                     parentType = parent.GetType().Name.ToUpper();
                  }
                  SqlStmt = "insert into BIMRL_SPATIALSTRUCTURE_" + bimrlProcessModel.currFedID.ToString("X4") + "(SPATIALELEMENTID, SPATIALELEMENTTYPE, PARENTID, PARENTTYPE, LEVELREMOVED)"
                              + " values ('" + sse.GlobalId.ToString() + "','" + sse.GetType().Name.ToUpper() + "','"
                              + parentGuid + "','" + parentType + "'," + levelRemoved + ")";
                  command.CommandText = SqlStmt;
                  currStep = SqlStmt;
                  commandStatus = command.ExecuteNonQuery();

                  currInsertCount++;

                  while (parent != null)
                  {
                     levelRemoved++;
                     IEnumerable<IIfcRelAggregates> decomposesGP = parent.Decomposes;
                     if (decomposesGP.Count() == 0)
                        break;
                     // The decomposes attribute is a set of [0,1] only
                     IIfcSpatialStructureElement grandparent = decomposesGP.FirstOrDefault().RelatingObject as IIfcSpatialStructureElement;
                     if (grandparent == null)
                        break;  // no ancestor to insert anymore

                     SqlStmt = "insert into BIMRL_SPATIALSTRUCTURE_" + bimrlProcessModel.currFedID.ToString("X4") + "(SPATIALELEMENTID, SPATIALELEMENTTYPE, PARENTID, PARENTTYPE, LEVELREMOVED)"
                                 + " values ('" + sse.GlobalId.ToString() + "','" + sse.GetType().Name.ToUpper() + "','"
                                 + grandparent.GlobalId.ToString() + "','" + grandparent.GetType().Name.ToUpper() + "'," + levelRemoved + ")";
                     command.CommandText = SqlStmt;
                     currStep = SqlStmt;
                     commandStatus = command.ExecuteNonQuery();
                     currInsertCount++;
                     parent = grandparent;
                  }

                  if (currInsertCount % DBOperation.commitInterval == 0)
                  {
                     //Do commit at interval but keep the long transaction (reopen)
                     DBOperation.commitTransaction();
                  }
               }
            }
         }
         catch (OracleException e)
         {
            string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
            _refBIMRLCommon.StackPushError(excStr);
            command.Dispose();
            throw;
         }

         DBOperation.commitTransaction();
         command.Dispose();
      }
   }
}
