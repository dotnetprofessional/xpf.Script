using xpf.Scripting.SQLServer;

namespace xpf.Scripting
{
    public static class SqlDatabase
    {
        public static SqlScript Database(this Script script, string databaseName = null)
        {
            return new SqlScript(databaseName);
        }
    }
}