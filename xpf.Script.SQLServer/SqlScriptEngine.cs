using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

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

        SnapshotMode SelectedSnapshotMode;

        private SqlDatabase Database { get; set; }

        public SqlScriptEngine(string databaseName)
        {
            this.Database = new SqlDatabase();
            this.Database.SetConnectionStringFromConfig(databaseName);
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
            this.Database.ConnectionString = connectionString;
            return this;
        }

        public SqlScriptEngine WithTimeout(int timeoutInSeconds)
        {
            this.Database.Timeout = timeoutInSeconds;
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

        public async Task<Result> ExecuteAsync()
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
                    var t = Task.Run(async () =>
                    {
                        var r = await this.ExecuteAsync(s);
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
                    var r = await this.ExecuteAsync(s);
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
                    this.Execute(new ScriptDetail { Command = script });
        }

        bool PerformRestoreSnapshot()
        {
            var snapShotScript = "SnapshotSQL.RestoreDatabase.sql";
            var script = this.LoadScript(snapShotScript, false, this.GetType().Assembly);


            // First we need to determine if there are any existing snapshots to restore, attempting to restore without snapshots will result in an exception
            var scriptCount = new Script().Database().WithConnectionString(this.Database.ConnectionString)
                .UsingCommand("SELECT @Count = Count(*) FROM sys.databases sd WHERE sd.source_database_id = db_id()")
                .WithOut(new { Count = DbType.Int32 })
                .Execute();

            bool snapshotFound = scriptCount.Property.Count != 0;
            // Only perform a restore if they exist
            if (snapshotFound)
            {
                // A restore is a more complex operation that needs to run first against the target database
                // to get the database to restore. Then the result of that needs to run against the mater database

                // Step 1: Obtain the correct script by running initial script against target database
                var resultScript = this.Execute(new ScriptDetail { Command = script, OutParameters = new { SqlCmd = DbType.String } });

                // Step 2: Execute against the master database
                var engine = new Script().Database().WithConnectionString(this.Database.ConnectionString).UsingMaster()
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

            this.Execute(new ScriptDetail { Command = script });
        }

        protected override void ResetState()
        {
            base.ResetState();
            this.SelectedSnapshotMode = SnapshotMode.None;
        }

        FieldList Execute(ScriptDetail scriptDetail)
        {
            try
            {
                var c = this.GetDbCommandForScript(scriptDetail);
                this.Database.Execute(c);
                var values = new FieldList();
                foreach (DbParameter p in c.Parameters)
                    values.Add(new Field(p.ParameterName.Substring(1), p.Value));

                if (Script.Tracing.IsTracingEnabled)
                {
                    var result = new Result();
                    result.AddResult(values);
                    Script.Tracing.Trace(Thread.CurrentThread.ManagedThreadId, scriptDetail, result);
                }

                return values;
            }
            catch (SqlException ex)
            {
                if (Script.Tracing.IsTracingEnabled)
                    Script.Tracing.Trace(Thread.CurrentThread.ManagedThreadId, scriptDetail, null, ex);

                // To make diagnosics easier add some important details to the exception
                throw new SqlScriptException(string.Format("Connection string: {1}{0}Command: {2}", Environment.NewLine,
                    this.Database.ConnectionString, scriptDetail.Command), ex);
            }
        }

        /// <remarks>
        /// This is a duplicate of the sync Execute routine. Attempting to minimize code duplication further would 
        /// result in more complexity and the core code has already been centralized.
        /// </remarks>
        async Task<FieldList> ExecuteAsync(ScriptDetail scriptDetail)
        {
            try
            {
                var c = this.GetDbCommandForScript(scriptDetail);

                await this.Database.ExecuteAsync(c);

                var values = new FieldList();
                foreach (DbParameter p in c.Parameters)
                    values.Add(new Field(p.ParameterName.Substring(1), p.Value));

                if (Script.Tracing.IsTracingEnabled)
                {
                    var result = new Result();
                    result.AddResult(values);
                    Script.Tracing.Trace(Thread.CurrentThread.ManagedThreadId, scriptDetail, result);
                }

                return values;
            }
            catch (SqlException ex)
            {
                if (Script.Tracing.IsTracingEnabled)
                    Script.Tracing.Trace(Thread.CurrentThread.ManagedThreadId, scriptDetail, null, ex);

                // To make diagnosics easier add some important details to the exception
                throw new SqlScriptException(string.Format("Connection string: {1}{0}Command: {2}", Environment.NewLine,
                    this.Database.ConnectionString, scriptDetail.Command), ex);
            }
        }
        public ReaderResult ExecuteReader()
        {
            ScriptDetail scriptDetail = null;
            try
            {
                base.Execute();

                if (this.EnableParallelExecutionProperty)
                    throw new ArgumentException("Parallel execution is not supported for ExecuteReader.");

                scriptDetail = this.scriptsToExecute[0];
                var c = this.GetDbCommandForScript(scriptDetail);

                var dataReader = this.Database.ExecuteReader(c);

                if (Script.Tracing.IsTracingEnabled)
                    Script.Tracing.Trace(Thread.CurrentThread.ManagedThreadId, scriptDetail, null);

                this.ResetState();
                return new ReaderResult(dataReader);
            }
            catch (SqlException ex)
            {
                if (Script.Tracing.IsTracingEnabled)
                    Script.Tracing.Trace(Thread.CurrentThread.ManagedThreadId, scriptDetail, null, ex);

                // To make diagnosics easier add some important details to the exception
                throw new SqlScriptException(string.Format("Connection string: {1}{0}Command: {2}", Environment.NewLine,
                    this.Database.ConnectionString, scriptDetail.Command), ex);
            }
        }

        /// <remarks>
        /// This is a duplicate of the sync ExecuteReader routine. Attempting to minimize code duplication further would 
        /// result in more complexity and the core code has already been centralized.
        /// </remarks>
        public async Task<ReaderResult> ExecuteReaderAsync()
        {
            ScriptDetail scriptDetail = null;
            try
            {
                base.Execute();

                if (this.EnableParallelExecutionProperty)
                    throw new ArgumentException("Parallel execution is not supported for ExecuteReader.");

                scriptDetail = this.scriptsToExecute[0];
                var c = this.GetDbCommandForScript(scriptDetail);

                var dataReader = await this.Database.ExecuteReaderAsync(c);

                if (Script.Tracing.IsTracingEnabled)
                    Script.Tracing.Trace(Thread.CurrentThread.ManagedThreadId, scriptDetail, null);

                this.ResetState();
                return new ReaderResult(dataReader);

                return null;
            }
            catch (SqlException ex)
            {
                if (Script.Tracing.IsTracingEnabled)
                    Script.Tracing.Trace(Thread.CurrentThread.ManagedThreadId, scriptDetail, null, ex);

                // To make diagnosics easier add some important details to the exception
                throw new SqlScriptException(string.Format("Connection string: {1}{0}Command: {2}", Environment.NewLine,
                    this.Database.ConnectionString, scriptDetail.Command), ex);
            }
        }

        SqlCommand GetDbCommandForScript(ScriptDetail scriptDetail)
        {
            string executionScript = this.StripComments(scriptDetail.Command);

            var c = new SqlCommand(executionScript);


            if (scriptDetail.OutParameters != null)
            {
                if (scriptDetail.OutParameters is string[])
                {
                    foreach (var p in (IEnumerable<string>)scriptDetail.OutParameters)
                    {
                        c.AddOutParameter("@" + p, SqlDbType.Variant);
                    }
                }

                else
                {
                    var properties = scriptDetail.OutParameters.GetType().GetProperties();
                    foreach (var p in properties)
                    {
                        c.AddOutParameter("@" + p.Name, ConvertFromDbTypeToSqlType((DbType)p.GetValue(scriptDetail.OutParameters, null)));
                    }
                }
            }

            if (scriptDetail.InParameters != null)
            {
                var properties = scriptDetail.InParameters.GetType().GetProperties();
                foreach (var p in properties)
                {
                    var dbType = ConvertToSqlType(p.PropertyType);
                    var value = p.GetValue(scriptDetail.InParameters, null);
                    if (dbType == SqlDbType.Structured)
                    {
                        var typeName = "";
                        if (p.PropertyType.GenericTypeArguments.Length != 0)
                            typeName = $"{p.PropertyType.GenericTypeArguments[0].Name}Type";
                        else if (p.PropertyType.IsArray)
                            typeName = $"{p.PropertyType.GetElementType().Name}Type";
                        else
                            throw new ArgumentException("Collections must implement IEnumerable.");

                        c.AddStructuredInParameter("@" + p.Name, dbType, value as IList, typeName);
                    }
                    else
                    {
                        if (value == null)
                            dbType = SqlDbType.Variant;

                        c.AddInParameter("@" + p.Name, dbType, value);
                    }

                }
            }

            return c;
        }


        public SqlScriptEngine UseCataglog(string catalongName)
        {
            // Get the existing connection string and modify the inital catalog
            var cs = new SqlConnectionStringBuilder(this.Database.ConnectionString);
            cs.InitialCatalog = catalongName;
            this.Database.ConnectionString = cs.ConnectionString;
            return this;
        }

        public SqlScriptEngine UsingMaster()
        {
            return this.UseCataglog("master");
        }

        static SqlDbType ConvertFromDbTypeToSqlType(DbType datatype)
        {
            switch (datatype)
            {
                case DbType.String:
                    return SqlDbType.NVarChar;
                case DbType.Int32:
                    return SqlDbType.Int;
                case DbType.Int64:
                    return SqlDbType.BigInt;
                case DbType.DateTime:
                    return SqlDbType.DateTime2;
                case DbType.Guid:
                    return SqlDbType.UniqueIdentifier;
                case DbType.Binary:
                    return SqlDbType.Binary;
                default:
                    return SqlDbType.Variant;
            }
        }

        internal static SqlDbType ConvertToSqlType(Type datatype)
        {
            // Validate collections first
            if(!typeof(string).IsAssignableFrom(datatype) && typeof(IEnumerable).IsAssignableFrom(datatype))
                return SqlDbType.Structured;

            var datatypeName = datatype.Name;
            if (datatype.IsGenericType)
                datatypeName = datatype.GetGenericArguments()[0].Name;

            switch (datatypeName)
            {
                case "Int32":
                case "Int16":
                    return SqlDbType.Int;
                case "Int64":
                    return SqlDbType.BigInt;
                case "DateTime":
                    return SqlDbType.DateTime2;
                case "Guid":
                    return SqlDbType.UniqueIdentifier;
                case "Byte[]":
                    return SqlDbType.Binary;
                case "String":
                    return SqlDbType.Text;
                default:
                    return SqlDbType.Variant;
            }
        }

        string StripComments(string s)
        {
            string blockComments = @"/\*(?:(?:.|\n)*?)\*/";
            string lineComments = @"(--(?:.*?)\r?\n)";
            string emptyLinesAndSpaces = @"$((?:\s)*)";

            Regex myRegex = new Regex(string.Format("{0}|{1}", blockComments, lineComments), RegexOptions.None);
            var result = myRegex.Replace(s, Environment.NewLine);

            myRegex = new Regex(emptyLinesAndSpaces, RegexOptions.Multiline);
            return myRegex.Replace(result, Environment.NewLine);
        }


        public IdentityMap Persist(object entity, object handler)
        {
            if (this.EnableParallelExecutionProperty)
                throw new ArgumentException("Parallel execution is not supported by the Persist method.");

            // Find all the handlers for the entity types
            this.RegisterEventHandlers(handler);

            var persistenceMap = new IdentityMap();
            if (typeof(IEnumerable).GetTypeInfo().IsInstanceOfType(entity))
                this.PersistCollection(null, entity as IEnumerable, handler, persistenceMap);
            else
                this.PersistEntity(null, entity, handler, persistenceMap);

            return persistenceMap;
        }

        void PersistCollection(object parent, IEnumerable entity, object handler, IdentityMap persistenceMap)
        {
            foreach (var item in entity)
                if (typeof(IEnumerable).GetTypeInfo().IsInstanceOfType(item))
                    this.PersistCollection(parent, item as IEnumerable, handler, persistenceMap);
                else
                    this.PersistEntity(parent, item, handler, persistenceMap);
        }

        void PersistEntity(object parent, object entity, object handler, IdentityMap persistenceMap)
        {
            // Locate the method fot this entity
            var method = this.TypeHandlers[entity.GetType().FullName];
            method.Invoke(handler, new object[] { parent, entity, persistenceMap });

            foreach (PropertyInfo propertyInfo in entity.GetType().GetTypeInfo().DeclaredProperties)
            {
                var propertyType = propertyInfo.PropertyType;

                // Strings inherit from IEnumerable so need to do a special test for it
                if (propertyType.IsAssignableFrom(typeof(string)))
                    continue;

                // If the property is another collection then process as a collection
                if (typeof(IEnumerable).GetTypeInfo().IsAssignableFrom(propertyType))
                {
                    var value = propertyInfo.GetValue(entity, null);
                    this.PersistCollection(entity, value as IEnumerable, handler, persistenceMap);
                }
                else if (propertyType.IsClass)
                {
                    var value = propertyInfo.GetValue(entity, null);
                    this.PersistEntity(entity, value, handler, persistenceMap);
                }

            }
        }

        Dictionary<string, MethodInfo> TypeHandlers { get; set; }

        void RegisterEventHandlers(object handler)
        {
            this.TypeHandlers = new Dictionary<string, MethodInfo>();

            var interfaces = handler.GetType().GetInterfaces();

            foreach (var impl in interfaces)
            {
                // Skip any interface that isn't the IPersistType
                if (impl.Name != "IPersistType`1")
                    continue;

                var genericType = impl.GenericTypeArguments[0];
                var method = impl.GetMethod("Persist");
                this.TypeHandlers.Add(genericType.FullName, method);
            }
        }

        T ExecuteWithRetry<T>(Func<T> codeToExecute)
        {
            var attempts = 1;
            var retryAttempts = 3;
            var shouldRetry = false;
            do
            {
                try
                {
                    return codeToExecute();
                }
                // Only worry about Sql Exceptions
                catch (SqlException ex)
                {
                    switch (ex.Number)
                    {
                        case 1205: // Dead-lock
                        case -2: // Timeout Expired
                        case 64: // An error occurred during login
                        case 233: //Connection initialization error. 
                        case 10053: //A transport-level error occurred when receiving results from the server.
                        case 10054: //A transport-level error occurred when receiving results from the server.
                        case 10060: // Network or instance-specific error.  
                        case 40143: //Connection could not be initialized.  
                        case 40197: // The service encountered an error processing your request
                        case 40501: // The server is busy.  
                        case 40613: // The database is currently unavailable. 
                        case 4060: // Cannot open database "%.*ls" requested by the login. The login failed. 
                        case 10928: // The %s limit for the database is %d and has been reached
                        case 10929: // The %s limit for the database is %d and has been reached
                            // Validate if a retry should be performed
                            shouldRetry = attempts <= retryAttempts;
                            attempts++;

                            // Put a small delay before attempting again
                            if (shouldRetry)
                                Thread.Sleep(500);
                            break;
                        default:
                            throw;
                    }

                    if (!shouldRetry)
                        throw;
                }
            } while (true);
        }
    }
}