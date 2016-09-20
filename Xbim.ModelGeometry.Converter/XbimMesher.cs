using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xbim.Common.Geometry;
using Xbim.Common.Logging;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.IO;
using Xbim.ModelGeometry.Scene;
using Xbim.XbimExtensions;
using Xbim.XbimExtensions.Interfaces;
using System.Diagnostics;
using System.Reflection;
using Xbim.Ifc2x3.GeometricModelResource;
using System.Runtime.InteropServices;

namespace Xbim.ModelGeometry.Converter
{
    public class XbimMesher
    {
        /// <summary>
        /// Maximum size of a geoemtric region in metres
        /// </summary>
        private const float MaxWorldSize = 200;

        static XbimMesher()
        {
            AssemblyResolver.HandleUnresolvedAssemblies();
        }

        private class MapData
        {
            public IXbimGeometryModel Geometry;
            public XbimMatrix3D Matrix;
            public IfcProduct Product;

            public void Clear()
            {
                this.Geometry = null;
                this.Product = null;
            }

            public MapData(IXbimGeometryModel geomModel, XbimMatrix3D m3d, IfcProduct product)
            {
                this.Geometry = geomModel;
                this.Matrix = m3d;
                this.Product = product;
            }
        }

        private class MapRefData
        {
            public int RepresentationLabel;
            public int EntityLabel;
            public short EntityTypeId;
            public XbimMatrix3D Matrix;
            public int SurfaceStyleLabel;

            public MapRefData(MapData toAdd)
            {
                RepresentationLabel = toAdd.Geometry.RepresentationLabel;
                EntityLabel = toAdd.Product.EntityLabel;
                EntityTypeId = IfcMetaData.IfcTypeId(toAdd.Product);
                SurfaceStyleLabel = toAdd.Geometry.SurfaceStyleLabel;
                Matrix = XbimMatrix3D.Multiply(toAdd.Geometry.Transform, toAdd.Matrix);
            }
        }

