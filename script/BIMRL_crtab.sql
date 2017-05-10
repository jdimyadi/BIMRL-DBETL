CREATE SEQUENCE seq_BIMRL_MODELINFO_&1;

CREATE SEQUENCE seq_BIMRL_PARTGEOMETRY_&1;

CREATE TABLE BIMRL_ELEMENT_&1 (
		  ElementID varchar2(22) NOT NULL, 
		  LineNo number(10),
		  ElementType varchar2(64) NOT NULL, 
		  ModelID number(10) NOT NULL, 
		  TypeID varchar2(22), 
		  Name varchar2(256),
		  LongName varchar2(256),
		  OwnerHistoryID number(10),
		  Description varchar2(256),
		  ObjectType varchar2(256),
		  Tag varchar2(256),
		  Container varchar2(22),
		  GeometryBody SDO_GEOMETRY, 
		  GeometryBody_BBOX SDO_GEOMETRY, 
		  GeometryBody_BBOX_CENTROID SDO_GEOMETRY, 
		  GeometryFootprint SDO_GEOMETRY, 
		  GeometryAxis SDO_GEOMETRY, 
		  TRANSFORM_COL1 SDO_GEOMETRY,
		  TRANSFORM_COL2 SDO_GEOMETRY,
		  TRANSFORM_COL3 SDO_GEOMETRY,
		  TRANSFORM_COL4 SDO_GEOMETRY,
		  BODY_MAJOR_AXIS1 SDO_GEOMETRY,
		  BODY_MAJOR_AXIS2 SDO_GEOMETRY,
		  BODY_MAJOR_AXIS3 SDO_GEOMETRY,
		  BODY_MAJOR_AXIS_Centroid SDO_GEOMETRY,
		  OBB SDO_GEOMETRY,
		  TOTAL_SURFACE_AREA NUMBER,
		  PRIMARY KEY (ElementID)
);

CREATE OR REPLACE VIEW BIMRL_ELEMWOGEOM_&1 AS SELECT ElementID,LineNo,ElementType,ModelID,TypeID,Name,LongName,OwnerHistoryID,Description,ObjectType,Tag,Container,GeometryBody_BBOX,GeometryBody_BBOX_CENTROID from BIMRL_ELEMENT_&1;

CREATE TABLE BIMRL_TOPO_FACE_&1 (
		  ElementID varchar2(22) NOT NULL,
		  ID	varchar2(22) NOT NULL,
		  TYPE varchar2(8) NOT NULL,
		  Polygon SDO_GEOMETRY NOT NULL,
		  Normal SDO_GEOMETRY NOT NULL,
		  ANGLEFROMNORTH	NUMBER,
		  Centroid SDO_GEOMETRY NOT NULL,
		  Orientation varchar2(16),
		  Attribute varchar2(128),
		  TopOrBottom_Z NUMBER
);

CREATE OR REPLACE VIEW BIMRL_TOPOFACEV_&1 AS SELECT A.ELEMENTID,A.ID,A.TYPE,A.POLYGON,A.NORMAL,A.ANGLEFROMNORTH,A.CENTROID,A.ORIENTATION,A.ATTRIBUTE,B.ELEMENTTYPE FROM BIMRL_TOPO_FACE_&1 A, BIMRL_ELEMENT_&1 B WHERE A.ELEMENTID=B.ELEMENTID;

CREATE TABLE BIMRL_OWNERHISTORY_&1 (
		  ID	number(10),
		  ModelID number(10),
		  OwningPersonName	varchar2(256),
		  OwningPersonRoles	varchar2(256),
		  OwningPersonAddresses varchar2(1024),
		  OwningOrganizationId	varchar2(256),
		  OwningOrganizationName	varchar2(256),
		  OwningOrganizationDescription varchar2(256),
		  OwningOrganizationRoles varchar2(256),
		  OwningOrganizationAddresses varchar2(1024),
		  ApplicationName varchar2(256),
		  ApplicationVersion varchar2(256),
		  ApplicationDeveloper varchar2(256),
		  ApplicationID varchar2(256),
		  State varchar2(16),
		  ChangeAction varchar2(16),
		  LastModifiedDate date,
		  LastModifyingUserID varchar2(256),
		  LastModifyingApplicationID varchar2(256),
		  CreationDate	date,
		  PRIMARY KEY (ModelID, ID)
);

