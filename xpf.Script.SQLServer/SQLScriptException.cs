using System;
using System.Data;
using System.Data.Common;

namespace xpf.Scripting.SQLServer
{
    public class SqlScriptException : DbException
    {
        public SqlScriptException(string message, Exception innerException) : base(message, innerException)
        {
        }

    }
}
