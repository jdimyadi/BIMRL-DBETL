CREATE SEQUENCE seq_BIMRL_MODELINFO;

CREATE SEQUENCE seq_BIMRL_PARTGEOMETRY;

CREATE TABLE BIMRL_ELEMENT (
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
		  TRANSFORM_X_AXIS SDO_GEOMETRY,
		  TRANSFORM_Y_AXIS SDO_GEOMETRY,
		  TRANSFORM_Z_AXIS SDO_GEOMETRY,
		  BODY_MAJOR_AXIS1 SDO_GEOMETRY,
		  BODY_MAJOR_AXIS2 SDO_GEOMETRY,
		  BODY_MAJOR_AXIS3 SDO_GEOMETRY,
		  BODY_MAJOR_AXIS_Centroid SDO_GEOMETRY,
		  OBB SDO_GEOMETRY,
		  PRIMARY KEY (ElementID)
);

CREATE OR REPLACE VIEW BIMRL_ELEMWOGEOM AS SELECT ElementID,LineNo,ElementType,ModelID,TypeID,Name,LongName,OwnerHistoryID,Description,ObjectType,Tag,Container,GeometryBody_BBOX,GeometryBody_BBOX_CENTROID from BIMRL_ELEMENT;

CREATE TABLE BIMRL_TOPO_FACE (
		  ElementID varchar2(22) NOT NULL,
		  ID	varchar2(22) NOT NULL,
		  TYPE varchar2(8) NOT NULL,
		  Polygon SDO_GEOMETRY NOT NULL,
		  Normal SDO_GEOMETRY NOT NULL,
		  ANGLEFROMNORTH	NUMBER,
		  Centroid SDO_GEOMETRY NOT NULL,
		  Orientation varchar2(16),
		  Attribute varchar2(128)
);

CREATE OR REPLACE VIEW BIMRL_TOPOFACEV AS SELECT A.ELEMENTID,A.ID,A.TYPE,A.POLYGON,A.NORMAL,A.ANGLEFROMNORTH,A.CENTROID,A.ORIENTATION,A.ATTRIBUTE,B.ELEMENTTYPE FROM BIMRL_TOPO_FACE A, BIMRL_ELEMENT B WHERE A.ELEMENTID=B.ELEMENTID;

