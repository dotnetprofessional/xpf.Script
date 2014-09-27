using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        protected bool EnableValidationsProperty { get; set; }

        protected ScriptEngine()
        {
            // This is a bit of a hack due to an issue of exposing GetCallingAssembly in a PCL
            // This method should only have an issue with WinRT apps and combining with native code
            // which is going to be rare. If this is the case, make use of the UsingAssembly method
            this.ScriptAssembly = Script.CallingAssembly;
            this.ParameterPrefix = "";

            // swith on validations by default when in debug mode
            if (Debugger.IsAttached)
                this.EnableValidations();
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

        public T UsingAssembly(object instance)
        {
            this.ScriptAssembly = instance.GetType().GetTypeInfo().Assembly;
            return (T)(object)this;
        }

        public T UsingScript(string scriptName)
        {
            var script = this.LoadScript(scriptName, false);
            return this.UsingCommand(script);
        }

        /// <summary>
        /// Loads the script resource and resolves any referenced or nested scripts then executes.
        /// </summary>
        /// <param name="scriptName"></param>
        /// <returns>
        /// This is a seperate method to UsingScript as there is a performance hit evaluating the nested scripts
        /// </returns>
        public T UsingNestedScript(string scriptName)
        {
            var script = this.LoadScript(scriptName, true);
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

        /// <summary>
        /// This is used to validate the scripts input/output parameters
        /// If a scripting engine uses special characters to prefix parameters 
        /// such as SQL with @ this allows the correct full name to be built.
        /// </summary>
        protected string ParameterPrefix { get; set; }
        public T EnableValidations()
        {
            this.EnableValidationsProperty = true;
            return (T)(object)this;
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

            if(this.activeScript.InParameters != null)
                throw new ArgumentException("Unable to sepcify WithIn multiple times for a single script. Try adding addition parameters to the first use of WithIn\r\nExample: .WithIn(new {Property1 = \"a\", Property2=\"b\"})");

            this.activeScript.InParameters = inParameters;

            return (T)(object)this;
        }

        public T WithOut(object outParameters)
        {
            if (this.activeScript == null)
                throw new ArgumentException("Unable to add parameters without specifying a script first.");

            if (this.activeScript.OutParameters != null)
                throw new ArgumentException("Unable to sepcify WithOut multiple times for a single script. Try adding addition parameters to the first use of WithOut\r\nExample: .WithOut(new {Property1 = \"a\", Property2=\"b\"})");

            this.activeScript.OutParameters = outParameters;

            return (T)(object)this;
        }

        public virtual Result Execute()
        {
            // add the currently active script to the list
            if(this.activeScript != null)
                this.scriptsToExecute.Add(this.activeScript);

            if (this.EnableValidationsProperty)
                this.ValidateScript();

            // Perform some validations on the 
            // this is never used as this is an abstract class
            return null;
        }

        /// <summary>
        /// Perform validations of the script based on the input/output parameters
        /// This is only performed during debug mode or when explicitly selected via EnableValidations()
        /// </summary>
        private void ValidateScript()
        {
            var validationOutput = "";
            var scriptNumberCount = 0;
            foreach (var s in this.scriptsToExecute)
            {
                scriptNumberCount ++;
                // Validate all input parameters are being used by the script
                if (s.InParameters != null)
                    foreach (var p in s.InParameters.GetType().GetTypeInfo().DeclaredProperties)
                    {
                        if (!s.Command.Contains(string.Format("{0}{1}", this.ParameterPrefix, p.Name)))
                            validationOutput += string.Format("Script number {0} is missing the input paramter: {1}\r\n", scriptNumberCount, p.Name);
                    }

                if (s.OutParameters != null)
                    foreach (var p in s.OutParameters.GetType().GetTypeInfo().DeclaredProperties)
                    {
                        if (!s.Command.Contains(string.Format("{0}{1}", this.ParameterPrefix, p.Name)))
                            validationOutput += string.Format("Script number {0} is missing the out paramter: {1}\r\n", scriptNumberCount, p.Name);
                    }
            }

            if(validationOutput != "")
                throw new KeyNotFoundException("The following errors were found processing the scripts:\r\n\r\n" + validationOutput);
        }

        protected string LoadScript(string embeddedScriptName, bool includeNested)
        {
            return this.LoadScript(embeddedScriptName, includeNested, this.ScriptAssembly);
        }

        protected string LoadScript(string embeddedScriptName, bool includeNested, Assembly resourceAssembly)
        {
            string embeddedScript = EmbeddedResources.GetResourceString(resourceAssembly, embeddedScriptName);
            if (includeNested)
            {

            // Convert to lines
            var compositeScript = new StringBuilder();
                var scriptLines = embeddedScript.Split(new[] {"\r", "\n"}, StringSplitOptions.None);
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

                        compositeScript.Append(this.LoadScript(includeScript, true));
                }
                else
                    compositeScript.Append(line + "\r\n");
            }

            return compositeScript.ToString();
        }
            else
            {
                return embeddedScript;
            }
        }
    }
}