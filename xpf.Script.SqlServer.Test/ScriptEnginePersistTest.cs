using System;
using System.Linq;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using xpf.Scripting.SQLServer;
using xpf.Scripting.SqlServer.Test.Model;

namespace xpf.Scripting.SqlServer.Test
{
    /// <summary>
    /// Summary description for ScriptEnginePersistTest
    /// </summary>
    [TestClass]
    public class ScriptEnginePersistTest
    {
        [TestMethod]
        public void When_persisting_a_simple_class_a_single_event_is_fired()
        {
            var handler = new PersistCustomerHandler();
            var customer = new Customer {FirstName = "john", LastName = "Wayne"};

            var identityMap = new Script().Database().Persist(customer, handler);

            handler.GetTraceLog().Should().Be("OrderId none for Customer: john Wayne");
        }

        [TestMethod]
        public void When_persisting_a_complex_class_an_event_is_fired_for_each_class_instance()
        {
            var handler = new PersistCustomerHandler();
            var order = new Order();
            var customer = new Customer { FirstName = "john", LastName = "Wayne" };
            order.Customer = customer;
            order.LineItems.Add(new LineItem { Price = 100, SKU = "XX1" });
            order.LineItems.Add(new LineItem { Price = 200, SKU = "XX2" });
            order.LineItems.Add(new LineItem { Price = 300, SKU = "XX3" });
            order.LineItems.Add(new LineItem { Price = 400, SKU = "XX4" });

            var identityMap = new Script().Database().Persist(order, handler);

            // There are 6 class instances that needed to be stored
            identityMap.Count.Should().Be(6);

            handler.GetTraceLog().Should().Be(@"OrderId: 1234
OrderId 1234 for Customer: john Wayne
Line item: 1 with SKU XX1
Line item: 2 with SKU XX2
Line item: 3 with SKU XX3
Line item: 4 with SKU XX4");
        }
    }


    public class PersistCustomerHandler: IPersistType<Customer>, IPersistType<Order>, IPersistType<LineItem>
    {
        
        public List<string> TraceLog { get; set; }
        public PersistCustomerHandler()
        {
            this.TraceLog = new List<string>();
        }

        public void Persist(object parent, Customer typeInstance, IdentityMap identityMap)
        {
            var log = string.Format("OrderId {0} for Customer: {1} {2}", parent == null ? (object) "none" : ((Order)parent).Id, typeInstance.FirstName, typeInstance.LastName);
            this.TraceLog.Add(log);

            identityMap.StoreItem(typeInstance.Id.ToString(), typeInstance);
        }

        public string GetTraceLog()
        {
            return string.Join(Environment.NewLine, this.TraceLog);
        }

        public void Persist(object parent, Order typeInstance, IdentityMap identityMap)
        {
            typeInstance.Id = 1234;
            identityMap.StoreItem(typeInstance.Id.ToString(), typeInstance);

            var log = "OrderId: " + typeInstance.Id;
            this.TraceLog.Add(log);
        }

        public void Persist(object parent, LineItem typeInstance, IdentityMap identityMap)
        {
            var order = parent as Order;
            typeInstance.Id = order.LineItems.Max(l => l.Id) + 1;

            identityMap.StoreItem(typeInstance.Id.ToString(), typeInstance);

            var log = string.Format("Line item: {0} with SKU {1}", typeInstance.Id, typeInstance.SKU);
            this.TraceLog.Add(log);
        }
    }
}
