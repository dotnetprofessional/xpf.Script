using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Practices.EnterpriseLibrary.Data;

namespace xpf.Scripting.SQLServer
{
    public class SqlScriptEngine : ScriptEngine<SqlScriptEngine>
    {
        private string ConnectionString { get; set; }
        protected string DatabaseName { get; set; }
        protected int Timeout { get; set; }

        public SqlScriptEngine(string databaseName)
        {
            this.DatabaseName = databaseName;
            this.ParameterPrefix = "@";
        }

        // Removed until support is added
        //public SqlScript TakeSnapshot()
        //{
        //    return this;
        //}

        public SqlScriptEngine WithConnectionString(string connectionString)
        {
            this.ConnectionString = connectionString;
            return this;
        }

        public SqlScriptEngine WithTimeout(int timeoutInSeconds)
        {
            this.Timeout = timeoutInSeconds;
            return this;
        }

        public override Result Execute()
        {
            base.Execute();

            // if there are more than one script to execute add their results to a single result
            // may want to refactor to have the ability to support groups
            var result = new Result();


            // determine if it shoudl be run in parallel
            if (this.EnableParallelExecutionProperty)
            {
                // Check if there is a transient transaction if so throw a not supported exception
                if(Transaction.Current != null)
                    throw new NotSupportedException("Parallel execution does not support ambient transactions. Please remove the ambient (TransactionScope) transaction.");

                // Need to keep track of the transaction due to transaction scope being tied to a single thread
                var tasks = new List<Task>();
                foreach (var s in this.scriptsToExecute)
                {
                    var t = Task.Run(() =>
                    {
                        var r = this.Execute(s);
                        result.AddResult(r);
                    });
                    tasks.Add(t);
                }
                // Now execute and wait for them to finsish
                Task.WaitAll(tasks.ToArray());
            }
            else
                foreach (var s in this.scriptsToExecute)
                {
                    var r = this.Execute(s);
                    result.AddResult(r);
                }
            return result;
        }

        private FieldList Execute(ScriptDetail scriptDetail)
        {

            var dataAccess = GetDatabase();

            string executionScript = scriptDetail.Command;

            var c = dataAccess.GetSqlStringCommand(executionScript);
            if (this.Timeout != 0) c.CommandTimeout = Timeout;

            if (scriptDetail.OutParameters != null)
            {
                if (scriptDetail.OutParameters is string[])
                {
                    foreach (var p in (IEnumerable<string>)scriptDetail.OutParameters)
                    {
                        dataAccess.AddOutParameter(c, "@" + p, DbType.Object, int.MaxValue);
                    }
                }

                else
                {
                    var properties = scriptDetail.OutParameters.GetType().GetProperties();
                    foreach (var p in properties)
                    {
                        dataAccess.AddOutParameter(c, "@" + p.Name, (DbType)p.GetValue(scriptDetail.OutParameters, null), int.MaxValue);
                    }
                }

            }

            if (scriptDetail.InParameters != null)
            {
                var properties = scriptDetail.InParameters.GetType().GetProperties();
                foreach (var p in properties)
                {
                    dataAccess.AddInParameter(c, "@" + p.Name, ConvertToSqlType(p.PropertyType), p.GetValue(scriptDetail.InParameters, null));
                }
            }

            dataAccess.ExecuteNonQuery(c);

            var values = new FieldList();
            foreach(DbParameter p in c.Parameters)
                values.Add(new Field(p.ParameterName.Substring(1), p.Value));

            return values;
        }

        public ReaderResult ExecuteReader()
        {
            base.Execute();

            if(this.EnableParallelExecutionProperty)
                throw new ArgumentException("Parallel execution is not supported for ExecuteReader.");

            var dataAccess = GetDatabase();

            DbCommand c;

            var scriptDetail = this.scriptsToExecute[0];
            string executionScript = scriptDetail.Command;

            c = dataAccess.GetSqlStringCommand(executionScript);
            if (this.Timeout != 0) c.CommandTimeout = Timeout;

            // Define the input parameters
            if (scriptDetail.InParameters != null)
            {
                var properties = scriptDetail.InParameters.GetType().GetProperties();
                foreach (var p in properties)
                {
                    dataAccess.AddInParameter(c, "@" + p.Name, ConvertToSqlType(p.PropertyType), p.GetValue(scriptDetail.InParameters, null));
                }
            }

            return new ReaderResult(dataAccess.ExecuteReader(c));
        }
        private static DbType ConvertToSqlType(Type datatype)
        {
            switch (datatype.Name)
            {
                case "Int32":
                case "Int16":
                    return DbType.Int32;
                case "Int64":
                    return DbType.Int64;
                case "DateTime":
                    return DbType.DateTime;
                case "Guid":
                    return DbType.Guid;
                case "Byte[]":
                    return DbType.Binary;
                default:
                    return DbType.String;
            }
        }
        Database GetDatabase()
        {
            var factory = new DatabaseProviderFactory();
            Database dataAccess;

            if(this.ConnectionString != null)
                dataAccess = new Microsoft.Practices.EnterpriseLibrary.Data.Sql.SqlDatabase(this.ConnectionString);
            else if (this.DatabaseName == null)
                dataAccess = factory.CreateDefault();
            else
                dataAccess = factory.Create(this.DatabaseName);

            return dataAccess;
        }
    }
}