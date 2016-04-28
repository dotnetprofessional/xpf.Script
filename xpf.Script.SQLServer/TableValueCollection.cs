using System;
using System.Collections.Generic;
using Microsoft.SqlServer.Server;

namespace xpf.Scripting.SQLServer
{
    public class TableValueCollection : List<object>, IEnumerable<SqlDataRecord>
    {
        public TableValueCollection(IEnumerable<object> collection) : base(collection)
        {
        }

        IEnumerator<SqlDataRecord> IEnumerable<SqlDataRecord>.GetEnumerator()
        {
            // Craete the meta-data for the object type
            if (this.Count > 0)
            {

                var columns = new List<SqlMetaData>();
                var properties = this[0].GetType().GetProperties();
                foreach (var p in properties)
                    columns.Add(new SqlMetaData(p.Name, SqlScriptEngine.ConvertToSqlType(p.PropertyType)));

                var sdr = new SqlDataRecord(columns.ToArray());

                foreach (var item in this)
                {
                    int position = 0;
                    foreach (var p in properties)
                    {
                        var value = p.GetValue(item, null);
                        this.SetValue(sdr, position, p.PropertyType, value);
                        position++;
                    }

                    yield return sdr;
                }
            }
        }

        void SetValue(SqlDataRecord record, int position, Type type, object value)
        {
            switch (type.Name)
            {
                case "Int16":
                    record.SetInt16(position, (short)value);
                    break;
                case "Int32":
                    record.SetInt32(position, (int)value);
                    break;
                case "Int64":
                    record.SetInt64(position, (long)value);
                    break;
                case "Boolean":
                    record.SetBoolean(position, (bool)value);
                    break;
                case "Byte":
                    record.SetByte(position, (byte)value);
                    break;
                case "Bytes[]":
                    record.SetBytes(position, 0, (byte[])value, 0, ((byte[])value).Length);
                    break;
                case "Char":
                    record.SetChar(position, (char)value);
                    break;
                case "Char[]":
                    record.SetChars(position, 0, (char[])value, 0, ((char[])value).Length);
                    break;
                case "DateTime":
                    record.SetDateTime(position, (DateTime)value);
                    break;
                case "Decimal":
                    record.SetDecimal(position, (decimal)value);
                    break;
                case "Double":
                    record.SetDouble(position, (double)value);
                    break;
                case "Guid":
                    record.SetGuid(position, (Guid)value);
                    break;
                case "String":
                    record.SetSqlString(position, (string)value);
                    break;
                default:
                    record.SetValue(position, value);
                    break;
            }
        }
    }
}