CREATE TABLE BIMRL_OWNERHISTORY (
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

CREATE TABLE BIMRL_SPATIALSTRUCTURE (
		  SpatialElementID varchar2(22) NOT NULL, 
        SpatialElementType varchar2(64) NOT NULL,
		  ParentID varchar2(22),
		  ParentType varchar2(64),
		  LevelRemoved number(10) NOT NULL
);

CREATE TABLE BIMRL_MODELINFO (
		  ModelID number(10) NOT NULL, 
		  ModelName varchar2(256) NOT NULL, 
		  Source varchar2(256) NOT NULL, 
		  Location POINT3D, 
		  Transformation MATRIX3D, 
		  Scale POINT3D, 
		  PRIMARY KEY (ModelID)
);

CREATE TABLE BIMRL_RELCONNECTION (
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

CREATE TABLE BIMRL_PARTGEOMETRY (
		  ElementID varchar2(22) NOT NULL, 
		  PartID number(10) NOT NULL, 
		  PartName varchar2(256), 
		  GeometryBody SDO_GEOMETRY, 
		  GeometryFootprint SDO_GEOMETRY, 
		  GeometryAxis SDO_GEOMETRY, 
		  PRIMARY KEY (PartID)
);

CREATE TABLE BIMRL_ELEMENTPROPERTIES (
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

CREATE TABLE BIMRL_TYPEMATERIAL (
		  ElementID varchar2(22) NOT NULL, 
		  MaterialName varchar2(256) NOT NULL, 
		  Category varchar2(256), 
		  SetName varchar2(256), 
		  MaterialSequence number(10), 
		  MaterialThickness number(10), 
		  IsVentilated varchar2(16),
		  ForProfile varchar2(256)
);

CREATE TABLE BIMRL_ELEMENTMATERIAL (
		  ElementId varchar2(22) NOT NULL, 
		  MaterialName varchar2(256) NOT NULL, 
		  Category varchar2(256), 
		  SetName varchar2(256),
		  MaterialSequence number(10), 
	     MaterialThickness number(10), 
		  IsVentilated varchar2(16),
		  ForProfile varchar2(256)
);

CREATE TABLE BIMRL_ELEMCLASSIFICATION (
		  ElementID varchar2(22) NOT NULL,
		  ClassificationName varchar2(256) NOT NULL,
		  ClassificationItemCode varchar2(256) NOT NULL
);

CREATE TABLE BIMRL_CLASSIFICATION (
		  ClassificationName varchar2(256) NOT NULL, 
		  ClassificationSource varchar2(256), 
		  ClassificationEdition varchar2(256), 
		  ClassificationEditionDate date, 
		  ClassificationItemCode varchar2(256) NOT NULL, 
		  ClassificationItemName varchar2(256), 
		  ClassificationItemLocation varchar2(256), 
		  PRIMARY KEY (ClassificationName, ClassificationItemCode)
);

CREATE TABLE BIMRL_TYPE (
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

CREATE TABLE BIMRL_TYPCLASSIFICATION (
		  ElementID varchar2(22) NOT NULL,
		  ClassificationName varchar2(256) NOT NULL,
		  ClassificationItemCode varchar2(256) NOT NULL
);

CREATE or REPLACE VIEW BIMRL_CLASSIFASSIGNMENT (
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
		from bimrl_elemclassification a, bimrl_classification b 
			where b.classificationname=a.classificationname and b.classificationItemCode=a.classificationItemCode)
union
(select E.elementid, a.classificationname, a.classificationItemCode, b.classificationItemname, 
		  b.ClassificationItemLocation, b.classificationsource, b.classificationedition, 'TRUE'
  		from bimrl_typclassification a, bimrl_classification b, bimrl_element e 
			where b.classificationname=a.classificationname and b.classificationItemCode=a.classificationItemCode
			and a.elementid=e.typeid)
;

CREATE TABLE BIMRL_SPATIALINDEX (
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

CREATE TABLE BIMRL_TYPEPROPERTIES (
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

Create or replace View BIMRL_PROPERTIES (ElementID, PropertyGroupName, PropertyName, PropertyValue, PropertyDataType, PropertyUnit, fromType) AS
	(SELECT ElementID, PropertyGroupName, PropertyName, PropertyValue, PropertyDataType, PropertyUnit, 'FALSE'
		  from BIMRL_ELEMENTPROPERTIES)
UNION
	(SELECT A.ElementID, B.PropertyGroupName, B.PropertyName, B.PropertyValue, B.PropertyDataType, B.PropertyUnit, 'TRUE'
		  from BIMRL_ELEMENT A, BIMRL_TYPEPROPERTIES B WHERE B.ELEMENTID=A.TYPEID);

CREATE TABLE BIMRL_RELAGGREGATION (
		  MasterElementID varchar2(22) NOT NULL, 
		  MasterElementType varchar2(64) NOT NULL, 
		  AggregateElementID varchar2(22) NOT NULL, 
		  AggregateElementType varchar2(64) NOT NULL, 
		  PRIMARY KEY (MasterElementID, AggregateElementID)
);

CREATE TABLE BIMRL_RELSPACEBOUNDARY (
		  SpaceElementID varchar2(22) NOT NULL, 
		  BoundaryElementID varchar2(22) NOT NULL, 
		  BoundaryElementType varchar2(64) NOT NULL, 
		  BoundaryType varchar2(32), 
		  InternalOrExternal varchar2(32),
		  Primary Key (SpaceElementID, BoundaryElementID)
);

CREATE TABLE BIMRL_RELSPACEB_DETAIL (
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

CREATE OR REPLACE VIEW BIMRL_SPACEBOUNDARYV AS SELECT * FROM BIMRL_RELSPACEBOUNDARY FULL JOIN BIMRL_RELSPACEB_DETAIL 
	USING (SPACEELEMENTID, BOUNDARYELEMENTID);

CREATE TABLE BIMRL_RELGROUP (
		  GroupElementID varchar2(22) NOT NULL, 
		  GroupElementType varchar2(64) NOT NULL, 
		  MemberElementID varchar2(22) NOT NULL, 
		  MemberElementType varchar2(64) NOT NULL, 
		  PRIMARY KEY (GroupElementID, MemberElementID)
);

CREATE TABLE BIMRL_ELEMENTDEPENDENCY (
		  ElementID varchar2(22) NOT NULL, 
		  ElementType varchar2(64) NOT NULL, 
		  DependentElementID varchar2(22) NOT NULL,
		  DependentElementType varchar2(64) NOT NULL,
		  DependencyType varchar2(32) NOT NULL,
		  PRIMARY KEY (ElementID, DependentElementID)
);

CREATE INDEX Idx_elementtype on BIMRL_ELEMENT (elementtype);

CREATE INDEX Idx_topoFEID on BIMRL_TOPO_FACE (elementid);

CREATE INDEX BIMRL_ConnectingElement ON BIMRL_RELCONNECTION (ConnectingElementID);

CREATE INDEX BIMRL_ConnectedElement ON BIMRL_RELCONNECTION (ConnectedElementID);

CREATE INDEX IDX_TYPMATERIAL_ID ON BIMRL_TYPEMATERIAL (ElementID);

CREATE INDEX IDX_ELEMMATERIAL_ID ON BIMRL_ELEMENTMATERIAL (ElementID);

CREATE INDEX IDX_SPATIAL_CELLID ON BIMRL_SPATIALINDEX (CELLID);

CREATE INDEX IXMINB_SPATIALINDEX ON BIMRL_SPATIALINDEX (XMINBOUND);
CREATE INDEX IYMINB_SPATIALINDEX ON BIMRL_SPATIALINDEX (YMINBOUND);
CREATE INDEX IZMINB_SPATIALINDEX ON BIMRL_SPATIALINDEX (ZMINBOUND);
CREATE INDEX IXMAXB_SPATIALINDEX ON BIMRL_SPATIALINDEX (XMAXBOUND);
CREATE INDEX IYMAXB_SPATIALINDEX ON BIMRL_SPATIALINDEX (YMAXBOUND);
CREATE INDEX IZMAXB_SPATIALINDEX ON BIMRL_SPATIALINDEX (ZMAXBOUND);

ALTER TABLE BIMRL_ELEMENT ADD CONSTRAINT FK_MModelID FOREIGN KEY (ModelID) REFERENCES BIMRL_MODELINFO (ModelID);

ALTER TABLE BIMRL_ELEMENT ADD CONSTRAINT FK_TypeID FOREIGN KEY (TypeID) REFERENCES BIMRL_TYPE (ElementID);
ALTER TABLE BIMRL_ELEMENT ADD CONSTRAINT FK_OwnerHistID FOREIGN KEY (ModelID, OwnerHistoryID) REFERENCES BIMRL_OWNERHISTORY (ModelID, ID);
ALTER TABLE BIMRL_TYPE ADD CONSTRAINT FK_TOwnerHistID FOREIGN KEY (ModelID, OwnerHistoryID) REFERENCES BIMRL_OWNERHISTORY (ModelID, ID);

/*
ALTER TABLE BIMRL_SPATIALINDEX ADD CONSTRAINT FK_SPA_ELEMENTID FOREIGN KEY (ElementID) REFERENCES BIMRL_ELEMENT (ElementID);
*/

ALTER TABLE BIMRL_PARTGEOMETRY ADD CONSTRAINT FK_PART_ELEMENTID FOREIGN KEY (ElementID) REFERENCES BIMRL_ELEMENT (ElementID);

ALTER TABLE BIMRL_TYPEPROPERTIES ADD CONSTRAINT FK_TYPEPROP_ID FOREIGN KEY (ElementID) REFERENCES BIMRL_TYPE (ElementID);

ALTER TABLE BIMRL_ELEMENTPROPERTIES ADD CONSTRAINT FK_ELEMPROP_ID FOREIGN KEY (ElementID) REFERENCES BIMRL_ELEMENT (ElementID);

ALTER TABLE BIMRL_RELCONNECTION ADD CONSTRAINT FK_connecting FOREIGN KEY (ConnectingElementID) REFERENCES BIMRL_ELEMENT (ElementID);
ALTER TABLE BIMRL_RELCONNECTION ADD CONSTRAINT FK_connected FOREIGN KEY (ConnectedElementID) REFERENCES BIMRL_ELEMENT (ElementID);
ALTER TABLE BIMRL_RELCONNECTION ADD CONSTRAINT FK_realizing FOREIGN KEY (RealizingElementID) REFERENCES BIMRL_ELEMENT (ElementID);

ALTER TABLE BIMRL_ELEMCLASSIFICATION ADD CONSTRAINT FK_CLASSIFELEMID FOREIGN KEY (ELEMENTID) REFERENCES BIMRL_ELEMENT (ELEMENTID);
ALTER TABLE BIMRL_ELEMCLASSIFICATION ADD CONSTRAINT FK_CLASSIFCODE FOREIGN KEY (ClassificationName, ClassificationItemCode) REFERENCES BIMRL_CLASSIFICATION (ClassificationName, ClassificationItemCode);
ALTER TABLE BIMRL_TYPCLASSIFICATION ADD CONSTRAINT FK_CLASSIFTELEMID FOREIGN KEY (ELEMENTID) REFERENCES BIMRL_TYPE (ELEMENTID);
ALTER TABLE BIMRL_TYPCLASSIFICATION ADD CONSTRAINT FK_CLASSIFTCODE FOREIGN KEY (ClassificationName, ClassificationItemCode) REFERENCES BIMRL_CLASSIFICATION (ClassificationName, ClassificationItemCode);

ALTER TABLE BIMRL_ELEMENTMATERIAL ADD CONSTRAINT FK_MATERIAL_EID FOREIGN KEY (ElementId) REFERENCES BIMRL_ELEMENT (ElementID);

ALTER TABLE BIMRL_TYPEMATERIAL ADD CONSTRAINT FK_TYPEMATERIAL_TID FOREIGN KEY (ElementID) REFERENCES BIMRL_TYPE (ElementID);

ALTER TABLE BIMRL_RELAGGREGATION ADD CONSTRAINT FK_Master FOREIGN KEY (MasterElementID) REFERENCES BIMRL_ELEMENT (ElementID);
ALTER TABLE BIMRL_RELAGGREGATION ADD CONSTRAINT FK_Aggregate FOREIGN KEY (AggregateElementID) REFERENCES BIMRL_ELEMENT (ElementID);

ALTER TABLE BIMRL_RELSPACEBOUNDARY ADD CONSTRAINT FK_Space FOREIGN KEY (SpaceElementID) REFERENCES BIMRL_ELEMENT (ElementID);
ALTER TABLE BIMRL_RELSPACEBOUNDARY ADD CONSTRAINT FK_Boundaries FOREIGN KEY (BoundaryElementID) REFERENCES BIMRL_ELEMENT (ElementID);
ALTER TABLE BIMRL_RELSPACEB_DETAIL ADD CONSTRAINT FK_Space_det FOREIGN KEY (SpaceElementID) REFERENCES BIMRL_ELEMENT (ElementID);
ALTER TABLE BIMRL_RELSPACEB_DETAIL ADD CONSTRAINT FK_Boundaries_det FOREIGN KEY (BoundaryElementID) REFERENCES BIMRL_ELEMENT (ElementID);

ALTER TABLE BIMRL_SPATIALSTRUCTURE ADD CONSTRAINT FK_Parent FOREIGN KEY (ParentID) REFERENCES BIMRL_ELEMENT (ElementID);
ALTER TABLE BIMRL_SPATIALSTRUCTURE ADD CONSTRAINT FK_SpatialId FOREIGN KEY (SpatialElementID) REFERENCES BIMRL_ELEMENT (ElementID);

ALTER TABLE BIMRL_RELGROUP ADD CONSTRAINT FK_Group FOREIGN KEY (GroupElementID) REFERENCES BIMRL_ELEMENT (ElementID);
ALTER TABLE BIMRL_RELGROUP ADD CONSTRAINT FK_Member FOREIGN KEY (MemberElementID) REFERENCES BIMRL_ELEMENT (ElementID);

ALTER TABLE BIMRL_ELEMENTDEPENDENCY ADD CONSTRAINT FK_Dependency FOREIGN KEY (ElementID) REFERENCES BIMRL_ELEMENT (ElementID);
ALTER TABLE BIMRL_ELEMENTDEPENDENCY ADD CONSTRAINT FK_DEPENDENCY_EID FOREIGN KEY (DependentElementID) REFERENCES BIMRL_ELEMENT (ElementID);
