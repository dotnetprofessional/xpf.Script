using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace xpf.Scripting.SQLServer
{
    /// <summary>
    /// Set of extension methods for SqlServer ADO.NET
    /// </summary>
    static class SqlServerExtensions
    {
        public static void AddInParameter(this SqlCommand command, string name, SqlDbType type, object value)
        {
            if (value == null)
                value = DBNull.Value;

            var parameter = command.Parameters.AddWithValue(name, value);
            parameter.SqlDbType = type;
        }

        public static void AddStructuredInParameter(this SqlCommand command, string name, SqlDbType type, object value, string tableType)
        {
            if (value == null)
                value = DBNull.Value;

            var parameter = command.Parameters.AddWithValue(name, new TableValueCollection(value as IEnumerable<object>));
            parameter.SqlDbType = type;
            parameter.TypeName = tableType;
        }
        public static void AddOutParameter(this SqlCommand command, string name, SqlDbType type)
        {
            var p = command.Parameters.Add(name, type, int.MaxValue);
            p.Direction = ParameterDirection.Output;
        }
    }
}
