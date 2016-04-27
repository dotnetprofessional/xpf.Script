using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace xpf.Scripting.SQLServer
{
    class SqlDatabase
    {
        public SqlDatabase()
        {
            this.Timeout = 30;
        }

        public string ConnectionString { get; set; }
        public int Timeout { get; set; }

        public void SetConnectionStringFromConfig(string name)
        {
            if (name == null)
            {
                if (ConfigurationManager.ConnectionStrings.Count > 0)
                    this.ConnectionString = ConfigurationManager.ConnectionStrings[0].ConnectionString;
            }
            else
                this.ConnectionString = ConfigurationManager.ConnectionStrings[name].ConnectionString;
        }

        public void Execute(SqlCommand command)
        {
            var connection = new SqlConnection(this.ConnectionString);
            using (connection)
            {
                command.Connection = connection;
                command.CommandTimeout = this.Timeout;

                this.ExecuteWithRetry(() =>
                {
                    connection.Close(); // This should remove broken connections from the pool
                    connection.Open();
                    return command.ExecuteNonQuery();
                });
            }
        }

        public IDataReader ExecuteReader(SqlCommand command)
        {
            var connection = new SqlConnection(this.ConnectionString);
            command.Connection = connection;
            command.CommandTimeout = this.Timeout;
            return this.ExecuteWithRetry(() =>
            {
                connection.Close(); // This should remove broken connections from the pool
                connection.Open();
                return command.ExecuteReader();
            });
        }

        public async Task<IDataReader> ExecuteReaderAsync(SqlCommand command)
        {
            var connection = new SqlConnection(this.ConnectionString);
            command.Connection = connection;
            command.CommandTimeout = this.Timeout;
            return await this.ExecuteWithRetry(async () =>
            {
                connection.Close(); // This should remove broken connections from the pool
                connection.Open();

                return await command.ExecuteReaderAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public async Task ExecuteAsync(SqlCommand command)
        {
            var connection = new SqlConnection(this.ConnectionString);
            command.Connection = connection;
            command.CommandTimeout = this.Timeout;
            using (connection)
            {
                await this.ExecuteWithRetry(async () =>
                {
                    connection.Close(); // This should remove broken connections from the pool
                    connection.Open();

                    return await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }).ConfigureAwait(false);
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
                        case 0:     // ???
                        case 1205:  // Dead-lock
                        case -2:    // Timeout Expired
                        case 19:    // Physical connection is not usable
                        case 20:    // ??? 
                        case 64:    // An error occurred during login
                        case 233:   // Connection initialization error. 
                        case 10053: // A transport-level error occurred when receiving results from the server.
                        case 10054: // A transport-level error occurred when receiving results from the server.
                        case 10060: // Network or instance-specific error.  
                        case 40143: // Connection could not be initialized.  
                        case 40197: // The service encountered an error processing your request
                        case 40501: // The server is busy.  
                        case 40613: // The database is currently unavailable. 
                        case 41325: // The current transaction failed to commit due to a serializable validation failure.
                        case 41305: // The current transaction failed to commit due to a repeatable read validation failure.
                        case 41302: // The current transaction attempted to update a record that has been updated since the transaction started.
                        case 41301: // A previous transaction that the current transaction took a dependency on has aborted, and the current transaction can no longer commit
                        case 4060:  // Cannot open database "%.*ls" requested by the login. The login failed. 
                        case 10928: // The %s limit for the database is %d and has been reached
                        case 10929: // The %s limit for the database is %d and has been reached
                            // Validate if a retry should be performed
                            shouldRetry = attempts <= retryAttempts;
                            attempts++;

                            // Put a small delay before attempting again
                            if (shouldRetry)
                                Thread.Sleep(1000);
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
