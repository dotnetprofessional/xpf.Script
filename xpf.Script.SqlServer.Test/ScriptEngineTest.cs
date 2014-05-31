using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using xpf.Scripting;

namespace xpf.Scripting.SqlServer.Test
{
    [TestClass]
    public class ScriptEngineTest
    {
        [TestMethod]
        public void When_calling_WithIn_more_than_once_for_a_script_will_throw_exception()
        {
            try
            {
                new Script()
                    .Database()
                    .UsingCommand("SELECT * FROM TABLE")
                    .WithIn(new {Property1 = "Hello"})
                    .WithIn(new {Property2 = "Goodbye"});

                Assert.Fail("Argument Exception expected");
            }
            catch (Exception ex)
            {
                Assert.AreEqual(typeof(ArgumentException), ex.GetType());
            }

        }

        [TestMethod]
        public void When_calling_WithOut_more_than_once_for_a_script_will_throw_exception()
        {
            try
            {
                new Script()
                    .Database()
                    .UsingCommand("SELECT * FROM TABLE")
                    .WithOut(new { Property1 = "Hello" })
                    .WithOut(new { Property2 = "Goodbye" });

                Assert.Fail("Argument Exception expected");
            }
            catch (Exception ex)
            {
                Assert.AreEqual(typeof(ArgumentException), ex.GetType());
            }

        }

        [TestMethod]
        public void When_calling_Execute_using_WithIn_parameter_that_are_not_in_script_will_throw_exception_with_enable_validations()
        {
            try
            {
                new Script()
                    .Database()
                    .EnableValidations()
                    .UsingCommand("SELECT * FROM TABLE WHERE Field=@Property2")
                    .WithIn(new { Property1 = "Hello", Property2 = "Goodbye" })
                    .Execute();

                Assert.Fail("KeyNotFoundException expected");
            }
            catch (Exception ex)
            {
                Assert.AreEqual(typeof(KeyNotFoundException), ex.GetType());
                Assert.IsTrue(ex.Message.Contains("Property1"));
                Assert.IsFalse(ex.Message.Contains("Property2"));
            }
        }

        [TestMethod]
        public void When_calling_Execute_using_WithOut_parameter_that_are_not_in_script_will_throw_exception_with_enable_validations()
        {
            try
            {
                new Script()
                    .Database()
                    .EnableValidations()
                    .UsingCommand("SELECT * FROM TABLE WHERE Field=@Property2")
                    .WithOut(new { Property1 = "Hello", Property2 = "Goodbye" })
                    .Execute();

                Assert.Fail("KeyNotFoundException expected");
            }
            catch (Exception ex)
            {
                Assert.AreEqual(typeof(KeyNotFoundException), ex.GetType());
                Assert.IsTrue(ex.Message.Contains("Property1"));
                Assert.IsFalse(ex.Message.Contains("Property2"));
            }
        }
    }
}