CREATE TABLE BIMRL_SPATIALSTRUCTURE_&1 (
		  SpatialElementID varchar2(22) NOT NULL, 
        SpatialElementType varchar2(64) NOT NULL,
		  ParentID varchar2(22),
		  ParentType varchar2(64),
		  LevelRemoved number(10) NOT NULL
);

CREATE TABLE BIMRL_MODELINFO_&1 (
		  ModelID number(10) NOT NULL, 
		  ModelName varchar2(256) NOT NULL, 
		  Source varchar2(256) NOT NULL, 
		  Location POINT3D, 
		  Transformation MATRIX3D, 
		  Scale POINT3D, 
		  PRIMARY KEY (ModelID)
);

CREATE TABLE BIMRL_RELCONNECTION_&1 (
		  ConnectingElementId varchar2(22) NOT NULL, 
		  ConnectingElementType varchar2(64) NOT NULL, 
		  ConnectingElementAttrName varchar2(128), 
		  ConnectingElementAttrValue varchar2(256), 
		  ConnectedElementId varchar2(22) NOT NULL, 
		  ConnectedElementType varchar2(64) NOT NULL, 
		  ConnectedElementAttrName varchar2(128), 
		  ConnectedElementAttrValue varchar2(256), 
		  ConnectionAttrName varchar2(128), 
		  ConnectionAttrValue varchar2(256), 
		  RealizingElementId varchar2(22),
		  RealizingElementType varchar2(64),
		  RelationshipType varchar2(64) NOT NULL 
);

CREATE TABLE BIMRL_PARTGEOMETRY_&1 (
		  ElementID varchar2(22) NOT NULL, 
		  PartID number(10) NOT NULL, 
		  PartName varchar2(256), 
		  GeometryBody SDO_GEOMETRY, 
		  GeometryFootprint SDO_GEOMETRY, 
		  GeometryAxis SDO_GEOMETRY, 
		  PRIMARY KEY (PartID)
);

CREATE TABLE BIMRL_ELEMENTPROPERTIES_&1 (
		  ElementID varchar2(22) NOT NULL, 
		  PropertyGroupName varchar2(256) NOT NULL, 
		  PropertyName varchar2(256) NOT NULL, 
		  PropertyValue varchar2(1024), 
		  PropertyDataType varchar2(128), 
		  PropertyUnit varchar2(64)
/*,
		  PRIMARY KEY (ElementID, PropertyGroupName, PropertyName)
*/
);

CREATE TABLE BIMRL_TYPEMATERIAL_&1 (
		  ElementID varchar2(22) NOT NULL, 
		  MaterialName varchar2(256) NOT NULL, 
		  Category varchar2(256), 
		  SetName varchar2(256), 
		  MaterialSequence number(10), 
		  MaterialThickness number(10), 
		  IsVentilated varchar2(16),
		  ForProfile varchar2(256)
);

CREATE TABLE BIMRL_ELEMENTMATERIAL_&1 (
		  ElementId varchar2(22) NOT NULL, 
		  MaterialName varchar2(256) NOT NULL, 
		  Category varchar2(256), 
		  SetName varchar2(256),
		  MaterialSequence number(10), 
	     MaterialThickness number(10), 
		  IsVentilated varchar2(16),
		  ForProfile varchar2(256)
);

CREATE TABLE BIMRL_ELEMCLASSIFICATION_&1 (
		  ElementID varchar2(22) NOT NULL,
		  ClassificationName varchar2(256) NOT NULL,
		  ClassificationItemCode varchar2(256) NOT NULL
);

