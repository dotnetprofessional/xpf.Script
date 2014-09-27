using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
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

        [TestMethod]
        public void When_taking_a_snapshot_a_database_snapshot_is_created_in_the_target_database()
        {
            new Script()
              .Database()
              .TakeSnapshot()
              .Execute();

            // Assert
            var actual = new Script().Database()
                .UsingCommand("SELECT @Count = Count(*) FROM sys.databases sd WHERE sd.source_database_id = db_id()")
                .WithOut(new {Count =DbType.Int32})
                .Execute();

            Assert.AreEqual(1, actual.Property.Count);
        }

        [TestMethod]
        public void When_restoring_a_snapshot_any_changes_are_rolled_back()
        {
            new Script()
                .Database()
                .TakeSnapshot()
                .Execute();

            new Script()
                .Database()
                .UsingCommand("INSERT INTO TestTable (id, Field1) VALUES(1000,'Snapshottest')")
                .Execute();

            // Validate that the new record exists
            var result = new Script()
                .Database()
                .UsingCommand("SELECT @Id = Id FROM TestTable WHERE Id = 1000")
                .WithOut(new {Id = DbType.Int32})
                .Execute();

            Assert.AreEqual(1000, result.Property.Id);

            // Restore the database
            new Script()
                .Database()
                .RestoreSnapshot()
                .Execute();

            result = new Script()
                .Database()
                .UsingCommand("SELECT @Id = Id FROM TestTable WHERE Id = 1000")
                .WithOut(new {Id = DbType.Int32})
                .Execute();

            Assert.AreEqual(DBNull.Value, result.Property.Id);
        }

        [TestMethod]
        public void When_deleting_a_snapshot_it_is_removed_from_the_database()
        {
            // Arange
            new Script()
              .Database()
              .TakeSnapshot()
              .Execute();

            // Act
            new Script()
              .Database()
              .DeleteSnapshot()
              .Execute();

            // Assert
            var actual = new Script().Database()
                .UsingCommand("SELECT @Count = Count(*) FROM sys.databases sd WHERE sd.source_database_id = db_id()")
                .WithOut(new { Count = DbType.Int32 })
                .Execute();

            Assert.AreEqual(0, actual.Property.Count);
        }
    }
}
