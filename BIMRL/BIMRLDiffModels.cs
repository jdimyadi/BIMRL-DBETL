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
using System.IO;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.OctreeLib;
using Newtonsoft.Json;
using Ionic.Zip;

namespace BIMRL
{
   public class BIMRLDiffModels
   {
      static int? compNewModel;
      static int? compRefModel;
      static BIMRLCommon m_BIMRLCommonRef;
      public string GraphicsZipFile { get; private set; }
      IDictionary<string, DataTable> diffResultsDict = new Dictionary<string, DataTable>();

      public BIMRLDiffModels(int modelId1, int modelId2, BIMRLCommon bimrlCommon)
      {
         compNewModel = modelId1;
         compRefModel = modelId2;
         m_BIMRLCommonRef = bimrlCommon;
      }

      public BIMRLDiffModels(BIMRLCommon bimrlCommon)
      {
         compNewModel = null;
         compRefModel = null;
         m_BIMRLCommonRef = bimrlCommon;
      }

      public void RunDiff(string outputFileName, int? newModel = null, int? refModel = null, BIMRLDiffOptions options = null)
      {
         if (options == null)
            options = BIMRLDiffOptions.SelectAllOptions();

         if (newModel.HasValue)
            compNewModel = newModel.Value;
         if (refModel.HasValue)
            compRefModel = refModel.Value;

         if ((!compNewModel.HasValue || !compRefModel.HasValue) && (!newModel.HasValue || !refModel.HasValue))
            throw new Exception("Model IDs must be supplied!");
        
         string elemTableNew = "BIMRL_ELEMENT_" + compNewModel.Value.ToString("X4");
         string elemTableRef = "BIMRL_ELEMENT_" + compRefModel.Value.ToString("X4");

         // Create tables to keep track of the new or deleted objects
         runNonQuery("drop table newelements", true);
         runNonQuery("create table newelements as select elementid from " + elemTableNew + " minus select elementid from " + elemTableRef, true);
         runNonQuery("drop table deletedelements", true);
         runNonQuery("create table deletedelements as select elementid from " + elemTableRef + " minus select elementid from " + elemTableNew, true);

         if (options.CheckNewAndDeletedObjects)
            AddResultDict("CheckNewAndDeletedObjects", DiffObjects());

         if (options.CheckGeometriesDiffBySignature)
         {
            GraphicsZipFile = Path.Combine(Path.GetDirectoryName(outputFileName), Path.GetFileNameWithoutExtension(outputFileName) + "-GraphicsOutput.zip");
            AddResultDict("CheckGeometriesDiff", DiffGeometry(GraphicsZipFile, options.GeometryCompareTolerance));
         }

         if (options.CheckTypeAndTypeAssignments)
            AddResultDict("CheckTypeAndTypeAssignments", DiffType());

         if (options.CheckContainmentRelationships)
            AddResultDict("CheckContainmentRelationships", DiffContainment());

         if (options.CheckOwnerHistory)
            AddResultDict("CheckOwnerHistory", DiffOwnerHistory());

         if (options.CheckProperties)
            AddResultDict("CheckProperties", DiffProperty());

         if (options.CheckMaterials)
            AddResultDict("CheckMaterials", DiffMaterial());

         if (options.CheckClassificationAssignments)
            AddResultDict("CheckClassificationAssignments", DiffClassification());

         if (options.CheckGroupMemberships)
            AddResultDict("CheckGroupMemberships", DiffGroupMembership());

         if (options.CheckAggregations)
            AddResultDict("CheckAggregations", DiffAggregation());

         if (options.CheckConnections)
            AddResultDict("CheckConnections", DiffConnection());

         if (options.CheckElementDependencies)
            AddResultDict("CheckElementDependencies", DiffElementDependency());

         if (options.CheckSpaceBoundaries)
            AddResultDict("CheckSpaceBoundaries", DiffSpaceBoundary());

         try
         {
            string json = JsonConvert.SerializeObject(diffResultsDict, Formatting.Indented);
            using (StreamWriter file = File.CreateText(outputFileName))
            {
               JsonSerializer serializer = new JsonSerializer();
               serializer.Serialize(file, diffResultsDict);
            }
         }
         catch
         {
            throw new Exception("Fail to sereialize to " + outputFileName);
         }
      }

