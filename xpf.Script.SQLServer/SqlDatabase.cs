using xpf.Scripting.SQLServer;

namespace xpf.Scripting
{
    public static class SqlDatabase
    {
        public static SqlScriptEngine Database(this Script script, string databaseName = null)
        {
            return new SqlScriptEngine(databaseName);
        }
    }
}