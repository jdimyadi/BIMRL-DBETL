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
using System.Globalization;

using BIMRL.OctreeLib;

namespace BIMRL.BIMDataTile
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        CellID64 theCellID;

        public MainWindow()
        {
            InitializeComponent();
        }

        private bool getPointOrVector3D(string input, out object retObj)
        {
            string tmpStr = input.Trim();
            double[] res = new double[3];
            retObj = null;

            bool isPoint = true;    // default will be point
            bool isVector = false;

            if (tmpStr.StartsWith("P") || tmpStr.StartsWith("p"))
            {
                isPoint = true;
                tmpStr = tmpStr.Replace("P", "");
                tmpStr = tmpStr.Replace("p", "");
            }
            if (tmpStr.StartsWith("V") || tmpStr.StartsWith("v") || tmpStr.StartsWith("N") || tmpStr.StartsWith("n"))
            {
                isVector = true;
                tmpStr = tmpStr.Replace("V", "");
                tmpStr = tmpStr.Replace("v", "");
                tmpStr = tmpStr.Replace("N", "");
                tmpStr = tmpStr.Replace("n", "");
            }

            tmpStr = tmpStr.Replace('[', ' ');
            tmpStr = tmpStr.Replace(']', ' ');

            string[] tokens = tmpStr.Trim().Split(',');
            if (tokens.Length < 3)
                return false;
            for (int i=0; i<tokens.Length; i++)
            {
                res[i] = Convert.ToDouble(tokens[i]);
            }
            if (isPoint)
            {
                Point3D theP = new Point3D(res[0], res[1], res[2]);
                retObj = theP;
                return true;
            }
            if (isVector)
            {
                Vector3D theV = new Vector3D(res[0], res[1], res[2]);
                retObj = theV;
                return true;
            }
            return true;
        }

        private bool getIndexedList(string input, out List<int> retObj)
        {
            List<int> ret = new List<int>();
            string tmpStr = input.Trim();

            string[] rowTokens = tmpStr.Trim().Split('\n');
            foreach (string rowTok in rowTokens)
            {
                string row = rowTok;
                row = rowTok.Replace('[', ' ');
                row = rowTok.Replace(']', ' ');

                string[] tokens = row.Trim().Split(',');
                foreach (string tok in tokens)
                    ret.Add(Convert.ToInt32(tok));

            }
            retObj = ret;
            return true;
        }

        private bool getMultiplePoint(string input, out List<object> retObj)
        {
            List<object> ret = new List<object>();
            string tmpStr = input.Trim();

            bool isPoint = true;    // default will be point
            bool isVector = false;


            string[] rowTokens = tmpStr.Trim().Split('\n');
            for (int i = 0; i < rowTokens.Length; i++)
            {
                string tmpRow = rowTokens[i].Trim();
                if (tmpRow.StartsWith("P") || tmpRow.StartsWith("p"))
                {
                    isPoint = true;
                    tmpRow = tmpRow.Replace("P", "");
                    tmpRow = tmpRow.Replace("p", "");
                }
                if (tmpRow.StartsWith("V") || tmpRow.StartsWith("v") || tmpRow.StartsWith("N") || tmpRow.StartsWith("n"))
                {
                    isVector = true;
                    tmpRow = tmpRow.Replace("V", "");
                    tmpRow = tmpRow.Replace("v", "");
                    tmpRow = tmpRow.Replace("N", "");
                    tmpRow = tmpRow.Replace("n", "");
                }
                tmpRow = tmpRow.Replace('[', ' ');
                tmpRow = tmpRow.Replace(']', ' ');

                string[] tokens = tmpRow.Trim().Split(',',' ');
                if (isPoint)
                {
                    Point3D p = new Point3D(Convert.ToDouble(tokens[0]), Convert.ToDouble(tokens[1]), Convert.ToDouble(tokens[2]));
                    ret.Add(p);
                }
                if (isVector)
                {
                    Vector3D v = new Vector3D(Convert.ToDouble(tokens[0]), Convert.ToDouble(tokens[1]), Convert.ToDouble(tokens[2]));
                    ret.Add(v);
                }
            }
            retObj = ret;
            return true;
        }

        private bool getSqMatrix(string input, int size, out double[,] result)
        {
            double[,] res = new double[size,size];
            result = res;
            string[] rowTokens = input.Trim().Split('\n');
            if (rowTokens.Length != size)
                return false;
            for (int i=0; i<rowTokens.Length; i++)
            {
                string[] tokens = rowTokens[i].Trim().Split(' ');
                if (tokens.Length != size)
                    return false;
                for (int j = 0; j < tokens.Length; j++)
                    res[i, j] = Convert.ToDouble(tokens[j]);
            }
            return true;
        }

        # region CellID_tests
        private void Button_StrToBin_click(object sender, RoutedEventArgs args)
        {
            cellIDBin_TextBox.Clear();

            theCellID = new CellID64(cellIDstr_TextBox.Text.PadLeft(12, '0'));
            cellIDHex_TextBox.Clear();
            cellIDHex_TextBox.Text = theCellID.iCellID.ToString("X");
            cellIDBin_TextBox.Clear();
            cellIDBin_TextBox.Text = theCellID.ToStringBinary();

            int xmin, xmax, ymin, ymax, zmin, zmax;
            CellID64.getCellIDComponents(theCellID, out xmin, out ymin, out zmin, out xmax, out ymax, out zmax);
            TextBox_CellIDComponents.Text = "Min [" + xmin.ToString() + "," + ymin.ToString() + "," + zmin.ToString() + "]\n"
                                            + "Max [" + xmax.ToString() + "," + ymax.ToString() + "," + zmax.ToString() + "]";
        }

        private void Butto_HexToStr_Click(object sender, RoutedEventArgs args)
        {
            UInt64 cellIDnum;
            string cellidHex;
            if (String.Compare(cellIDHex_TextBox.Text, 0, "0x", 0, 2, true) == 0)
                cellIDHex_TextBox.Text = cellIDHex_TextBox.Text.Substring(2);
            //                cellidHex = "0x" + cellIDHex_TextBox.Text.PadLeft(16, '0');
            cellidHex = cellIDHex_TextBox.Text;
            bool res = UInt64.TryParse(cellidHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out cellIDnum);
            theCellID = new CellID64(cellIDnum);
            cellIDHex_TextBox.Text = cellidHex;
            cellIDstr_TextBox.Clear();
            cellIDstr_TextBox.Text = theCellID.ToString();
            cellIDBin_TextBox.Clear();
            cellIDBin_TextBox.Text = theCellID.ToStringBinary();

            int xmin, xmax, ymin, ymax, zmin, zmax;
            CellID64.getCellIDComponents(theCellID, out xmin, out ymin, out zmin, out xmax, out ymax, out zmax);
            TextBox_CellIDComponents.Text = "Min [" + xmin.ToString() + "," + ymin.ToString() + "," + zmin.ToString() + "]\n"
                                            + "Max [" + xmax.ToString() + "," + ymax.ToString() + "," + zmax.ToString() + "]";

        }

        private void Button_GetParent_Click(object sender, RoutedEventArgs e)
        {
            CellID64 parentCell = CellID64.parentCell(theCellID);
            TextBox_parentcell_idstr.Text = parentCell.ToString();
            TextBox_parentcell_bin.Text = parentCell.ToStringBinary();
        }


        private void Button_GetIndexLoc_Click(object sender, RoutedEventArgs e)
        {
            List<object> pts = new List<object>();
            List<Point3D> pList = new List<Point3D>();
            bool stat = getMultiplePoint(TextBox_WorldBB.Text, out pts);
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i] is Point3D) pList.Add(pts[i] as Point3D);
            }

            if (pList.Count < 2)
                return;

            Octree.WorldBB = new BoundingBox3D(pList);
            List<Point3D> loc = CellID64.getCellIdxCorner(theCellID);
            TextBox_CellIdxLoc.Text = loc[0].ToString() + "\n" + loc[1].ToString();
        }

        private void Button_GetParentLIst_Click(object sender, RoutedEventArgs e)
        {
            List<string> cellList = Octree.parentCellList(theCellID);
            TextBox_parentList.Text = "";
            foreach (string cellStr in cellList)
            {
                TextBox_parentList.Text += cellStr + "\n" ;
            }
        }

        private void Button_GetContainingCell_Click(object sender, RoutedEventArgs e)
        {
            object pCoords;
            bool ret = getPointOrVector3D(TextBox_Position.Text, out pCoords);
            if (!(pCoords is Point3D))
                return;
            Point3D P1 = new Point3D((Point3D)pCoords);

            int depth;
            if (!int.TryParse(TextBox_ContCell_Depth.Text, out depth))
                return;

            CellID64 contCell = CellID64.cellAtDepth(P1, depth);
            TextBox_ContainingCell.Text = contCell.ToString();
        }

        private void Button_CellSearchCond_Click(object sender, RoutedEventArgs e)
        {
            TextBox_ParentCellCond.Text = Octree.parentsCellCondition(theCellID);
            TextBox_ChildrenCellCond.Text = Octree.childrenCellCondition(theCellID);
        }

        #endregion

        # region LineSegment_tests

        LineSegment3D _LS_LS1;
        LineSegment3D _LS_LS2;
        Point3D _LS_P;

        private void TabItem_LS_Initialized(object sender, EventArgs e)
        {
            TextBox_LS_Tol.Text = MathUtils.tol.ToString("E2");
            Button_LS_Intersect.IsEnabled = false;
            Button_LS_Overlap.IsEnabled = false;
            Button_LS_PoL.IsEnabled = false;
        }

        private void Button_LS_DefLS1_Click(object sender, RoutedEventArgs e)
        {
            List<object> input = new List<object>();
            bool stat = getMultiplePoint(TextBox_LS_DefLS1.Text, out input);
            if (input.Count < 2) return;
            _LS_LS1 = new LineSegment3D((Point3D)input[0], (Point3D)input[1]);
            TextBox_LS_Out1.Text = _LS_LS1.ToString();
        }

        private void Button_LS_DefPoint_Click(object sender, RoutedEventArgs e)
        {
            object input = null;
            bool stat = getPointOrVector3D(TextBox_LS_DefPoint.Text, out input);
            if (input == null) return;
            _LS_P = new Point3D((Point3D)input);
            Label_LS_Out2.Content = "Point";
            TextBox_LS_Out2.Text = _LS_P.ToString();
            Button_LS_PoL.IsEnabled = true;
        }

        private void Button_LS_DefLS2_Click(object sender, RoutedEventArgs e)
        {
            List<object> input = new List<object>();
            bool stat = getMultiplePoint(TextBox_LS_DefLS2.Text, out input);
            if (input.Count < 2) return;
            _LS_LS2 = new LineSegment3D((Point3D)input[0], (Point3D)input[1]);
            TextBox_LS_Out2.Text = _LS_LS2.ToString();
            Label_LS_Out2.Content = "LineSegment";
            Button_LS_Intersect.IsEnabled = true;
            Button_LS_Overlap.IsEnabled = true;
        }

        private void TextBox_LS_Tol_TextChanged(object sender, TextChangedEventArgs e)
        {
            MathUtils.tol = Convert.ToDouble(TextBox_LS_Tol.Text);
        }

        private void Button_LS_Intersect_Click(object sender, RoutedEventArgs e)
        {
            Point3D intP = new Point3D();
            LineSegmentIntersectEnum mode = LineSegmentIntersectEnum.Undefined;

            bool res = LineSegment3D.intersect(_LS_LS1, _LS_LS2, out intP, out mode);

            TextBox_LS_Result.Text = "";
            if (res)
            {
                if (mode == LineSegmentIntersectEnum.IntersectedWithinSegments)
                    TextBox_LS_Result.Text = String.Format("Intersect at: ") + intP.ToString();
            }
            else
            {
                if (mode == LineSegmentIntersectEnum.IntersectedOutsideSegments)
                    TextBox_LS_Result.Text = String.Format("Intersect occurs OUTSIDE the segment at: ") + intP.ToString();
                else
                    TextBox_LS_Result.Text = "No intersection";
            }

        }

        private void Button_LS_Overlap_Click(object sender, RoutedEventArgs e)
        {
            LineSegment3D ovlapSeg = new LineSegment3D(new Point3D(0, 0, 0), new Point3D(0, 0, 0));
            LineSegmentOverlapEnum mode;
            bool res = LineSegment3D.overlap(_LS_LS1, _LS_LS2, out ovlapSeg, out mode);
            TextBox_LS_Result.Text = "";
            if (res)
            {
                switch (mode)
                {
                    case LineSegmentOverlapEnum.PartiallyOverlap:
                        TextBox_LS_Result.Text = "Segments are PARTIALLY overlapped";
                        break;
                    case LineSegmentOverlapEnum.ExactlyOverlap:
                        TextBox_LS_Result.Text = "Segments are EXACTLY overlapped";
                        break;
                    case LineSegmentOverlapEnum.Touch:
                        TextBox_LS_Result.Text = "Segments are TOUCHING each other";
                        break;
                    case LineSegmentOverlapEnum.SubSegment:
                        TextBox_LS_Result.Text = "Line#2 is a sub-segment of Line#1";
                        break;
                    case LineSegmentOverlapEnum.SuperSegment:
                        TextBox_LS_Result.Text = "Line#1 is a sub-segment of Line#2";
                        break;
                    default:
                        TextBox_LS_Result.Text = "Unknown status";
                        break;
                }
            }
            else
            {
                TextBox_LS_Result.Text = "Segments do not overlap";
            }

        }

        private void Button_LS_PoL_Click(object sender, RoutedEventArgs e)
        {
            LineSegmentOnSegmentEnum mode = LineSegmentOnSegmentEnum.Undefined;

            bool res = LineSegment3D.isInSegment(_LS_LS1, _LS_P, out mode);
            TextBox_LS_Result.Text = " ";
            if (res)
            {
                switch (mode)
                {
                    case LineSegmentOnSegmentEnum.InsideSegment:
                        TextBox_LS_Result.Text = "Point lies WITHIN the segment";
                        break;
                    case LineSegmentOnSegmentEnum.CoincideEndSegment:
                        TextBox_LS_Result.Text = "Point COINCIDES at the end point of the segment";
                        break;
                    default:
                        TextBox_LS_Result.Text = "Unknown status";
                        break;
                }
            }
            else
            {
                if (mode == LineSegmentOnSegmentEnum.OutsideSegment)
                    TextBox_LS_Result.Text = "Point lies on the line OUTSIDE the segment";
                else
                    TextBox_LS_Result.Text = "Point is NOT anywhere on the line";
            }
        }
      #endregion

      #region Matrix_tests

      private void Button_M3x3_CP_Click(object sender, RoutedEventArgs e)
      {
         double[,] res = new double[3, 3];
         bool stat = getSqMatrix(TextBox_M3x3CP_L.Text, 3, out res);
         if (!stat)
            return;

         Matrix3x3 ML = new Matrix3x3(res[0, 0], res[0, 1], res[0, 2], res[1, 0], res[1, 1], res[1, 2], res[2, 0], res[2, 1], res[2, 2]);

         stat = getSqMatrix(TextBox_M3x3CP_R.Text, 3, out res);
         if (!stat)
            return;

         Matrix3x3 MR = new Matrix3x3(res[0, 0], res[0, 1], res[0, 2], res[1, 0], res[1, 1], res[1, 2], res[2, 0], res[2, 1], res[2, 2]);

         Matrix3x3 Mres = ML * MR;
         TextBox_M3x3CP_Result.Text = Mres.ToString();
      }

      private void Button_M4x4_CP_Click(object sender, RoutedEventArgs e)
      {
         double[,] res = new double[4, 4];
         bool stat = getSqMatrix(TextBox_M4x4CP_L.Text, 4, out res);
         if (!stat)
            return;

         Matrix3D ML = new Matrix3D(res[0, 0], res[0, 1], res[0, 2], res[0, 3], res[1, 0], res[1, 1], res[1, 2], res[1, 3],
                                       res[2, 0], res[2, 1], res[2, 2], res[2, 3], res[3, 0], res[3, 1], res[3, 2], res[3, 3]);

         stat = getSqMatrix(TextBox_M4x4CP_R.Text, 4, out res);
         if (!stat)
            return;

         Matrix3D MR = new Matrix3D(res[0, 0], res[0, 1], res[0, 2], res[0, 3], res[1, 0], res[1, 1], res[1, 2], res[1, 3],
                                       res[2, 0], res[2, 1], res[2, 2], res[2, 3], res[3, 0], res[3, 1], res[3, 2], res[3, 3]);

         Matrix3D Mres = ML * MR;
         TextBox_M4x4CP_Result.Text = Mres.ToString();
      }

      private void Button_MInv_RtoL_Click(object sender, RoutedEventArgs e)
        {
            double[,] res = new double[3, 3];
            bool stat = getSqMatrix(TextBox_M3x3Inv_R.Text, 3, out res);

            Matrix3x3 MR = new Matrix3x3(res[0, 0], res[0, 1], res[0, 2], res[1, 0], res[1, 1], res[1, 2], res[2, 0], res[2, 1], res[2, 2]);
            Matrix3x3 ML = MR.inverse;
            TextBox_M3x3Inv_L.Text = ML.ToString(); 
        }

        private void Button_MInv_LtoR_Click(object sender, RoutedEventArgs e)
        {
            double[,] res = new double[3, 3];
            bool stat = getSqMatrix(TextBox_M3x3Inv_L.Text, 3, out res);

            Matrix3x3 ML = new Matrix3x3(res[0, 0], res[0, 1], res[0, 2], res[1, 0], res[1, 1], res[1, 2], res[2, 0], res[2, 1], res[2, 2]);
            Matrix3x3 MR = ML.inverse;
            TextBox_M3x3Inv_R.Text = MR.ToString();
        }


      private void Button_M4x4Inv_RtoL_Click(object sender, RoutedEventArgs e)
      {
         double[,] res = new double[4, 4];
         bool stat = getSqMatrix(TextBox_M4x4Inv_R.Text, 4, out res);

         Matrix3D MR = new Matrix3D(res[0, 0], res[0, 1], res[0, 2], res[0, 3], res[1, 0], res[1, 1], res[1, 2], res[1, 3],
                                       res[2, 0], res[2, 1], res[2, 2], res[2, 3], res[3, 0], res[3, 1], res[3, 2], res[3, 3]);
         Matrix3D ML = MR.inverse();
         TextBox_M4x4Inv_L.Text = ML.ToString();
      }

      private void Button_M4x4Inv_LtoR_Click(object sender, RoutedEventArgs e)
      {
         double[,] res = new double[4, 4];
         bool stat = getSqMatrix(TextBox_M4x4Inv_L.Text, 4, out res);

         Matrix3D ML = new Matrix3D(res[0, 0], res[0, 1], res[0, 2], res[0, 3], res[1, 0], res[1, 1], res[1, 2], res[1, 3],
                                       res[2, 0], res[2, 1], res[2, 2], res[2, 3], res[3, 0], res[3, 1], res[3, 2], res[3, 3]);

         Matrix3D MR = ML.inverse();
         TextBox_M4x4Inv_R.Text = MR.ToString();
      }

      private void button_transform1_Click(object sender, RoutedEventArgs e)
      {
         double[,] res = new double[3, 3];
         bool stat = getSqMatrix(TextBox_M3x3Inv_R.Text, 3, out res);
         if (!stat)
            return;

         Matrix3x3 MR = new Matrix3x3(res[0, 0], res[0, 1], res[0, 2], res[1, 0], res[1, 1], res[1, 2], res[2, 0], res[2, 1], res[2, 2]);

         object pObj;
         if (!getPointOrVector3D(TextBox_Vector1.Text, out pObj))
            return;
         if (!(pObj is Point3D))
            return;

         Point3D p = pObj as Point3D;
         p.W = 1;
         Point3D trfRes = calc_transformed_3x3(MR, p);
         TextBox_Vector1_result.Text = trfRes.ToString();
      }

      private void button_transform2_Click(object sender, RoutedEventArgs e)
      {
         double[,] res = new double[3, 3];
         bool stat = getSqMatrix(TextBox_M3x3CP_Result.Text, 3, out res);
         if (!stat)
            return;

         Matrix3x3 MR = new Matrix3x3(res[0, 0], res[0, 1], res[0, 2], res[1, 0], res[1, 1], res[1, 2], res[2, 0], res[2, 1], res[2, 2]);

         object pObj;
         if (!getPointOrVector3D(TextBox_Vector2.Text, out pObj))
            return;
         if (!(pObj is Point3D))
            return;

         Point3D p = pObj as Point3D;
         p.W = 1;
         Point3D trfRes = calc_transformed_3x3(MR, p);
         TextBox_Vector2_result.Text = trfRes.ToString();
      }

      private void button_transform3_Click(object sender, RoutedEventArgs e)
      {
         double[,] res = new double[4, 4];
         bool stat = getSqMatrix(TextBox_M4x4CP_Result.Text, 4, out res);
         if (!stat)
            return;

         Matrix3D MR = new Matrix3D(res[0, 0], res[0, 1], res[0, 2], res[0, 3], res[1, 0], res[1, 1], res[1, 2], res[1, 3],
                                       res[2, 0], res[2, 1], res[2, 2], res[2, 3], res[3, 0], res[3, 1], res[3, 2], res[3, 3]);

         object pObj;
         if (!getPointOrVector3D(TextBox_Vector3.Text, out pObj))
            return;
         if (!(pObj is Point3D))
            return;

         Point3D pR = pObj as Point3D;
         pR.W = 1;
         pR.Transform(MR);
         TextBox_Vector3_result.Text = pR.ToString();
      }

      private void button_transform4_Click(object sender, RoutedEventArgs e)
      {
         double[,] res = new double[4, 4];
         bool stat = getSqMatrix(TextBox_M4x4Inv_R.Text, 4, out res);

         Matrix3D MR = new Matrix3D(res[0, 0], res[0, 1], res[0, 2], res[0, 3], res[1, 0], res[1, 1], res[1, 2], res[1, 3],
                                       res[2, 0], res[2, 1], res[2, 2], res[2, 3], res[3, 0], res[3, 1], res[3, 2], res[3, 3]);

         object pObj;
         if (!getPointOrVector3D(TextBox_Vector4.Text, out pObj))
            return;
         if (!(pObj is Point3D))
            return;

         Point3D pR = pObj as Point3D;
         pR.W = 1;
         pR.Transform(MR);
         TextBox_Vector4_result.Text = pR.ToString();
      }

      private Point3D calc_transformed_3x3(Matrix3x3 M3, Point3D vec)
      {
         Point3D res = new Point3D(vec);
         res.Transform(M3x3ToMatrix3D(M3));
         return res;
      }

      private Matrix3D M3x3ToMatrix3D(Matrix3x3 M3)
      {
         Matrix3D M3D = new Matrix3D(M3.M[0, 0], M3.M[0, 1], M3.M[0, 2], 0.0,
                                       M3.M[1, 0], M3.M[1, 1], M3.M[1, 2], 0.0,
                                       M3.M[2, 0], M3.M[2, 1], M3.M[2, 2], 0.0,
                                       M3.M[3, 0], M3.M[3, 1], M3.M[3, 2], 1);
         return M3D;
      }
      #endregion

      #region Face3D_tests
      private Face3D _F3D_Face1;
        private Face3D _F3D_Face2;
        private Point3D _F3D_Point;
        private LineSegment3D _F3D_LS;
        private enum F3D_selected_type
        {
            Point,
            LineSegment,
            Face
        }
        private F3D_selected_type _F3DSel;
        
        private void Button_F3D_PointDef_Click(object sender, RoutedEventArgs e)
        {
            object pCoords;
            bool ret = getPointOrVector3D(TextBox_F3D_PointDef.Text, out pCoords);
            if (!(pCoords is Point3D))
                return;
            Point3D P1 = new Point3D((Point3D) pCoords);
            _F3D_Point = P1;
            TextBox_F3D_Out2.Text = P1.ToString();
            _F3DSel = F3D_selected_type.Point;
            Label_F3D_Out.Content = _F3DSel.ToString();
            Button_F3D_InsideTest.IsEnabled = true;
        }

        private void Button_F3D_LSDef_Click(object sender, RoutedEventArgs e)
        {
            List<object> pts = new List<object>();
            List<Point3D> pList = new List<Point3D>();
            bool stat = getMultiplePoint(TextBox_F3D_LSDef.Text, out pts);
            for (int i = 0; i < pts.Count; i++ )
            {
                if (pts[i] is Point3D) pList.Add(pts[i] as Point3D);
            }

            if (pList.Count < 2)
                return;
            _F3D_LS = new LineSegment3D((Point3D)pts[0], (Point3D)pts[1]);
            TextBox_F3D_Out2.Text = _F3D_LS.ToString();
            _F3DSel = F3D_selected_type.LineSegment;
            Label_F3D_Out.Content = _F3DSel.ToString();
            Button_F3D_IntersectTest.IsEnabled = true;
            Button_F3D_InsideTest.IsEnabled = true;
        }

        private void Button_F3D_Face2Def_Click(object sender, RoutedEventArgs e)
        {
            List<object> pts = new List<object>();
            bool stat = getMultiplePoint(TextBox_F3D_Face2Def.Text, out pts);
            List<Point3D> inp = new List<Point3D>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i] is Point3D) inp.Add((Point3D)pts[i]);
            }
            if (inp.Count < 3)
                return;

            _F3D_Face2 = new Face3D(inp);
            TextBox_F3D_Out2.Text = _F3D_Face2.ToString();
            _F3DSel = F3D_selected_type.Face;
            Label_F3D_Out.Content = _F3DSel.ToString();
            Button_F3D_IntersectTest.IsEnabled = true;
            Button_F3D_InsideTest.IsEnabled = true;
        }

        private void Button_F3D_Face1Def_Click(object sender, RoutedEventArgs e)
        {
            List<object> verts = new List<object>();
            bool stat = getMultiplePoint(TextBox_F3D_Face1Def.Text, out verts);
            List<Point3D> inp = new List<Point3D>();
            for (int i = 0; i < verts.Count; i++)
            {
                inp.Add((Point3D)verts[i]);
            }
            _F3D_Face1 = new Face3D(inp);
            TextBox_F3D_OutFace1.Text = _F3D_Face1.ToString();
        }


        private void Button_F3D_InsideTest_Click(object sender, RoutedEventArgs e)
        {
            if (_F3DSel == F3D_selected_type.Point)
            {
                bool stat = Face3D.inside(_F3D_Face1, _F3D_Point);
                if (stat)
                    TextBox_F3D_Result.Text = "Point IS inside the Face";
                else
                    TextBox_F3D_Result.Text = "Point IS outside the Face";
            }
            else if (_F3DSel == F3D_selected_type.LineSegment)
            {
                bool stat = Face3D.inside(_F3D_Face1, _F3D_LS);
                if (stat)
                    TextBox_F3D_Result.Text = "LineSegment IS inside the Face";
                else
                    TextBox_F3D_Result.Text = "LineSegment IS outside the Face";
            }
            else if (_F3DSel == F3D_selected_type.Face)
            {
                bool stat = Face3D.inside(_F3D_Face1, _F3D_Face2);
                if (stat)
                    TextBox_F3D_Result.Text = "Face2 IS inside the Face";
                else
                    TextBox_F3D_Result.Text = "Face2 IS outside the Face";
            }
        }

        private void Button_F3D_IntersectTest_Click(object sender, RoutedEventArgs e)
        {
            if (_F3DSel == F3D_selected_type.LineSegment)
            {
                List<Point3D> inPts = new List<Point3D>();
                bool stat = Face3D.intersect(_F3D_Face1, _F3D_LS, out inPts);
                if (stat)
                {
                    TextBox_F3D_Result.Text = "LineSegment INTERSECTS the Face at ";
                    for (int i=0; i< inPts.Count; i++)
                    {
                        TextBox_F3D_Result.Text += "\n" + inPts[i].ToString();
                    }
                }
                else
                    TextBox_F3D_Result.Text = "LineSegment DOES NOT Intersect the Face";
            }
            else if (_F3DSel == F3D_selected_type.Face)
            {
                LineSegment3D inLS = new LineSegment3D(new Point3D(), new Point3D());
                FaceIntersectEnum mode;
                bool stat = Face3D.intersect(_F3D_Face1, _F3D_Face2, out inLS, out mode);
                if (stat)
                {
                    if (mode == FaceIntersectEnum.IntersectPartial)
                        TextBox_F3D_Result.Text = "Face2 INTERSECTS the Face at LineSegment \n" + inLS.ToString();
                    else if (mode == FaceIntersectEnum.Overlap)
                        TextBox_F3D_Result.Text = "Face2 OVERLAPS the Face (on the same plane)\n";
                    else
                        TextBox_F3D_Result.Text = "Face2 INTERSECTS the Face.\n";
                }
                else
                {
                    if (mode == FaceIntersectEnum.NoIntersectionParallel)
                        TextBox_F3D_Result.Text = "Face2 is PARALLEL with Face1, no Intersection";
                    else
                        TextBox_F3D_Result.Text = "Face2 DOES NOT Intersect the Face";
                }
            }
        }

        private void TextBox_F3D_Tol_TextChanged(object sender, TextChangedEventArgs e)
        {
            MathUtils.tol = Convert.ToDouble(TextBox_F3D_Tol.Text);
        }

        private void TabItem_Face_Initialized(object sender, EventArgs e)
        {
            TextBox_F3D_Tol.Text = MathUtils.tol.ToString("E2");
            Button_F3D_InsideTest.IsEnabled = false;
            Button_F3D_IntersectTest.IsEnabled = false;
        }