      IList<DataTable> DiffObjects()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string elemTableNew = "BIMRL_ELEMENT_" + compNewModel.Value.ToString("X4");
         string elemTableRef = "BIMRL_ELEMENT_" + compRefModel.Value.ToString("X4");

         string newElementReport = "select elementid, elementtype, name, longname, description, modelid, container, typeid from " + elemTableNew
                                    + " where elementid in (select elementid from newelements)";

         DataTable newElemRes = queryMultipleRows(newElementReport, "New Elements");
         qResults.Add(newElemRes);

         string delElementReport = "select elementid, elementtype, name, longname, description, modelid, container, typeid from " + elemTableRef
                                    + " where elementid in (select elementid from deletedelements)";

         DataTable delElemRes = queryMultipleRows(delElementReport, "Deleted Elements");
         qResults.Add(delElemRes);

         return qResults;
      }

      IList<DataTable> DiffGeometry(string graphicsOutputZip, double tol = 0.0001)
      {
         IList<DataTable> qResults = new List<DataTable>();
         string elemTableNew = "BIMRL_ELEMENT_" + compNewModel.Value.ToString("X4");
         string elemTableRef = "BIMRL_ELEMENT_" + compRefModel.Value.ToString("X4");
         double tolNeg = -tol;

         string diffGeomReport = "select a.elementid \"Elementid\", a.elementtype \"Element Type\", a.total_surface_area \"Surface Area (New)\", b.total_surface_area \"Surface Area (Ref)\", "
                                    + "a.geometrybody_bbox_centroid \"Centroid (New)\", b.geometrybody_bbox_centroid \"Centroid (Ref)\", a.geometrybody_bbox \"Bounding Box (New)\", b.geometrybody_bbox \"Bounding Box (Ref)\""
                                    + "from " + elemTableNew + " a, " + elemTableRef + " b "
                                    + "where a.elementid = b.elementid and a.geometrybody is not null "
                                    + "and((a.total_surface_area - b.total_surface_area) < " + tolNeg.ToString("G") + " or (a.total_surface_area - b.total_surface_area) > " + tol.ToString("G")
                                    + "or sdo_geom.sdo_difference(a.geometrybody_bbox, b.geometrybody_bbox, " + tol.ToString("G") + ") is not null "
                                    + "or sdo_geom.sdo_difference(a.geometrybody_bbox_centroid, b.geometrybody_bbox_centroid, " + tol.ToString("G") + ") is not null)";

         DataTable geomDiffRes = queryMultipleRows(diffGeomReport, "Geometry Difference By Signature");

         // Go through the differences and show them in X3d file (need to add the X3d file name into the geomDiffRes
         exportGraphicsDiff(geomDiffRes, compNewModel.Value, compRefModel.Value, graphicsOutputZip);

         qResults.Add(geomDiffRes);
         return qResults;
      }

