/*
 * NOTE: this source is almost a complete duplicate of the one used in XbimWindowsUI for handling Federated model
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Xbim.Common.Geometry;

namespace BIMRLDiffModelsCmd
{
    class XYZ
    {
        [JsonProperty("x")]
        public double X { get; set; }
        [JsonProperty("y")]
        public double Y { get; set; }
        [JsonProperty("z")]
        public double Z { get; set; }
        [JsonIgnore]
        public static XYZ Zero { get { return new XYZ(0.0, 0.0, 0.0); } }
        public XYZ(double XValue, double YValue, double ZValue)
        {
            X = XValue;
            Y = YValue;
            Z = ZValue;
        }
    }
    class ModelTransformInfo
    {
        [JsonProperty("translation")]
        public XYZ TranslationInfo { get; set; }
        [JsonProperty("scale")]
        public XYZ ScaleInfo { get; set; }
        [JsonProperty("rotation")]
        public XYZ RotationAxisInfo { get; set; }
        [JsonProperty("angle")]
        public double RotationAngle { get; set; } 

        public ModelTransformInfo()
        {
            // Initialize default values for the ModelTransformInfo
            TranslationInfo = XYZ.Zero;
            ScaleInfo = new XYZ(1.0, 1.0, 1.0);
            RotationAxisInfo = new XYZ(0.0, 0.0, 1.0);
            RotationAngle = 0.0;
        }
    }

    class FederationModelItem
    {
        [JsonProperty("modelfilename")]
        public string ModelFileName { get; set; }
        [JsonProperty("modeltransform")]
        public ModelTransformInfo ModelTransform { get; set; }
        /// <summary>
        /// This is an additional transform to alter the ref model relative to the rest relative to the (0,0,0), invloving Translation, Scale, and Rotation
        /// </summary>
        [JsonIgnore]
        XbimMatrix3D additionalModelTransform
        {
            get
            {
            if (ModelTransform == null)
                ModelTransform = new ModelTransformInfo();

            XbimVector3D rotAxis = new XbimVector3D(ModelTransform.RotationAxisInfo.X, ModelTransform.RotationAxisInfo.Y, ModelTransform.RotationAxisInfo.Z);
            rotAxis = rotAxis.Normalized();
            //!!!! Not sure that this is correct (follow row? or column?)
            double rx = ModelTransform.RotationAxisInfo.X;
            double ry = ModelTransform.RotationAxisInfo.Y;
            double rz = ModelTransform.RotationAxisInfo.Z;
            double ra = (ModelTransform.RotationAngle / 180) * Math.PI;
            double m11 = rx * rx + (1 - (rx * rx)) * Math.Cos(ra);
            double m12 = rx * ry * (1 - Math.Cos(ra)) - rz * Math.Sin(ra);
            double m13 = rx * rz * (1 - Math.Cos(ra)) + ry * Math.Sin(ra);
            double m14 = 0.0;
            double m21 = rx * ry * (1 - Math.Cos(ra)) + rz * Math.Sin(ra);
            double m22 = ry * ry + (1 - (ry * ry)) * Math.Cos(ra);
            double m23 = ry * rz * (1 - Math.Cos(ra)) - rx * Math.Sin(ra);
            double m24 = 0.0;
            double m31 = rx * rz * (1 - Math.Cos(ra)) - ry * Math.Sin(ra);
            double m32 = ry * rz * (1 - Math.Cos(ra)) + rx * Math.Sin(ra);
            double m33 = rz * rz + (1 - (rz * rz)) * Math.Cos(ra);
            double m34 = 0.0;
            double m41 = 0.0;
            double m42 = 0.0;
            double m43 = 0.0;
            double m44 = 1.0;
            XbimMatrix3D rotMtx = new XbimMatrix3D(m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44);

            XbimMatrix3D scaleMtx = XbimMatrix3D.CreateScale(ModelTransform.ScaleInfo.X, ModelTransform.ScaleInfo.Y, ModelTransform.ScaleInfo.Z);
            XbimMatrix3D transMtx = XbimMatrix3D.CreateTranslation(ModelTransform.TranslationInfo.X, ModelTransform.TranslationInfo.Y, ModelTransform.TranslationInfo.Z);

            XbimMatrix3D m3D = XbimMatrix3D.Multiply(rotMtx, scaleMtx);
            m3D = XbimMatrix3D.Multiply(m3D, transMtx);
            return m3D;
            }
        }
    }


    class XBimFederation
    {
        [JsonProperty("federationname")]
        public string FederationName { get; set; }
        [JsonProperty("description")]
        public string FederationDescription { get; set; }
        [JsonProperty("creationdate")]
        public DateTime CreationDate { get; set; }
        [JsonProperty("createdby")]
        public string CreatedBy { get; set; }
        [JsonProperty("modifieddate")]
        public DateTime ModifiedDate { get; set; }
        [JsonProperty("modifiedby")]
        public string ModifiedBy { get; set; }
        [JsonProperty("userelativepath")]
        public bool UseRelativePath { get; set; }
        [JsonProperty("membermodels")]
        IDictionary<string, FederationModelItem> MemberModels { get; set; }
        [JsonIgnore]
        public string fedFilePath { get; set; }

        public IList<FederationModelItem> GetMemberModelList()
        {
            string currDir = Directory.GetCurrentDirectory();
            if (UseRelativePath)
            {
                string fedFileLoc = Path.GetDirectoryName(fedFilePath);
                // If it is an empty string, it is located in the current directory
                if (string.IsNullOrEmpty(fedFileLoc))
                  fedFileLoc = @".\";
                Directory.SetCurrentDirectory(fedFileLoc);
            }

            IList<FederationModelItem> items = new List<FederationModelItem>();
            foreach (KeyValuePair<string,FederationModelItem> modelItem in MemberModels)
            {
                if (UseRelativePath)
                {
                    string fileName = Path.GetFullPath(modelItem.Value.ModelFileName);
                    FederationModelItem item = modelItem.Value;
                    item.ModelFileName = fileName;
                    items.Add(item);
                }
                else
                    items.Add(modelItem.Value);
            }

            if (UseRelativePath)
                Directory.SetCurrentDirectory(currDir);

            return items;
        }

        public XBimFederation(string federationName, string description)
        {
            FederationName = federationName;
            FederationDescription = description;
            MemberModels = new Dictionary<string,FederationModelItem>();
        }

        public void Add(string modelFile)
        {
            if (MemberModels.ContainsKey(modelFile))
                return;
            FederationModelItem mItem = new FederationModelItem();
            mItem.ModelFileName = modelFile;
            mItem.ModelTransform = new ModelTransformInfo();
            MemberModels.Add(modelFile, mItem);
        }

        public void Add(string modelFile, ModelTransformInfo modelTrf)
        {
            if (MemberModels.ContainsKey(modelFile))
            return;
            FederationModelItem mItem = new FederationModelItem();
            mItem.ModelFileName = modelFile;
            mItem.ModelTransform = modelTrf;
            MemberModels.Add(modelFile, mItem);
        }

        public void Remove(string modelFile)
        {
            string model = modelFile;
            if (!MemberModels.ContainsKey(model))
            {
                if (UseRelativePath)
                {
                    model = XBimFederation.relativePath(Path.GetDirectoryName(fedFilePath), model);
                    if (!MemberModels.ContainsKey(model))
                        return;
                }
            }
            MemberModels.Remove(model);
        }

        public void Save(string fileName, bool firstTime=false)
        {
            if (firstTime)
            {
                CreationDate = DateTime.Now;
                CreatedBy = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            }
            else
            {
                ModifiedBy = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                ModifiedDate = DateTime.Now;
            }

            string fedModelInfo = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (StreamWriter file = File.CreateText(fileName))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, this);
            }
        }

        [JsonIgnore]
        public IList<string> MemberFileList
        {
            get
            {
                IList<string> fileList = new List<string>();
                foreach (FederationModelItem item in GetMemberModelList())
                {
                    fileList.Add(item.ModelFileName);
                }
                return fileList;
            }
        }

        public ModelTransformInfo GetTransformAt(int index)
        {
            if (index > GetMemberModelList().Count())
                return null;
            return GetMemberModelList()[index].ModelTransform;
        }

      public static string relativePath(string refDir, string pathToUpdate)
      {
         if (string.IsNullOrEmpty(refDir) || string.IsNullOrEmpty(pathToUpdate))
            return pathToUpdate;

         string[] firstPathParts = refDir.Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
         string[] secondPathParts = pathToUpdate.Trim(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);

         int sameCounter = 0;
         for (int i = 0; i < Math.Min(firstPathParts.Length, secondPathParts.Length); i++)
         {
            if (string.Compare(firstPathParts[i], secondPathParts[i], ignoreCase: true) != 0)
            {
               break;
            }
            sameCounter++;
         }

         if (sameCounter == 0)
         {
            return pathToUpdate;
         }

         string relativePath = String.Empty;
         for (int i = sameCounter; i < firstPathParts.Length; i++)
         {
            if (i > sameCounter)
            {
               relativePath += Path.DirectorySeparatorChar;
            }
            relativePath += "..";
         }
         if (relativePath.Length == 0)
         {
            relativePath = ".";
         }
         for (int i = sameCounter; i < secondPathParts.Length; i++)
         {
            relativePath += Path.DirectorySeparatorChar;
            relativePath += secondPathParts[i];
         }

         return relativePath;
      }
   }
}
