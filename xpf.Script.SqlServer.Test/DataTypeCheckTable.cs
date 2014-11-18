using System;

namespace xpf.IO.Test
{
    public class DataTypeCheckTable
    {
        public int Id { get; set; }
        public string NonNullString { get; set; }
        public string NullableString { get; set; }
        public int NonNullNumeric { get; set; }
        public int? NullableNumeric { get; set; }
        public DateTime NonNullDateTime { get; set; }
        public DateTime? NullableDateTime { get; set; }
    }
}