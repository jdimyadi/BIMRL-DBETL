namespace NetSdoGeometry
{
    using System;
    using System.Text;
    using Oracle.DataAccess.Types;

    [Serializable]
    [OracleCustomTypeMappingAttribute("MDSYS.SDO_GEOMETRY")]
    public class SdoGeometry : OracleCustomTypeBase<SdoGeometry>
    {
        [OracleObjectMappingAttribute(0)]
        public decimal? SdoGtype { get; set; }

        [OracleObjectMappingAttribute(1)]
        public decimal? SdoSRID { get; set; }
        
        [OracleObjectMappingAttribute(2)]
        public SdoPoint SdoPoint { get; set; }

        [OracleObjectMappingAttribute(3)]
        public decimal[] SdoElemInfo { get; set; }

        [OracleObjectMappingAttribute(4)]
        public decimal[] SdoOrdinates { get; set; }

        public int SdoGtypeAsInt
        {
            get
            {
                return System.Convert.ToInt32(this.SdoGtype);
            }
        }

        public int SdoSRIDAsInt
        {
            get 
            { 
                return System.Convert.ToInt32(this.SdoSRID); 
            }

            set 
            { 
                this.SdoSRID = System.Convert.ToDecimal(value); 
            }
        }

        public int[] ElemArrayOfInts
        {
            get
            {
                int[] elems = null;
                if (this.SdoElemInfo != null)
                {
                    elems = new int[this.SdoElemInfo.Length];
                    for (int k = 0; k < this.SdoElemInfo.Length; k++)
                    {
                        elems[k] = System.Convert.ToInt32(this.SdoElemInfo[k]);
                    }
                }

                return elems;
            }

            set
            {
                if (value != null)
                {
                    int c = value.GetLength(0);
                    this.SdoElemInfo = new decimal[c];
            
                    for (int k = 0; k < c; k++)
                    {
                        this.SdoElemInfo[k] = System.Convert.ToDecimal(value[k]);
                    }
                }
                else
                {
                    this.SdoElemInfo = null;
                }
            }
        }

        public double[] OrdinatesArrayOfDoubles
        {
            get
            {
                double[] elems = null;
                if (this.SdoOrdinates != null)
                {
                    elems = new double[this.SdoOrdinates.Length];
                    for (int k = 0; k < this.SdoOrdinates.Length; k++)
                    {
                        elems[k] = System.Convert.ToDouble(this.SdoOrdinates[k]);
                    }
                }

                return elems;
            }

            set
            {
                if (value != null)
                {
                    int c = value.GetLength(0);
                    this.SdoOrdinates = new decimal[c];
                    for (int k = 0; k < c; k++)
                    {
                        this.SdoOrdinates[k] = System.Convert.ToDecimal(value[k]);
                    }
                }
                else
                {
                    this.SdoOrdinates = null;
                }
            }
        }

        public int Dimensionality { get; set; }

        public int LRS { get; set; }

        public int GeometryType { get; set; }

        public string AsText
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("MDSYS.SDO_GEOMETRY(");
                sb.Append((this.SdoGtype != null) ? this.SdoGtype.ToString() : "null");
                sb.Append(",");
                sb.Append((this.SdoSRID != null) ? this.SdoSRID.ToString() : "null");
                sb.Append(",");
                
                // begin point
                if (this.SdoPoint != null)
                {
                    sb.Append("MDSYS.SDO_POINT_TYPE(");
                    sb.Append(string.Format(
                        "{0:#.##########},{1:#.##########}{2}{3:#.##########}",
                        this.SdoPoint.X,
                        this.SdoPoint.Y,
                        (this.SdoPoint.Z == null) ? null : ",",
                        this.SdoPoint.Z).Trim());
                    sb.Append(")");
                }
                else
                {
                    sb.Append("null");
                }

                sb.Append(",");
            
                // begin element array
                if (this.SdoElemInfo != null)
                {
                    sb.Append("MDSYS.SDO_ELEM_INFO_ARRAY(");
                    for (int i = 0; i < this.SdoElemInfo.Length; i++)
                    {
                        sb.Append(string.Format("{0}", this.SdoElemInfo[i]));
                        if (i < (this.SdoElemInfo.Length - 1))
                        {
                            sb.Append(",");
                        }
                    }

                    sb.Append(")");
                }
                else
                {
                    sb.Append("null");
                }
                
                sb.Append(",");
                
                // begin ordinates array
                if (this.SdoOrdinates != null)
                {
                    sb.Append("MDSYS.SDO_ORDINATE_ARRAY(");
                    for (int i = 0; i < this.SdoOrdinates.Length; i++)
                    {
                        sb.Append(string.Format("{0:#.##########}", this.SdoOrdinates[i]));
                        if (i < (this.SdoOrdinates.Length - 1))
                        {
                            sb.Append(",");
                        }
                    }

                    sb.Append(")");
                }
                else
                {
                    sb.Append("null");
                }
                
                sb.Append(")");
                
                return sb.ToString();
            }
        }

        public override void MapFromCustomObject()
        {
            this.SetValue((int)OracleObjectColumns.SDO_GTYPE, this.SdoGtype);
            this.SetValue((int)OracleObjectColumns.SDO_SRID, this.SdoSRID);
            this.SetValue((int)OracleObjectColumns.SDO_POINT, this.SdoPoint);
            this.SetValue((int)OracleObjectColumns.SDO_ELEM_INFO, this.SdoElemInfo);
            this.SetValue((int)OracleObjectColumns.SDO_ORDINATES, this.SdoOrdinates);
        }

        public override void MapToCustomObject()
        {
            this.SdoGtype = this.GetValue<decimal?>((int)OracleObjectColumns.SDO_GTYPE);
            this.SdoSRID = this.GetValue<decimal?>((int)OracleObjectColumns.SDO_SRID);
            this.SdoPoint = this.GetValue<SdoPoint>((int)OracleObjectColumns.SDO_POINT);
            this.SdoElemInfo = this.GetValue<decimal[]>((int)OracleObjectColumns.SDO_ELEM_INFO);
            this.SdoOrdinates = this.GetValue<decimal[]>((int)OracleObjectColumns.SDO_ORDINATES);
        }

        public int PropertiesFromGTYPE()
        {
            if ((int)this.SdoGtype != 0)
            {
                int v = (int)this.SdoGtype;
                int dim = v / 1000;
                this.Dimensionality = dim;
                v -= dim * 1000;
                int lrsDim = v / 100;
                this.LRS = lrsDim;
                v -= lrsDim * 100;
                this.GeometryType = v;
                return (this.Dimensionality * 1000) + (this.LRS * 100) + this.GeometryType;
            }
            else
            {
                return 0;
            }
        }

        public int PropertiesToGTYPE()
        {
            int v = this.Dimensionality * 1000;
            v = v + (this.LRS * 100);
            v = v + this.GeometryType;

            this.SdoGtype = System.Convert.ToDecimal(v);

            return v;
        }

        public override string ToString()
        {
            return this.AsText;
        }
    }
}