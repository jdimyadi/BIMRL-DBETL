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
using Xbim.Ifc2x3.UtilityResource;
using Xbim.Ifc2x3.ActorResource;
using Xbim.XbimExtensions;
using Xbim.ModelGeometry;
using Xbim.ModelGeometry.Scene;
using Xbim.XbimExtensions.Interfaces;
using Xbim.Ifc2x3.Extensions;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Xbim.Common.Exceptions;
using System.Diagnostics;
using Xbim.Common.Geometry;
using Xbim.ModelGeometry.Converter;
using Xbim.Ifc2x3.ExternalReferenceResource;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using BIMRL.OctreeLib;

namespace BIMRL
{
    public class BIMRLOwnerHistory
    {
        private XbimModel _model;
        private BIMRLCommon _refBIMRLCommon;

        public BIMRLOwnerHistory(XbimModel m, BIMRLCommon refBIMRLCommon)
        {
            _model = m;
            _refBIMRLCommon = refBIMRLCommon;
        }

        public void processOwnerHistory()
        {
            string currStep = string.Empty;

            DBOperation.beginTransaction();

            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);

            IEnumerable<IfcOwnerHistory> ownerHistoryList = _model.InstancesLocal.OfType<IfcOwnerHistory>();
            foreach (IfcOwnerHistory ownH in ownerHistoryList)
            {
                string columnSpec = "ID, ModelID";
                int ID = Math.Abs(ownH.EntityLabel);
                string valueList = ID.ToString() + ", " + bimrlProcessModel.currModelID;

                string owningPersonName = ownH.OwningUser.ThePerson.GetFullName().Trim();
                if (!string.IsNullOrEmpty(owningPersonName))
                {
                    columnSpec += ", OwningPersonName";
                    valueList += ", '" + owningPersonName + "'";
                }

                if (ownH.OwningUser.RolesString != null)
                {
                    string owningPersonRoles = ownH.OwningUser.RolesString.Trim();
                    if (!string.IsNullOrEmpty(owningPersonRoles))
                    {
                        columnSpec += ", OwningPersonRoles";
                        valueList += ", '" + owningPersonRoles + "'";
                    }
                }

                if (ownH.OwningUser.ThePerson.Addresses != null)
                {
                    string owningPersonAddresses = ownH.OwningUser.ThePerson.Addresses.ToString();
                    if (!string.IsNullOrEmpty(owningPersonAddresses))
                    {
                        columnSpec += ", OwningPersonAddresses";
                        valueList += ", '" + owningPersonAddresses + "'";
                    }
                }

                if (ownH.OwningUser.TheOrganization.Id != null)
                {
                    columnSpec += ", OwningOrganizationId";
                    valueList += ", '" + ownH.OwningUser.TheOrganization.Id + "'";
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

                if (ownH.OwningUser.TheOrganization.RolesString != null)
                {
                    string owningOrganizationRoles = ownH.OwningUser.TheOrganization.RolesString.Trim();
                    if (!string.IsNullOrEmpty(owningOrganizationRoles))
                    {
                        columnSpec += ", OwningOrganizationRoles";
                        valueList += ", '" + owningOrganizationRoles + "'";
                    }
                }

                if (ownH.OwningUser.TheOrganization.Addresses != null)
                {
                    string owningOrganizationAddresses = ownH.OwningUser.TheOrganization.Addresses.ToString();
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
                    valueList += ", '" + ownH.LastModifyingUser.ThePerson.GetFullName() + "'";
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
                    string excStr = "%%Insert Error (IGNORED) - " + e.Message + "\n\t" + currStep;
                    _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
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
    }
}
