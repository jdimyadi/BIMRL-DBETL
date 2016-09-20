namespace NetSdoGeometry
{
    using System;
    using Oracle.DataAccess.Client;
    using Oracle.DataAccess.Types;

    public abstract class OracleArrayTypeFactoryBase<T> : IOracleArrayTypeFactory
    {
        public Array CreateArray(int numElems)
        {
            return new T[numElems];
        }

        public Array CreateStatusArray(int numElems)
        {
            return new OracleUdtStatus[numElems];
        }
    }
}