CREATE TABLE BIMRL_CLASSIFICATION_&1 (
		  ClassificationName varchar2(256) NOT NULL, 
		  ClassificationSource varchar2(256), 
		  ClassificationEdition varchar2(256), 
		  ClassificationEditionDate date, 
		  ClassificationItemCode varchar2(256) NOT NULL, 
		  ClassificationItemName varchar2(256), 
		  ClassificationItemLocation varchar2(256), 
		  PRIMARY KEY (ClassificationName, ClassificationItemCode)
);

CREATE TABLE BIMRL_TYPE_&1 (
		  ElementID varchar2(22) NOT NULL,
		  IfcType varchar2(64) NOT NULL,
		  Name varchar2(256) NOT NULL, 
		  Description varchar2(256),
 		  OwnerHistoryID number(10),
		  ModelID number(10),
		  ApplicableOccurrence	varchar2(256),
		  Tag varchar2(256),
		  ElementType varchar2(256),
		  PredefinedType varchar2(256),	
		  AssemblyPlace varchar2(256),	
		  OperationType varchar2(256),	
		  ConstructionType varchar2(256),	
		  PRIMARY KEY (ElementID)
);

CREATE TABLE BIMRL_TYPCLASSIFICATION_&1 (
		  ElementID varchar2(22) NOT NULL,
		  ClassificationName varchar2(256) NOT NULL,
		  ClassificationItemCode varchar2(256) NOT NULL
);

CREATE or REPLACE VIEW BIMRL_CLASSIFASSIGNMENT_&1 (
		  ElementID, 
		  ClassificationName, 
		  ClassificationItemCode, 
		  ClassificationItemName, 
		  ClassificationItemLocation, 
		  ClassificationSource, 
		  ClassificationEdition, 
		  FromType) as 
(select a.elementid, a.classificationname, a.classificationItemCode, b.classificationItemname, 
		  b.ClassificationItemLocation, b.classificationsource, b.classificationedition, 'FALSE'
		from bimrl_elemclassification_&1 a, bimrl_classification_&1 b 
			where b.classificationname=a.classificationname and b.classificationItemCode=a.classificationItemCode)
union
(select E.elementid, a.classificationname, a.classificationItemCode, b.classificationItemname, 
		  b.ClassificationItemLocation, b.classificationsource, b.classificationedition, 'TRUE'
  		from bimrl_typclassification_&1 a, bimrl_classification_&1 b, bimrl_element_&1 e 
			where b.classificationname=a.classificationname and b.classificationItemCode=a.classificationItemCode
			and a.elementid=e.typeid)
;

CREATE TABLE BIMRL_SPATIALINDEX_&1 (
		  ElementID varchar2(22) NOT NULL, 
		  CellID varchar2(12) NOT NULL, 
		  XMINBOUND	integer,
		  YMINBOUND	integer,
		  ZMINBOUND integer,
		  XMAXBOUND integer,
		  YMAXBOUND integer,
		  ZMAXBOUND integer,
		  DEPTH integer,
		  PRIMARY KEY (ElementID, CellID)
);

CREATE TABLE BIMRL_TYPEPROPERTIES_&1 (
		  ElementID varchar2(22) NOT NULL, 
		  PropertyGroupName varchar2(256) NOT NULL, 
		  PropertyName varchar2(256) NOT NULL, 
		  PropertyValue varchar2(1024), 
		  PropertyDataType varchar2(128),
		  PropertyUnit Varchar2(64) 
/*,
		  PRIMARY KEY (ElementID, PropertyGroupName, PropertyName)
*/
);

Create or replace View BIMRL_PROPERTIES_&1 (ElementID, PropertyGroupName, PropertyName, PropertyValue, PropertyDataType, PropertyUnit, fromType) AS
	(SELECT ElementID, PropertyGroupName, PropertyName, PropertyValue, PropertyDataType, PropertyUnit, 'FALSE'
		  from BIMRL_ELEMENTPROPERTIES_&1)
