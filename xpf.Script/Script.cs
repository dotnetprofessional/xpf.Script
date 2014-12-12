using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace xpf.Scripting
{
    public class Script
    {
        public Script()
        {
            // This is a bit of a hack due to an issue of exposing GetCallingAssembly in a PCL
            // This method should only have an issue with WinRT apps and combining with native code
            // which is going to be rare. 
            CallingAssembly = (Assembly) typeof (Assembly).GetTypeInfo()
                .GetDeclaredMethod("GetCallingAssembly")
                .Invoke(null, new object[0]);
        }

        internal static Assembly CallingAssembly { get; private set; }

        public static class Tracing
        {
            static object lockObject = new object();

            static bool tracingEnabled = false;
            static List<TraceEntry> _traceEntries = new List<TraceEntry>();

            public static void StartTracing()
            {
                tracingEnabled = true;
            }
            public static void StartTracingWithReset()
            {
                tracingEnabled = true;
                ResetTrace();
            }

            public static void StopTracing()
            {
                tracingEnabled = false;

            }

            public static bool IsTracingEnabled
            {
                get { return tracingEnabled; }
            }

            public static List<TraceEntry> Entries
            {
                get { return _traceEntries; }
            }

            public static void ResetTrace()
            {
                lock (lockObject)
                {
                    _traceEntries.Clear();
                }
            }

            /// <summary>
            /// Used by libraries to trace their output. 
            /// </summary>
            /// <typeparam name="T"></typeparam>
            /// <param name="threadId">Optional: The thread id that was used to execute the script</param>
            /// <param name="script">The script detail that has executed</param>
            /// <param name="result">The result of the script execution</param>
            /// <param name="ex">Optional Exception that may have occured</param>
            /// <remarks>
            /// The method needs to take the ThreadId parameter as ThreadId is not available in PCLs.
            /// </remarks>
            public static void Trace(int threadId, ScriptDetail script, Result result, Exception ex = null)
            {
                var entry = new TraceEntry
                {
                    Script = script.Command,
                    InParameters = script.InParameters,
                    OutParameters = script.OutParameters,
                    ThreadId = threadId,
                    Result = result,
                    Exception = ex,
                };

                lock (lockObject)
                {
                    _traceEntries.Add(entry);
                }
            }
        }
    }

}