namespace NetSdoGeometry
{
    using System;
    using Oracle.DataAccess.Client;
    using Oracle.DataAccess.Types;

    [Serializable]
    public abstract class OracleCustomTypeBase<T> : INullable, IOracleCustomType, IOracleCustomTypeFactory
    where T : OracleCustomTypeBase<T>, new()
    {
        private static string errorMessageHead = "Error converting Oracle User Defined Type to .Net Type " + typeof(T).ToString() + ", oracle column is null, failed to map to . NET valuetype, column ";
        [NonSerialized]
        private OracleConnection connection;
        private IntPtr udtHandle;
        private bool isNull;

        public static T Null
        {
            get
            {
                T t = new T();
                t.isNull = true;
                return t;
            }
        }

        public virtual bool IsNull
        {
            get
            {
                return this.isNull;
            }
        }

        public IOracleCustomType CreateObject()
        {
            return new T();
        }

        public void FromCustomObject(OracleConnection connection, IntPtr udtHandle)
        {
            this.SetConnectionAndPointer(connection, udtHandle);
            this.MapFromCustomObject();
        }

        public void ToCustomObject(OracleConnection connection, IntPtr udtHandle)
        {
            this.SetConnectionAndPointer(connection, udtHandle);
            this.MapToCustomObject();
        }

        public abstract void MapFromCustomObject();

        public abstract void MapToCustomObject();

        protected void SetConnectionAndPointer(OracleConnection connection, IntPtr udtHandle)
        {
            this.connection = connection;
            this.udtHandle = udtHandle;
        }

        protected void SetValue(string oracleColumnName, object value)
        {
            if (value != null)
            {
                OracleUdt.SetValue(this.connection, this.udtHandle, oracleColumnName, value);
            }
        }

        protected void SetValue(int oracleColumnId, object value)
        {
            if (value != null)
            {
                OracleUdt.SetValue(this.connection, this.udtHandle, oracleColumnId, value);
            }
        }

        protected U GetValue<U>(string oracleColumnName)
        {
            if (OracleUdt.IsDBNull(this.connection, this.udtHandle, oracleColumnName))
            {
                if (default(U) is ValueType)
                {
                    throw new Exception(errorMessageHead + oracleColumnName.ToString() + " of value type " + typeof(U).ToString());
                }
                else
                {
                    return default(U);
                }
            }
            else
            {
                return (U)OracleUdt.GetValue(this.connection, this.udtHandle, oracleColumnName);
            }
        }

        protected U GetValue<U>(int oracleColumnId)
        {
            if (OracleUdt.IsDBNull(this.connection, this.udtHandle, oracleColumnId))
            {
                if (default(U) is ValueType)
                {
                    throw new Exception(errorMessageHead + oracleColumnId.ToString() + " of value type " + typeof(U).ToString());
                }
                else
                {
                    return default(U);
                }
            }
            else
            {
                return (U)OracleUdt.GetValue(this.connection, this.udtHandle, oracleColumnId);
            }
        }
    }
}