      void exportGraphicsDiff(DataTable geomDiffRes, int modelIDNew, int modelIDRef, string zippedX3DFiles)
      {
         string tempDirectory = Path.Combine(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
         Directory.CreateDirectory(tempDirectory);

         geomDiffRes.Columns.Add("GraphicFile");
         foreach (DataRow row in geomDiffRes.Rows)
         {
            string elemid = row["Elementid"].ToString();
            string x3dFile = elemid + ".x3d";
            BIMRLExportSDOToX3D x3dExp = new BIMRLExportSDOToX3D(m_BIMRLCommonRef, Path.Combine(tempDirectory, x3dFile));    // Initialize first

            if (row["Bounding Box (New)"] != null)
            {
               x3dExp.IDsToHighlight.Add(elemid);
               x3dExp.highlightColor = new ColorSpec();
               x3dExp.highlightColor.emissiveColorRed = 255;      // Set the New object to RED color
               x3dExp.highlightColor.emissiveColorGreen = 0;
               x3dExp.highlightColor.emissiveColorBlue = 0;
               x3dExp.transparencyOverride = 0.30;
               x3dExp.exportElemGeomToX3D(modelIDNew, "elementid='" + elemid + "'");
            }

            if (row["Bounding Box (Ref)"] != null)
            {
               x3dExp.IDsToHighlight.Add(elemid);
               x3dExp.highlightColor.emissiveColorRed = 0;      // Set the New object to BLUE color
               x3dExp.highlightColor.emissiveColorGreen = 0;
               x3dExp.highlightColor.emissiveColorBlue = 255;
               x3dExp.transparencyOverride = 0.30;
               x3dExp.exportElemGeomToX3D(modelIDRef, "elementid='" + elemid + "'");
            }

            // Add background element from IFCSLAB to give a sense of spatial relative location
            x3dExp.transparencyOverride = 0.9;
            string whereCondElemGeom = "ELEMENTID IN (SELECT ELEMENTID FROM BIMRL_ELEMENT_" + modelIDNew.ToString("X4") + " WHERE elementtype like 'IFCSLAB%')";
            x3dExp.exportElemGeomToX3D(modelIDNew, whereCondElemGeom);
            x3dExp.endExportToX3D();
            row["GraphicFile"] = x3dFile;
         }

         if (File.Exists(zippedX3DFiles))
            File.Delete(zippedX3DFiles);
         try
         {
            using (ZipFile zip = new ZipFile(zippedX3DFiles))
            {
               zip.AddDirectory(tempDirectory);
               zip.Save();
               Directory.Delete(tempDirectory, true);
            }
         }
         catch (Exception e)
         {
            throw new Exception("Error during writing the zip file! " + e.Message);
         }
      }

      IList<DataTable> DiffType()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string elemTableNew = "BIMRL_ELEMENT_" + compNewModel.Value.ToString("X4");
         string elemTableRef = "BIMRL_ELEMENT_" + compRefModel.Value.ToString("X4");
         string typeTableNew = "BIMRL_TYPE_" + compNewModel.Value.ToString("X4");
         string typeTableRef = "BIMRL_TYPE_" + compRefModel.Value.ToString("X4");

         //string newTypeReport = "select elementid, ifctype, name from " + typeTableNew + " minus "
         //                        + "select elementid, ifctype, name from " + typeTableRef;

         //DataTable newTypeRes = queryMultipleRows(newTypeReport);
         //newTypeRes.TableName = "New Types";
         //qResults.Add(newTypeRes);

         //string delTypeReport = "select elementid, ifctype, name from " + typeTableRef + " minus "
         //               + "select elementid, ifctype, name from " + typeTableNew;

         //DataTable delTypeRes = queryMultipleRows(delTypeReport);
         //delTypeRes.TableName = "Deleted Types";
         //qResults.Add(delTypeRes);

         string TypeAssignmentReport = "select tab1.*, tab2.* from "
                                       + "(select a.elementid id_new, a.elementtype \"Element Type (New)\", a.name \"Element Name (New)\", b.ifcType \"IFC Type Entity (New)\", b.elementid as typeid_new, b.name as typename_new from " + elemTableNew + " a, " + typeTableNew + " b where b.elementid = a.typeid) tab1 "
                                       + "full outer join "
                                       + "(select c.elementid as id_ref, c.elementtype \"Element Type (Ref)\", c.name \"Element Name (Ref)\", d.ifcType \"IFC Type Entity (Ref)\", d.elementid as typeid_ref, d.name as typename_ref from " + elemTableRef + " c, " + typeTableRef + " d where d.elementid = c.typeid) tab2 "
                                       + "on tab1.id_new = tab2.id_ref "
                                       + "where tab1.typename_new != tab2.typename_ref or (tab1.typename_new is not null and tab2.typename_ref is null) or (tab1.typename_new is null and tab2.typename_ref is not null)";

         DataTable assigElemRes = queryMultipleRows(TypeAssignmentReport, "Type Assignment Changes");
         qResults.Add(assigElemRes);

         return qResults;
      }


