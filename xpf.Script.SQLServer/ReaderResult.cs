using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Xml.Serialization;

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
                for (int i = 0; i < this._dataReader.FieldCount; i++)
                {
                    this.Fields.Add(new Field(this._dataReader.GetName(i), this._dataReader[i]));
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
                    result = FromXmlToInstance<T>(xmlBuilder.ToString());
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
                        var property = entity.GetType().GetProperty(dr.GetName(i));
                        if (property == null)
                            throw new KeyNotFoundException(string.Format("Expected property {0} on entity which doesn't exist.", dr.GetName(i)));

                        var value = dr[i];
                        if (value == DBNull.Value)
                            property.SetValue(entity, null, null);
                        else
                            property.SetValue(entity, value, null);
                    }

                    result.Add(entity);
                }
            }

            return result;

        }

        public static T FromXmlToInstance<T>(string xml, params Type[] extraTypes)
            where T : class
        {
            return FromXmlStreamToInstance<T>(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
        }

        public static T FromXmlStreamToInstance<T>(Stream stream, params Type[] extraTypes)
            where T : class
        {
            T entity;
            using (stream)
            {
                var xser = new XmlSerializer(typeof(T), extraTypes);
                entity = xser.Deserialize(stream) as T;
            }
            return entity;
        }
    }

}