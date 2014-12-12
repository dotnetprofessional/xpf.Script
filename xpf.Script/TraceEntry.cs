using System;

namespace xpf.Scripting
{
    /// <summary>
    /// This class holds details about a trace event
    /// </summary>
    public class TraceEntry
    {
        public int ThreadId { get; set; }

        public string Script { get; set; }

        public object InParameters { get; set; }

        public object OutParameters { get; set; }

        public Result Result { get; set; }
        public Exception Exception { get; set; }
    }
}
