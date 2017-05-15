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
using System.Xml;
using Xbim.Common.Geometry;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.OctreeLib;

namespace BIMRL
{
    public struct ColorSpec
    {
        public double? ambientIntensity;
        public int? diffuseColorRed;
        public int? diffuseColorGreen;
        public int? diffuseColorBlue;
        public int? emissiveColorRed;
        public int? emissiveColorGreen;
        public int? emissiveColorBlue;
        public double? shininess;
        public int? specularColorRed;
        public int? specularColorGreen;
        public int? specularColorBlue;
        public double? transparency;

        public ColorSpec(double amb, int dCR, int dCG, int dCB, int eCR, int eCG, int eCB, double shine, int sCR, int sCG, int sCB, double trans)
        {
            ambientIntensity = amb;
            diffuseColorRed = dCR;
            diffuseColorGreen = dCG;
            diffuseColorBlue = dCB;
            emissiveColorRed = eCR;
            emissiveColorGreen = eCG;
            emissiveColorBlue = eCB;
            shininess = shine;
            specularColorRed = sCR;
            specularColorGreen = sCG;
            specularColorBlue = sCB;
            transparency = trans;
        }
    }

    public class BIMRLExportSDOToX3D
    {
        static Dictionary<string, ColorSpec> colorDict = new Dictionary<string, ColorSpec>();
        public ColorSpec highlightColor;
        public HashSet<string> IDsToHighlight = new HashSet<string>();
        public ColorSpec userGeomColor;
        public double transparencyOverride = -1;
        BIMRLCommon _refBIMRLCommon;
        string _outfile;
        XmlWriter oFile;
        public string altUserTable { get; set;}
        public static ColorSpec defaultColor = new ColorSpec(0.2, 204, 204, 204, 0, 0, 255, 0.2, 0, 0, 0, 0.0);
        Matrix3D transformToX3D = new Matrix3D(1, 0, 0, 0,
                                            0, 0, 1, 0,
                                            0, -1, 0, 0,
                                            0, 0, 0, 1);

        public BIMRLExportSDOToX3D(BIMRLCommon refBIMRLCommon, string outfilename)
        {
            // keep in dictionary the information of color and tranparency of objects. Query a table for this information
            if (colorDict.Count == 0)
                initializeDict();
            highlightColor = new ColorSpec() { emissiveColorBlue = 0, emissiveColorGreen = 0, emissiveColorRed = 255 };
            userGeomColor = new ColorSpec() { emissiveColorBlue = 204, emissiveColorGreen = 191, emissiveColorRed = 64 };

            _refBIMRLCommon = refBIMRLCommon;
            _outfile = outfilename;
            altUserTable = null;

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "\t";
            settings.ConformanceLevel = ConformanceLevel.Auto;
            oFile = XmlWriter.Create(_outfile, settings);

            oFile.WriteStartElement("X3D");
            oFile.WriteStartElement("Scene");
        }

        public void endExportToX3D()
        {
            oFile.WriteEndElement(); //Scene
            oFile.WriteEndElement(); //X3D
            oFile.Close();
        }

        public void initializeDict()
        {
            DBOperation.beginTransaction();
            string currStep = string.Empty;

            try
            {
                string sqlStmt = "Select Elementtype, AmbientIntensity, DiffuseColorRed, DiffuseColorGreen, DiffuseColorBlue, "
                                + "EmissiveColorRed, EmissiveColorGreen, EmissiveColorBlue, Shininess, "
                                + "SpecularColorRed, SpecularColorGreen, SpecularColorBlue, Transparency from colorDict";
                OracleCommand command = new OracleCommand(sqlStmt, DBOperation.DBConn);
                OracleDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    ColorSpec color = new ColorSpec();

                    string elementType = reader.GetString(0);
                    
                    if (!reader.IsDBNull(1))
                        color.ambientIntensity = reader.GetFloat(1);

                    if (!reader.IsDBNull(2))
                        color.diffuseColorRed = reader.GetInt32(2);
                    if (!reader.IsDBNull(3))
                        color.diffuseColorGreen = reader.GetInt32(3);
                    if (!reader.IsDBNull(4))
                        color.diffuseColorBlue = reader.GetInt32(4);

                    if (!reader.IsDBNull(5))
                        color.emissiveColorRed = reader.GetInt32(5);
                    if (!reader.IsDBNull(6))
                        color.emissiveColorGreen = reader.GetInt32(6);
                    if (!reader.IsDBNull(7))
                        color.emissiveColorBlue = reader.GetInt32(7);

                    if (!reader.IsDBNull(8))
                        color.shininess = reader.GetFloat(8);

                    if (!reader.IsDBNull(9))
                        color.specularColorRed = reader.GetInt32(9);
                    if (!reader.IsDBNull(10))
                        color.specularColorGreen = reader.GetInt32(10);
                    if (!reader.IsDBNull(11))
                        color.specularColorBlue = reader.GetInt32(11);

                    if (!reader.IsDBNull(12))
                        color.transparency = reader.GetFloat(12);

                    colorDict.Add(elementType, color);
                }
                reader.Dispose();
                command.Dispose();
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }
        }

        public void exportToX3D(int fedModelID, string whereCond)
        {
            exportToX3D(fedModelID, whereCond, true, false, false, false, false);
        }

        public void exportToX3D(int fedModelID, string whereCond, bool drawElemGeom, bool drawUserGeom, bool drawFacesOnly, bool drawOctree, bool drawWorldBB)
        {
            //XmlWriterSettings settings = new XmlWriterSettings();
            //settings.Indent = true;
            //settings.IndentChars = "\t";
            //settings.ConformanceLevel = ConformanceLevel.Auto;
            //XmlWriter oFile = XmlWriter.Create(_outfile, settings);

            //oFile.WriteStartElement("X3D");
            //oFile.WriteStartElement("Scene");

            if (drawWorldBB)
                exportWorldBBToX3D(fedModelID);

            if (drawElemGeom && !drawFacesOnly)
            {
                exportElemGeomToX3D(fedModelID, whereCond);
                if (drawOctree)
                    exportOctreeCellsToX3D(fedModelID, whereCond);
            }
            else if (drawElemGeom && drawFacesOnly)
            {
                exportFacesToX3D(fedModelID, whereCond);
                if (drawOctree)
                    exportOctreeCellsToX3D(fedModelID, whereCond);
            }
            
            if (drawUserGeom && !drawFacesOnly)
            {
                exportUserGeomToX3D(whereCond);
                if (drawOctree)
                    exportOctreeCellsToX3D(oFile, fedModelID, whereCond, true);
            }
            else if (drawUserGeom && drawFacesOnly)
            {
                exportFacesToX3D(fedModelID, whereCond, true);
                if (drawOctree)
                    exportOctreeCellsToX3D(oFile, fedModelID, whereCond, true);
            }

            // Move these lines to endExportToX3D to allow appending the output from multiple requests
            //oFile.WriteEndElement(); //Scene
            //oFile.WriteEndElement(); //X3D
            //oFile.Close();
        }

