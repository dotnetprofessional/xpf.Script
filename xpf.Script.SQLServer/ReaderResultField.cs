using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;

namespace xpf.Scripting.SQLServer
{
    public class ReaderResultField : DynamicObject
    {
        IDataReader _dataReader = null;
        Dictionary<string, int> _fields = new Dictionary<string, int>();

        // Used to inform user what fields are available when they get it wrong!
        string formattedFieldList = null;
        internal ReaderResultField(IDataReader dataReader)
        {
            this._dataReader = dataReader;
            var fieldList = new List<string>();
            for (int i = 0; i < dataReader.FieldCount; i++)
            {
                var name = dataReader.GetName(i);
                this._fields.Add(name.ToLower(), i);
                fieldList.Add(name);
            }
            this.formattedFieldList = string.Join(", ", fieldList);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            if (this._fields.ContainsKey(binder.Name.ToLower()))
            {
                var fieldId = this._fields[binder.Name.ToLower()];
                result = this._dataReader[fieldId];
                return true;
            }
            else
            {
                throw new ArgumentException(string.Format("The property {0} is not part of the schema. The schema has the following fields: {1}", binder.Name, this.formattedFieldList));
            }
        }
    }
}