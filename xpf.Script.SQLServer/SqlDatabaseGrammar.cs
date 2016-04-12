using xpf.Scripting.SQLServer;

namespace xpf.Scripting
{
    public static class SqlDatabaseGrammar
    {
        public static SqlScriptEngine Database(this Script script, string databaseName = null)
        {
            return new SqlScriptEngine(databaseName);
        }
    }
}