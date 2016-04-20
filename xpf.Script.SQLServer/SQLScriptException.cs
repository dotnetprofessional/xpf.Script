using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace xpf.Scripting.SQLServer
{
    public class SqlScriptException : DbException
    {
        public SqlScriptException(string message, string connectionString, string command, Exception innerException) : base(message, innerException)
        {
            var safeConnectionString = "";
            var connBuilder = new SqlConnectionStringBuilder(connectionString);
            this.DataSource = connBuilder.DataSource;
            this.Catalog = connBuilder.InitialCatalog;
            this.UserName = connBuilder.UserID;

            safeConnectionString = connectionString.Replace(connBuilder.Password, "****");
            this.Command = command;
            this.ConnectionString = safeConnectionString;
        }

        public string DataSource { get; private set; }

        public string Catalog { get; private set; }

        public string UserName { get; private set; }
        public string Command { get; private set; }

        public string ConnectionString { get; private set; }
    }
}
