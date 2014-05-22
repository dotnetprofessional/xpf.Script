namespace xpf.Scripting
{
    public class ScriptDetail
    {
        /// <summary>
        /// The script text to execute
        /// </summary>
        public string Command { get; set; }

        public object InParameters { get; set; }

        public object OutParameters { get; set; }
    }
}