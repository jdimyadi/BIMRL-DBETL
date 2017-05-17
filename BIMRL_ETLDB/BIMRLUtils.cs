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
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using Xbim.ModelGeometry.Scene;
using Xbim.ModelGeometry.Scene.Extensions;
using Xbim.Common.Geometry;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.MeasureResource;
using Oracle.DataAccess.Types;
using Oracle.DataAccess.Client;
using NetSdoGeometry;
using BIMRL.Common;

namespace BIMRL
{
   public static class BIMRLUtils
   {
      public static string checkSingleQuote(string source)
      {
         if (String.IsNullOrEmpty(source)) 
               return source;
         return source.Replace("'", "''");      // Oracle needs single quoe in the string to be escaped with another single quote
      }

      /// <summary>
      /// This function is to patch data, updating element's major axes and their OBB at the same time
      /// </summary>
      /// <param name="fedID"></param>
      /// <param name="whereCond"></param>
      public static void updateMajorAxesAndOBB(int fedID, string whereCond)
      {
         BIMRLCommon bimrlCommon = new BIMRLCommon();

         string sqlStmt = "SELECT ELEMENTID, GEOMETRYBODY FROM " + DBOperation.formatTabName("BIMRL_ELEMENT", fedID) + " WHERE GEOMETRYBODY IS NOT NULL";
         if (!string.IsNullOrEmpty(whereCond))
               sqlStmt += " AND " + whereCond;

         OracleCommand cmd = new OracleCommand(sqlStmt, DBOperation.DBConn);
         OracleDataReader reader = cmd.ExecuteReader();
         while (reader.Read())
         {
            string elementid = reader.GetString(0);
            SdoGeometry geom = reader.GetValue(1) as SdoGeometry;

            Polyhedron polyH;
            if (!SDOGeomUtils.generate_Polyhedron(geom, out polyH))
               continue;       // something wrong, unable to get the polyhedron, skip

            BIMRLGeometryPostProcess postProc = new BIMRLGeometryPostProcess(elementid, polyH, bimrlCommon, fedID, null);
            postProc.deriveMajorAxes();
            postProc.trueOBBFaces();
            postProc.projectedFaces();

            //// create OBB topo face information
            //if (postProc.OBB != null)
            //{
            //    Polyhedron obbGeom;
            //    if (SDOGeomUtils.generate_Polyhedron(postProc.OBB, out obbGeom))
            //    {
            //        BIMRLGeometryPostProcess processFaces = new BIMRLGeometryPostProcess(elementid, obbGeom, bimrlCommon, fedID, "OBB");
            //        processFaces.simplifyAndMergeFaces();
            //        processFaces.insertIntoDB(false);
            //    }
            //}
         }
      }

