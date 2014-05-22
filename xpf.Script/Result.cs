using System.Collections.Generic;

namespace xpf.Scripting
{
    public class Result
    {
        object syncLoc = new object();
        public Result()
        {
            lock (syncLoc)
            {
                this.Results = new List<ResultItem>();
            }
        }

        public void AddResult(FieldList values)
        {
            lock (syncLoc)
            {
                this.Results.Add(new ResultItem(values));
                if (this.Results.Count == 1)
                {
                    this.Property = this.Results[0].Property;
                    this.Properties = values;
                }
            }
        }

        public List<ResultItem> Results { get; private set; }

        /// <summary>
        /// Accesses the properties of the first executed script
        /// </summary>
        public dynamic Property { get; private set; }

        public FieldList Properties { get; private set; }
    }

    /*
     *  Usage
     *
     * xpf.Script("sql").
     * xpf.Script("ps").
     * 
     * 
     *  xpf.Script()
     *      .WithDatabase("xxx")
     *      .TakeSnapshot.
     *      .UsingScript("xxx")
     *          .WithIn(new {Name = "ssss"})
     *          .WithOut(nwe {Name = DbType.String})
     *      .UsingCommand("xxx")
     *          .Execute();
     * 
     * 
     */
}
