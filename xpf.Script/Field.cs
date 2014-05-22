namespace xpf.Scripting
{
    public class Field
    {
        public Field(string name, object value)
        {
            this.Name = name;
            this.Value = value;
        }

        public string Name { get; private set; }
        public object Value { get; private set; }
    }
}
