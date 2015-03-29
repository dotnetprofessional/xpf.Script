using System.Collections.Generic;

namespace xpf.Scripting.SQLServer
{
    /// <summary>
    /// Tracks instances of objects that have been persisted
    /// </summary>
    public class IdentityMap 
    {
        Dictionary<string, object> Map { get; set; }

        public IdentityMap()
        {
            this.Map = new Dictionary<string, object>();
        }

        /// <summary>
        /// Returns the item if the key exists otherwise default(T)
        /// </summary>
        /// <typeparam name="T">type of item</typeparam>
        /// <param name="key">the unique key used when storing the item</param>
        /// <returns></returns>
        public T GetItem<T>(string key)
        {
            if (this.Map.ContainsKey(key))
                return (T)this.Map[key];

            return default(T);
        }

        /// <summary>
        /// Returns true if an item has been stored with the key otherwise false
        /// </summary>
        /// <param name="key">the unique key used when storing the item</param>
        /// <returns></returns>
        public bool ContainsKey<T>(string key)
        {
            return this.Map.ContainsKey(key);
        }

        public void StoreItem(string key, object item)
        {
            this.Map.Add(key, item);
        }

        public int Count { get { return this.Map.Count; } }
    }
}