      IList<DataTable> DiffContainment()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string elemTableNew = "BIMRL_ELEMENT_" + compNewModel.Value.ToString("X4");
         string elemTableRef = "BIMRL_ELEMENT_" + compRefModel.Value.ToString("X4");

         string containerReport = "select tab1.*, tab2.* from "
                                 + "(select a.elementid id_new, a.elementtype \"Element Type (New)\", a.name \"Element Name (New)\", b.elementid as containerid_new, b.name as containername_new, b.longname as containerlongname_new from " + elemTableNew + " a, " + elemTableNew + " b where b.elementid = a.container) tab1 "
                                 + "full outer join "
                                 + "(select c.elementid as id_ref, c.elementtype \"Element Type (Ref)\", c.name \"Element Name (Ref)\", d.elementid as containerid_ref, d.name as containername_ref, d.longname as containerlongname_ref from " + elemTableRef + " c, " + elemTableRef + " d where d.elementid = c.container) tab2 "
                                 + "on tab1.id_new = tab2.id_ref "
                                 + "where tab1.containerid_new != tab2.containerid_ref or(tab1.containerid_new is not null and tab2.containerid_ref is null) or(tab1.containerid_new is null and tab2.containerid_ref is not null)";

         DataTable containerRes = queryMultipleRows(containerReport, "Container Assignment Changes");
         qResults.Add(containerRes);

