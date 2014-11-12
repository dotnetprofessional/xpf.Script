﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Practices.EnterpriseLibrary.Data;

namespace xpf.Scripting.SQLServer
{
    public class SqlScriptEngine : ScriptEngine<SqlScriptEngine>
    {
        internal enum SnapshotMode
        {
            None,
            TakeSnapshot,
            Restore,
            Delete
        }

        string ConnectionString { get; set; }
        protected string DatabaseName { get; set; }
        protected int Timeout { get; set; }

        SnapshotMode SelectedSnapshotMode;

        public SqlScriptEngine(string databaseName)
        {
            this.DatabaseName = databaseName;
            this.ParameterPrefix = "@";
        }

        /// <summary>
        /// This method creates a snapshot of the database
        /// </summary>
        /// <returns></returns>
        public SqlScriptEngine TakeSnapshot(bool restoreExistingSnapshot = true)
        {
            this.RestoreExistingSnapshot = restoreExistingSnapshot;
            SetSnapshotMode(SnapshotMode.TakeSnapshot);
            return this;
        }

        bool RestoreExistingSnapshot { get; set; }

        /// <summary>
        /// This method restores a snapshot of the database
        /// </summary>
        /// <returns></returns>
        public SqlScriptEngine RestoreSnapshot()
        {
            SetSnapshotMode(SnapshotMode.Restore);
            return this;
        }

        /// <summary>
        /// This method deletes a snapshot of the database
        /// </summary>
        /// <returns></returns>
        public SqlScriptEngine DeleteSnapshot(bool restoreExistingSnapshot = true)
        {
            this.RestoreExistingSnapshot = restoreExistingSnapshot;
            SetSnapshotMode(SnapshotMode.Delete);
            return this;
        }

        void SetSnapshotMode(SnapshotMode snapshotMode)
        {
            if (this.SelectedSnapshotMode == SnapshotMode.None)
                this.SelectedSnapshotMode = snapshotMode;
            else
                throw new ArgumentException("Only one snapshot mode can be set per execution. The snapshot mode is currently set to " +
                                            this.SelectedSnapshotMode);
        }

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

            PerformSnapshotOperation(this.SelectedSnapshotMode);

            // determine if it shoudl be run in parallel
            if (this.EnableParallelExecutionProperty)
            {
                // Check if there is a transient transaction if so throw a not supported exception
                if (Transaction.Current != null)
                    throw new NotSupportedException(
                        "Parallel execution does not support ambient transactions. Please remove the ambient (TransactionScope) transaction.");

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

            // Reset the object state so that chaining Executes is possible. Without doing this,
            // multiple executes would repeat the previous scripts.
            this.ResetState();
            return result;
        }

        void PerformSnapshotOperation(SnapshotMode mode)
        {
            switch (mode)
            {
                case SnapshotMode.TakeSnapshot:
                    this.PerformTakeSnapshot();
                    break;
                case SnapshotMode.Restore:
                    this.PerformRestoreSnapshot();
                    break;
                case SnapshotMode.Delete:
                    this.PerformDeleteSnapshot();
                    break;
            }
        }

        void PerformTakeSnapshot()
        {
            var snapShotScript = "SnapshotSQL.CreateSnapshot.sql";
            var script = this.LoadScript(snapShotScript, false, this.GetType().Assembly);

            if (this.RestoreExistingSnapshot)
                // Requesting a restore first will still leave the original snapshot so if a restore occured (true)
                // then its not necessary to perform the snapshot as it already exists.
                if (!this.PerformRestoreSnapshot())
                    this.Execute(new ScriptDetail {Command = script});
        }

        bool PerformRestoreSnapshot()
        {
            var snapShotScript = "SnapshotSQL.RestoreDatabase.sql";
            var script = this.LoadScript(snapShotScript, false, this.GetType().Assembly);


            // First we need to determine if there are any existing snapshots to restore, attempting to restore without snapshots will result in an exception
            var scriptCount = new Script().Database().WithConnectionString(this.ConnectionString)
                .UsingCommand("SELECT @Count = Count(*) FROM sys.databases sd WHERE sd.source_database_id = db_id()")
                .WithOut(new { Count = DbType.Int32 })
                .Execute();

            bool snapshotFound = scriptCount.Property.Count != 0;
            // Only perform a restore if they exist
            if (snapshotFound)
            {
                // A restore is a more complex operation that needs to run first against the target database
                // to get the database to restore. Then the result of that needs to run against the mater database

                // Step 1: Obtain the correct script by running intial script against target database
                var resultScript = this.Execute(new ScriptDetail { Command = script, OutParameters = new { SqlCmd = DbType.String } });

                // Step 2: Execute against the master database
                var engine = new Script().Database().WithConnectionString(this.ConnectionString).UsingMaster()
                    .UsingCommand(resultScript[0].Value as string)
                    .Execute();
            }

            return snapshotFound;
        }

        void PerformDeleteSnapshot()
        {
            var snapShotScript = "SnapshotSQL.DeleteSnapshot.sql";
            var script = this.LoadScript(snapShotScript, false, this.GetType().Assembly);
            if (this.RestoreExistingSnapshot)
            {
                // Requesting a restore first will remove any data from the working copy prior to deleting the snapshot
                // this has the effect of making the snapshot the active database again.
                this.PerformRestoreSnapshot();
            }

            this.Execute(new ScriptDetail {Command = script});
        }

        private void ResetState()
        {
            this.SelectedSnapshotMode = SnapshotMode.None;
        }

        private FieldList Execute(ScriptDetail scriptDetail)
        {

            var dataAccess = GetDatabase();

            string executionScript = this.StripComments(scriptDetail.Command);

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

            c = dataAccess.GetSqlStringCommand(this.StripComments(executionScript));
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

        public SqlScriptEngine UseCataglog(string catalongName)
        {
            // Get the existing connection string and modify the inital catalog
            var db = this.GetDatabase();

            var cs = new SqlConnectionStringBuilder(db.ConnectionString);
            cs.InitialCatalog = catalongName;
            this.ConnectionString = cs.ConnectionString;
            return this;
        }

        public SqlScriptEngine UsingMaster()
        {
            return this.UseCataglog("master");
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

        public string StripComments(string s)
        {
            string blockComments = @"/\*(?:(?:.|\n)*?)\*/";
            string lineComments = @"(--(?:.*?)\r?\n)";
            string emptyLinesAndSpaces = @"$((?:\s)*)";

            Regex myRegex = new Regex(string.Format("{0}|{1}", blockComments, lineComments), RegexOptions.None);
            var result = myRegex.Replace(s, Environment.NewLine);

            myRegex = new Regex(emptyLinesAndSpaces, RegexOptions.Multiline);
            return myRegex.Replace(result, Environment.NewLine);
        }
    }
}