UNION
	(SELECT A.ElementID, B.PropertyGroupName, B.PropertyName, B.PropertyValue, B.PropertyDataType, B.PropertyUnit, 'TRUE'
		  from BIMRL_ELEMENT_&1 A, BIMRL_TYPEPROPERTIES_&1 B WHERE B.ELEMENTID=A.TYPEID);

CREATE TABLE BIMRL_RELAGGREGATION_&1 (
		  MasterElementID varchar2(22) NOT NULL, 
		  MasterElementType varchar2(64) NOT NULL, 
		  AggregateElementID varchar2(22) NOT NULL, 
		  AggregateElementType varchar2(64) NOT NULL, 
		  PRIMARY KEY (MasterElementID, AggregateElementID)
);

CREATE TABLE BIMRL_RELSPACEBOUNDARY_&1 (
		  SpaceElementID varchar2(22) NOT NULL, 
		  BoundaryElementID varchar2(22) NOT NULL, 
		  BoundaryElementType varchar2(64) NOT NULL, 
		  BoundaryType varchar2(32), 
		  InternalOrExternal varchar2(32),
		  Primary Key (SpaceElementID, BoundaryElementID)
);

CREATE TABLE BIMRL_RELSPACEB_DETAIL_&1 (
		  SpaceElementID varchar2(22) NOT NULL,
		  SFACEBOUNDID varchar2(22) NOT NULL,
		  COMMONPOINTATS SDO_GEOMETRY,
		  BoundaryElementID varchar2(22) NOT NULL,
		  BFACEBOUNDID varchar2(22) NOT NULL,
		  COMMONPOINTATB SDO_GEOMETRY,
		  SFACEPOLYGON	SDO_GEOMETRY NOT NULL,
		  SFACENORMAL SDO_GEOMETRY NOT NULL,
		  SFACEANGLEFROMNORTH NUMBER,
		  SFACECENTROID SDO_GEOMETRY NOT NULL,
		  BFACEPOLYGON SDO_GEOMETRY NOT NULL,
		  BFACENORMAL SDO_GEOMETRY NOT NULL,
		  BFACEANGLEFROMNORTH NUMBER,
		  BFACECENTROID SDO_GEOMETRY NOT NULL,
		  Primary Key (SpaceElementID, SFaceBoundID, BoundaryElementID, BFaceBoundID)
);

CREATE OR REPLACE VIEW BIMRL_SPACEBOUNDARYV_&1 AS SELECT * FROM BIMRL_RELSPACEBOUNDARY_&1 FULL JOIN 
   (SELECT A.*, B.ELEMENTTYPE BOUNDARYELEMENTTYPE FROM BIMRL_RELSPACEB_DETAIL_&1 A, BIMRL_ELEMENT_&1 B 
	WHERE A.BOUNDARYELEMENTID=B.ELEMENTID) USING (SPACEELEMENTID, BOUNDARYELEMENTID, BOUNDARYELEMENTTYPE);

CREATE TABLE BIMRL_RELGROUP_&1 (
		  GroupElementID varchar2(22) NOT NULL, 
		  GroupElementType varchar2(64) NOT NULL, 
		  MemberElementID varchar2(22) NOT NULL, 
		  MemberElementType varchar2(64) NOT NULL, 
		  PRIMARY KEY (GroupElementID, MemberElementID)
);

CREATE TABLE BIMRL_ELEMENTDEPENDENCY_&1 (
		  ElementID varchar2(22) NOT NULL, 
		  ElementType varchar2(64) NOT NULL, 
		  DependentElementID varchar2(22) NOT NULL,
		  DependentElementType varchar2(64) NOT NULL,
		  DependencyType varchar2(32) NOT NULL,
		  PRIMARY KEY (ElementID, DependentElementID)
);

CREATE INDEX Idx_elementtype_&1 on BIMRL_ELEMENT_&1 (elementtype);

