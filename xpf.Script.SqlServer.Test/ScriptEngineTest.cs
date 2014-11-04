using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using FluentAssertions;
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
            Action action = () => new Script()
                .Database()
                .UsingCommand("SELECT * FROM TABLE")
                .WithOut(new {Property1 = "Hello"})
                .WithOut(new {Property2 = "Goodbye"});

            action.ShouldThrow<ArgumentException>();
        }

        [TestMethod]
        public void When_calling_Execute_using_WithIn_parameter_that_are_not_in_script_will_throw_exception_with_enable_validations()
        {
            Action action = () => new Script()
                .Database()
                .EnableValidations()
                .UsingCommand("SELECT * FROM TABLE WHERE Field=@Property2")
                .WithIn(new {Property1 = "Hello", Property2 = "Goodbye"})
                .Execute();

            action.ShouldThrow<KeyNotFoundException>().WithMessage("*Property1*").Which.Message.Should().NotContain("Property2");
        }

        [TestMethod]
        public void When_calling_Execute_using_WithOut_parameter_that_are_not_in_script_will_throw_exception_with_enable_validations()
        {
            Action action = () => new Script()
                .Database()
                .EnableValidations()
                .UsingCommand("SELECT * FROM TABLE WHERE Field=@Property2")
                .WithOut(new {Property1 = "Hello", Property2 = "Goodbye"})
                .Execute();

            action.ShouldThrow<KeyNotFoundException>().WithMessage("*Property1*").Which.Message.Should().NotContain("Property2");
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
        public void When_taking_a_snapshot_and_providing_a_connection_string()
        {
            new Script()
              .Database().WithConnectionString("Data Source=.;Initial Catalog=xpfScript;Trusted_Connection=yes;")
              .TakeSnapshot()
              .Execute();

            // Assert
            var actual = new Script().Database()
                .UsingCommand("SELECT @Count = Count(*) FROM sys.databases sd WHERE sd.source_database_id = db_id()")
                .WithOut(new { Count = DbType.Int32 })
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

        [TestMethod]
        public void When_taking_a_snapshot_that_currently_has_an_existing_snapshot_the_existing_snapshot_is_restored_before_taking_new_snapshot()
        {
            // Arrange - create a snapshot
            new Script()
                .Database()
                .TakeSnapshot()
                .Execute();

            // Create a record within the working copy
            new Script()
                .Database()
                .UsingCommand("INSERT INTO TestTable (id, Field1) VALUES(1000,'Snapshottest')")
                .Execute();

            // Act! - Take new Snapshot which should revert the previous working copy data
            new Script()
                .Database()
                .TakeSnapshot()
                .Execute();

            // Validate that the new record from the working copy do not exist
            var result = new Script()
                .Database()
                .UsingCommand("SELECT @Id = Id FROM TestTable WHERE Id = 1000")
                .WithOut(new { Id = DbType.Int32 })
                .Execute();

            // Record shouldn't exist
            Assert.AreEqual(DBNull.Value, result.Property.Id);

            Assert.AreEqual(DBNull.Value, result.Property.Id);

            new Script()
                .Database()
                .DeleteSnapshot()
                .Execute();
        }
    }
}
