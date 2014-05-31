using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using xpf.IO;

namespace xpf.Scripting.SQLServer
{
    public class ReaderResult : IDisposable
    {
        IDataReader _dataReader = null;

        public ReaderResult(IDataReader dataReader)
        {
            this._dataReader = dataReader;
            this.Fields = new FieldList();
            this.Field = new ReaderResultField(dataReader);

            this.RecordsAffected = dataReader.RecordsAffected;

        }

        public dynamic Field { get; private set; }

        public int RecordsAffected { get; private set; }

        public bool NextRecord()
        {
            var nextResult = this._dataReader.Read();
            if (nextResult)
            {
                this.Fields = new FieldList();
                for (int i = 0; i < _dataReader.FieldCount; i++)
                {
                    this.Fields.Add(new Field(_dataReader.GetName(i), _dataReader[0]));
                }
            }

            return nextResult;
        }

        public bool NextResult()
        {
            return this._dataReader.NextResult();
        }

        public FieldList Fields { get; private set; } 
        public void Dispose()
        {
            this._dataReader.Dispose();
        }

        public T FromXmlToInstance<T>()
            where T : class
        {
            var result = default(T);

            using (var dr = this._dataReader)
            {
                var xmlBuilder = new StringBuilder();
                while (dr.Read())
                {
                    xmlBuilder.Append(dr[0]);
                }

                // Now we have the XML we can try desearlizing it.
                if (xmlBuilder.Length > 0)
                    result = xmlBuilder.ToString().FromXmlToInstance<T>();
            }

            return result;
        }

        public List<T> ToInstance<T>()
        {
            var result = new List<T>();

            using (var dr = this._dataReader)
            {
                while (dr.Read())
                {
                    var entity = Activator.CreateInstance<T>();
                    for (int i = 0; i < dr.FieldCount; i++)
                    {
                        entity.GetType().GetProperty(dr.GetName(i)).SetValue(entity, dr[i], null);
                    }

                    result.Add(entity);
                }
            }

            return result;

        }


    }

}