CREATE INDEX Idx_topoFEID_&1 on BIMRL_TOPO_FACE_&1 (elementid);

CREATE INDEX BIMRL_ConnectingElement_&1 ON BIMRL_RELCONNECTION_&1 (ConnectingElementID);

CREATE INDEX BIMRL_ConnectedElement_&1 ON BIMRL_RELCONNECTION_&1 (ConnectedElementID);

CREATE INDEX IDX_TYPMATERIAL_ID_&1 ON BIMRL_TYPEMATERIAL_&1 (ElementID);

CREATE INDEX IDX_ELEMMATERIAL_ID_&1 ON BIMRL_ELEMENTMATERIAL_&1 (ElementID);

CREATE INDEX IDX_SPATIAL_CELLID_&1 ON BIMRL_SPATIALINDEX_&1 (CELLID);

CREATE INDEX IXMINB_SPATIALINDEX_&1 ON BIMRL_SPATIALINDEX_&1 (XMINBOUND);
CREATE INDEX IYMINB_SPATIALINDEX_&1 ON BIMRL_SPATIALINDEX_&1 (YMINBOUND);
CREATE INDEX IZMINB_SPATIALINDEX_&1 ON BIMRL_SPATIALINDEX_&1 (ZMINBOUND);
CREATE INDEX IXMAXB_SPATIALINDEX_&1 ON BIMRL_SPATIALINDEX_&1 (XMAXBOUND);
CREATE INDEX IYMAXB_SPATIALINDEX_&1 ON BIMRL_SPATIALINDEX_&1 (YMAXBOUND);
CREATE INDEX IZMAXB_SPATIALINDEX_&1 ON BIMRL_SPATIALINDEX_&1 (ZMAXBOUND);

ALTER TABLE BIMRL_ELEMENT_&1 ADD CONSTRAINT FK_MModelID_&1 FOREIGN KEY (ModelID) REFERENCES BIMRL_MODELINFO_&1 (ModelID);

ALTER TABLE BIMRL_ELEMENT_&1 ADD CONSTRAINT FK_TypeID_&1 FOREIGN KEY (TypeID) REFERENCES BIMRL_TYPE_&1 (ElementID);
ALTER TABLE BIMRL_ELEMENT_&1 ADD CONSTRAINT FK_OwnerHistID_&1 FOREIGN KEY (ModelID, OwnerHistoryID) REFERENCES BIMRL_OWNERHISTORY_&1 (ModelID, ID);
ALTER TABLE BIMRL_TYPE_&1 ADD CONSTRAINT FK_TOwnerHistID_&1 FOREIGN KEY (ModelID, OwnerHistoryID) REFERENCES BIMRL_OWNERHISTORY_&1 (ModelID, ID);

/*
ALTER TABLE BIMRL_SPATIALINDEX_&1 ADD CONSTRAINT FK_SPA_ELEMENTID_&1 FOREIGN KEY (ElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
*/

ALTER TABLE BIMRL_PARTGEOMETRY_&1 ADD CONSTRAINT FK_PART_ELEMENTID_&1 FOREIGN KEY (ElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);

ALTER TABLE BIMRL_TYPEPROPERTIES_&1 ADD CONSTRAINT FK_TYPEPROP_ID_&1 FOREIGN KEY (ElementID) REFERENCES BIMRL_TYPE_&1 (ElementID);

ALTER TABLE BIMRL_ELEMENTPROPERTIES_&1 ADD CONSTRAINT FK_ELEMPROP_ID_&1 FOREIGN KEY (ElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);

