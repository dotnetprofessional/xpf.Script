using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace xpf.Scripting.SqlServer.Test.Model
{
    public class Customer
    {
        public int Id { get; set; }
        public string FirstName { get; set; }

        public string LastName { get; set; }
    }

    public class Order 
    {
        public Order()
        {
            this.LineItems = new List<LineItem>();
        }

        public int Id { get; set; }

        public DateTime OrderDate { get; set; }

        public Customer Customer { get; set; }

        public List<LineItem>LineItems { get; set; } 
    }

    public class LineItem
    {
        public int Id { get; set; }

        public decimal Price { get; set; }

        public string SKU { get; set; }
    }
}