        /// <summary>
        /// Use this function on a Xbim Model that has just been creted from IFC content
        /// This will create the default 3D mesh geometry for all IfcProducts and add it to the model
        /// </summary>
        /// <param name="model"></param>
        public static void GenerateGeometry(XbimModel model, ILogger Logger = null, ReportProgressDelegate progDelegate = null)
        {
            //Create the geometry engine by reflection to allow dynamic loading of different binary platforms (32, 64 etc)
            Assembly assembly = AssemblyResolver.GetModelGeometryAssembly();
            if (assembly == null)
            {
                if (Logger != null)
                {
                    Logger.Error("Failed to load Xbim.ModelGeometry.OCC.dll Please ensure it is installed correctly");
                }
                return;
            }
            IXbimGeometryEngine engine = (IXbimGeometryEngine)assembly.CreateInstance("Xbim.ModelGeometry.XbimGeometryEngine");
            engine.Init(model);
            if (engine == null)
            {
                if (Logger != null)
                {
                    Logger.Error("Failed to create Xbim Geometry engine. Please ensure Xbim.ModelGeometry.OCC.dll is installed correctly");
                }
                return;
            }
           
            //now convert the geometry
            //IEnumerable<IfcProduct> toDraw = model.InstancesLocal.OfType<IfcProduct>().Where(t => !(t is IfcFeatureElement));
            IEnumerable<IfcProduct> toDraw = model.InstancesLocal.OfType<IfcProduct>();     // include feature element (i.e. opening)
            if (!toDraw.Any()) return; //nothing to do
            TransformGraph graph = new TransformGraph(model);
            //create a new dictionary to hold maps
            ConcurrentDictionary<int, Object> maps = new ConcurrentDictionary<int, Object>();
            //add everything that may have a representation
            graph.AddProducts(toDraw); //load the products as we will be accessing their geometry

            ConcurrentDictionary<int, MapData> mappedModels = new ConcurrentDictionary<int, MapData>();
            ConcurrentQueue<MapRefData> mapRefs = new ConcurrentQueue<MapRefData>();
            ConcurrentDictionary<int, int[]> written = new ConcurrentDictionary<int, int[]>();

            int tally = 0;
            int percentageParsed = 0;
            int total = graph.ProductNodes.Values.Count;

            try
            {
                //Dictionary<int, IXbimGeometryModel> solids = new Dictionary<int, IXbimGeometryModel>();
                //foreach (var item in model.Instances.OfType<IfcSolidModel>())
                //{
                //    IXbimGeometryModel geomModel = engine.GetGeometry3D(item, maps);
                //    solids.Add(item.EntityLabel, geomModel);
                //}

                //use parallel as this improves the OCC geometry generation greatly
                ParallelOptions opts = new ParallelOptions();
                opts.MaxDegreeOfParallelism = 16;
                XbimRect3D bounds = XbimRect3D.Empty;
                double deflection = 4;// model.GetModelFactors.DeflectionTolerance;
#if DOPARALLEL
                Parallel.ForEach<TransformNode>(graph.ProductNodes.Values, opts, node => //go over every node that represents a product
#else
                foreach (var node in graph.ProductNodes.Values)
#endif
                {
                    IfcProduct product = node.Product(model);
                    try
                    {
                        IXbimGeometryModel geomModel = engine.GetGeometry3D(product, maps);
                        if (geomModel != null)  //it has geometry
                        {
                            XbimMatrix3D m3d = node.WorldMatrix();
                            if (geomModel.IsMap) //do not process maps now
                            {
                                MapData toAdd = new MapData(geomModel, m3d, product);
                                if (!mappedModels.TryAdd(geomModel.RepresentationLabel, toAdd)) //get unique rep
                                    mapRefs.Enqueue(new MapRefData(toAdd)); //add ref
                            }
                            else
                            {
                                int[] geomIds;
                                XbimGeometryCursor geomTable = model.GetGeometryTable();

                                XbimLazyDBTransaction transaction = geomTable.BeginLazyTransaction();
                                if (written.TryGetValue(geomModel.RepresentationLabel, out geomIds))
                                {
                                    byte[] matrix = m3d.ToArray(true);
                                    short? typeId = IfcMetaData.IfcTypeId(product);
                                    foreach (var geomId in geomIds)
                                    {
                                        geomTable.AddMapGeometry(geomId, product.EntityLabel, typeId.Value, matrix, geomModel.SurfaceStyleLabel);
                                    }
                                }
                                else
                                {
                                    List<XbimTriangulatedModel> tm = geomModel.Mesh();
                                    XbimRect3D bb = geomModel.GetBoundingBox();

                                    byte[] matrix = m3d.ToArray(true);
                                    short? typeId = IfcMetaData.IfcTypeId(product);

                                    geomIds = new int[tm.Count + 1];
                                    geomIds[0] = geomTable.AddGeometry(product.EntityLabel, XbimGeometryType.BoundingBox, typeId.Value, matrix, bb.ToArray(), 0, geomModel.SurfaceStyleLabel);
                                    bb = XbimRect3D.TransformBy(bb, m3d);
                                    if (bounds.IsEmpty)
                                        bounds = bb;
                                    else
                                        bounds.Union(bb);
                                    short subPart = 0;
                                    foreach (XbimTriangulatedModel b in tm)
                                    {
                                        geomIds[subPart + 1] = geomTable.AddGeometry(product.EntityLabel, XbimGeometryType.TriangulatedMesh, typeId.Value, matrix, b.Triangles, subPart, b.SurfaceStyleLabel);
                                        subPart++;
                                    }

                                    //            Debug.Assert(written.TryAdd(geomModel.RepresentationLabel, geomIds));
                                    Interlocked.Increment(ref tally);
                                    if (progDelegate != null)
                                    {
                                        int newPercentage = Convert.ToInt32((double)tally / total * 100.0);
                                        if (newPercentage > percentageParsed)
                                        {
                                            percentageParsed = newPercentage;
                                            progDelegate(percentageParsed, "Converted");
                                        }
                                    }
                                }
                                transaction.Commit();
                                model.FreeTable(geomTable);

                            }
                        }
                        else
                        {
                            Interlocked.Increment(ref tally);
                        }
                    }
                    catch (Exception e1)
                    {
                        String message = String.Format("Error Triangulating product geometry of entity {0} - {1}",
                            product.EntityLabel,
                            product.ToString());
                        if (Logger != null) Logger.Warn(message, e1);
                    }
                }
#if DOPARALLEL
                );
#endif
                graph = null;

                // Debug.WriteLine(tally);
#if DOPARALLEL
                //now sort out maps again in parallel
                Parallel.ForEach<KeyValuePair<int, MapData>>(mappedModels, opts, map =>
#else
                foreach (var map in mappedModels)
#endif
                {
                    IXbimGeometryModel geomModel = map.Value.Geometry;
                    XbimMatrix3D m3d = map.Value.Matrix;
                    IfcProduct product = map.Value.Product;

                    //have we already written it?
                    int[] writtenGeomids = new int[0];
                    if (!written.TryAdd(geomModel.RepresentationLabel, writtenGeomids))
                    {
                        //make maps    
                        mapRefs.Enqueue(new MapRefData(map.Value)); //add ref
                    }
                    else
                    {
                        m3d = XbimMatrix3D.Multiply(geomModel.Transform, m3d);
                        WriteGeometry(model, written, geomModel, ref bounds, m3d, product, deflection);

                    }

                    Interlocked.Increment(ref tally);
                    if (progDelegate != null)
                    {
                        int newPercentage = Convert.ToInt32((double)tally / total * 100.0);
                        if (newPercentage > percentageParsed)
                        {
                            percentageParsed = newPercentage;
                            progDelegate(percentageParsed, "Converted");
                        }
                    }
                    map.Value.Clear(); //release any native memory we are finished with this
                }
#if DOPARALLEL
                );
#endif
                //clear up maps
                mappedModels.Clear();
                XbimGeometryCursor geomMapTable = model.GetGeometryTable();
                XbimLazyDBTransaction mapTrans = geomMapTable.BeginLazyTransaction();
                foreach (var map in mapRefs) //don't do this in parallel to avoid database thrashing as it is very fast
                {

                    int[] geomIds;
                    if (!written.TryGetValue(map.RepresentationLabel, out geomIds))
                    {
                        if (Logger != null) Logger.WarnFormat("A geometry mapped reference (#{0}) has been found that has no base geometry", map.RepresentationLabel);
                    }
                    else
                    {

                        byte[] matrix = map.Matrix.ToArray(true);
                        foreach (var geomId in geomIds)
                        {
                            geomMapTable.AddMapGeometry(geomId, map.EntityLabel, map.EntityTypeId, matrix, map.SurfaceStyleLabel);
                        }
                        mapTrans.Commit();
                        mapTrans.Begin();

                    }
                    Interlocked.Increment(ref tally);
                    if (progDelegate != null)
                    {
                        int newPercentage = Convert.ToInt32((double)tally / total * 100.0);
                        if (newPercentage > percentageParsed)
                        {
                            percentageParsed = newPercentage;
                            progDelegate(percentageParsed, "Converted");
                        }
                    }
                    if (tally % 100 == 100)
                    {
                        mapTrans.Commit();
                        mapTrans.Begin();
                    }

                }
                mapTrans.Commit();
                mapTrans.Begin();
                XbimRegionCollection regions = PartitionWorld(model, bounds);
                IfcProject project = model.IfcProject;
                int projectId = 0;
                if (project != null)
                    projectId = Math.Abs(project.EntityLabel);
                geomMapTable.AddGeometry(projectId, XbimGeometryType.Region, IfcMetaData.IfcTypeId(typeof(IfcProject)), XbimMatrix3D.Identity.ToArray(), regions.ToArray());

                mapTrans.Commit();
                model.FreeTable(geomMapTable);

            }
            catch (Exception e2)
            {
                if (Logger != null) Logger.Warn("General Error Triangulating geometry", e2);
            }
            finally
            {

            }
        }

