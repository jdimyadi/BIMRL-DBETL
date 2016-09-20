namespace NetSdoGeometry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Oracle.DataAccess.Types;

    [OracleCustomTypeMappingAttribute("MDSYS.SDO_ORDINATE_ARRAY")]
    public class OrdinatesArrayFactory : OracleArrayTypeFactoryBase<decimal> 
    { 
    }
}
