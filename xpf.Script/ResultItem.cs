using System.Collections.Generic;

namespace xpf.Scripting
{
    public class ResultItem
    {
        public ResultItem(FieldList properties)
        {
            this.Property = new ResultProperties(properties);
            this.Properties = properties;
        }

        /// <summary>
        /// Accesses the properties of the first executed script
        /// </summary>
        public dynamic Property { get; private set; }

        public FieldList Properties { get; private set; }
    }
}