using System.Collections.ObjectModel;

namespace xpf.Scripting
{
    public class FieldList : KeyedCollection<string, Field>
    {
        protected override string GetKeyForItem(Field item)
        {
            return item.Name.ToLower();
        }

        public new Field this[string name]
        {
            get
            {
                // This indexer ensures keys are case insensitive
                return base[name.ToLower()];
            }
        }

        public new bool Contains(string name)
        {
            // This ensures keys are case insensitive
            return this[name] != null;
        }
    }
}