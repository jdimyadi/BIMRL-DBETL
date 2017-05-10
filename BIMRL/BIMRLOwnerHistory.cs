using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.Ifc4.Interfaces;
using Xbim.Common;
using Xbim.Ifc;
using Oracle.DataAccess.Client;
using BIMRL.OctreeLib;

namespace BIMRL
{
   public class BIMRLOwnerHistory
   {
      private IModel _model;
      private BIMRLCommon _refBIMRLCommon;

      public BIMRLOwnerHistory(IModel m, BIMRLCommon refBIMRLCommon)
      {
         _model = m;
         _refBIMRLCommon = refBIMRLCommon;
      }

      public void processOwnerHistory()
      {
         string currStep = string.Empty;

         DBOperation.beginTransaction();

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

         IEnumerable<IIfcOwnerHistory> ownerHistoryList = _model.Instances.OfType<IIfcOwnerHistory>();
         foreach (IIfcOwnerHistory ownH in ownerHistoryList)
         {
            string columnSpec = "ID, ModelID";
            int ID = Math.Abs(ownH.EntityLabel);
            string valueList = ID.ToString() + ", " + bimrlProcessModel.currModelID;

            IIfcPerson thePerson = ownH.OwningUser.ThePerson;
            string middleNames = string.Empty;
            foreach (string middleName in thePerson.MiddleNames)
            {
               BIMRLCommon.appendToString(middleName.Trim(), " ", ref middleNames);
            }
            string owningPersonName = thePerson.GivenName.ToString().Trim() + " " + middleNames + " " + thePerson.FamilyName.ToString().Trim();

            if (!string.IsNullOrEmpty(owningPersonName))
            {
               columnSpec += ", OwningPersonName";
               valueList += ", '" + owningPersonName + "'";
            }

            if (ownH.OwningUser.Roles != null)
            {
               string roleString = string.Empty;
               foreach (var role in ownH.OwningUser.Roles)
               {
                  string roleStr = role.RoleString;
                  if (roleStr.Equals("USERDEFINED") && role.UserDefinedRole.HasValue)
                     roleStr = role.UserDefinedRole.Value.ToString();
                  BIMRLCommon.appendToString(roleStr, ", ", ref roleString);
               }
               if (!string.IsNullOrEmpty(roleString))
               {
                  columnSpec += ", OwningPersonRoles";
                  valueList += ", '" + roleString + "'";
               }
            }
            //if (ownH.OwningUser.RolesString != null)
            //    {
            //        string owningPersonRoles = ownH.OwningUser.RolesString.Trim();
            //        if (!string.IsNullOrEmpty(owningPersonRoles))
            //        {
            //            columnSpec += ", OwningPersonRoles";
            //            valueList += ", '" + owningPersonRoles + "'";
            //        }
            //    }

            if (ownH.OwningUser.ThePerson.Addresses != null)
            {
               string owningPersonAddresses = "";
               foreach (IIfcAddress addr in ownH.OwningUser.ThePerson.Addresses)
               {
                  owningPersonAddresses += addr + ", ";
               }

               if (!string.IsNullOrEmpty(owningPersonAddresses))
               {
                  columnSpec += ", OwningPersonAddresses";
                  valueList += ", '" + owningPersonAddresses + "'";
               }
            }

            if (ownH.OwningUser.TheOrganization.Identification != null)
            {
               columnSpec += ", OwningOrganizationId";
               valueList += ", '" + ownH.OwningUser.TheOrganization.Identification + "'";
            }

            if (ownH.OwningUser.TheOrganization.Name != null)
            {
               columnSpec += ", OwningOrganizationName";
               valueList += ", '" + ownH.OwningUser.TheOrganization.Name + "'";
            }

            if (ownH.OwningUser.TheOrganization.Description != null)
            {
               columnSpec += ", OwningOrganizationDescription";
               valueList += ", '" + ownH.OwningUser.TheOrganization.Description + "'";
            }

            if (ownH.OwningUser.TheOrganization.Roles != null)
            {
               string roleString = string.Empty;
               foreach (var role in ownH.OwningUser.TheOrganization.Roles)
               {
                  string roleStr = role.RoleString;
                  if (roleStr.Equals("USERDEFINED") && role.UserDefinedRole.HasValue)
                     roleStr = role.UserDefinedRole.Value.ToString();
                  BIMRLCommon.appendToString(roleStr, ", ", ref roleString);
               }
               if (!string.IsNullOrEmpty(roleString))
               {
                  columnSpec += ", OwningOrganizationRoles";
                  valueList += ", '" + roleString + "'";
               }

            //string owningOrganizationRoles = ownH.OwningUser.TheOrganization.RolesString.Trim();
            //     if (!string.IsNullOrEmpty(owningOrganizationRoles))
            //     {
            //         columnSpec += ", OwningOrganizationRoles";
            //         valueList += ", '" + owningOrganizationRoles + "'";
            //     }
            }

            if (ownH.OwningUser.TheOrganization.Addresses != null)
            {
               string owningOrganizationAddresses = "";
               foreach (IIfcAddress addr in ownH.OwningUser.TheOrganization.Addresses)
               {
                  owningOrganizationAddresses += addr + ", ";
               }
               if (!string.IsNullOrEmpty(owningOrganizationAddresses))
               {
                  columnSpec += ", OwningOrganizationAddresses";
                  valueList += ", '" + owningOrganizationAddresses + "'";
               }
            }

            columnSpec += ", ApplicationName, ApplicationVersion, ApplicationDeveloper, ApplicationID";
            valueList += ", '" + ownH.OwningApplication.ApplicationFullName + "', '" + ownH.OwningApplication.Version + "', '"
                        + ownH.OwningApplication.ApplicationDeveloper.Name + "', '" + ownH.OwningApplication.ApplicationIdentifier + "'";

            if (ownH.State != null)
            {
               columnSpec += ", State";
               valueList += ", '" + ownH.State.ToString() + "'";
            }

            columnSpec += ", ChangeAction";
            valueList += ", '" + ownH.ChangeAction.ToString() + "'";

            if (ownH.LastModifiedDate != null)
            {
               long lastModTS = (long)ownH.LastModifiedDate.Value / 86400; // No of days
               columnSpec += ", LastModifiedDate";
               valueList += ", to_date('01-01-1970 00:00:00','DD-MM-YYYY HH24:MI:SS')+" + lastModTS.ToString() + " "; 
            }

            if (ownH.LastModifyingUser != null)
            {
               columnSpec += ", LastModifyingUserID";
            IIfcPerson modPerson = ownH.LastModifyingUser.ThePerson;
            string LastModifyingUserId = modPerson.GivenName.ToString().Trim() + " " + modPerson.MiddleNames.ToString().Trim() + " " + modPerson.FamilyName.ToString().Trim();

            valueList += ", '" + LastModifyingUserId + "'";
            }

            if (ownH.LastModifyingApplication != null)
            {
               columnSpec += ", LastModifyingApplicationID";
               valueList += ", 'ID: " + ownH.LastModifyingApplication.ApplicationIdentifier + "; Name: " + ownH.LastModifyingApplication.ApplicationFullName
                           + "; Ver: " + ownH.LastModifyingApplication.Version + "; Dev: " + ownH.LastModifyingApplication.ApplicationDeveloper.Name + "'";
            }

            long crDateTS = ownH.CreationDate / 86400; // No of days
            columnSpec += ", CreationDate";
            valueList += ", to_date('01-01-1970 00:00:00','DD-MM-YYYY HH24:MI:SS')+" + crDateTS.ToString() + " ";

            string sqlStmt = "insert into BIMRL_OWNERHISTORY_" + bimrlProcessModel.currFedID.ToString("X4") + "(" + columnSpec + ") values (" + valueList + ")";
            currStep = sqlStmt;

            command.CommandText = sqlStmt;

            try
            {
               int commandStatus = command.ExecuteNonQuery();
               Tuple<int, int> ownHEntry = new Tuple<int, int>(ID, bimrlProcessModel.currModelID);
               _refBIMRLCommon.OwnerHistoryAdd(ownHEntry);
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
   }
}