      public static void UpdateElementTransform(IfcStore _model, string projectNumber, string projectName)
      {
         DBOperation.beginTransaction();
         DBOperation.commitInterval = 5000;
         string currStep = string.Empty;
         BIMRLCommon _refBIMRLCommon = new BIMRLCommon();

         int commandStatus = -1;
         int currInsertCount = 0;

         OracleCommand command = new OracleCommand(" ", DBOperation.DBConn);
         XbimMatrix3D m3D = new XbimMatrix3D();

         if (string.IsNullOrEmpty(projectName))
            projectName = projectNumber + " - Federated";

         string modelName;
         if (!string.IsNullOrEmpty(_model.FileName))
            modelName = Path.GetFileNameWithoutExtension(_model.FileName);
         else
            modelName = projectNumber + " - " + projectName;

         command.CommandText = "SELECT FEDERATEDID FROM bimrl_federatedmodel WHERE MODELNAME = '" + modelName + "' AND PROJECTNUMBER='" + projectNumber + "' AND PROJECTNAME='" + projectName + "'";
         object oFedID = command.ExecuteScalar();
         if (oFedID == null)
            return;

         int fedID = int.Parse(oFedID.ToString());
         command.CommandText = "SELECT ELEMENTID, LINENO FROM " + DBOperation.formatTabName("bimrl_element", fedID) + " WHERE GEOMETRYBODY IS NOT NULL";
         OracleDataReader reader = command.ExecuteReader();
         SortedDictionary<int, string> elemList = new SortedDictionary<int, string>();

         while (reader.Read())
         {
            string elemid = reader.GetString(0);
            int lineNo = reader.GetInt32(1);
            elemList.Add(lineNo, elemid);
         }

         Xbim3DModelContext context = new Xbim3DModelContext(_model);

         foreach (KeyValuePair<int,string> elemListItem in elemList)
         {
            //IEnumerable<XbimGeometryData> geomDataList = _model.GetGeometryData(elemListItem.Key, XbimGeometryType.TriangulatedMesh);
            IIfcProduct product = _model.Instances[elemListItem.Key] as IIfcProduct;

            IEnumerable<XbimShapeInstance> shapeInstances = context.ShapeInstancesOf(product).Where(x => x.RepresentationType == XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded);
            if (shapeInstances.Count() == 0)
               continue;         // SKip if the product has no geometry

            XbimMeshGeometry3D prodGeom = new XbimMeshGeometry3D();
            IXbimShapeGeometryData shapeGeom = context.ShapeGeometry(shapeInstances.FirstOrDefault().ShapeGeometryLabel);
            //XbimModelExtensions.Read(prodGeom, shapeGeom.ShapeData, shapeInstances.FirstOrDefault().Transformation);

            //XbimGeometryData sdoGeomData = geomDataList.First();
            //m3D = sdoGeomData.Transform;
            //m3D = XbimMatrix3D.FromArray(sdoGeomData.DataArray2);       // Xbim 3.0 removes Tranform property
            m3D = shapeInstances.FirstOrDefault().Transformation;
            string sqlStmt = "update " + DBOperation.formatTabName("BIMRL_ELEMENT", fedID) + " set TRANSFORM_COL1=:1, TRANSFORM_COL2=:2, TRANSFORM_COL3=:3, TRANSFORM_COL4=:4"
                           + " Where elementid = '" + elemListItem.Value + "'";
            // int status = DBOperation.updateGeometry(sqlStmt, sdoGeomData);
            currStep = sqlStmt;
            command.CommandText = sqlStmt;

            try
            {
               OracleParameter[] sdoGeom = new OracleParameter[4];
               for (int i = 0; i < sdoGeom.Count(); ++i)
               {
                  sdoGeom[i] = command.Parameters.Add((i + 1).ToString(), OracleDbType.Object);
                  sdoGeom[i].Direction = ParameterDirection.Input;
                  sdoGeom[i].UdtTypeName = "MDSYS.SDO_GEOMETRY";
                  sdoGeom[i].Size = 1;
               }

               SdoGeometry trcol1 = new SdoGeometry();
               trcol1.Dimensionality = 3;
               trcol1.LRS = 0;
               trcol1.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
               int gType = trcol1.PropertiesToGTYPE();
               SdoPoint trcol1V = new SdoPoint();
               trcol1V.XD = m3D.M11;
               trcol1V.YD = m3D.M12;
               trcol1V.ZD = m3D.M13;
               trcol1.SdoPoint = trcol1V;
               sdoGeom[1].Value = trcol1;

               SdoGeometry trcol2 = new SdoGeometry();
               trcol2.Dimensionality = 3;
               trcol2.LRS = 0;
               trcol2.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
               gType = trcol2.PropertiesToGTYPE();
               SdoPoint trcol2V = new SdoPoint();
               trcol2V.XD = m3D.M21;
               trcol2V.YD = m3D.M22;
               trcol2V.ZD = m3D.M23;
               trcol2.SdoPoint = trcol2V;
               sdoGeom[2].Value = trcol2;

               SdoGeometry trcol3 = new SdoGeometry();
               trcol3.Dimensionality = 3;
               trcol3.LRS = 0;
               trcol3.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
               gType = trcol3.PropertiesToGTYPE();
               SdoPoint trcol3V = new SdoPoint();
               trcol3V.XD = m3D.M31;
               trcol3V.YD = m3D.M32;
               trcol3V.ZD = m3D.M33;
               trcol3.SdoPoint = trcol3V;
               sdoGeom[3].Value = trcol3;

               SdoGeometry trcol4 = new SdoGeometry();
               trcol4.Dimensionality = 3;
               trcol4.LRS = 0;
               trcol4.GeometryType = (int)SdoGeometryTypes.GTYPE.POINT;
               gType = trcol4.PropertiesToGTYPE();
               SdoPoint trcol4V = new SdoPoint();
               trcol4V.XD = m3D.OffsetX;
               trcol4V.YD = m3D.OffsetY;
               trcol4V.ZD = m3D.OffsetZ;
               trcol4.SdoPoint = trcol4V;
               sdoGeom[4].Value = trcol4;

               commandStatus = command.ExecuteNonQuery();
               command.Parameters.Clear();

               currInsertCount++;

               if (currInsertCount % DBOperation.commitInterval == 0)
               {
                  //Do commit at interval but keep the long transaction (reopen)
                  DBOperation.commitTransaction();
               }
            }
            catch (OracleException e)
            {
               string excStr = "%%Error - " + e.Message + "\n\t" + currStep;
               _refBIMRLCommon.StackPushError(excStr);
               //command.Dispose();   // Log Oracle error and continue
               command = new OracleCommand(" ", DBOperation.DBConn);
               // throw;
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

      // Misc functions to handle IfcUnits
      static IDictionary<IfcSIUnitName, string> m_SIUnitNameRep = new Dictionary<IfcSIUnitName, string>();
      static IDictionary<IfcSIPrefix, string> m_SIPrefixRep = new Dictionary<IfcSIPrefix, string>();
      static IDictionary<string, string> m_ConversionBasedNameRep = new Dictionary<string, string>();
      static IDictionary<IfcUnitEnum, string> m_IfcProjectNamedUnitRep = new Dictionary<IfcUnitEnum, string>();
      static IDictionary<string, IfcUnitEnum> m_IfcPropTypeToNamedUnitEnum = new Dictionary<string, IfcUnitEnum>();
      static HashSet<string> m_PropTypeProcessedNoRep = new HashSet<string>();
      static IDictionary<IfcDerivedUnitEnum, string> m_IfcProjectDerivedUnitRep = new Dictionary<IfcDerivedUnitEnum, string>();
      static IDictionary<string, IfcDerivedUnitEnum> m_IfcPropTypeToDerivedUnitEnum = new Dictionary<string, IfcDerivedUnitEnum>();
      static string m_IfcProjectMonetaryUnit = string.Empty;

      public static void ResetIfcUnitDicts()
      {
         m_IfcProjectNamedUnitRep.Clear();
         m_IfcPropTypeToNamedUnitEnum.Clear();
         m_IfcProjectDerivedUnitRep.Clear();
         m_IfcPropTypeToDerivedUnitEnum.Clear();
         m_PropTypeProcessedNoRep.Clear();
         m_IfcProjectMonetaryUnit = string.Empty;
      }

      public static void setupUnitRep()
      {
         setupUnitNameRep();
         setupUnitPrefix();
         setupConversionBasedNameRep();
      }

      public static void setupUnitPrefix()
      {
         if (m_SIPrefixRep.Count == 0)
         {
            m_SIPrefixRep.Add(IfcSIPrefix.EXA, "E");
            m_SIPrefixRep.Add(IfcSIPrefix.PETA, "P");
            m_SIPrefixRep.Add(IfcSIPrefix.TERA, "T");
            m_SIPrefixRep.Add(IfcSIPrefix.GIGA, "G");
            m_SIPrefixRep.Add(IfcSIPrefix.MEGA, "M");
            m_SIPrefixRep.Add(IfcSIPrefix.KILO, "k");
            m_SIPrefixRep.Add(IfcSIPrefix.HECTO, "h");
            m_SIPrefixRep.Add(IfcSIPrefix.DECA, "da");
            m_SIPrefixRep.Add(IfcSIPrefix.DECI, "d");
            m_SIPrefixRep.Add(IfcSIPrefix.CENTI, "c");
            m_SIPrefixRep.Add(IfcSIPrefix.MILLI, "m");
            m_SIPrefixRep.Add(IfcSIPrefix.MICRO, "u");
            m_SIPrefixRep.Add(IfcSIPrefix.NANO, "n");
            m_SIPrefixRep.Add(IfcSIPrefix.PICO, "p");
            m_SIPrefixRep.Add(IfcSIPrefix.FEMTO, "f");
            m_SIPrefixRep.Add(IfcSIPrefix.ATTO, "a");
         }
      }

      public static void setupUnitNameRep()
      {
         if (m_SIUnitNameRep.Count == 0)
         {
            m_SIUnitNameRep.Add(IfcSIUnitName.AMPERE, "A");
            m_SIUnitNameRep.Add(IfcSIUnitName.BECQUEREL, "Bq");
            m_SIUnitNameRep.Add(IfcSIUnitName.CANDELA, "cd");
            m_SIUnitNameRep.Add(IfcSIUnitName.COULOMB, "C");
            m_SIUnitNameRep.Add(IfcSIUnitName.CUBIC_METRE, "m^3");
            m_SIUnitNameRep.Add(IfcSIUnitName.DEGREE_CELSIUS, "degC");
            m_SIUnitNameRep.Add(IfcSIUnitName.FARAD, "F");
            m_SIUnitNameRep.Add(IfcSIUnitName.GRAM, "g");
            m_SIUnitNameRep.Add(IfcSIUnitName.GRAY, "Gy");
            m_SIUnitNameRep.Add(IfcSIUnitName.HENRY, "H");
            m_SIUnitNameRep.Add(IfcSIUnitName.HERTZ, "Hz");
            m_SIUnitNameRep.Add(IfcSIUnitName.JOULE, "J");
            m_SIUnitNameRep.Add(IfcSIUnitName.KELVIN, "K");
            m_SIUnitNameRep.Add(IfcSIUnitName.LUMEN, "lm");
            m_SIUnitNameRep.Add(IfcSIUnitName.LUX, "lx");
            m_SIUnitNameRep.Add(IfcSIUnitName.METRE, "m");
            m_SIUnitNameRep.Add(IfcSIUnitName.MOLE, "mol");
            m_SIUnitNameRep.Add(IfcSIUnitName.NEWTON, "N");
            m_SIUnitNameRep.Add(IfcSIUnitName.OHM, "ohm");
            m_SIUnitNameRep.Add(IfcSIUnitName.PASCAL, "Pa");
            m_SIUnitNameRep.Add(IfcSIUnitName.RADIAN, "rad");
            m_SIUnitNameRep.Add(IfcSIUnitName.SECOND, "s");
            m_SIUnitNameRep.Add(IfcSIUnitName.SIEMENS, "S");
            m_SIUnitNameRep.Add(IfcSIUnitName.SIEVERT, "Sv");
            m_SIUnitNameRep.Add(IfcSIUnitName.SQUARE_METRE, "m^2");
            m_SIUnitNameRep.Add(IfcSIUnitName.STERADIAN, "sr");
            m_SIUnitNameRep.Add(IfcSIUnitName.TESLA, "T");
            m_SIUnitNameRep.Add(IfcSIUnitName.VOLT, "V");
            m_SIUnitNameRep.Add(IfcSIUnitName.WATT, "W");
            m_SIUnitNameRep.Add(IfcSIUnitName.WEBER, "Wb");
         }
      }

      public static void setupConversionBasedNameRep()
      {
         if (m_ConversionBasedNameRep.Count == 0)
         {
            m_ConversionBasedNameRep.Add("INCH", "in");
            m_ConversionBasedNameRep.Add("FOOT", "ft");
            m_ConversionBasedNameRep.Add("YARD", "yd");
            m_ConversionBasedNameRep.Add("MILE", "mi");
            m_ConversionBasedNameRep.Add("SQUARE INCH", "in^2");
            m_ConversionBasedNameRep.Add("SQUARE FOOT", "ft^2");
            m_ConversionBasedNameRep.Add("SQUARE YARD", "yd^2");
            m_ConversionBasedNameRep.Add("ACRE", "ac");
            m_ConversionBasedNameRep.Add("SQUARE MILE", "mi^2");
            m_ConversionBasedNameRep.Add("CUBIC INCH", "in^3");
            m_ConversionBasedNameRep.Add("CUBIC FOOT", "ft^3");
            m_ConversionBasedNameRep.Add("CUBIC YARD", "yd^3");
            m_ConversionBasedNameRep.Add("LITRE", "l");
            m_ConversionBasedNameRep.Add("FLUID OUNCE UK", "fl oz");
            m_ConversionBasedNameRep.Add("FLUID OUNCE US", "fl oz");
            m_ConversionBasedNameRep.Add("PINT UK", "pint");
            m_ConversionBasedNameRep.Add("PINT US", "pint");
            m_ConversionBasedNameRep.Add("GALLON UK", "gal");
            m_ConversionBasedNameRep.Add("GALLON US", "gal");
            m_ConversionBasedNameRep.Add("DEGREE", "deg");
            m_ConversionBasedNameRep.Add("OUNCE", "oz");
            m_ConversionBasedNameRep.Add("POUND", "oz");
            m_ConversionBasedNameRep.Add("TON UK", "ton");
            m_ConversionBasedNameRep.Add("TON US", "ton");
            m_ConversionBasedNameRep.Add("LBF", "lbf");
            m_ConversionBasedNameRep.Add("KIP", "kip");
            m_ConversionBasedNameRep.Add("PSI", "psi");
            m_ConversionBasedNameRep.Add("KSI", "ksi");
            m_ConversionBasedNameRep.Add("MINUTE", "M");
            m_ConversionBasedNameRep.Add("HOUR", "H");
            m_ConversionBasedNameRep.Add("DAY", "day");
            m_ConversionBasedNameRep.Add("BTU", "btu");
         }
      }

      public static void AddIfcProjectUnitDict(IIfcUnit unitDef)
      {
         // Initialize the static Dicts, if it is still empty upon the first use. These Dicts do not need to be reset
         setupUnitRep();

         IfcUnitEnum namedUnitType;
         IfcDerivedUnitEnum derivedUnitType;
         string unitRepStr = string.Empty;

         try
         {
            if (unitDef is IIfcMonetaryUnit)
            {
               m_IfcProjectMonetaryUnit = (unitDef as IIfcMonetaryUnit).Currency.ToString();
            }
            else if (unitDef is IIfcNamedUnit)
            {
               if (getNamedUnitRepStr(unitDef, out namedUnitType, out unitRepStr))
                  if (!m_IfcProjectNamedUnitRep.ContainsKey(namedUnitType))
                     m_IfcProjectNamedUnitRep.Add(namedUnitType, unitRepStr);
            }
            else if (unitDef is IIfcDerivedUnit)
            {
               if (getDerivedUnitRepStr(unitDef, out derivedUnitType, out unitRepStr))
                  if (!m_IfcProjectDerivedUnitRep.ContainsKey(derivedUnitType))
                     m_IfcProjectDerivedUnitRep.Add(derivedUnitType, unitRepStr);
            }
         }
         catch
         {
            // Ignore error (likely due to existing entry)
         }
      }

      static bool getNamedUnitRepStr(IIfcUnit unitDef, out IfcUnitEnum unitType, out string unitRepStr)
      {
         // Initialize the static Dicts, if it is still empty upon the first use. These Dicts do not need to be reset
         setupUnitRep();

         unitType = IfcUnitEnum.LENGTHUNIT;     // initial value
         unitRepStr = string.Empty;
         if (unitDef is IIfcContextDependentUnit)
         {
            // Not supported yet at this time
         }
         else if (unitDef is IIfcConversionBasedUnit)
         {
            unitType = ((IIfcConversionBasedUnit)unitDef).UnitType;
            unitRepStr = getConversionBasedUnitRepStr(unitDef);
            if (!string.IsNullOrEmpty(unitRepStr))
               return true;
         }
         else if (unitDef is IIfcSIUnit)
         {
            unitType = ((IIfcSIUnit)unitDef).UnitType;
            unitRepStr = getSIUnitRepStr(unitDef);
            if (!string.IsNullOrEmpty(unitRepStr))
               return true;
         }
         return false;
      }

      static bool getDerivedUnitRepStr(IIfcUnit unitDef, out IfcDerivedUnitEnum unitType, out string unitRepStr)
      {
         // Initialize the static Dicts, if it is still empty upon the first use. These Dicts do not need to be reset
         setupUnitRep();

         unitType = IfcDerivedUnitEnum.USERDEFINED;      // initial value
         IIfcDerivedUnit derivedUnit = unitDef as IIfcDerivedUnit;
         unitType = derivedUnit.UnitType;

         unitRepStr = string.Empty;
         IList<string> positiveExpUnits = new List<string>();
         IList<string> negativeExpUnits = new List<string>();
         foreach (IIfcDerivedUnitElement dUnitElem in derivedUnit.Elements)
         {
            IfcUnitEnum elemUnitType;
            string elemUnitRepStr = string.Empty;
            int exponent = (int)dUnitElem.Exponent;
            if (getNamedUnitRepStr(dUnitElem.Unit, out elemUnitType, out elemUnitRepStr))
            {
               if (exponent >= 0)
               {
                  if (exponent > 1)
                     elemUnitRepStr = elemUnitRepStr + "^" + exponent.ToString();
                  //elemUnitRepStr = elemUnitRepStr + unicodeSuperScript(exponent);
                  positiveExpUnits.Add(elemUnitRepStr);
               }
               else
               {
                  if (exponent < -1)
                     elemUnitRepStr = elemUnitRepStr + "^" + Math.Abs(exponent).ToString();
                  //elemUnitRepStr = elemUnitRepStr + unicodeSuperScript(Math.Abs(exponent));
                  negativeExpUnits.Add(elemUnitRepStr);
               }
            }
         }

         if (positiveExpUnits.Count > 0)
         {
            foreach (string elemUnit in positiveExpUnits)
            {
               BIMRLCommon.appendToString(elemUnit, ".", ref unitRepStr);
               //BAIFCCommon.appendToString(elemUnit, "\u22C5", ref negUnitRepStr);
            }
         }
         else
         {
            if (negativeExpUnits.Count > 0)
               unitRepStr = "1";
         }

         string negUnitRepStr = string.Empty;
         if (negativeExpUnits.Count > 0)
         {
            foreach (string elemUnit in negativeExpUnits)
            {
               BIMRLCommon.appendToString(elemUnit, "·", ref negUnitRepStr);
               //BAIFCCommon.appendToString(elemUnit, "\u22C5", ref negUnitRepStr);
            }
         }
         if (!string.IsNullOrEmpty(negUnitRepStr))
            unitRepStr += "/" + negUnitRepStr;

         if (!string.IsNullOrEmpty(unitRepStr))
            return true;
         else
            return false;
      }

      static string getSIUnitRepStr(IIfcUnit unitDef)
      {
         // Initialize the static Dicts, if it is still empty upon the first use. These Dicts do not need to be reset
         setupUnitRep();

         IIfcSIUnit siUnit = unitDef as IIfcSIUnit;
         string unitRepStr = string.Empty;
         m_SIUnitNameRep.TryGetValue(siUnit.Name, out unitRepStr);
         if (siUnit.Prefix.HasValue)
         {
            string prefixStr = string.Empty;
            m_SIPrefixRep.TryGetValue(siUnit.Prefix.Value, out prefixStr);
            unitRepStr = prefixStr + unitRepStr;
         }
         return unitRepStr;
      }

      static string getConversionBasedUnitRepStr(IIfcUnit unitDef)
      {
         // Initialize the static Dicts, if it is still empty upon the first use. These Dicts do not need to be reset
         setupUnitRep();

         IIfcConversionBasedUnit convUnit = unitDef as IIfcConversionBasedUnit;
         string convUnitStr = convUnit.Name.ToString().ToUpper();
         string unitRepStr = string.Empty;
         m_ConversionBasedNameRep.TryGetValue(convUnitStr, out unitRepStr);
         //string unitRepStr = getIfcUnitStr(convUnit.ConversionFactor.UnitComponent);
         return unitRepStr;
      }

      public static string getIfcUnitStr(IIfcUnit unitDef)
      {
         // Initialize the static Dicts, if it is still empty upon the first use. These Dicts do not need to be reset
         setupUnitRep();

         IfcUnitEnum namedUnitType;
         IfcDerivedUnitEnum derivedUnitType;
         string unitRepStr = string.Empty;

         if (unitDef is IIfcMonetaryUnit)
         {
            return m_IfcProjectMonetaryUnit;
         }
         else if (unitDef is IIfcNamedUnit)
         {
            if (getNamedUnitRepStr(unitDef, out namedUnitType, out unitRepStr))
               return unitRepStr;
         }
         else if (unitDef is IIfcDerivedUnit)
         {
            if (getDerivedUnitRepStr(unitDef, out derivedUnitType, out unitRepStr))
               return unitRepStr;
         }
         return null;
      }

      public static string getDefaultIfcUnitStr(IIfcValue value)
      {
         // Initialize the static Dicts, if it is still empty upon the first use. These Dicts do not need to be reset
         setupUnitRep();

         if (value != null)
            return getDefaultIfcUnitStr(value.GetType());
         else
            return null;
      }

      public static string getDefaultIfcUnitStr(Type propType)
      {
         // Initialize the static Dicts, if it is still empty upon the first use. These Dicts do not need to be reset
         setupUnitRep();

         string unitRepStr = string.Empty;
         // We will first try MeasureUnit
         // From IfcValue.IfcMeasureValue 
         if (propType == typeof(IfcPositivePlaneAngleMeasure) || propType == typeof(IfcPlaneAngleMeasure)
               || propType == typeof(IfcCompoundPlaneAngleMeasure))
            m_IfcProjectNamedUnitRep.TryGetValue(IfcUnitEnum.PLANEANGLEUNIT, out unitRepStr);
         else if (propType == typeof(IfcPositiveLengthMeasure) || propType == typeof(IfcLengthMeasure)
                  || propType == typeof(IfcNonNegativeLengthMeasure))
            m_IfcProjectNamedUnitRep.TryGetValue(IfcUnitEnum.LENGTHUNIT, out unitRepStr);
         else
         {
            IfcUnitEnum? uEnum = getUnitEnum(propType.Name);
            if (uEnum.HasValue)
               m_IfcProjectNamedUnitRep.TryGetValue(uEnum.Value, out unitRepStr);
         }

         if (string.IsNullOrEmpty(unitRepStr))
         {
            IfcDerivedUnitEnum? dEnum = getDerivedUnitEnum(propType.Name);
            if (dEnum.HasValue)
               m_IfcProjectDerivedUnitRep.TryGetValue(dEnum.Value, out unitRepStr);
         }

         return unitRepStr;
      }

      static IfcUnitEnum? getUnitEnum(string propType)
      {
         IfcUnitEnum? propUnitEnum = null;
         IfcUnitEnum propUnitEnumValue;
         // Skip if the prop type has been processed before and has no corresponding enum found
         if (!m_PropTypeProcessedNoRep.Contains(propType))
         {
            if (!m_IfcPropTypeToNamedUnitEnum.TryGetValue(propType, out propUnitEnumValue))
            {
               //string typeStr = (propType.Replace("IFC", "").Replace("MEASURE", "") + "UNIT").ToUpper();   // Trim the IFC and Measure
               string typeStr;
               if (string.Compare(propType, propType.Length - 7, "MEASURE", 0, 7, ignoreCase: true) == 0)
                  typeStr = propType.Substring(3, propType.Length - 10).ToUpper() + "UNIT";
               else
                  typeStr = propType.Substring(3, propType.Length - 3).ToUpper() + "UNIT";   // Trim the IFC and Measure
               IfcUnitEnum uEnum;
               if (Enum.TryParse<IfcUnitEnum>(typeStr, out uEnum))
               {
                  m_IfcPropTypeToNamedUnitEnum.Add(propType, uEnum);
                  propUnitEnum = uEnum;
               }
               else
               {
                  // The type has no associated Enum, keep it in HashSet so that we can skip it the next time without expensive EnumTryPass
                  m_PropTypeProcessedNoRep.Add(propType);
               }
            }
            else
               propUnitEnum = propUnitEnumValue;
         }
         return propUnitEnum;
      }

      static IfcDerivedUnitEnum? getDerivedUnitEnum(string propType)
      {
         IfcDerivedUnitEnum? propUnitEnum = null;
         IfcDerivedUnitEnum propUnitEnumValue;
         // Skip if the prop type has been processed before and has no corresponding enum found
         if (!m_PropTypeProcessedNoRep.Contains(propType))
         {
            if (!m_IfcPropTypeToDerivedUnitEnum.TryGetValue(propType, out propUnitEnumValue))
            {
               //string typeStr = (propType.Replace("IFC", "").Replace("MEASURE", "") + "UNIT").ToUpper();   // Trim the IFC and Measure
               string typeStr;
               if (string.Compare(propType, propType.Length - 7, "MEASURE", 0, 7, ignoreCase: true) == 0)
                  typeStr = propType.Substring(3, propType.Length - 10).ToUpper() + "UNIT";
               else
                  typeStr = propType.Substring(3, propType.Length - 3).ToUpper() + "UNIT";   // Trim the IFC and Measure
               IfcDerivedUnitEnum dEnum;
               if (Enum.TryParse<IfcDerivedUnitEnum>(typeStr, out dEnum))
               {
                  m_IfcPropTypeToDerivedUnitEnum.Add(propType, dEnum);
                  propUnitEnum = dEnum;
               }
               else
               {
                  // The type has no associated Enum, keep it in HashSet so that we can skip it the next time without expensive EnumTryPass
                  m_PropTypeProcessedNoRep.Add(propType);
               }
            }
            else
               propUnitEnum = propUnitEnumValue;
         }
         return propUnitEnum;
      }

   }
}