        public void exportWorldBBToX3D(int fedModelID)
        {
            // Draw the world BBox
            string sqlStmt = "select WORLDBBOX from BIMRL_FEDERATEDMODEL WHERE FEDERATEDID='" + fedModelID.ToString() + "'";
            OracleCommand command = new OracleCommand("", DBOperation.DBConn);
            command.CommandText = sqlStmt;
            try
            {
                OracleDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    oFile.WriteStartElement("Transform");
                    oFile.WriteStartElement("Shape");
                    oFile.WriteStartElement("Appearance");
                    oFile.WriteStartElement("Material");
                    oFile.WriteAttributeString(null, "emissiveColor", null, "1 1 1");
                    oFile.WriteEndElement(); // Material
                    oFile.WriteEndElement(); // Appearance

                    SdoGeometry sdoGeomData = reader.GetValue(0) as SdoGeometry;
                    Point3D llb = new Point3D();
                    llb.X = sdoGeomData.OrdinatesArrayOfDoubles[0];
                    llb.Y = sdoGeomData.OrdinatesArrayOfDoubles[1];
                    llb.Z = sdoGeomData.OrdinatesArrayOfDoubles[2];
                    llb = transformToX3D.Transform(llb);
                    Point3D urt = new Point3D();
                    urt.X = sdoGeomData.OrdinatesArrayOfDoubles[3];
                    urt.Y = sdoGeomData.OrdinatesArrayOfDoubles[4];
                    urt.Z = sdoGeomData.OrdinatesArrayOfDoubles[5];
                    urt = transformToX3D.Transform(urt);

                    Octree.WorldBB = new BoundingBox3D(llb, urt);

                    string coordListStr = llb.X + " " + llb.Y + " " + llb.Z + " " + urt.X + " " + llb.Y + " " + llb.Z + " "
                                        + urt.X + " " + urt.Y + " " + llb.Z + " " + llb.X + " " + urt.Y + " " + llb.Z + " "
                                        + llb.X + " " + llb.Y + " " + urt.Z + " " + urt.X + " " + llb.Y + " " + urt.Z + " "
                                        + urt.X + " " + urt.Y + " " + urt.Z + " " + llb.X + " " + urt.Y + " " + urt.Z;
                    string pointIdxStr = "0 1 2 3 0 4 5 6 7 4 5 1 2 6 7 3";

                    oFile.WriteStartElement("IndexedLineSet");
                    oFile.WriteAttributeString(null, "coordIndex", null, pointIdxStr);
                    oFile.WriteStartElement("Coordinate");
                    oFile.WriteAttributeString(null, "point", null, coordListStr);
                    oFile.WriteEndElement();  // Coordinate
                    oFile.WriteEndElement();  // IndexedLineSet
                    oFile.WriteEndElement();  // Shape
                    oFile.WriteEndElement();  // Transform
                    oFile.Flush();
                    reader.Dispose();
                }
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + sqlStmt;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }
            command.Dispose();
        }
    
        public void exportElemGeomToX3D(int fedModelID, string whereCond)
        {
            DBOperation.beginTransaction();
            string currStep = string.Empty;
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            OracleDataReader reader;
            string sqlStmt;

            try
            {
                SdoGeometry sdoGeomData = new SdoGeometry();
                sqlStmt = "select elementid, elementtype, geometrybody from BIMRL_ELEMENT_" + fedModelID.ToString("X4") + " where geometrybody is not null ";
                if (!string.IsNullOrEmpty(whereCond))
                    sqlStmt += " AND " + whereCond;
                currStep = sqlStmt;

                command.CommandText = sqlStmt;
                command.FetchSize = 20;

                reader = command.ExecuteReader();
                ColorSpec theColor;

                while (reader.Read())
                {
                    string elemID = reader.GetString(0);
                    string elemTyp = reader.GetString(1);
                    bool toBeHighlighted = false;

                    oFile.WriteStartElement("Transform");

                    colorDict.TryGetValue(elemTyp, out theColor);

                    if (IDsToHighlight.Contains(elemID))
                    {
                        theColor = highlightColor;
                        toBeHighlighted = true;
                    }

                    StringBuilder pointStr = new StringBuilder();
                    StringBuilder coordIndexStr = new StringBuilder();

                    sdoGeomData = reader.GetValue(2) as SdoGeometry;

                    int[] elInfo = sdoGeomData.ElemArrayOfInts;
                    double[] ordArray = sdoGeomData.OrdinatesArrayOfDoubles;
                    int eInfoIdx = 0;   // Index on Element Info array
                    int vertIdx = 0;     // new Index for the X3D index to the vertex coordinate lists
                    Point3D v = new Point3D();
                    Point3D v1 = new Point3D();

                    // First loop: loop for lump
                    while (eInfoIdx < elInfo.Length)
                    {
                        // advance to the 6th array in the element info to get the number of faces
                        eInfoIdx += 5;
                        int noFace = elInfo[eInfoIdx];

                        eInfoIdx += 1;      // Advance to the first face offset

                        // second loop for the number of faces inside a lump
                        for (int f = 0; f < noFace; f++)
                        {
                            bool vert = true;
                            int vcount = 0;
                            int fIdx = elInfo[eInfoIdx];

                            while (vert)
                            {
                                if (vcount == 0)
                                {
                                    v1.X = ordArray[fIdx - 1];     // -1 because the index starts at no 1
                                    v1.Y = ordArray[fIdx];
                                    v1.Z = ordArray[fIdx + 1];
                                    v1 = transformToX3D.Transform(v1);

                                    pointStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v1.X, v1.Y, v1.Z));
                                    coordIndexStr.Append(string.Format("{0} ", vertIdx++));
                                    vcount++;
                                    fIdx += 3;
                                    continue;
                                }
                                v.X = ordArray[fIdx - 1];
                                v.Y = ordArray[fIdx];
                                v.Z = ordArray[fIdx + 1];
                                v = transformToX3D.Transform(v);

                                if (Point3D.Equals(v, v1))
                                {
                                    // We are at the end of the vertex list. Oracle SDO repeat the last point as the first point, we can skip this for X3D
                                    coordIndexStr.Append(string.Format("-1 "));
                                    vert = false;
                                    eInfoIdx += 3;
                                    continue;
                                }

                                pointStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));
                                coordIndexStr.Append(string.Format("{0} ", vertIdx++));
                                fIdx += 3;
                            }
                        }

                        oFile.WriteStartElement("Shape");
                        oFile.WriteAttributeString(null, "DEF", null, elemID);
                        oFile.WriteStartElement("Appearance");
                        oFile.WriteStartElement("Material");
                        if (theColor.ambientIntensity != null)
                            oFile.WriteAttributeString(null, "ambientIntensity", null, theColor.ambientIntensity.ToString());

                        // minimum diffuse color should be set, at lease using default color
                        if (theColor.diffuseColorRed != null && theColor.diffuseColorGreen != null && theColor.diffuseColorBlue != null)
                        {
                            string diffuseColorStr = (theColor.diffuseColorRed / 255.0).ToString() + " " + (theColor.diffuseColorGreen / 255.0).ToString() + " " + (theColor.diffuseColorBlue / 255.0).ToString();
                            oFile.WriteAttributeString(null, "diffuseColor", null, diffuseColorStr);
                        }

                        if (theColor.emissiveColorRed != null && theColor.emissiveColorGreen != null && theColor.emissiveColorBlue != null)
                        {
                            string emissiveColorStr = (theColor.emissiveColorRed / 255.0).ToString() + " " + (theColor.emissiveColorGreen / 255.0).ToString() + " " + (theColor.emissiveColorBlue / 255.0).ToString();
                            oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);
                        }
                        else
                        {
                            string emissiveColorStr = "0 0 1";
                            oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);
                        }

                        if (theColor.shininess != null)
                            oFile.WriteAttributeString(null, "shininess", null, theColor.shininess.ToString());

                        if (theColor.specularColorRed != null && theColor.specularColorGreen != null && theColor.specularColorBlue != null)
                        {
                            string specularColorStr = (theColor.specularColorRed / 255.0).ToString() + " " + (theColor.specularColorGreen / 255.0).ToString() + " " + (theColor.specularColorBlue / 255.0).ToString();
                            oFile.WriteAttributeString(null, "specularColor", null, specularColorStr);
                        }

                        // If the transparencyOverride is set, we will override the transparency setting with this one, except for IfcSpace
                        if (transparencyOverride >= 0 && string.Compare(elemTyp, "IFCSPACE") != 0 && !toBeHighlighted)
                        {
                            oFile.WriteAttributeString(null, "transparency", null, transparencyOverride.ToString());
                        }
                        else
                        {
                            if (theColor.transparency != null)
                                oFile.WriteAttributeString(null, "transparency", null, theColor.transparency.ToString());
                        }

                        oFile.WriteEndElement(); // Material
                        oFile.WriteEndElement(); // Appearance

                        oFile.WriteStartElement("IndexedFaceSet");
                        oFile.WriteAttributeString(null, "coordIndex", null, coordIndexStr.ToString().TrimEnd());
                        oFile.WriteStartElement("Coordinate");
                        oFile.WriteAttributeString(null, "point", null, pointStr.ToString().TrimEnd());
                        oFile.WriteEndElement();  // Coordinate
                        oFile.WriteEndElement();  // IndexedFaceSet
                        oFile.WriteEndElement();  // Shape

                        vertIdx = 0; // Reset the Ord array as every shape has its own array
                        //pointList.Clear();
                        pointStr.Clear();
                        coordIndexStr.Clear();
                    }

                    oFile.WriteEndElement();  // Transform
                    oFile.Flush();
                }
                reader.Dispose();

            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }


            command.Dispose();
        }

        public void exportOctreeCellsToX3D(int fedModelID, string elementid)
        {
            exportOctreeCellsToX3D(oFile, fedModelID, elementid, false);    // default set to "false" for backward compatibility
        }

        public void exportOctreeCellsToX3D(XmlWriter oFile, int fedModelID, string whereCond, bool forUsergeom)
        {
            // Needed to set the world box information to be used by the Octree cell computation
            Point3D llb;
            Point3D urt;
            DBOperation.getWorldBB(fedModelID, out llb, out urt);

            DBOperation.beginTransaction();
            string currStep = string.Empty;
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            OracleDataReader reader;
            string whereStr = "";
            if (!string.IsNullOrEmpty(whereCond))
                whereStr = " WHERE " + whereCond;

            try
            {
                string sqlStmt;
                if (!forUsergeom)
                    sqlStmt = "select cellid from BIMRL_SPATIALINDEX_" + fedModelID.ToString("X4") + whereStr;
                else
                    sqlStmt = "select cellid, celltype from USERGEOM_SPATIALINDEX " + whereStr;

                currStep = sqlStmt;

                command.CommandText = sqlStmt;
                command.FetchSize = 500;
                oFile.WriteStartElement("Transform");
                reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string cellID = reader.GetString(0);
                    int celltype = -1;
                    if (forUsergeom)
                        celltype = reader.GetInt32(1);

                    // Replaces all the lines below
                    drawCellInX3d(cellID, celltype);

                    //CellID64 cell = new CellID64(cellID);
                    //Vector3D cellSize = CellID64.cellSize(cell.Level);
                    //Point3D cellLoc = CellID64.getCellIdxLoc(cell);
                    //StringBuilder coordStrBuilder = new StringBuilder();

                    //Point3D v = new Point3D(cellLoc.X, cellLoc.Y, cellLoc.Z);
                    //v = transformToX3D.Transform(v);
                    //coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

                    //v = new Point3D(cellLoc.X + cellSize.X, cellLoc.Y, cellLoc.Z);
                    //v = transformToX3D.Transform(v);
                    //coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

                    //v = new Point3D(cellLoc.X + cellSize.X, cellLoc.Y + cellSize.Y, cellLoc.Z);
                    //v = transformToX3D.Transform(v);
                    //coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

                    //v = new Point3D(cellLoc.X, cellLoc.Y + cellSize.Y, cellLoc.Z);
                    //v = transformToX3D.Transform(v);
                    //coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

                    //v = new Point3D(cellLoc.X, cellLoc.Y, cellLoc.Z + cellSize.Z);
                    //v = transformToX3D.Transform(v);
                    //coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

                    //v = new Point3D(cellLoc.X + cellSize.X, cellLoc.Y, cellLoc.Z + cellSize.Z);
                    //v = transformToX3D.Transform(v);
                    //coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

                    //v = new Point3D(cellLoc.X + cellSize.X, cellLoc.Y + cellSize.Y, cellLoc.Z + cellSize.Z);
                    //v = transformToX3D.Transform(v);
                    //coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

                    //v = new Point3D(cellLoc.X, cellLoc.Y + cellSize.Y, cellLoc.Z + cellSize.Z);
                    //v = transformToX3D.Transform(v);
                    //coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

                    ////string coordListStr = cellLoc.X + " " + cellLoc.Y + " " + cellLoc.Z + " " + (cellLoc.X + cellSize.X) + " " + cellLoc.Y + " " + cellLoc.Z + " "
                    ////                    + (cellLoc.X + cellSize.X) + " " + (cellLoc.Y + cellSize.Y) + " " + cellLoc.Z + " " + cellLoc.X + " " + (cellLoc.Y + cellSize.Y) + " " + cellLoc.Z + " "
                    ////                    + cellLoc.X + " " + cellLoc.Y + " " + (cellLoc.Z + cellSize.Z) + " " + (cellLoc.X + cellSize.X) + " " + cellLoc.Y + " " + (cellLoc.Z + cellSize.Z) + " "
                    ////                    + (cellLoc.X + cellSize.X) + " " + (cellLoc.Y + cellSize.Y) + " " + (cellLoc.Z + cellSize.Z) + " " + cellLoc.X + " " + (cellLoc.Y + cellSize.Y) + " " + (cellLoc.Z + cellSize.Z);

                    //string coordListStr = coordStrBuilder.ToString();

                    //string pointIdxStr = "0 1 2 3 0 4 5 6 7 4 5 1 2 6 7 3";

                    //oFile.WriteStartElement("Shape");
                    //oFile.WriteAttributeString(null, "DEF", null, cellID);
                    //oFile.WriteStartElement("Appearance");
                    //oFile.WriteStartElement("Material");
                    //if (celltype <= 1)
                    //{
                    //    oFile.WriteAttributeString(null, "emissiveColor", null, "1 1 1");
                    //    oFile.WriteAttributeString(null, "transparency", null, "0.5");
                    //}
                    //else
                    //{
                    //    oFile.WriteAttributeString(null, "emissiveColor", null, "0.6 0.4 0.2");
                    //    oFile.WriteAttributeString(null, "transparency", null, "0.35");
                    //}
                    //oFile.WriteEndElement(); // Material
                    //oFile.WriteEndElement(); // Appearance

                    //oFile.WriteStartElement("IndexedLineSet");
                    //oFile.WriteAttributeString(null, "coordIndex", null, pointIdxStr);
                    //oFile.WriteStartElement("Coordinate");
                    //oFile.WriteAttributeString(null, "point", null, coordListStr);
                    //oFile.WriteEndElement();  // Coordinate
                    //oFile.WriteEndElement();  // IndexedLineSet
                    //oFile.WriteEndElement();  // Shape
                }
                reader.Dispose();

                oFile.WriteEndElement();  // Transform
                oFile.Flush();
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }

            command.Dispose();
        }

        public void exportFacesToX3DbyElemid(int fedModelID, string elementid)
        {
            exportFacesToX3DbyElemid(fedModelID, elementid, false);     // default when is not set is "false" - for backward compatibility
        }

        public void exportFacesToX3DbyElemid(int fedModelID, string elementid, bool forUsergeom)
        {
            string wherecond = " ELEMENTID = '" + elementid + "'";
            exportFacesToX3D(fedModelID, wherecond, forUsergeom);
        }

        public void exportFacesToX3D(int fedModelID, string wherecond)
        {
            exportFacesToX3D(fedModelID, wherecond, false);     // default when is not set is "false" - for backward compatibility
         }

        public void exportFacesToX3D(int fedModelID, string wherecond, bool forUsergeom)
        {
            // Select faces from the DB and collect them into a List
            List<Face3D> facesData = new List<Face3D>();

            DBOperation.beginTransaction();
            string currStep = string.Empty;
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            OracleDataReader reader;

            try
            {
                SdoGeometry sdoGeomData = new SdoGeometry();

                string sqlStmt;
                if (!forUsergeom)
                    sqlStmt = "select A.ELEMENTID, A.ID, A.POLYGON, A.TYPE from BIMRL_TOPO_FACE_" + fedModelID.ToString("X4") + " A ";
                else
                    sqlStmt = "select A.ELEMENTID, A.ID, A.POLYGON, A.TYPE from USERGEOM_TOPO_FACE A";

                if (!string.IsNullOrEmpty(wherecond))
                    sqlStmt += " WHERE " + wherecond;
 
                currStep = sqlStmt;
                command.CommandText = sqlStmt;
                command.FetchSize = 300;

                reader = command.ExecuteReader();

                oFile.WriteStartElement("Transform");

                // X3D uses only triangulated geometry and therefore does not support hole. When we draw a face with hole(s) we will just draw the hole as another face
                while (reader.Read())
                {
                    string eid = reader.GetString(0);
                    string fid = reader.GetString(1);

                    sdoGeomData = reader.GetValue(2) as SdoGeometry;
                    string type = reader.GetString(3);

                    int[] elInfo = sdoGeomData.ElemArrayOfInts;
                    double[] ordArray = sdoGeomData.OrdinatesArrayOfDoubles;

                    int noLoop = elInfo.Length / 3;     // first loop is the outerloop and the rest will be innerloop
                    int totalVerts = ordArray.Length / 3;
                    List<int> vertsInLoop = new List<int>();
                    for (int i = 0; i < noLoop; i++)
                    {
                        if (i == noLoop - 1)
                            vertsInLoop.Add((totalVerts - (elInfo[i*3]-1)/3));
                        else
                            vertsInLoop.Add((elInfo[(i+1)*3] - elInfo[i*3]) / 3);
                    }

                    StringBuilder coordStr = new StringBuilder();
                    StringBuilder vertIdxStr = new StringBuilder();

                    // We will only write out the outer loop since the inner loop will have its own face
                    int initPos = 0;
                    //for (int i = 0; i < noLoop; i++)
                    for (int i = 0; i < 1; i++)
                    { 
                        for (int v = 0; v < vertsInLoop[i]; v++)
                        {
                            initPos = elInfo[i * 3] - 1;
                            int pos = initPos + v * 3;
                            Point3D pt = transformToX3D.Transform(new Point3D(ordArray[pos], ordArray[pos + 1], ordArray[pos + 2]));
                            coordStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", pt.X, pt.Y, pt.Z));
                            vertIdxStr.Append(string.Format("{0} ", v));
                        }
                        vertIdxStr.Append("-1 ");
                    }    

                    oFile.WriteStartElement("Shape");
                    oFile.WriteAttributeString(null, "DEF", null, eid + ":" + fid);
                    oFile.WriteStartElement("Appearance");
                    oFile.WriteStartElement("Material");

                    string emissiveColorStr = "1 0 0";
                    if (string.Compare(type, "HOLE") == 0)
                        emissiveColorStr = "0 1 0";
                    if (string.Compare(type, "OBB") == 0)
                        emissiveColorStr = "0 0 1";
                    if (string.Compare(type, "PROJOBB") == 0)
                        emissiveColorStr = "1 0 1";
                    oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);
                    
                    // If the transparencyOverride is set, we will override the transparency settint with this one
                    if (transparencyOverride >= 0)
                    {
                        oFile.WriteAttributeString(null, "transparency", null, transparencyOverride.ToString());
                    }
                    else if (string.Compare(type, "OBB") == 0 || string.Compare(type, "PROJOBB") == 0)
                    {
                        oFile.WriteAttributeString(null, "transparency", null, "0.85");      // For OBB faces always forced to 85% transparency
                    }
                    else
                    {
                        double transparency = 0.0;
                        oFile.WriteAttributeString(null, "transparency", null, transparency.ToString());
                    }

                    oFile.WriteEndElement(); // Material
                    oFile.WriteEndElement(); // Appearance

                    oFile.WriteStartElement("IndexedFaceSet");
                    oFile.WriteAttributeString(null, "coordIndex", null, vertIdxStr.ToString().TrimEnd());
                    oFile.WriteStartElement("Coordinate");
                    oFile.WriteAttributeString(null, "point", null, coordStr.ToString().TrimEnd());
                    oFile.WriteEndElement();  // Coordinate
                    oFile.WriteEndElement();  // IndexedFaceSet
                    oFile.WriteEndElement();  // Shape

                    vertIdxStr.Clear();
                    coordStr.Clear();
                }
                oFile.WriteEndElement();  // Transform
                oFile.Flush();
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }

            command.Dispose();
        }

        public void exportFacesToX3D(List<Face3D> facesData)
        {
            //XmlWriterSettings settings = new XmlWriterSettings();
            //settings.Indent = true;
            //settings.IndentChars = "\t";
            //XmlWriter oFile = XmlWriter.Create(_outfile, settings);

            //oFile.WriteStartElement("X3D");
            //oFile.WriteStartElement("Scene");

            oFile.WriteStartElement("Transform");

            StringBuilder coordIndexStr = new StringBuilder();
            StringBuilder pointStr = new StringBuilder();
            //StringBuilder normalStr = new StringBuilder();
            //StringBuilder normalIndexStr = new StringBuilder();

            int vertIdx = 0;
            //int normalIdx = 0;

            foreach (Face3D f in facesData)
            {
                Vector3D fNormal = f.basePlane.normalVector;

                Vector3D N = fNormal;
                foreach (List<Point3D> pList in f.verticesWithHoles)
                {
                    int vertIdxAtStart = vertIdx;
                    for (int i = 0; i < pList.Count; i++)
                    {
                        Point3D P = transformToX3D.Transform(pList[i]);
                        if ((i == pList.Count - 1) && (P == pList[0]))
                        {
                                coordIndexStr.Append(string.Format("{0} ", vertIdxAtStart));     // No need to add vert coord since the start point is the same as end point, just add the index
                                //normalIndexStr.Append(string.Format("{0} ", vertIdxAtStart));     // No need to add vert coord since the start point is the same as end point, just add the index
                        }
                        else
                        {
                            pointStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", P.X, P.Y, P.Z));
                            coordIndexStr.Append(string.Format("{0} ", vertIdx++));
                            //normalStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", N.X, N.Y, N.Z));
                            //normalIndexStr.Append(string.Format("{0} ", vertIdx++));
                        }
                    }
                    //N = holeNormal;
                    coordIndexStr.Append(string.Format("-1 "));
                    //normalIndexStr.Append(string.Format("-1 "));
                }
            }

            oFile.WriteStartElement("Shape");
            // oFile.WriteAttributeString(null, "DEF", null, cellID);
            oFile.WriteStartElement("Appearance");

            oFile.WriteStartElement("Material");
            string emissiveColorStr = "1 0 0";
            oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);
            double transparency = 0.6;
            oFile.WriteAttributeString(null, "transparency", null, transparency.ToString());
            oFile.WriteEndElement(); // Material

            oFile.WriteEndElement(); // Appearance

            oFile.WriteStartElement("IndexedFaceSet");
            oFile.WriteAttributeString(null, "coordIndex", null, coordIndexStr.ToString().TrimEnd());
            // oFile.WriteAttributeString(null, "normalIndex", null, normalIndexStr.ToString().TrimEnd());
            oFile.WriteStartElement("Coordinate");
            oFile.WriteAttributeString(null, "point", null, pointStr.ToString().TrimEnd());
            oFile.WriteEndElement();  // Coordinate

            //oFile.WriteStartElement("Normal");
            //oFile.WriteAttributeString(null, "vector", null, normalStr.ToString().TrimEnd());
            //oFile.WriteEndElement();  // Normal

            oFile.WriteEndElement();  // IndexedFaceSet
            oFile.WriteEndElement();  // Shape

            oFile.WriteEndElement();  // Transform

            // Move these lines to endExportToX3D to allow appending the output from multiple requests
            //oFile.WriteEndElement(); //Scene
            //oFile.WriteEndElement(); //X3D
            //oFile.Close();
        }

        public void exportUserGeomToX3D(string wherecond)
        {
            // Select faces from the DB and collect them into a List
            List<Face3D> facesData = new List<Face3D>();

            DBOperation.beginTransaction();
            string currStep = string.Empty;
            OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
            OracleDataReader reader;

            try
            {
                SdoGeometry sdoGeomData = new SdoGeometry();

                string tabName = "USERGEOM";
                if (!string.IsNullOrEmpty(altUserTable))
                    tabName = altUserTable;
                string sqlStmt = "select A.ELEMENTID, GEOMETRY, A.BODY_MAJOR_AXIS1, A.BODY_MAJOR_AXIS2, A.BODY_MAJOR_AXIS3"
                                    + " from " + tabName + "_GEOMETRY A ";
                if (!string.IsNullOrEmpty(wherecond))
                    sqlStmt += " WHERE " + wherecond;

                currStep = sqlStmt;
                command.CommandText = sqlStmt;
                command.FetchSize = 300;

                reader = command.ExecuteReader();

                oFile.WriteStartElement("Transform");

                // X3D uses only triangulated geometry and therefore does not support hole. When we draw a face with hole(s) we will just draw the hole as another face
                while (reader.Read())
                {
                    string eid = reader.GetString(0);
                    string defStr = "elementid: " + eid;

                    sdoGeomData = reader.GetValue(1) as SdoGeometry;
                    SdoGeometry mjAxis1 = reader.GetValue(2) as SdoGeometry;
                    SdoGeometry mjAxis2 = reader.GetValue(3) as SdoGeometry;
                    SdoGeometry mjAxis3 = reader.GetValue(4) as SdoGeometry;

                    int[] elInfo = sdoGeomData.ElemArrayOfInts;
                    double[] ordArray = sdoGeomData.OrdinatesArrayOfDoubles;

                    StringBuilder coordStr = new StringBuilder();
                    StringBuilder vertIdxStr = new StringBuilder();

                    int gtype = sdoGeomData.PropertiesFromGTYPE();
                    int geomtyp = sdoGeomData.GeometryType;
                    if (geomtyp == (int)SdoGeometryTypes.GTYPE.SOLID)
                    {
                        /****** For a solid *****/
                        int eInfoIdx = 0;   // Index on Element Info array
                        int vertIdx = 0;     // new Index for the X3D index to the vertex coordinate lists
                        Point3D v = new Point3D();
                        Point3D v1 = new Point3D();

                        while (eInfoIdx < elInfo.Length)
                        {
                            // advance to the 6th array in the element info to get the number of faces
                            eInfoIdx += 5;
                            int noFace = elInfo[eInfoIdx];

                            eInfoIdx += 1;      // Advance to the first face offset

                            // second loop for the number of faces inside a lump
                            for (int f = 0; f < noFace; f++)
                            {
                                bool vert = true;
                                int vcount = 0;
                                int fIdx = elInfo[eInfoIdx];

                                while (vert)
                                {
                                    if (vcount == 0)
                                    {
                                        v1.X = ordArray[fIdx - 1];     // -1 because the index starts at no 1
                                        v1.Y = ordArray[fIdx];
                                        v1.Z = ordArray[fIdx + 1];
                                        v1 = transformToX3D.Transform(v1);

                                        coordStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v1.X, v1.Y, v1.Z));
                                        vertIdxStr.Append(string.Format("{0} ", vertIdx++));
                                        vcount++;
                                        fIdx += 3;
                                        continue;
                                    }
                                    v.X = ordArray[fIdx - 1];
                                    v.Y = ordArray[fIdx];
                                    v.Z = ordArray[fIdx + 1];
                                    v = transformToX3D.Transform(v);

                                    if (Point3D.Equals(v, v1))
                                    {
                                        // We are at the end of the vertex list. Oracle SDO repeat the last point as the first point, we can skip this for X3D
                                        vertIdxStr.Append(string.Format("-1 "));
                                        vert = false;
                                        eInfoIdx += 3;
                                        continue;
                                    }

                                    coordStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));
                                    vertIdxStr.Append(string.Format("{0} ", vertIdx++));
                                    fIdx += 3;
                                }
                            }

                            oFile.WriteStartElement("Shape");
                            oFile.WriteAttributeString(null, "DEF", null, defStr);
                            oFile.WriteStartElement("Appearance");
                            oFile.WriteStartElement("Material");

                            string emissiveColorStr = (userGeomColor.emissiveColorRed / 255.0).ToString() + " " + (userGeomColor.emissiveColorGreen / 255.0).ToString() + " " + (userGeomColor.emissiveColorBlue / 255.0).ToString();
                            oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);
                            //string emissiveColorStr = "0.25 0.75 0.8";

                            // If the transparencyOverride is set, we will override the transparency settint with this one
                            //if (transparencyOverride >= 0)
                            //{
                            //    oFile.WriteAttributeString(null, "transparency", null, transparencyOverride.ToString());
                            //}
                            //else
                            //{
                                double transparency = 0.0;
                                if (userGeomColor.transparency.HasValue)
                                {
                                    transparency = userGeomColor.transparency.Value;
                                }
                                else if (transparencyOverride >= 0)
                                {
                                    transparency = transparencyOverride;
                                }
                                oFile.WriteAttributeString(null, "transparency", null, transparency.ToString());
                            //}

                            oFile.WriteEndElement(); // Material
                            oFile.WriteEndElement(); // Appearance

                            oFile.WriteStartElement("IndexedFaceSet");
                            oFile.WriteAttributeString(null, "coordIndex", null, vertIdxStr.ToString().TrimEnd());
                            oFile.WriteStartElement("Coordinate");
                            oFile.WriteAttributeString(null, "point", null, coordStr.ToString().TrimEnd());
                            oFile.WriteEndElement();  // Coordinate
                            oFile.WriteEndElement();  // IndexedFaceSet
                            oFile.WriteEndElement();  // Shape

                            vertIdx = 0; // Reset the Ord array as every shape has its own array
                            coordStr.Clear();
                            vertIdxStr.Clear();
                        }

                    }
                    else if (geomtyp == (int)SdoGeometryTypes.GTYPE.POLYGON)
                    {
                        /****** For a face/polygon *****/
                        int noLoop = elInfo.Length / 3;
                        int totalVerts = ordArray.Length / 3;
                        List<int> vertsInLoop = new List<int>();
                        for (int i = 0; i < noLoop; i++)
                        {
                            if (i == noLoop - 1)
                                vertsInLoop.Add((totalVerts - (elInfo[i * 3] - 1) / 3));
                            else
                                vertsInLoop.Add((elInfo[(i + 1) * 3] - elInfo[i * 3]) / 3);
                        }
                        // We will only write out the outer loop since the inner loop will have its own face
                        int initPos = 0;
                        //for (int i = 0; i < noLoop; i++)
                        for (int i = 0; i < 1; i++)
                        {
                            for (int v = 0; v < vertsInLoop[i]; v++)
                            {
                                initPos = elInfo[i * 3] - 1;
                                int pos = initPos + v * 3;
                                Point3D pt = transformToX3D.Transform(new Point3D(ordArray[pos], ordArray[pos + 1], ordArray[pos + 2]));
                                coordStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", pt.X, pt.Y, pt.Z));
                                vertIdxStr.Append(string.Format("{0} ", v));
                            }
                            vertIdxStr.Append("-1 ");
                        }

                        oFile.WriteStartElement("Shape");
                        oFile.WriteAttributeString(null, "DEF", null, defStr);
                        oFile.WriteStartElement("Appearance");
                        oFile.WriteStartElement("Material");

                        string emissiveColorStr = (userGeomColor.emissiveColorRed / 255.0).ToString() + " " + (userGeomColor.emissiveColorGreen / 255.0).ToString() + " " + (userGeomColor.emissiveColorBlue / 255.0).ToString();
                        oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);

                        double transparency = 0.0;
                        if (userGeomColor.transparency.HasValue)
                            transparency = userGeomColor.transparency.Value;
                        oFile.WriteAttributeString(null, "transparency", null, transparency.ToString());
                        //string emissiveColorStr = "0.25 0.9 0.4";
                        //oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);
                        //double transparency = 0.0;
                        //oFile.WriteAttributeString(null, "transparency", null, transparency.ToString());

                        oFile.WriteEndElement(); // Material
                        oFile.WriteEndElement(); // Appearance
                        oFile.WriteStartElement("IndexedFaceSet");
                        oFile.WriteAttributeString(null, "coordIndex", null, vertIdxStr.ToString().TrimEnd());
                        oFile.WriteStartElement("Coordinate");
                        oFile.WriteAttributeString(null, "point", null, coordStr.ToString().TrimEnd());
                        oFile.WriteEndElement();  // Coordinate
                        oFile.WriteEndElement();  // IndexedFaceSet
                        oFile.WriteEndElement();  // Shape

                        // creating separate faces for the holes
                        for (int i = 1; i < noLoop; i++)
                        {
                            vertIdxStr.Clear();
                            coordStr.Clear();
                            for (int v = 0; v < vertsInLoop[i]; v++)
                            {
                                initPos = elInfo[i * 3] - 1;
                                int pos = initPos + v * 3;
                                Point3D pt = transformToX3D.Transform(new Point3D(ordArray[pos], ordArray[pos + 1], ordArray[pos + 2]));
                                coordStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", pt.X, pt.Y, pt.Z));
                                vertIdxStr.Append(string.Format("{0} ", v));
                            }
                            vertIdxStr.Append("-1 ");
                            oFile.WriteStartElement("Shape");
                            oFile.WriteAttributeString(null, "DEF", null, "(HOLE) " + defStr);
                            oFile.WriteStartElement("Appearance");
                            oFile.WriteStartElement("Material");

                            emissiveColorStr = "1.0 1.0 0.0";
                            oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);
                            transparency = 0.0;
                            oFile.WriteAttributeString(null, "transparency", null, transparency.ToString());

                            oFile.WriteEndElement(); // Material
                            oFile.WriteEndElement(); // Appearance
                            oFile.WriteStartElement("IndexedFaceSet");
                            oFile.WriteAttributeString(null, "coordIndex", null, vertIdxStr.ToString().TrimEnd());
                            oFile.WriteStartElement("Coordinate");
                            oFile.WriteAttributeString(null, "point", null, coordStr.ToString().TrimEnd());
                            oFile.WriteEndElement();  // Coordinate
                            oFile.WriteEndElement();  // IndexedFaceSet
                            oFile.WriteEndElement();  // Shape
                        }

                    }
                    else if (geomtyp == (int)SdoGeometryTypes.GTYPE.LINE || geomtyp == (int)SdoGeometryTypes.GTYPE.MULTILINE)
                    {
                        /****** For a line *****/
                        int noPoints = ordArray.Length / 3;
                        int pIndex = 0;
                        for (int i = 0; i < ordArray.Length; i += 3)
                        {
                            vertIdxStr.Append(string.Format("{0} ", pIndex));
                            Point3D pt = transformToX3D.Transform(new Point3D(ordArray[i], ordArray[i + 1], ordArray[i + 2]));
                            coordStr.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", pt.X, pt.Y, pt.Z));
                            pIndex++;
                        }
                        oFile.WriteStartElement("Shape");
                        oFile.WriteAttributeString(null, "DEF", null, defStr);
                        oFile.WriteStartElement("Appearance");
                        oFile.WriteStartElement("Material");

                        string emissiveColorStr = (userGeomColor.emissiveColorRed / 255.0).ToString() + " " + (userGeomColor.emissiveColorGreen / 255.0).ToString() + " " + (userGeomColor.emissiveColorBlue / 255.0).ToString();
                        oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);

                        double transparency = 0.0;
                        if (userGeomColor.transparency.HasValue)
                            transparency = userGeomColor.transparency.Value;
                        oFile.WriteAttributeString(null, "transparency", null, transparency.ToString());
                        //string emissiveColorStr = "1.0 0.2 0.0";
                        //oFile.WriteAttributeString(null, "emissiveColor", null, emissiveColorStr);
                        //double transparency = 0.0;
                        //oFile.WriteAttributeString(null, "transparency", null, transparency.ToString());

                        oFile.WriteEndElement(); // Material
                        oFile.WriteEndElement(); // Appearance
                        oFile.WriteStartElement("IndexedLineSet");
                        oFile.WriteAttributeString(null, "coordIndex", null, vertIdxStr.ToString().TrimEnd());
                        oFile.WriteStartElement("Coordinate");
                        oFile.WriteAttributeString(null, "point", null, coordStr.ToString().TrimEnd());
                        oFile.WriteEndElement();  // Coordinate
                        oFile.WriteEndElement();  // IndexedLineSet
                        oFile.WriteEndElement();  // Shape
                    }

                    vertIdxStr.Clear();
                    coordStr.Clear();
                }
                oFile.WriteEndElement();  // Transform
                oFile.Flush();
            }
            catch (OracleException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
            }
            catch (SystemException e)
            {
                string excStr = "%%Read Error - " + e.Message + "\n\t" + currStep;
                _refBIMRLCommon.StackPushError(excStr);
                throw;
            }

            command.Dispose();
        }

        public void drawCellInX3d(string cellID, int celltype = -1)
        {
            CellID64 cell = new CellID64(cellID);
            Vector3D cellSize = CellID64.cellSize(cell.Level);
            Point3D cellLoc = CellID64.getCellIdxLoc(cell);
            StringBuilder coordStrBuilder = new StringBuilder();

            Point3D v = new Point3D(cellLoc.X, cellLoc.Y, cellLoc.Z);
            v = transformToX3D.Transform(v);
            coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

            v = new Point3D(cellLoc.X + cellSize.X, cellLoc.Y, cellLoc.Z);
            v = transformToX3D.Transform(v);
            coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

            v = new Point3D(cellLoc.X + cellSize.X, cellLoc.Y + cellSize.Y, cellLoc.Z);
            v = transformToX3D.Transform(v);
            coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

            v = new Point3D(cellLoc.X, cellLoc.Y + cellSize.Y, cellLoc.Z);
            v = transformToX3D.Transform(v);
            coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

            v = new Point3D(cellLoc.X, cellLoc.Y, cellLoc.Z + cellSize.Z);
            v = transformToX3D.Transform(v);
            coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

            v = new Point3D(cellLoc.X + cellSize.X, cellLoc.Y, cellLoc.Z + cellSize.Z);
            v = transformToX3D.Transform(v);
            coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

            v = new Point3D(cellLoc.X + cellSize.X, cellLoc.Y + cellSize.Y, cellLoc.Z + cellSize.Z);
            v = transformToX3D.Transform(v);
            coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

            v = new Point3D(cellLoc.X, cellLoc.Y + cellSize.Y, cellLoc.Z + cellSize.Z);
            v = transformToX3D.Transform(v);
            coordStrBuilder.Append(string.Format("{0:0.##########} {1:0.##########} {2:0.##########} ", v.X, v.Y, v.Z));

            //string coordListStr = cellLoc.X + " " + cellLoc.Y + " " + cellLoc.Z + " " + (cellLoc.X + cellSize.X) + " " + cellLoc.Y + " " + cellLoc.Z + " "
            //                    + (cellLoc.X + cellSize.X) + " " + (cellLoc.Y + cellSize.Y) + " " + cellLoc.Z + " " + cellLoc.X + " " + (cellLoc.Y + cellSize.Y) + " " + cellLoc.Z + " "
            //                    + cellLoc.X + " " + cellLoc.Y + " " + (cellLoc.Z + cellSize.Z) + " " + (cellLoc.X + cellSize.X) + " " + cellLoc.Y + " " + (cellLoc.Z + cellSize.Z) + " "
            //                    + (cellLoc.X + cellSize.X) + " " + (cellLoc.Y + cellSize.Y) + " " + (cellLoc.Z + cellSize.Z) + " " + cellLoc.X + " " + (cellLoc.Y + cellSize.Y) + " " + (cellLoc.Z + cellSize.Z);

            string coordListStr = coordStrBuilder.ToString();

            string pointIdxStr = "0 1 2 3 0 4 5 6 7 4 5 1 2 6 7 3";

            oFile.WriteStartElement("Shape");
            oFile.WriteAttributeString(null, "DEF", null, cellID);
            oFile.WriteStartElement("Appearance");
            oFile.WriteStartElement("Material");
            if (celltype <= 1)
            {
                oFile.WriteAttributeString(null, "emissiveColor", null, "1 1 1");
                oFile.WriteAttributeString(null, "transparency", null, "0.5");
            }
            else
            {
                oFile.WriteAttributeString(null, "emissiveColor", null, "0.6 0.4 0.2");
                oFile.WriteAttributeString(null, "transparency", null, "0.35");
            }
            oFile.WriteEndElement(); // Material
            oFile.WriteEndElement(); // Appearance

            oFile.WriteStartElement("IndexedLineSet");
            oFile.WriteAttributeString(null, "coordIndex", null, pointIdxStr);
            oFile.WriteStartElement("Coordinate");
            oFile.WriteAttributeString(null, "point", null, coordListStr);
            oFile.WriteEndElement();  // Coordinate
            oFile.WriteEndElement();  // IndexedLineSet
            oFile.WriteEndElement();  // Shape
        }
    }
}