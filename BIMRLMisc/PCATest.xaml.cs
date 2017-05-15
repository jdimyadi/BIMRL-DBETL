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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using BIMRL;
using BIMRL.OctreeLib;

namespace BIMRLMisc
{
    /// <summary>
    /// Interaction logic for PCATest.xaml
    /// </summary>
    public partial class PCATest : Window
    {
        List<Point3D> pointList = new List<Point3D>();
        PrincipalComponentAnalysis pca = null;
        List<Point3D> outputPList;
        public PCATest()
        {
            InitializeComponent();
            Button_inverseTransform.IsEnabled = false;
        }

        private void Button_Transform_Click(object sender, RoutedEventArgs e)
        {
            string[] tokens = TextBox_inputPointList.Text.Trim().Split(' ');
            
            if (tokens.Length < 3)
            {
                throw new Exception("No enough values!");
            }

            pointList.Clear();
            for (int i=0; i < Math.Floor((double)tokens.Length); i+=3)
            {
                Point3D p = new Point3D(double.Parse(tokens[i]), double.Parse(tokens[i+1]), double.Parse(tokens[i+2]));
                pointList.Add(p);
            }

            pca = new PrincipalComponentAnalysis(pointList);
            Vector3D[] pcaComp = pca.identifyMajorAxes();
            TextBox_PCAComponents.Text = pcaComp[0].ToString() + "\n" + pcaComp[1].ToString() + "\n" + pcaComp[2].ToString();
            Matrix3D invM = pca.transformMatrix.inverse();
            TextBox_TransformMatrices.Text = pca.transformMatrix.ToString() + "\n\n" + invM.ToString();
            Button_inverseTransform.IsEnabled = true;

            string resultStr = "";
            List<Point3D> resPList = new List<Point3D>();
            foreach (Point3D p in pointList)
            {
                Point3D trfP = pca.transformMatrix.Transform(p);
                resPList.Add(trfP);
                resultStr += trfP.X.ToString("N") + " " + trfP.Y.ToString("N") + " " + trfP.Z.ToString("N") + " ";
            }
            TextBox_TransformedPointList.Text = resultStr;
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Button_inverseTransform_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBox_TransformedPointList.Text) || pca == null)
            {
                return;
            }

            string[] tokens = TextBox_TransformedPointList.Text.Trim().Split(' ');

            if (tokens.Length < 3)
            {
                throw new Exception("No enough values!");
            }

            pointList.Clear();
            for (int i = 0; i < Math.Floor((double)tokens.Length); i += 3)
            {
                Point3D p = new Point3D(double.Parse(tokens[i]), double.Parse(tokens[i + 1]), double.Parse(tokens[i + 2]));
                pointList.Add(p);
            }

            string resultStr = "";
            Matrix3D invM = pca.transformMatrix.inverse();
            List<Point3D> resPList = new List<Point3D>();
            foreach (Point3D p in pointList)
            {
                Point3D trfP = invM.Transform(p);
                resPList.Add(trfP);
                resultStr += trfP.X.ToString("N") + " " + trfP.Y.ToString("N") + " " + trfP.Z.ToString("N") + " ";
            }
            TextBox_inputPointList.Text = resultStr;
        }
    }
}