        private static XbimRegionCollection PartitionWorld(XbimModel model, XbimRect3D bounds)
        {
            float metre = (float)model.GetModelFactors.OneMetre;
            XbimRegionCollection regions = new XbimRegionCollection();
            if (bounds.Length() / metre <= MaxWorldSize)
            {
                regions.Add(new XbimRegion("All", bounds, -1));
            }
            else //need to partition the model
            {

                XbimOctree<int> octree = new XbimOctree<int>(bounds.Length(), MaxWorldSize * metre, 1f, bounds.Centroid());
                foreach (var geomData in model.GetGeometryData(XbimGeometryType.BoundingBox))
                {
                    XbimRect3D bound = XbimRect3D.FromArray(geomData.ShapeData);
                    XbimMatrix3D m3D = geomData.Transform;
                    bound = XbimRect3D.TransformBy(bound, m3D);
                    octree.Add(geomData.GeometryLabel, bound);
                }
                int i = 1;
                foreach (var item in octree.Populated)
                {
                    regions.Add(new XbimRegion("Region " + i++, item.ContentBounds(), item.ContentIncludingChildContent().Count()));
                }

            }
            return regions;
        }

        private static void WriteGeometry(XbimModel model, ConcurrentDictionary<int, int[]> written, IXbimGeometryModel geomModel, ref XbimRect3D bounds, XbimMatrix3D m3d, IfcProduct product, double deflection)
        {
            List<XbimTriangulatedModel> tm = geomModel.Mesh(true, deflection);
            XbimRect3D bb = geomModel.GetBoundingBox();
            byte[] matrix = m3d.ToArray(true);
            short? typeId = IfcMetaData.IfcTypeId(product);
            XbimGeometryCursor geomTable = model.GetGeometryTable();

            XbimLazyDBTransaction transaction = geomTable.BeginLazyTransaction();
            int[] geomIds = new int[tm.Count + 1];
            geomIds[0] = geomTable.AddGeometry(product.EntityLabel, XbimGeometryType.BoundingBox, typeId.Value, matrix, bb.ToArray(), 0, geomModel.SurfaceStyleLabel);

            bb = XbimRect3D.TransformBy(bb, m3d);

            if (bounds.IsEmpty)
                bounds = bb;
            else
                bounds.Union(bb);
            short subPart = 0;
            foreach (XbimTriangulatedModel b in tm)
            {
                geomIds[subPart + 1] = geomTable.AddGeometry(product.EntityLabel, XbimGeometryType.TriangulatedMesh, typeId.Value, matrix, b.Triangles, subPart, b.SurfaceStyleLabel);
                subPart++;
            }
            transaction.Commit();
            written.AddOrUpdate(geomModel.RepresentationLabel, geomIds, (k, v) => v = geomIds);
            model.FreeTable(geomTable);

        }

        
        public static void InitGeometryEngine(string basePath)
        {

            Assembly assembly = AssemblyResolver.GetModelGeometryAssembly(basePath);
        }
    }
}