ALTER TABLE BIMRL_RELCONNECTION_&1 ADD CONSTRAINT FK_connecting_&1 FOREIGN KEY (ConnectingElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
ALTER TABLE BIMRL_RELCONNECTION_&1 ADD CONSTRAINT FK_connected_&1 FOREIGN KEY (ConnectedElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
ALTER TABLE BIMRL_RELCONNECTION_&1 ADD CONSTRAINT FK_realizing_&1 FOREIGN KEY (RealizingElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);

ALTER TABLE BIMRL_ELEMCLASSIFICATION_&1 ADD CONSTRAINT FK_CLASSIFELEMID_&1 FOREIGN KEY (ELEMENTID) REFERENCES BIMRL_ELEMENT_&1 (ELEMENTID);
ALTER TABLE BIMRL_ELEMCLASSIFICATION_&1 ADD CONSTRAINT FK_CLASSIFCODE_&1 FOREIGN KEY (ClassificationName, ClassificationItemCode) REFERENCES BIMRL_CLASSIFICATION_&1 (ClassificationName, ClassificationItemCode);
ALTER TABLE BIMRL_TYPCLASSIFICATION_&1 ADD CONSTRAINT FK_CLASSIFTELEMID_&1 FOREIGN KEY (ELEMENTID) REFERENCES BIMRL_TYPE_&1 (ELEMENTID);
ALTER TABLE BIMRL_TYPCLASSIFICATION_&1 ADD CONSTRAINT FK_CLASSIFTCODE_&1 FOREIGN KEY (ClassificationName, ClassificationItemCode) REFERENCES BIMRL_CLASSIFICATION_&1 (ClassificationName, ClassificationItemCode);

ALTER TABLE BIMRL_ELEMENTMATERIAL_&1 ADD CONSTRAINT FK_MATERIAL_EID_&1 FOREIGN KEY (ElementId) REFERENCES BIMRL_ELEMENT_&1 (ElementID);

ALTER TABLE BIMRL_TYPEMATERIAL_&1 ADD CONSTRAINT FK_TYPEMATERIAL_TID_&1 FOREIGN KEY (ElementID) REFERENCES BIMRL_TYPE_&1 (ElementID);

ALTER TABLE BIMRL_RELAGGREGATION_&1 ADD CONSTRAINT FK_Master_&1 FOREIGN KEY (MasterElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
ALTER TABLE BIMRL_RELAGGREGATION_&1 ADD CONSTRAINT FK_Aggregate_&1 FOREIGN KEY (AggregateElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);

ALTER TABLE BIMRL_RELSPACEBOUNDARY_&1 ADD CONSTRAINT FK_Space_&1 FOREIGN KEY (SpaceElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
ALTER TABLE BIMRL_RELSPACEBOUNDARY_&1 ADD CONSTRAINT FK_Boundaries_&1 FOREIGN KEY (BoundaryElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
ALTER TABLE BIMRL_RELSPACEB_DETAIL_&1 ADD CONSTRAINT FK_Space_det_&1 FOREIGN KEY (SpaceElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
ALTER TABLE BIMRL_RELSPACEB_DETAIL_&1 ADD CONSTRAINT FK_Boundaries_det_&1 FOREIGN KEY (BoundaryElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);

ALTER TABLE BIMRL_SPATIALSTRUCTURE_&1 ADD CONSTRAINT FK_Parent_&1 FOREIGN KEY (ParentID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
ALTER TABLE BIMRL_SPATIALSTRUCTURE_&1 ADD CONSTRAINT FK_SpatialId_&1 FOREIGN KEY (SpatialElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);

ALTER TABLE BIMRL_RELGROUP_&1 ADD CONSTRAINT FK_Group_&1 FOREIGN KEY (GroupElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
ALTER TABLE BIMRL_RELGROUP_&1 ADD CONSTRAINT FK_Member_&1 FOREIGN KEY (MemberElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);

ALTER TABLE BIMRL_ELEMENTDEPENDENCY_&1 ADD CONSTRAINT FK_Dependency_&1 FOREIGN KEY (ElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
ALTER TABLE BIMRL_ELEMENTDEPENDENCY_&1 ADD CONSTRAINT FK_DEPENDENCY_EID_&1 FOREIGN KEY (DependentElementID) REFERENCES BIMRL_ELEMENT_&1 (ElementID);
