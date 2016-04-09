using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace xpf.Scripting
{
    public abstract class ScriptEngine<T>
    {
        ScriptDetail activeScript = null;

        private Assembly ScriptAssembly { get; set; }

        protected List<ScriptDetail> scriptsToExecute = new List<ScriptDetail>();
        protected bool EnableParallelExecutionProperty { get; set; }

        protected bool EnableValidationsProperty { get; set; }

        protected List<string> CommandToAppend = new List<string>();
        bool _disableAppendFormatting;

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
            return (T)(object)this;
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

        public T AppendCommand(string commandText)
        {
            this.CommandToAppend.Add(commandText);
            return (T)(object)this;
        }

        public T DisableAppendFormatting
        {
            get
            {
                _disableAppendFormatting = true;
                return (T)(object)this;
            }
        }

        void ApplyAppendedScriptsToActiveScript()
        {
            // Check that we have any commands to append
            if (this.CommandToAppend.Count == 0)
                return;

            var scriptFormattingPrefix = "";

            var scriptToAppend = "";
            if (!_disableAppendFormatting)
                scriptFormattingPrefix = Environment.NewLine;

            scriptToAppend = scriptFormattingPrefix + string.Join(scriptFormattingPrefix, this.CommandToAppend);

            this.activeScript.Command += scriptToAppend;

            // Clear out the append scripts
            this.CommandToAppend.Clear();
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
            return (T)(object)this;
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
                this.ApplyAppendedScriptsToActiveScript();
                this.scriptsToExecute.Add(this.activeScript);
            }

            this.activeScript = new ScriptDetail();
            this.activeScript.Command = commandText;
            return (T)(object)this;
        }

        public T WithIn(object inParameters)
        {
            if (this.activeScript == null)
                throw new ArgumentException("Unable to add parameters without specifying a script first.");

            if (this.activeScript.InParameters != null)
                throw new ArgumentException("Unable to sepcify WithIn multiple times for a single script. Try adding additional parameters to the first use of WithIn\r\nExample: .WithIn(new {Property1 = \"a\", Property2=\"b\"})");

            this.activeScript.InParameters = inParameters;

            return (T)(object)this;
        }

        public T WithOut(object outParameters)
        {
            if (this.activeScript == null)
                throw new ArgumentException("Unable to add parameters without specifying a script first.");

            if (this.activeScript.OutParameters != null)
                throw new ArgumentException("Unable to sepcify WithOut multiple times for a single script. Try adding additional parameters to the first use of WithOut\r\nExample: .WithOut(new {Property1 = \"a\", Property2=\"b\"})");

            this.activeScript.OutParameters = outParameters;

            return (T)(object)this;
        }

        public T Bind(object instance)
        {
            if (this.activeScript == null)
                throw new ArgumentException("Unable to use bind without specifying a script first.");

            if (instance == null)
                throw new ArgumentException("Unable to sepcify Bind multiple times for a single script. Try adding additional properties to the object instance,\r\nExample: .Bind(new {Property1 = \"a\", Property2=\"b\"})");

            var validationOutput = "";
            var script = this.activeScript;
            // As binding modifies the script immediately, validations are performed immediately too.
            if (this.EnableValidationsProperty)
            {
                foreach (var p in instance.GetType().GetTypeInfo().DeclaredProperties)
                {
                    if (!script.Command.Contains(string.Format("{0}{1}{2}", "{#", p.Name, "}")) &&
                        !script.Command.Contains(string.Format("{0}{1}{2}", "{!", p.Name, "}")))
                        validationOutput += string.Format("Script number {0} is missing a reference to the bound parameter: {1}\r\n", this.scriptsToExecute.Count + 1, p.Name);
                }

                if (validationOutput != "")
                    throw new KeyNotFoundException("The following errors were found processing the scripts:\r\n\r\n" + validationOutput);
            }

            this.BindActiveScript(instance);

            return (T)(object)this;
        }

        void BindActiveScript(object instance)
        {
            Regex rx = new Regex(@"{(#|!)[\w\d\.]+}", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            var properties = instance.GetType().GetTypeInfo().DeclaredProperties;

            string result = rx.Replace(this.activeScript.Command, (m) => this.Bind(m, properties, instance));
            this.activeScript.Command = result;
        }

        string Bind(Match m, IEnumerable<PropertyInfo> properties, object instance)
        {
            var key = m.ToString();
            // Remove braces
            var  property = key.Substring(2, key.Length - 3);
            var bindingType = key.Substring(1,1);

            // Find key in resource manager for this culture
            PropertyInfo value = properties.FirstOrDefault(p => p.Name == property);

            if (value != null)
            {
                var textValue = value.GetValue(instance).ToString();
                if(bindingType == "#")
                    return SanitizeValue(textValue);
                else
                    return textValue;
            }
            else
            {
                return m.ToString();
            }
        }

        static string SanitizeValue(string stringValue)
        {
            if (null == stringValue)
                return stringValue;
            return RegexReplace(RegexReplace(RegexReplace(stringValue,"-{2,}", "-"), // transforms multiple --- in - use to comment in sql scripts
                        @"[*/]+", string.Empty),                                     // removes / and * used also to comment in sql scripts
                        @"(;|\s)(exec|execute|select|insert|update|delete|create|alter|drop|rename|truncate|backup|restore|and|or|not)\s", string.Empty, RegexOptions.IgnoreCase);
        }


        private static string RegexReplace(string stringValue, string matchPattern, string toReplaceWith)
        {
            return Regex.Replace(stringValue, matchPattern, toReplaceWith);
        }

        private static string RegexReplace(string stringValue, string matchPattern, string toReplaceWith, RegexOptions regexOptions)
        {
            return Regex.Replace(stringValue, matchPattern, toReplaceWith, regexOptions);
        }

        public virtual Result Execute()
        {
            // add the currently active script to the list
            if (this.activeScript != null)
            {
                this.ApplyAppendedScriptsToActiveScript();
                this.scriptsToExecute.Add(this.activeScript);
            }

            // Perform some validations on the script
            if (this.EnableValidationsProperty)
                this.ValidateScript();

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
                scriptNumberCount++;
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

            if (validationOutput != "")
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

        protected virtual void ResetState()
        {
            this.CommandToAppend.Clear();
        }
    }
}