#endregion

        #region Plane3D_test
        private Plane3D _PL_PL1;
        private Plane3D _PL_PL2;
        Point3D _PL_P;
        LineSegment3D _PL_LS;
        Line3D _PL_L;
        Line3D _PL_Ray;
        Face3D _PL_F;

        enum PlaneTest_enum
        {
            Point,
            Line,
            LineSegment,
            Ray,
            Face,
            Plane
        }
        PlaneTest_enum applTest;

        private void TabItem_Plane_Initialized(object sender, EventArgs e)
        {
            TextBox_PL_Tol.Text = MathUtils.tol.ToString("E2");
            Button_PL_PoPTest.IsEnabled = false;
            Button_PL_Intersect.IsEnabled = false;
            Button_PL_Overlap.IsEnabled = false;
            Button_PL_Parallel.IsEnabled = false;
        }

        private void TextBox_PL_Tol_TextChanged(object sender, TextChangedEventArgs e)
        {
            MathUtils.tol = Convert.ToDouble(TextBox_PL_Tol.Text);
        }

        private void Button_PL_DefPoint_Click(object sender, RoutedEventArgs e)
        {
            object inp;
            bool stat = getPointOrVector3D(TextBox_PL_DefPoint.Text, out inp);
            _PL_P = (Point3D)inp;
            Button_PL_PoPTest.IsEnabled = true;
            applTest = PlaneTest_enum.Point;
            Label_PL_Out2.Content = applTest.ToString();
            TextBox_PL_Out2.Text = _PL_P.ToString();
        }

        private void Button_PL_DefLine_Click(object sender, RoutedEventArgs e)
        {
            List<object> inp = new List<object>();
            List<Point3D> pList = new List<Point3D>();
            List<Vector3D> vList = new List<Vector3D>();

            bool stat = getMultiplePoint(TextBox_PL_DefLine.Text, out inp);
            for (int i=0; i<inp.Count; i++)
            {
                if (inp[i] is Point3D) pList.Add(inp[i] as Point3D);
                if (inp[i] is Vector3D) vList.Add(inp[i] as Vector3D);
            }
            if (pList.Count >= 2) 
                _PL_L = new Line3D(pList[0], pList[1]);
            else if (pList.Count >= 1 && vList.Count >= 1) 
                _PL_L = new Line3D(pList[0], vList[0]);   // Only take the first items in each list
            else 
                return;

            Button_PL_Intersect.IsEnabled = true;
            applTest = PlaneTest_enum.Line;
            Label_PL_Out2.Content = applTest.ToString();
            TextBox_PL_Out2.Text = _PL_L.ToString();
        }

        private void Button_PL_DefLS_Click(object sender, RoutedEventArgs e)
        {
            List<object> inp = new List<object>();
            List<Point3D> pList = new List<Point3D>();

            bool stat = getMultiplePoint(TextBox_PL_DefLine.Text, out inp);
            for (int i = 0; i < inp.Count; i++)
            {
                if (inp[i] is Point3D) pList.Add(inp[i] as Point3D);
            }
            if (pList.Count >= 2)
                _PL_LS = new LineSegment3D (pList[0], pList[1]);
            else
                return;

            Button_PL_Intersect.IsEnabled = true;
            applTest = PlaneTest_enum.LineSegment;
            Label_PL_Out2.Content = applTest.ToString();
            TextBox_PL_Out2.Text = _PL_LS.ToString();
        }

        private void Button_PL_DefRay_Click(object sender, RoutedEventArgs e)
        {
            List<object> inp = new List<object>();
            List<Point3D> pList = new List<Point3D>();
            List<Vector3D> vList = new List<Vector3D>();

            bool stat = getMultiplePoint(TextBox_PL_DefLine.Text, out inp);
            for (int i = 0; i < inp.Count; i++)
            {
                if (inp[i] is Point3D) pList.Add(inp[i] as Point3D);
                if (inp[i] is Vector3D) vList.Add(inp[i] as Vector3D);
            }

            // Ray can only be defined with 1P + 1V
            if (pList.Count >= 1 && vList.Count >= 1)
                _PL_Ray = new Line3D(pList[0], vList[0]);   // Only take the first items in each list
            else
                return;

            Button_PL_Intersect.IsEnabled = true;
            applTest = PlaneTest_enum.Ray;
            Label_PL_Out2.Content = applTest.ToString();
            TextBox_PL_Out2.Text = _PL_Ray.ToString();
        }

        private void Button_PL_DefFace_Click(object sender, RoutedEventArgs e)
        {
            List<object> inp = new List<object>();
            List<Point3D> pList = new List<Point3D>();

            bool stat = getMultiplePoint(TextBox_PL_DefPlane2.Text, out inp);
            for (int i = 0; i < inp.Count; i++)
            {
                if (inp[i] is Point3D) pList.Add(inp[i] as Point3D);
            }
            if (pList.Count >= 3)
                _PL_F = new Face3D(pList);
            else
                return;

            Button_PL_Intersect.IsEnabled = true;
            applTest = PlaneTest_enum.Face;
            Label_PL_Out2.Content = applTest.ToString();
            TextBox_PL_Out2.Text = _PL_F.ToString();
        }

        private void Button_PL_DefPlane2_Click(object sender, RoutedEventArgs e)
        {
            List<object> inp = new List<object>();
            List<Point3D> pList = new List<Point3D>();
            List<Vector3D> vList = new List<Vector3D>();

            bool stat = getMultiplePoint(TextBox_PL_DefPlane2.Text, out inp);
            for (int i = 0; i < inp.Count; i++)
            {
                if (inp[i] is Point3D) pList.Add(inp[i] as Point3D);
                if (inp[i] is Vector3D) vList.Add(inp[i] as Vector3D);
            }

            if (pList.Count >= 3)       // Plane by 3 Points
                _PL_PL2 = new Plane3D(pList[0], pList[1], pList[2]);
            else if (pList.Count >= 1 && vList.Count >= 2)  // Plane by 1 Point and 2 Vectors on the Plane
                _PL_PL2 = new Plane3D(pList[0], vList[0], vList[1]);
            else if (pList.Count >= 1 && vList.Count >= 1)  // Plane by 1 Point and 1 Normal (this has to be befind the 1P + 2V!!)
                _PL_PL2 = new Plane3D(pList[0], vList[0]);
            else if (pList.Count >= 2 && vList.Count >= 1)  // Plane by 2 Points and 1 Vector on the Plane
                _PL_PL2 = new Plane3D(pList[0], pList[1], vList[0]);
            else
                return;

            Button_PL_Intersect.IsEnabled = true;
            Button_PL_Overlap.IsEnabled = true;
            Button_PL_Parallel.IsEnabled = true;
            applTest = PlaneTest_enum.Plane;
            Label_PL_Out2.Content = applTest.ToString();
            TextBox_PL_Out2.Text = _PL_PL2.ToString();
        }

        private void Button_PL_DefPlane1_Click(object sender, RoutedEventArgs e)
        {
            List<object> inp = new List<object>();
            List<Point3D> pList = new List<Point3D>();
            List<Vector3D> vList = new List<Vector3D>();

            bool stat = getMultiplePoint(TextBox_PL_DefPlane1.Text, out inp);
            for (int i = 0; i < inp.Count; i++)
            {
                if (inp[i] is Point3D) pList.Add(inp[i] as Point3D);
                if (inp[i] is Vector3D) vList.Add(inp[i] as Vector3D);
            }

            if (pList.Count >= 3)       // Plane by 3 Points
                _PL_PL1 = new Plane3D(pList[0], pList[1], pList[2]);
            else if (pList.Count >= 1 && vList.Count >= 2)  // Plane by 1 Point and 2 Vectors on the Plane
                _PL_PL1 = new Plane3D(pList[0], vList[0], vList[1]);
            else if (pList.Count >= 1 && vList.Count >= 1)  // Plane by 1 Point and 1 Normal (this has to be before the 1P + 2V!!)
                _PL_PL1 = new Plane3D(pList[0], vList[0]);
            else if (pList.Count >= 2 && vList.Count >= 1)  // Plane by 2 Points and 1 Vector on the Plane
                _PL_PL1 = new Plane3D(pList[0], pList[1], vList[0]);
            else
                return;

            TextBox_PL_Out1.Text = _PL_PL1.ToString();
        }

        private void Button_PL_PoPTest_Click(object sender, RoutedEventArgs e)
        {
            bool stat = Plane3D.pointOnPlane(_PL_PL1, _PL_P);
            if (stat)
                TextBox_PL_Result.Text = "Point is located on the Plane.";
            else
            {
                double dist;
                stat = Plane3D.pointOnPlane(_PL_PL1, _PL_P, out dist);
                TextBox_PL_Result.Text = "Point is NOT on the Plane. Point distance from the plane = " + dist.ToString();
            }
        }

        private void Button_PL_Intersect_Click(object sender, RoutedEventArgs e)
        {
            bool stat = false;
            switch (applTest)
            {
                case PlaneTest_enum.Line:
                    Point3D iPt = new Point3D();
                    stat = Plane3D.PLintersect(_PL_PL1, _PL_L, out iPt);
                    if (stat)
                        TextBox_PL_Result.Text = "Line INTERSECTS the Plane at: " + iPt.ToString();
                    else
                        TextBox_PL_Result.Text = "Line DOES NOT intersect the Plane!";
                    break;

                case PlaneTest_enum.Ray:
                    iPt = new Point3D();
                    stat = Plane3D.PLintersect(_PL_PL1, _PL_Ray, out iPt);
                    if (stat)
                        TextBox_PL_Result.Text = "Ray INTERSECTS the Plane at: " + iPt.ToString();
                    else
                        TextBox_PL_Result.Text = "Ray DOES NOT intersects the Plane!";
                    break;

                case PlaneTest_enum.LineSegment:
                    stat = Plane3D.PLintersect(_PL_PL1, _PL_LS, out iPt);
                    if (stat)
                        TextBox_PL_Result.Text = "Line Segment INTERSECTS the Plane at: " + iPt.ToString();
                    else
                        TextBox_PL_Result.Text = "Line Segment DOES NOT intersect the Plane!";
                    break;

                case PlaneTest_enum.Face:
                    LineSegment3D iLS;
                    stat = Plane3D.PPintersect(_PL_PL1, _PL_F, out iLS);
                    if (stat)
                        TextBox_PL_Result.Text = "Face INTERSECTS the Plane at: " + iLS.ToString();
                    else
                        TextBox_PL_Result.Text = "Face DOES NOT intersect the Plane!";
                    break;

                case PlaneTest_enum.Plane:
                    Line3D iL;
                    stat = Plane3D.PPintersect(_PL_PL1, _PL_PL2, out iL);
                    if (stat)
                        TextBox_PL_Result.Text = "Plane2 INTERSECTS the Plane at: " + iL.ToString();
                    else
                        TextBox_PL_Result.Text = "Plane2 DOES NOT intersect the Plane!";
                    break;

                default:
                    break;
            }

        }

        private void Button_PL_Overlap_Click(object sender, RoutedEventArgs e)
        {
            switch (applTest)
            {
                case PlaneTest_enum.Line:
                    bool stat = Plane3D.Overlaps(_PL_PL1, _PL_L);
                    if (stat)
                        TextBox_PL_Result.Text = "Line OVERLAPS the Plane1";
                    else
                        TextBox_PL_Result.Text = "Line DOES NOT overlap the Plane1 !";
                    break;

                case PlaneTest_enum.LineSegment:
                    stat = Plane3D.Overlaps(_PL_PL1, _PL_LS);
                    if (stat)
                        TextBox_PL_Result.Text = "LineSegment OVERLAPS the Plane1";
                    else
                        TextBox_PL_Result.Text = "LineSegment DOES NOT overlap the Plane1 !";
                    break;

                case PlaneTest_enum.Ray:
                    stat = Plane3D.Overlaps(_PL_PL1, _PL_Ray);
                    if (stat)
                        TextBox_PL_Result.Text = "Ray OVERLAPS the Plane1";
                    else
                        TextBox_PL_Result.Text = "Ray DOES NOT overlap the Plane1 !";
                    break;

                case PlaneTest_enum.Plane:
                    stat = Plane3D.Overlaps(_PL_PL1, _PL_PL2);
                    if (stat)
                        TextBox_PL_Result.Text = "Plane2 OVERLAPS the Plane1";
                    else
                        TextBox_PL_Result.Text = "Plane2 DOES NOT overlap the Plane1 !";
                    break;

                case PlaneTest_enum.Face:
                    stat = Plane3D.Overlaps(_PL_PL1, _PL_F);
                    if (stat)
                        TextBox_PL_Result.Text = "Face OVERLAPS the Plane1";
                    else
                        TextBox_PL_Result.Text = "Face DOES NOT overlap the Plane1 !";
                    break;

                default:
                    break;
            }

        }

        private void Button_PL_Parallel_Click(object sender, RoutedEventArgs e)
        {
            switch (applTest)
            {
                case PlaneTest_enum.Line:
                    bool stat = Plane3D.Parallels(_PL_PL1, _PL_L);
                    if (stat)
                        TextBox_PL_Result.Text = "Line is PARALLEL the Plane1";
                    else
                        TextBox_PL_Result.Text = "Line IS NOT parallel the Plane1 !";
                    break;

                case PlaneTest_enum.LineSegment:
                    stat = Plane3D.Overlaps(_PL_PL1, _PL_LS);
                    if (stat)
                        TextBox_PL_Result.Text = "LineSegment is PARALLEL the Plane1";
                    else
                        TextBox_PL_Result.Text = "LineSegment IS NOT parallel the Plane1 !";
                    break;

                case PlaneTest_enum.Ray:
                    break;

                case PlaneTest_enum.Plane:
                    stat = Plane3D.Overlaps(_PL_PL1, _PL_PL2);
                    if (stat)
                        TextBox_PL_Result.Text = "Plane2 is PARALLEL the Plane1";
                    else
                        TextBox_PL_Result.Text = "Plane2 IS NOT parallel the Plane1 !";
                    break;

                case PlaneTest_enum.Face:
                    stat = Plane3D.Overlaps(_PL_PL1, _PL_F);
                    if (stat)
                        TextBox_PL_Result.Text = "Face is PARALLEL the Plane1";
                    else
                        TextBox_PL_Result.Text = "Face IS NOT parallel the Plane1 !";
                    break;

                default:
                    break;
            }

        }

        #endregion

        #region PolyHedron test

        Polyhedron _PolyH_PolyH1;
        Polyhedron _PolyH_PolyH2;
        Point3D _PolyH_P;
        LineSegment3D _PolyH_LS;
        Face3D _PolyH_F;
        PolyH_test_enum _PolyH_sel;
        enum PolyH_test_enum
        {
            Point,
            LineSegment,
            Face,
            Polyhedron
        }
        PolyhedronFaceTypeEnum _PolyH_facetype = PolyhedronFaceTypeEnum.TriangleFaces;

        private void TabItem_Polyhedron_Initialized(object sender, EventArgs e)
        {
            TextBox_PolyH_Tol.Text = MathUtils.tol.ToString("E2");
            Button_PolyH_Inside.IsEnabled = false;
            Button_PolyH_Intersect.IsEnabled = false;
        }

        private void TextBox_PolyH_Tol_TextChanged(object sender, TextChangedEventArgs e)
        {
            MathUtils.tol = Convert.ToDouble(TextBox_PolyH_Tol.Text);
        }

        private void Button_PolyH_DefPoint_Click(object sender, RoutedEventArgs e)
        {
            object pCoords;
            bool ret = getPointOrVector3D(TextBox_PolyH_DefPoint.Text, out pCoords);
            Point3D P1 = new Point3D((Point3D)pCoords);
            _PolyH_P = P1;
            TextBox_PolyH_Out2.Text = P1.ToString();
            _PolyH_sel = PolyH_test_enum.Point;
            Label_PolyH_Out2.Content = _PolyH_sel.ToString(); 
            Button_PolyH_Inside.IsEnabled = true;
        }

        private void Button_PolyH_DefLS_Click(object sender, RoutedEventArgs e)
        {
            List<object> pts = new List<object>();
            List<Point3D> pList = new List<Point3D>();
            bool stat = getMultiplePoint(TextBox_PolyH_DefLS.Text, out pts);
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i] is Point3D) pList.Add(pts[i] as Point3D);
            }

            if (pList.Count < 2)
                return;
            _PolyH_LS = new LineSegment3D(pList[0], pList[1]);
            TextBox_PolyH_Out2.Text = _PolyH_LS.ToString();
            _PolyH_sel = PolyH_test_enum.LineSegment;
            Label_PolyH_Out2.Content = _PolyH_sel.ToString();
            Button_PolyH_Intersect.IsEnabled = true;
            Button_PolyH_Inside.IsEnabled = true;
        }

        private void Button_PolyH_DefFace_Click(object sender, RoutedEventArgs e)
        {
            List<object> pts = new List<object>();
            List<Point3D> pList = new List<Point3D>();
            bool stat = getMultiplePoint(TextBox_PolyH_DefFace.Text, out pts);
            for (int i = 0; i < pts.Count; i++)
            {
                if (pts[i] is Point3D) pList.Add(pts[i] as Point3D);
            }

            if (pList.Count < 3)
                return;
            _PolyH_F = new Face3D(pList);
            TextBox_PolyH_Out2.Text = _PolyH_F.ToString();
            _PolyH_sel = PolyH_test_enum.Face;
            Label_PolyH_Out2.Content = _PolyH_sel.ToString();
            Button_PolyH_Intersect.IsEnabled = true;
            Button_PolyH_Inside.IsEnabled = true;
        }

        private void Button_PolyH_DefPolyh1_Click(object sender, RoutedEventArgs e)
        {
            List<object> vertsO = new List<object>();
            List<double> verts = new List<double>();
            List<int> faceIdx = new List<int>();
            List<int> noVert = new List<int>();

            bool stat = getMultiplePoint(TextBox_PolyH_DefVertices.Text, out vertsO);
            foreach (object o in vertsO)
            {
                verts.Add(((Point3D)o).X);
                verts.Add(((Point3D)o).Y);
                verts.Add(((Point3D)o).Z);
            }
            stat = getIndexedList(TextBox_PolyH_DefFaceIdx.Text, out faceIdx);
            if (_PolyH_facetype == PolyhedronFaceTypeEnum.ArbitraryFaces)
                stat = getIndexedList(TextBox_PolyH_DefNoVert.Text, out noVert);

            _PolyH_PolyH1 = new Polyhedron(_PolyH_facetype, true, verts, faceIdx, noVert);
        }

        private void Button_PolyH_DefPolyh2_Click(object sender, RoutedEventArgs e)
        {
            List<object> vertsO = new List<object>();
            List<double> verts = new List<double>();
            List<int> faceIdx = new List<int>();
            List<int> noVert = new List<int>();

            bool stat = getMultiplePoint(TextBox_PolyH_DefVertices.Text, out vertsO);
            foreach (object o in vertsO)
            {
                verts.Add(((Point3D)o).X);
                verts.Add(((Point3D)o).Y);
                verts.Add(((Point3D)o).Z);
            }
            stat = getIndexedList(TextBox_PolyH_DefFaceIdx.Text, out faceIdx);
            if (_PolyH_facetype == PolyhedronFaceTypeEnum.ArbitraryFaces)
                stat = getIndexedList(TextBox_PolyH_DefNoVert.Text, out noVert);

            _PolyH_PolyH2 = new Polyhedron(_PolyH_facetype, true, verts, faceIdx, noVert);
            Button_PolyH_Intersect.IsEnabled = true;
            Button_PolyH_Inside.IsEnabled = true;

        }

        private void Button_PolyH_Inside_Click(object sender, RoutedEventArgs e)
        {
            switch (_PolyH_sel)
            {
                case PolyH_test_enum.Point:
                    bool stat = Polyhedron.inside(_PolyH_PolyH1, _PolyH_P);
                    if (stat)
                        TextBox_PolyH_Result.Text = "Point is INSIDE the Polyhedron.";
                    else
                        TextBox_PolyH_Result.Text = "Point is OUTSIDE the Polyhedron!";
                    break;

                case PolyH_test_enum.LineSegment:
                    stat = Polyhedron.inside(_PolyH_PolyH1, _PolyH_LS);
                    if (stat)
                        TextBox_PolyH_Result.Text = "LineSegment is INSIDE the Polyhedron.";
                    else
                        TextBox_PolyH_Result.Text = "LineSegment is OUTSIDE the Polyhedron!";
                    break;

                case PolyH_test_enum.Face:
                    stat = Polyhedron.inside(_PolyH_PolyH1, _PolyH_F);
                    if (stat)
                        TextBox_PolyH_Result.Text = "Face is INSIDE the Polyhedron.";
                    else
                        TextBox_PolyH_Result.Text = "Face is OUTSIDE the Polyhedron!";
                    break;

                case PolyH_test_enum.Polyhedron:
                    stat = Polyhedron.inside(_PolyH_PolyH1, _PolyH_PolyH2);
                    if (stat)
                        TextBox_PolyH_Result.Text = "Polyhedron is INSIDE the first Polyhedron.";
                    else
                        TextBox_PolyH_Result.Text = "Polyhedron is OUTSIDE the first Polyhedron!";
                    break;

                default:
                    break;
            }
        }

        private void Button_PolyH_Intersect_Click(object sender, RoutedEventArgs e)
        {
            switch (_PolyH_sel)
            {
                case PolyH_test_enum.LineSegment:
                    List<Point3D> iPts = new List<Point3D>();
                    bool stat = Polyhedron.intersect(_PolyH_PolyH1, _PolyH_LS, out iPts);
                    if (stat)
                    {
                        string tmpStr = string.Empty;
                        foreach (Point3D iPt in iPts)
                            tmpStr += iPt.ToString();
                        TextBox_PolyH_Result.Text = "LineSegment INTERSECTS the Polyhedron at: " + tmpStr;
                    }
                    else
                        TextBox_PolyH_Result.Text = "LineSegment does NOT intersect the Polyhedron!";
                    break;

                case PolyH_test_enum.Face:
                    stat = Polyhedron.intersect(_PolyH_PolyH1, _PolyH_F);
                    if (stat)
                        TextBox_PolyH_Result.Text = "Face INTERSECTS the Polyhedron.";
                    else
                        TextBox_PolyH_Result.Text = "Face does NOT intersect the Polyhedron!";
                    break;

                case PolyH_test_enum.Polyhedron:
                    stat = Polyhedron.intersect(_PolyH_PolyH1, _PolyH_PolyH2);
                    if (stat)
                        TextBox_PolyH_Result.Text = "Polyhedron INTERSECTS the first Polyhedron.";
                    else
                        TextBox_PolyH_Result.Text = "Polyhedron does NOT intersect the Polyhedron!";
                    break;

                default:
                    break;
            }
        }

        private void RButton_Checked(object sender, RoutedEventArgs e)
        {

            if (RButton_Triangle.IsChecked == true)
                _PolyH_facetype = PolyhedronFaceTypeEnum.TriangleFaces;
            else if (RButton_Rectangle.IsChecked == true)
                _PolyH_facetype = PolyhedronFaceTypeEnum.RectangularFaces;
            else if (RButton_Arbitrary.IsChecked == true)
                _PolyH_facetype = PolyhedronFaceTypeEnum.ArbitraryFaces;
        }

        #endregion

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TabItem_gotFocus(object sender, RoutedEventArgs e)
        {
            TextBox_PolyH_Tol.Text = MathUtils.tol.ToString("E2");
            TextBox_LS_Tol.Text = MathUtils.tol.ToString("E2");
            TextBox_PL_Tol.Text = MathUtils.tol.ToString("E2");
            TextBox_F3D_Tol.Text = MathUtils.tol.ToString("E2");
        }

   }
}
