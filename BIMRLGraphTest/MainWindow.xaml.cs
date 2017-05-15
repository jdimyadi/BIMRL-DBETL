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
using System.Windows.Navigation;
using System.Windows.Shapes;
using BIMRLGraph;
using BIMRL.Common;

namespace GraphTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool startNodeHasValue = false;
        bool endNodeHasValue = false;
        BuildGraph graph;
        int fedID = -1;

        public MainWindow()
        {
            InitializeComponent();
            if (string.IsNullOrEmpty(TextBox_StartNode.Text) || string.IsNullOrEmpty(TextBox_EndNode.Text))
                Button_ShortestPath.IsEnabled = false;
            if (string.IsNullOrEmpty(TextBox_kPaths.Text))
                Button_kShortestPath.IsEnabled = false;
                
        }

        private void Button_genCirculation_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TextBox_FedModelID.Text, out fedID))
                return;

            GraphData graphData = new GraphData();
            //graphData.createCirculationGraph(fedID);
            graphData.createCirculationGraph(fedID);
            if (GraphData.refBimrlCommon.BIMRLErrorStackCount > 0)
            {
                TextBox_Output.Clear();
                string wholeStack = string.Empty;
                while (true)
                {
                    wholeStack += GraphData.refBimrlCommon.StackPopError() + "\n";
                    if (GraphData.refBimrlCommon.BIMRLErrorStackCount == 0) break;
                }
                TextBox_Output.Text = wholeStack;
            }

            if (graph != null)
                graph = null;

        }

        private void Button_AdjacencyGraph_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(TextBox_FedModelID.Text, out fedID))
                return;

            GraphData graphData = new GraphData();
            // graphData.createAdjacencyGraph(fedID);
        }

        private void Button_ShortestPath_Click(object sender, RoutedEventArgs e)
        {
            // Generate the graph using Quickgraph first if it is not yet created
            if (graph == null)
            {
                if (fedID < 0)
                    if (!int.TryParse(TextBox_FedModelID.Text, out fedID))
                        return;
                graph = new BuildGraph(GraphData.refBimrlCommon);
                //graph.generateUndirectedGraph(fedID, "CIRCULATION_" + fedID.ToString("X4"));
                graph.generateBiDirectionalGraph(fedID, "CIRCULATION_" + fedID.ToString("X4"));
            }

            List<string> result = graph.getShortestPath(TextBox_StartNode.Text, TextBox_EndNode.Text);
            if (result.Count > 0)
            {
                TextBox_Output.Text = "";
                foreach (string res in result)
                    TextBox_Output.Text += res + "\n";
            }
            else
            {
                TextBox_Output.Text = "No result!";
            }

        }

        private void TextBox_StartNode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextBox_StartNode.Text))
                startNodeHasValue = true;
            if (startNodeHasValue && endNodeHasValue)
                Button_ShortestPath.IsEnabled = true;
        }

        private void TextBox_EndNode_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextBox_EndNode.Text))
                endNodeHasValue = true;
            if (startNodeHasValue && endNodeHasValue)
                Button_ShortestPath.IsEnabled = true;
        }

        int kPaths = 0;
        private void TextBox_kPaths_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TextBox_kPaths.Text, out kPaths))
            {
                Button_kShortestPath.IsEnabled = true;
            }
        }

        private void Button_kShortestPath_Click(object sender, RoutedEventArgs e)
        {
            // Generate the graph using Quickgraph first if it is not yet created
            if (graph == null)
            {
                if (fedID < 0)
                    if (!int.TryParse(TextBox_FedModelID.Text, out fedID))
                        return;
                graph = new BuildGraph(GraphData.refBimrlCommon);
                graph.generateBiDirectionalGraph(fedID, "CIRCULATION_" + fedID.ToString("X4"));
            }

            List<List<string>> results = graph.getkShortestPath(TextBox_StartNode.Text, TextBox_EndNode.Text, kPaths);
            if (results != null)
            {
                TextBox_Output.Text = "";
                int k = 1;
                foreach (List<string> result in results)
                {
                    TextBox_Output.Text += "** Path # " + k++.ToString() + " :\n";
                    foreach (string res in result)
                        TextBox_Output.Text += res + "\n";
                    TextBox_Output.Text += "\n";
                }
            }
            else
            {
                TextBox_Output.Text = "No result!";
            }
        }
    }
}
