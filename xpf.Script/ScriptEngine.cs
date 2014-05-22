using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace xpf.Scripting
{
    public abstract class ScriptEngine<T>
    {
        ScriptDetail activeScript = null;

        private Assembly ScriptAssembly { get; set; }

        protected List<ScriptDetail> scriptsToExecute = new List<ScriptDetail>();
        protected bool EnableParallelExecutionProperty { get; set; }

        protected ScriptEngine()
        {
            // This is a bit of a hack due to an issue of exposing GetCallingAssembly in a PCL
            // This method should only have an issue with WinRT apps and combining with native code
            // which is going to be rare. If this is the case, make use of the UsingAssembly method
            this.ScriptAssembly = Script.CallingAssembly;
        }

        /// <summary>
        /// This method determines which assembly contains any embedded resources. This is only needed
        /// to be set if resources are not in the assembly making the call the to script
        /// </summary>
        /// <param name="assembly"></param>
        /// <returns></returns>
        public T UsingAssembly(Assembly assembly)
        {
            this.ScriptAssembly = assembly;
            return (T) (object) this;
        }

        public T UsingScript(string scriptName)
        {
            var script = this.LoadScript(scriptName);
            return this.UsingCommand(script);
        }

        /// <summary>
        /// Determines if batches are executed in parallel or sync
        /// </summary>
        public T EnableParallelExecution()
        {
            this.EnableParallelExecutionProperty = true;
            return (T) (object) this;
        }

        public T UsingCommand(string commandText)
        {
            if (this.activeScript != null)
            {
                this.scriptsToExecute.Add(this.activeScript);
            }

            this.activeScript = new ScriptDetail();
            this.activeScript.Command = commandText;
            return (T)(object)this ;
        }

        public T WithIn(object inParameters)
        {
            if (this.activeScript == null)
                throw new ArgumentException("Unable to add parameters without specifying a script first.");

            this.activeScript.InParameters = inParameters;

            return (T)(object)this;
        }

        public T WithOut(object outParameters)
        {
            if (this.activeScript == null)
                throw new ArgumentException("Unable to add parameters without specifying a script first.");

            this.activeScript.OutParameters = outParameters;

            return (T)(object)this;
        }

        public virtual Result Execute()
        {
            // add the currently active script to the list
            if(this.activeScript != null)
                this.scriptsToExecute.Add(this.activeScript);

            // this is never used as this is an abstract class
            return null;
        }

        private string LoadScript(string embeddedScriptName)
        {
            string embeddedScript = EmbeddedResources.GetResourceString(this.ScriptAssembly, embeddedScriptName);
            // Convert to lines
            var compositeScript = new StringBuilder();
            var scriptLines = embeddedScript.Split(new[] { "\r", "\n" }, StringSplitOptions.None);
            foreach (var line in scriptLines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("include") ||
                    trimmedLine.StartsWith(":r"))
                {
                    // Include the embedded script
                    var includeScript = trimmedLine.Substring(trimmedLine.IndexOf(" ", System.StringComparison.Ordinal) + 1);
                    // If the script name is using the :r syntax it might have a sub-path defined, if so need to convert to dot notation
                    includeScript = includeScript.Replace(@"\", ".");

                    compositeScript.Append(this.LoadScript(includeScript));
                }
                else
                    compositeScript.Append(line + "\r\n");
            }

            return compositeScript.ToString();
        }
    }
}