         return qResults;
      }

      IList<DataTable> DiffOwnerHistory()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string oHTableNew = "BIMRL_OWNERHISTORY_" + compNewModel.Value.ToString("X4");
         string oHTableRef = "BIMRL_OWNERHISTORY_" + compRefModel.Value.ToString("X4");

         string oHReport = "select a.*, b.* from " + oHTableNew + " a "
                                 + "full outer join " + oHTableRef + " b "
                                 + "on ( a.id = b.id and a.modelid = b.modelid) ";
         DataTable oHRes = queryMultipleRows(oHReport, "Owner History Changes");
         qResults.Add(oHRes);

         return qResults;
      }

      IList<DataTable> DiffProperty()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string propTableNew = "BIMRL_PROPERTIES_" + compNewModel.Value.ToString("X4");
         string propTableRef = "BIMRL_PROPERTIES_" + compRefModel.Value.ToString("X4");

         string elemNewProps = "select elementid, fromtype, propertygroupname, propertyname from " + propTableNew
                                + " minus "
                                + "select elementid, fromtype, propertygroupname, propertyname from " + propTableRef
                                + " order by elementid, fromtype, propertygroupname, propertyname";

         DataTable elemNewRes = queryMultipleRows(elemNewProps, "Elements with New Properties");
         qResults.Add(elemNewRes);

         string elemDelProps = "select elementid, fromtype, propertygroupname, propertyname from " + propTableRef
                                + " minus "
                                + "select elementid, fromtype, propertygroupname, propertyname from " + propTableNew
                                + " order by elementid, fromtype, propertygroupname, propertyname";

         DataTable elemDelRes = queryMultipleRows(elemDelProps, "Elements with Deleted Properties");
         qResults.Add(elemDelRes);

         string changeProps = "select a.elementid \"Element ID (New)\", a.fromType \"Type?\", a.propertygroupname \"PSet Name (New)\", a.propertyname \"Property Name (New)\", a.propertyvalue \"Value (New)\", " 
                              + "b.propertyvalue \"Value (Ref)\", b.propertyname \"Property Name (Ref)\", b.propertygroupname \"PSet Name (Ref)\", b.fromType \"Type?\", b.elementid \"Deleted ID\"  from " + propTableNew + " a "
                              + "full outer join " + propTableRef 
                              + " b on (a.elementid = b.elementid and a.propertygroupname = b.propertygroupname and a.propertyname = b.propertyname) "
                              + "where (a.propertyvalue != b.propertyvalue or(a.propertyvalue is null and b.propertyvalue is not null) or(a.propertyvalue is not null and b.propertyvalue is null)) "
                              + "and (a.elementid not in (select elementid from newelements) or b.elementid not in (select elementid from deletedelements))";

         DataTable changePropRes = queryMultipleRows(changeProps, "Elements with Chages in Property Value");
         qResults.Add(changePropRes);

         return qResults;
      }

      IList<DataTable> DiffMaterial()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string matTableNew = "BIMRL_ELEMENTMATERIAL_" + compNewModel.Value.ToString("X4");
         string matTableRef = "BIMRL_ELEMENTMATERIAL_" + compRefModel.Value.ToString("X4");

         string newMatReport = "select elementid, materialname, materialthickness from " + matTableNew + " where setname is null "
                                 + "minus "
                                 + "select elementid, materialname, materialthickness from " + matTableRef + " where setname is null";

         DataTable newMatRes = queryMultipleRows(newMatReport, "New Material (Single/List)");
         qResults.Add(newMatRes);

         string delMatReport = "select elementid, materialname, materialthickness from " + matTableRef + " where setname is null "
                        + "minus "
                        + "select elementid, materialname, materialthickness from " + matTableNew + " where setname is null";

         DataTable delMatRes = queryMultipleRows(delMatReport, "Deleted Material (Single/List)");
         qResults.Add(delMatRes);

         string newMatSetReport = "select a.elementid, a.setname, a.materialname \"New Material\", b.materialname \"Ref Material\" from " + matTableNew + " a "
                                 + "left outer join " + matTableRef + " b on (a.elementid = b.elementid and a.setname=b.setname) "
                                 + "where a.setname is not null and b.setname is not null and ((a.materialname != b.materialname) "
                                 + "or (a.materialname is null and b.materialname is not null) or (a.materialname is not null and b.materialname is null))"; 

         DataTable newMatSetRes = queryMultipleRows(newMatSetReport, "New Material Set");
         qResults.Add(newMatSetRes);

         string delMatSetReport = "select a.elementid, a.setname, a.materialname \"Deleted Material\" from " + matTableRef + " a "
                                 + "left outer join " + matTableNew + " b on (a.elementid = b.elementid and a.setname=b.setname) "
                                 + "where a.setname is not null and b.setname is not null and ((a.materialname != b.materialname) "
                                 + "or (a.materialname is null and b.materialname is not null) or (a.materialname is not null and b.materialname is null))";

         DataTable delMatSetRes = queryMultipleRows(delMatSetReport, "Deleted Material Set");
         qResults.Add(delMatSetRes);

         return qResults;
      }

      IList<DataTable> DiffClassification()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string classifTableNew = "BIMRL_CLASSIFASSIGNMENT_" + compNewModel.Value.ToString("X4");
         string classifTableRef = "BIMRL_CLASSIFASSIGNMENT_" + compRefModel.Value.ToString("X4");

         string classifReport = "select a.elementid \"ElementID (New)\", a.classificationname \"Classification Name (New)\", a.classificationitemcode \"Code (New)\", a.fromtype \"FromType?\", "
                                 + "b.elementid \"ElementID (Ref)\", b.classificationname \"Classification Name (Ref)\", b.classificationitemcode \"Code (Ref)\", b.fromtype \"FromType?\""
                                 + " from " + classifTableNew + " a full outer join " + classifTableRef + " b on (a.elementid = b.elementid and a.classificationname = b.classificationname and a.classificationitemcode = b.classificationitemcode) "
                                 + "where (a.classificationitemcode is null and b.classificationitemcode is not null) or (a.classificationitemcode is not null and b.classificationitemcode is null)";

         DataTable classifRes = queryMultipleRows(classifReport, "Classification Assignments");
         qResults.Add(classifRes);

         return qResults;
      }

      IList<DataTable> DiffGroupMembership()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string groupRelTableNew = "BIMRL_RELGROUP_" + compNewModel.Value.ToString("X4");
         string groupRelTableRef = "BIMRL_RELGROUP_" + compRefModel.Value.ToString("X4");

         string groupRelReport = "Select a.groupelementid \"Group ID (New)\", a.groupelementtype \"Group Type (New)\", a.memberelementid \"Member ID (New)\", a.memberelementtype \"Member Type (New)\", "
                                 + "b.groupelementid \"Group ID (Ref)\", b.groupelementtype \"Group Type (Ref)\", b.memberelementid \"Member ID (Ref)\", b.memberelementtype \"Member Type (Ref)\" "
                                 + "from " + groupRelTableNew + " a full outer join " + groupRelTableRef + " b on (a.groupelementid=b.groupelementid and a.memberelementid=b.memberelementid) "
                                 + "where (a.memberelementid is null and b.memberelementid is not null) or (a.memberelementid is not null and b.memberelementid is null)";

         DataTable groupRelRes = queryMultipleRows(groupRelReport, "Group Membership");
         qResults.Add(groupRelRes);

         return qResults;
      }


      IList<DataTable> DiffAggregation()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string aggrTableNew = "BIMRL_RELAGGREGATION_" + compNewModel.Value.ToString("X4");
         string aggrTableRef = "BIMRL_RELAGGREGATION_" + compRefModel.Value.ToString("X4");

         string aggrReport = "Select a.masterelementid \"Master ID (New)\", a.masterelementtype \"Master Type (New)\", a.aggregateelementid \"Aggre ID (New)\", a.aggregateelementtype \"Aggr Type (New)\", "
                              + "b.masterelementid \"Master ID (Ref)\", b.masterelementtype \"Master Type (Ref)\", b.aggregateelementid \"Aggre ID (Ref)\", b.aggregateelementtype \"Aggr Type (Ref)\" "
                              + "from " + aggrTableNew + " a full outer join " + aggrTableRef + " b on (a.masterelementid=b.masterelementid and a.aggregateelementid=b.aggregateelementid) "
                              + "where (a.aggregateelementid is null and b.aggregateelementid is not null) or (a.aggregateelementid is not null and b.aggregateelementid is null)";

         DataTable aggrRes = queryMultipleRows(aggrReport, "Aggregation Changes");
         qResults.Add(aggrRes);

         return qResults;
      }

      IList<DataTable> DiffConnection()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string connTableNew = "BIMRL_RELCONNECTION_" + compNewModel.Value.ToString("X4");
         string connTableRef = "BIMRL_RELCONNECTION_" + compRefModel.Value.ToString("X4");

         string connReport = "Select a.CONNECTINGELEMENTID \"Connecting Elem ID (New)\", a.CONNECTINGELEMENTTYPE \"Connecting Type (New)\", a.CONNECTEDELEMENTID \"Connected Elem ID(New)\", a.CONNECTEDELEMENTTYPE \"Connected Type (New)\", "
                              + "b.CONNECTINGELEMENTID \"Connecting Elem ID (Ref)\", b.CONNECTINGELEMENTTYPE \"Connecting Type (Ref)\", b.CONNECTEDELEMENTID \"Connected Elem ID(Ref)\", b.CONNECTEDELEMENTTYPE \"Connected Type (Ref)\" "
                              + "from " + connTableNew + " a full outer join " + connTableRef + " b on (a.CONNECTINGELEMENTID=b.CONNECTINGELEMENTID and a.CONNECTEDELEMENTID=b.CONNECTEDELEMENTID) "
                              + "where (a.CONNECTINGELEMENTID is null and b.CONNECTINGELEMENTID is not null) or (a.CONNECTINGELEMENTID is not null and b.CONNECTINGELEMENTID is null) "
                              + "or (a.CONNECTEDELEMENTID is null and b.CONNECTEDELEMENTID is not null) or (a.CONNECTEDELEMENTID is not null and b.CONNECTEDELEMENTID is null)";

         DataTable connRes = queryMultipleRows(connReport, "New and Deleted Connection");
         qResults.Add(connRes);

         string connAttrReport = "Select a.CONNECTINGELEMENTID \"Connecting Elem ID (New)\", a.CONNECTINGELEMENTTYPE \"Connecting Type (New)\", a.CONNECTEDELEMENTID \"Connected Elem ID(New)\", a.CONNECTEDELEMENTTYPE \"Connected Type (New)\", "
                                 + "a.connectingelementattrname \"Connecting Attr Name (New)\", a.connectingelementattrvalue \"Connecting Attr Value (New)\", a.connectedelementattrname \"Connected Attr Name (New)\", a.connectedelementattrvalue \"Connected Attr Value (New)\", "
                                 + "a.connectionattrname \"Connection Attr Name (New)\", a.connectionattrvalue \"Connection Attr Value (New)\", "
                                 + "b.CONNECTINGELEMENTID \"Connecting Elem ID (Ref)\", b.CONNECTINGELEMENTTYPE \"Connecting Type (Ref)\", b.CONNECTEDELEMENTID \"Connected Elem ID(Ref)\", b.CONNECTEDELEMENTTYPE \"Connected Type (Ref)\", "
                                 + "b.connectingelementattrname \"Connecting Attr Name (Ref)\", b.connectingelementattrvalue \"Connecting Attr Value (Ref)\", b.connectedelementattrname \"Connected Attr Name (Ref)\", b.connectedelementattrvalue \"Connected Attr Value (Ref)\", "
                                 + "b.connectionattrname \"Connection Attr Name (Ref)\", b.connectionattrvalue \"Connection Attr Value (Ref)\" from " + connTableNew + " a full outer join "
                                 + connTableRef + " b on (a.CONNECTINGELEMENTID = b.CONNECTINGELEMENTID and a.CONNECTEDELEMENTID = b.CONNECTEDELEMENTID and a.connectingelementattrname = b.connectingelementattrname and a.connectedelementattrname = b.connectedelementattrname) "
                                 + "where a.connectingelementattrvalue != b.connectingelementattrvalue or a.connectedelementattrvalue != b.connectedelementattrvalue or(a.connectingelementattrvalue is null and b.connectingelementattrvalue is not null) or(a.connectedelementattrvalue is not null and b.connectedelementattrvalue is null)";

         DataTable connAttrRes = queryMultipleRows(connAttrReport, "Connection Atrribute Changes");
         qResults.Add(connAttrRes);

         return qResults;
      }

      IList<DataTable> DiffElementDependency()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string dependTableNew = "BIMRL_ELEMENTDEPENDENCY_" + compNewModel.Value.ToString("X4");
         string dependTableRef = "BIMRL_ELEMENTDEPENDENCY_" + compRefModel.Value.ToString("X4");

         string dependReport = "Select a.elementid \"Element ID (New)\", a.elementtype \"Element Type (New)\", a.DEPENDENTELEMENTID \"Dependent Element ID (New)\", a.DEPENDENTELEMENTTYPE \"Dependent Element Type (New)\", a.dependencytype \"Dependency Type (New)\", "
                                 + "b.elementid \"Element ID (Ref)\", b.elementtype \"Element Type (Ref)\", b.DEPENDENTELEMENTID \"Dependent Element ID (Ref)\", b.DEPENDENTELEMENTTYPE \"Dependent Element Type (Ref)\", b.dependencytype \"Dependency Type (Ref)\" "
                                 + "from " + dependTableNew + " a full outer join " + dependTableRef + " b on (a.elementid=b.elementid and a.dependentelementid=b.dependentelementid) "
                                 + "where (a.dependentelementid is null and b.dependentelementid is not null) or (a.dependentelementid is not null and b.dependentelementid is null)";

         DataTable dependRes = queryMultipleRows(dependReport, "Element Dependency Changes");
         qResults.Add(dependRes);

         return qResults;
      }

      IList<DataTable> DiffSpaceBoundary()
      {
         IList<DataTable> qResults = new List<DataTable>();
         string spacebTableNew = "BIMRL_RELSPACEBOUNDARY_" + compNewModel.Value.ToString("X4");
         string spacebTableRef = "BIMRL_RELSPACEBOUNDARY_" + compRefModel.Value.ToString("X4");

         string spacebReport = "select a.spaceelementid \"Space ID (New)\", a.boundaryelementid \"Boundary ID (New)\", a.boundaryelementtype \"Boundary Elem Type (New)\", a.boundarytype \"Boundary Type (New)\", a.internalorexternal \"Internal or External (New)\", "
                                 + "b.spaceelementid \"Space ID (Ref)\", b.boundaryelementid \"Boundary ID (Ref)\", b.boundaryelementtype \"Boundary Elem Type (Ref)\", b.boundarytype \"Boundary Type (Ref)\", b.internalorexternal \"Internal or External (Ref)\" "
                                 + "from " + spacebTableNew + " a full outer join " + spacebTableRef + " b on(a.spaceelementid = b.spaceelementid and a.boundaryelementid = b.boundaryelementid) "
                                 + "where a.boundaryelementid != b.boundaryelementid or (a.boundaryelementid is null and b.boundaryelementid is not null) or (a.boundaryelementid is not null and b.boundaryelementid is null)";

         DataTable spacebRes = queryMultipleRows(spacebReport, "Space Boundary Changes");
         qResults.Add(spacebRes);

         return qResults;
      }

      public DataTable queryMultipleRows(string sqlStmt, string tabName)
      {
         if (String.IsNullOrEmpty(sqlStmt))
            return null;

         DataTable queryDataTableBuffer = new DataTable(tabName);

         OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);

         // TODO!!!!! This one still gives mysterious error if the "alias".* on BIMRLEP$<var> has different column list in previous statement
         // The * seems to "remember" the earlier one. If the number of columns are shorter than the previous one, it will throw OracleException for the "missing"/unrecognized column name
         try
         {
            OracleDataReader reader = command.ExecuteReader();
            queryDataTableBuffer.Load(reader);
         }
         catch (OracleException e)
         {
            string excStr = "%%Error - " + e.Message + "\n" + command.CommandText;
            m_BIMRLCommonRef.StackPushError(excStr);
            if (DBOperation.UIMode)
            {
               BIMRLErrorDialog erroDlg = new BIMRLErrorDialog(m_BIMRLCommonRef);
               erroDlg.ShowDialog();
            }
            else
               Console.Write(m_BIMRLCommonRef.ErrorMessages);
         }

         command.Dispose();
         return queryDataTableBuffer;
      }

      public int runNonQuery(string sqlStmt, bool ignoreError)
      {
         if (String.IsNullOrEmpty(sqlStmt))
            return 0;

         OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
         try
         {
            int status = command.ExecuteNonQuery();
            return status;
         }
         catch (OracleException e)
         {
            if (ignoreError)
            {
               command.Dispose();
               return 0;
            }
            string excStr = "%%Error - " + e.Message + "\n" + command.CommandText;
            m_BIMRLCommonRef.StackPushError(excStr);
            command.Dispose();
            return 0;
         }
      }

      void AddResultDict(string name, IList<DataTable> diffResults)
      {
         foreach(DataTable tab in diffResults)
         {
            diffResultsDict.Add(name + ": " + tab.TableName, tab);
         }
      }
   }
}
