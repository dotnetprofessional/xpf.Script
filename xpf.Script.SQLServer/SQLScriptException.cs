using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;

namespace xpf.Scripting.SQLServer
{
    public class SqlScriptException : DbException
    {
        public SqlScriptException(string message, string connectionString, string command, Exception innerException, int retryCount = 0)
            : base(message, innerException)
        {
            var safeConnectionString = "";
            var connBuilder = new SqlConnectionStringBuilder(connectionString);
            this.DataSource = connBuilder.DataSource;
            this.Catalog = connBuilder.InitialCatalog;
            this.UserName = connBuilder.UserID;

            if (connBuilder.Password.Length > 0)
                safeConnectionString = connectionString.Replace(connBuilder.Password, "****");
            else
                safeConnectionString = connectionString;

            this.Command = command;
            this.RetryCount = retryCount;
            this.ConnectionString = safeConnectionString;
            if (Script.Tracing.IsTracingEnabled)
            {
                Script.Tracing.Trace(Thread.CurrentThread.ManagedThreadId, null, null, innerException);
            }
        }

        public string DataSource { get; private set; }

        public string Catalog { get; private set; }

        public string UserName { get; private set; }
        public string Command { get; private set; }

        public string ConnectionString { get; private set; }

        public int RetryCount { get; private set; }
    }
}
