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
using System.Globalization;
using BIMRL.Common;

namespace BIMRL
{
   public class BIMRLClassification
   {
      BIMRLCommon _refBIMRLCommon;
      IfcStore _model;

      public BIMRLClassification(IfcStore m, BIMRLCommon refBIMRLCommon)
      {
         _refBIMRLCommon = refBIMRLCommon;
         _model = m;
      }

      public void processClassificationItems()
      {
         List<string> arrGuid = new List<string>();
         List<string> arrClName = new List<string>();
         List<string> arrClCode = new List<string>();
         List<string> arrTGuid = new List<string>();
         List<string> arrTClName = new List<string>();
         List<string> arrTClCode = new List<string>();

         string currStep = "Insert Classification records";

         List<string> className = new List<string>();
         List<string> classSource= new List<string>();
         List<OracleParameterStatus> classSPS = new List<OracleParameterStatus>();
         List<string> classEdition = new List<string>();
         List<string> classEdDate = new List<string>();
         List<OracleParameterStatus> classEdDPS = new List<OracleParameterStatus>();
         List<string> classItemCode = new List<string>();
         List<string> classItemName = new List<string>();
         List<OracleParameterStatus> classItemNPS = new List<OracleParameterStatus>();
         List<string> classItemLocation = new List<string>();
         List<OracleParameterStatus> classItemLPS = new List<OracleParameterStatus>();

         DBOperation.beginTransaction();

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
         OracleCommand command2 = new OracleCommand(" ", DBOperation.DBConn);
         OracleCommand command3 = new OracleCommand(" ", DBOperation.DBConn);

         string sqlStmt = "Insert into " + DBOperation.formatTabName("BIMRL_CLASSIFICATION") + " (ClassificationName, ClassificationSource, "
                           + "ClassificationEdition, ClassificationEditionDate, ClassificationItemCode, ClassificationItemName, ClassificationItemLocation) Values "
                           + "(:1, :2, :3, to_date(:4, 'DD-MM-YYYY'), :5, :6, :7)";
         command.CommandText = sqlStmt;

         OracleParameter[] Param = new OracleParameter[7];
         for (int i = 0; i < 7; i++)
         {
               Param[i] = command.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
               Param[i].Direction = ParameterDirection.Input;
         }

         string sqlStmt2 = "Insert into " + DBOperation.formatTabName("BIMRL_ELEMCLASSIFICATION") + " (ElementID, ClassificationName, ClassificationItemCode) "
                           + " Values (:1, :2, :3)";
         command2.CommandText = sqlStmt2;

         OracleParameter[] Param2 = new OracleParameter[3];
         for (int i = 0; i < 3; i++)
         {
               Param2[i] = command2.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
               Param2[i].Direction = ParameterDirection.Input;
         }

         string sqlStmt3 = "Insert into " + DBOperation.formatTabName("BIMRL_TYPCLASSIFICATION") + " (ElementID, ClassificationName, ClassificationItemCode) "
                           + " Values (:1, :2, :3)";
         command3.CommandText = sqlStmt3;

         OracleParameter[] Param3 = new OracleParameter[3];
         for (int i = 0; i < 3; i++)
         {
            Param3[i] = command3.Parameters.Add(i.ToString(), OracleDbType.Varchar2);
            Param3[i].Direction = ParameterDirection.Input;
         }

         IEnumerable<IIfcRelAssociatesClassification> relClasses = _model.Instances.OfType<IIfcRelAssociatesClassification>();
         foreach (IIfcRelAssociatesClassification relClass in relClasses)
         {
            if (!(relClass.RelatingClassification is IIfcClassificationReference))
               continue;   // We only deal with IfcClassificationReference here

            IIfcClassificationReference classRef = relClass.RelatingClassification as IIfcClassificationReference;
            Xbim.Ifc2x3.Interfaces.IIfcClassificationReference classRef2x3 = relClass.RelatingClassification as Xbim.Ifc2x3.Interfaces.IIfcClassificationReference;

            string itemLocation = string.Empty;
            if (classRef.Location != null)
               itemLocation = classRef.Location;

            string itemRef = string.Empty;
            if (classRef2x3 != null)
            {
               if (classRef2x3.ItemReference == null)
                  continue;
               itemRef = classRef2x3.ItemReference;
            }
            else
            {
               if (classRef.Identification == null)
                  continue;
               itemRef = classRef.Identification;

            }

            string itemName = string.Empty;
            if (classRef.Name != null)
               itemName = classRef.Name;

            string refName = string.Empty;
            string refEdition = string.Empty;
            string refSource = string.Empty;
            string refEdDate = string.Empty;
            if (classRef.ReferencedSource == null)
            {
               refName = "Default Classification";
               refEdition = "Default Edition";
            }
            else
            {
               if (classRef.ReferencedSource is IIfcClassification)
               {
                  IIfcClassification theRefSource = classRef.ReferencedSource as IIfcClassification;
                  refName = theRefSource.Name;
                  refEdition = theRefSource.Edition;
                  refSource = theRefSource.Source;
                  if (theRefSource.EditionDate != null)
                     refEdDate = theRefSource.EditionDate.Value.ToString();
               }
               else
               {
                  IIfcClassificationReference theRefSource = classRef.ReferencedSource as IIfcClassificationReference;
                  refSource = theRefSource.Identification;
                  refName = theRefSource.Name;
               }
            }
                
            Tuple<string,string> bimrlClass = new Tuple<string,string>(refName, itemRef);   // PK for BIMRL_CLASSIFICATION
            if (!_refBIMRLCommon.ClassificationSetExist(bimrlClass))
            {
               // Record not in DB yet, insert
               className.Add(refName);
               classSource.Add(refSource);
               classEdition.Add(refEdition);
               classEdDate.Add(refEdDate);
               if (string.IsNullOrEmpty(refEdDate))
                  classEdDPS.Add(OracleParameterStatus.NullInsert);
               else
                  classEdDPS.Add(OracleParameterStatus.Success);
               classItemCode.Add(itemRef);
               classItemName.Add(itemName);
               if (string.IsNullOrEmpty(itemName))
                  classItemNPS.Add(OracleParameterStatus.NullInsert);
               else
                  classItemNPS.Add(OracleParameterStatus.Success);
               classItemLocation.Add(itemLocation);
               if (string.IsNullOrEmpty(itemName))
                  classItemLPS.Add(OracleParameterStatus.NullInsert);
               else
                  classItemLPS.Add(OracleParameterStatus.Success);

               // Add into Hashset, so that we do not have to insert duplicate record
               _refBIMRLCommon.ClassificationSetAdd(bimrlClass);
            }
                
            IEnumerable<IIfcDefinitionSelect> relObjects = relClass.RelatedObjects;
            foreach (IIfcDefinitionSelect relObjSel in relObjects)
            {
               IIfcObjectDefinition relObj = relObjSel as IIfcObjectDefinition;
               if (relObj is IIfcTypeObject)
               {
                  arrTGuid.Add(relObj.GlobalId.ToString());
                  arrTClName.Add(refName);
                  arrTClCode.Add(itemRef);
               }
               else if (relObj is IIfcObject)
               {
                  arrGuid.Add(relObj.GlobalId.ToString());
                  arrClName.Add(refName);
                  arrClCode.Add(itemRef);
               }
            }

            if ((className.Count + arrGuid.Count + arrTGuid.Count) >= DBOperation.commitInterval)
            {
               int commandStatus;

               try
               {
                  // for BIMRL_CLASSIFICATION
                  if (className.Count > 0)
                  {
                        Param[0].Value = className.ToArray();
                        Param[1].Value = classSource.ToArray();
                        Param[1].ArrayBindStatus = classSPS.ToArray();
                        Param[2].Value = classEdition.ToArray();
                        Param[3].Value = classEdDate.ToArray();
                        Param[3].ArrayBindStatus = classEdDPS.ToArray();
                        Param[4].Value = classItemCode.ToArray();
                        Param[5].Value = classItemName.ToArray();
                        Param[5].ArrayBindStatus = classItemNPS.ToArray();
                        Param[6].Value = classItemLocation.ToArray();
                        Param[6].ArrayBindStatus = classItemLPS.ToArray();
                        for (int i = 0; i < 6; i++)
                           Param[i].Size = className.Count;
                        command.ArrayBindCount = className.Count;

                        currStep = sqlStmt;
                        commandStatus = command.ExecuteNonQuery();
                  }

                  // for BIMRL_ELEMCLASSIFICATION
                  if (arrGuid.Count > 0)
                  {
                        Param2[0].Value = arrGuid.ToArray();
                        Param2[1].Value = arrClName.ToArray();
                        Param2[2].Value = arrClCode.ToArray();
                        for (int i = 0; i < 3; i++)
                           Param[i].Size = arrGuid.Count;
                        command2.ArrayBindCount = arrGuid.Count;

                        currStep = sqlStmt2;
                        commandStatus = command2.ExecuteNonQuery();
                  }

                  // for BIMRL_TYPCLASSIFICATION
                  if (arrTGuid.Count > 0)
                  {
                        Param3[0].Value = arrTGuid.ToArray();
                        Param3[1].Value = arrTClName.ToArray();
                        Param3[2].Value = arrTClCode.ToArray();
                        for (int i = 0; i < 3; i++)
                           Param3[i].Size = arrTGuid.Count;
                        command3.ArrayBindCount = arrTGuid.Count;

                        currStep = sqlStmt3;
                        commandStatus = command3.ExecuteNonQuery();
                  }

                  DBOperation.commitTransaction();

                  className.Clear();
                  classSource.Clear();
                  classSPS.Clear();
                  classEdition.Clear();
                  classEdDate.Clear();
                  classEdDPS.Clear();
                  classItemCode.Clear();
                  classItemName.Clear();
                  classItemNPS.Clear();
                  classItemLocation.Clear();
                  classItemLPS.Clear();

                  arrGuid.Clear();
                  arrClCode.Clear();
                  arrClName.Clear();

                  arrTGuid.Clear();
                  arrTClCode.Clear();
                  arrTClName.Clear();
               }
               catch (OracleException e)
               {
                  string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                  _refBIMRLCommon.StackPushIgnorableError(excStr);

                  className.Clear();
                  classSource.Clear();
                  classSPS.Clear();
                  classEdition.Clear();
                  classEdDate.Clear();
                  classEdDPS.Clear();
                  classItemCode.Clear();
                  classItemName.Clear();
                  classItemNPS.Clear();
                  classItemLocation.Clear();
                  classItemLPS.Clear();

                  arrGuid.Clear();
                  arrClCode.Clear();
                  arrClName.Clear();

                  arrTGuid.Clear();
                  arrTClCode.Clear();
                  arrTClName.Clear();

                  continue;
               }
               catch (SystemException e)
               {
                  string excStr = "%%Insert Error - " + e.Message + "\n\t" + currStep;
                  _refBIMRLCommon.StackPushIgnorableError(excStr);
               }
            }
         }

         if ((className.Count + arrGuid.Count + arrTGuid.Count) > 0)
         {
               int commandStatus;

               try
               {
                  // for BIMRL_CLASSIFICATION
                  if (className.Count > 0)
                  {
                     Param[0].Value = className.ToArray();
                     Param[1].Value = classSource.ToArray();
                     Param[1].ArrayBindStatus = classSPS.ToArray();
                     Param[2].Value = classEdition.ToArray();
                     Param[3].Value = classEdDate.ToArray();
                     Param[3].ArrayBindStatus = classEdDPS.ToArray();
                     Param[4].Value = classItemCode.ToArray();
                     Param[5].Value = classItemName.ToArray();
                     Param[5].ArrayBindStatus = classItemNPS.ToArray();
                     Param[6].Value = classItemLocation.ToArray();
                     Param[6].ArrayBindStatus = classItemLPS.ToArray();
                     for (int i = 0; i < 6; i++)
                           Param[i].Size = className.Count;
                     command.ArrayBindCount = className.Count;

                     currStep = sqlStmt;
                     commandStatus = command.ExecuteNonQuery();
                  }

                  // for BIMRL_ELEMCLASSIFICATION
                  if (arrGuid.Count > 0)
                  {
                     Param2[0].Value = arrGuid.ToArray();
                     Param2[1].Value = arrClName.ToArray();
                     Param2[2].Value = arrClCode.ToArray();
                     for (int i = 0; i < 3; i++)
                           Param[i].Size = arrGuid.Count;
                     command2.ArrayBindCount = arrGuid.Count;

                     currStep = sqlStmt2;
                     commandStatus = command2.ExecuteNonQuery();
                  }

                  // for BIMRL_TYPCLASSIFICATION
                  if (arrTGuid.Count > 0)
                  {
                     Param3[0].Value = arrTGuid.ToArray();
                     Param3[1].Value = arrTClName.ToArray();
                     Param3[2].Value = arrTClCode.ToArray();
                     for (int i = 0; i < 3; i++)
                           Param3[i].Size = arrTGuid.Count;
                     command3.ArrayBindCount = arrTGuid.Count;

                     currStep = sqlStmt3;
                     commandStatus = command3.ExecuteNonQuery();
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
         command3.Dispose();
      }
   }
}
