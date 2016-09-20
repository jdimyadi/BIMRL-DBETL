using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Xbim.IO;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.SharedBldgElements;
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
using Xbim.Ifc2x3.ActorResource;
using Xbim.Common.Geometry;
using Xbim.ModelGeometry.Converter;
using Xbim.Ifc2x3.ExternalReferenceResource;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using BIMRL.OctreeLib;

namespace BIMRL
{
    class BIMRLSpatialStructure
    {
        XbimModel _model;
        BIMRLCommon _refBIMRLCommon;

        public BIMRLSpatialStructure(XbimModel m, BIMRLCommon refBIMRLCommon)
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
                IEnumerable<IfcSpatialStructureElement> spatialStructure = _model.InstancesLocal.OfType<IfcSpatialStructureElement>();
                foreach (IfcSpatialStructureElement sse in spatialStructure)
                {
                    string SqlStmt;

                    // do something
                    string guid = sse.GlobalId.ToString();
                    int IfcLineNo = sse.EntityLabel;

                    string elementtype = sse.GetType().Name.ToUpper();
                    string typeID = String.Empty;
                    int typeLineNo = 0;
                    if (sse.GetDefiningType() != null)
                    {
                        typeID = sse.GetDefiningType().GlobalId;
                        typeLineNo = sse.GetDefiningType().EntityLabel;
                    }
                    string sseName = BIMRLUtils.checkSingleQuote(sse.Name);
                    string sseDescription = BIMRLUtils.checkSingleQuote(sse.Description);
                    string sseObjectType = BIMRLUtils.checkSingleQuote(sse.ObjectType);
                    string sseLongName = BIMRLUtils.checkSingleQuote(sse.LongName);
                    IfcRelDecomposes relContainer = sse.Decomposes.FirstOrDefault();
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
                _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
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
                IEnumerable<IfcSpatialStructureElement> spatialStructure = _model.InstancesLocal.OfType<IfcSpatialStructureElement>();
                foreach (IfcSpatialStructureElement sse in spatialStructure)
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
                    IfcSpatialStructureElement parent = sse.SpatialStructuralElementParent as IfcSpatialStructureElement;
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
                        IfcSpatialStructureElement grandparent = parent.SpatialStructuralElementParent as IfcSpatialStructureElement;
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
            catch (OracleException e)
            {
                string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.BIMRlErrorStack.Push(excStr);
                command.Dispose();
                throw;
            }

            DBOperation.commitTransaction();
            command.Dispose();
        }
    }
}
