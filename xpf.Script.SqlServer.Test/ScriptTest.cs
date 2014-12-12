using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Transactions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using xpf.Scripting;
using xpf.Scripting.SQLServer;

namespace xpf.IO.Test
{
    [TestClass]
    public class ScriptTest
    {
        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void Setup(TestContext testContext)
        {
            // Define where the SQL Resource files for these tests exist
            //Script.SetResourceAssembly(typeof(ScriptTest));
        }


        private const string GetTestTableRecordScript = "Execute_GetTestTableRecord.sql";

        [TestMethod]
        public void Execute_SupportOutParamsOnly()
        {
            string embeddedScriptName = "Execute_SupportOutParams.sql";
            var result = new Script()
                .Database()
                .UsingScript(embeddedScriptName)
                .WithOut(new { outParam1 = DbType.Int32 })
                .Execute();

            Assert.AreEqual(1, result.Property.OutParam1);
        }

        [TestMethod]
        public void Execute_SupportInAndOutParams()
        {
            string embeddedScriptName = "Execute_SupportInAndOutParams.sql";

            var result = new Script()
                .Database()
                .UsingScript(embeddedScriptName)
                .WithIn(new {MyParam1 = 2})
                .WithOut(new { outParam1 = DbType.Int32 })
                .Execute();

            Assert.AreEqual(2, result.Property.OutParam1);
        }

        [TestMethod]
        public void Execute_SupportInParamsWithNullValues()
        {
            string nullValue = null;
            var result = new Script()
                .Database()
                .UsingCommand("Select @OutParam1 = Id from TestTable where Id = 1 OR Id = @MyParam1")
                .WithIn(new { MyParam1 = nullValue })
                .WithOut(new { OutParam1 = DbType.Int32})
                .Execute();

            Assert.AreEqual(1, result.Property.OutParam1);
        }

        [TestMethod]
        public void ExecuteReader_SupportInParamsWithNullValues()
        {
            string nullValue = null;
            using (var result = new Script()
                .Database()
                .UsingCommand("Select * from TestTable where Id = 1")
                .WithIn(new {MyParam1 = nullValue})
                .ExecuteReader())
            {
                result.Should().NotBeNull();
            }
        }
        
        [TestMethod]
        public void Execute_ScriptNameOnly()
        {
            using (var x = new TransactionScope())
            {
                // Execute an update on the table
                new Script()
                    .Database()
                    .UsingScript("Execute_ScriptNameOnly.sql")
                    .Execute();

                // Now verify that the update was successful
                var result = new Script()
                    .Database()
                    .UsingScript(GetTestTableRecordScript)
                    .WithIn(new { Param1 = 2 })
                    .WithOut(new { outParam1 = DbType.Int32, outParam2 = DbType.AnsiString, outParam3 = DbType.AnsiString})
                    .Execute();


                Assert.AreEqual("Record X", result.Property.OutParam2);
            }
        }

        [TestMethod]
        public void ExecuteReader_ScriptNameWithAppendCommand()
        {
            var count = 0;
                // Execute an update on the table
            using (var script = new Script()
                .Database()
                .UsingScript("ExecuteReader_SelectAll.sql").AppendCommand("WHERE Id = 1")
                .ExecuteReader())
            {
                while (script.NextRecord())
                {
                    count++;
                }
            }

            // The Where clause added should restrict the result to a single record
            count.Should().Be(1);
        }

        [TestMethod]
        public void ExecuteReader_ScriptNameWithMultipleAppendCommands()
        {
            var count = 0;
            // Execute an update on the table
            using (var script = new Script()
                .Database()
                .UsingScript("ExecuteReader_SelectAll.sql").AppendCommand("WHERE Id = 1").AppendCommand("OR Id = 2").AppendCommand("OR Id=3")
                .ExecuteReader())
            {
                while (script.NextRecord())
                {
                    count++;
                }
            }

            // The Where clause added should restrict the result to a single record
            count.Should().Be(3);
        }

        [TestMethod]
        public void ExecuteReader_ScriptNameWithMultipleAppendCommandsWithFormattingDisabled()
        {
            var count = 0;
            // Execute an update on the table
            using (var script = new Script()
                .Database()
                .UsingScript("ExecuteReader_SelectAll.sql").AppendCommand(" WHERE Field1='").AppendCommand("Record 1").AppendCommand("'").DisableAppendFormatting
                .ExecuteReader())
            {
                while (script.NextRecord())
                {
                    count++;
                }
            }

            // The Where clause added should restrict the result to a single record
            count.Should().Be(1);
        }



        [TestMethod]
        public void Execute_ScriptNameWithAppendCommand()
        {
            var count = 0;
            // Execute an update on the table
            var script = new Script()
                .Database()
                .UsingScript(GetTestTableRecordScript).AppendCommand("OR Id = 1")
                .WithIn(new {Param1 = 200}) // Key doesn't exist
                .WithOut(new {outParam1 = DbType.Int32, outParam2 = DbType.AnsiString, outParam3 = DbType.AnsiString})
                .Execute();

            // The Where clause added should restrict the result to a single record
            Assert.AreEqual(1, script.Properties["outParam1"].Value);
        }

        [TestMethod]
        public void Execute_ScriptNameWithMultipleAppendCommands()
        {
            var script = new Script()
               .Database()
               .UsingScript(GetTestTableRecordScript).AppendCommand("OR Id = 1").AppendCommand("OR Id = 201")
               .WithIn(new { Param1 = 200 }) // Key doesn't exist
               .WithOut(new { outParam1 = DbType.Int32, outParam2 = DbType.AnsiString, outParam3 = DbType.AnsiString })
               .Execute();

            // The Where clause added should restrict the result to a single record
            Assert.AreEqual(1, script.Properties["outParam1"].Value);
        }

        [TestMethod]
        public void Execute_ScriptNameWithMultipleAppendCommandsWithFormattingDisabled()
        {
            var script = new Script()
               .Database()
               .UsingScript(GetTestTableRecordScript).AppendCommand(" OR Field1='").AppendCommand("Record 2").AppendCommand("'").DisableAppendFormatting
               .WithIn(new { Param1 = 200 }) // Key doesn't exist
               .WithOut(new { outParam1 = DbType.Int32, outParam2 = DbType.AnsiString, outParam3 = DbType.AnsiString })
               .Execute();

            // The Where clause added should restrict the result to a single record
            Assert.AreEqual(2, script.Properties["outParam1"].Value);
        }

        [TestMethod]
        public void Execute_CommandWithMultipleAppendCommandsWithFormattingDisabled()
        {
            var script = new Script()
               .Database()
               .UsingCommand("SELECT @Count = COUNT(1) FROM TestTable").AppendCommand(" WHERE Field1='").AppendCommand("Record 1").AppendCommand("'").DisableAppendFormatting
               .WithOut(new { Count = DbType.Int32})
               .Execute();

            // The Where clause added should restrict the result to a single record
            Assert.AreEqual(1, script.Properties["Count"].Value);
        }

        [TestMethod]
        public void Execute_CommandWithMultipleAppendCommands()
        {
            var script = new Script()
               .Database()
               .UsingCommand("SELECT @Count = COUNT(1) FROM TestTable").AppendCommand("WHERE Id = 1").AppendCommand("OR Id = 2").AppendCommand("OR Id=3")
               .WithOut(new { Count = DbType.Int32 })
               .Execute();

            // The Where clause added should restrict the result to a single record
            Assert.AreEqual(3, script.Properties["Count"].Value);
        }

        [TestMethod]
        public void Execute_WithConnectionString()
        {
            using (var x = new TransactionScope())
            {
                // Execute an update on the table
                new Script()
                    .Database()
                    .WithConnectionString("Data Source=.;Initial Catalog=xpfScript;Trusted_Connection=yes;")
                    .UsingScript("Execute_ScriptNameOnly.sql")
                    .Execute();

                // Now verify that the update was successful
                var result = new Script()
                    .Database()
                    .UsingScript(GetTestTableRecordScript)
                    .WithIn(new { Param1 = 2 })
                    .WithOut(new { outParam1 = DbType.Int32, outParam2 = DbType.AnsiString, outParam3 = DbType.AnsiString })
                    .Execute();

                Assert.AreEqual("Record X", result.Property.OutParam2);
            }
        }

        [TestMethod]
        public void Execute_NestedScriptNameOnlyWithNestedInclude()
        {
            using (var x = new TransactionScope())
            {
                string embeddedScriptName = "Execute_IncludeScript.sql";
                new Script()
                    .Database()
                    .UsingNestedScript(embeddedScriptName)
                    .Execute();

                // Now verify that the update was successful
                var result = new Script()
                    .Database()
                    .UsingCommand("SELECT @RowCount = COUNT(*) FROM TestTable WHERE Id >= 10")
                    .WithOut(new { RowCount = DbType.Int32})
                    .Execute();

                Assert.AreEqual(3, result.Property.RowCount);
            }
        }

        [TestMethod]
        public void Execute_NestedScriptNameOnlyWithNestedColonR()
        {
            using (var x = new TransactionScope())
            {
                string embeddedScriptName = "Execute_IncludeScriptUsingColonRSyntax.sql";

                new Script()
                    .Database()
                    .UsingNestedScript(embeddedScriptName)
                    .Execute();

                // Now verify that the update was successful
                var result = new Script()
                    .Database()
                    .UsingCommand("SELECT @RowCount = COUNT(*) FROM TestTable WHERE Id >= 10")
                    .WithOut(new { RowCount = DbType.Int32 })
                    .Execute();

                Assert.AreEqual(3, result.Property.RowCount);
            }
        }

        [TestMethod]
        public void Execute_DatabaseNameAndScriptName()
        {
            // Essentially the same test as Execute_ScriptNameOnly
            using (var x = new TransactionScope())
            {
                // Execute an update on the table
                new Script()
                    .Database()
                    .UsingScript("Execute_ScriptNameOnly.sql")
                    .Execute();

                // Now verify that the update was successful
                var result = new Script()
                    .Database("xpfScript")
                    .UsingScript(GetTestTableRecordScript)
                    .WithIn(new { Param1 = 2 })
                    .WithOut(new { outParam1 = DbType.Int32, outParam2 = DbType.AnsiString, outParam3 = DbType.AnsiString })
                    .Execute();


                Assert.AreEqual("Record X", result.Property.OutParam2);
            }
        }

        [TestMethod]
        public void Execute_ScriptNameAndOutParmsAsStringArrayAndInParams()
        {
            string embeddedScriptName = "Execute_SupportInAndOutParams.sql";

            // Testing the less recommended way of passing outparams as a simple string array
            var result = new Script()
                .Database()
                .UsingScript(embeddedScriptName)
                .WithIn(new { MyParam1 = 2 })
                .WithOut(new[] { "outParam1"})
                .Execute();

            Assert.AreEqual(2, result.Property.OutParam1);
        }


        [TestMethod]
        public void ExecuteReader_ScriptNameOnly()
        {
            string embeddedScriptName = "ExecuteReader_ScriptNameOnly.sql";


            using (var result = new Script()
                .Database("xpfScript")
                .UsingScript(embeddedScriptName)
                .ExecuteReader())
            {
                int count = 0;
                while (result.NextRecord())
                    count++;

                // Now verify results
                Assert.AreEqual(3, count);
            }
        }

        [TestMethod]
        public void ExecuteReader_SupportInParams()
        {
            string embeddedScriptName = "ExecuteReader_SupportInParams.sql";

            using (var result = new Script()
                .Database()
                .UsingScript(embeddedScriptName)
                .WithIn(new { Param1 = 2 })
                .ExecuteReader())
            {
                int count = 0;
                while (result.NextRecord())
                    count++;

                // Now verify results
                Assert.AreEqual(1, count);
            }
        }

        [TestMethod]
        public void ExecuteReader_Access_fields_by_name()
        {
            string embeddedScriptName = "ExecuteReader_SelectAll.sql";

            using (var result = new Script()
                .Database()
                .UsingScript(embeddedScriptName)
                .ExecuteReader())
            {
                int count = 0;
                while (result.NextRecord())
                {
                    count ++;
                    Assert.AreEqual(count, result.Fields["Id"].Value);
                    Assert.AreEqual("Record " + count, result.Fields["Field1"].Value);
                }

                // Now verify results
                Assert.AreEqual(3, count);
            }
        }

        [TestMethod]
        public void ExecuteReader_Access_field_by_name()
        {
            string embeddedScriptName = "ExecuteReader_SelectAll.sql";

            using (var result = new Script()
                .Database()
                .UsingScript(embeddedScriptName)
                .ExecuteReader())
            {
                int count = 0;
                while (result.NextRecord())
                {
                    count++;
                    Assert.AreEqual(count, result.Field.Id);
                    Assert.AreEqual("Record " + count, result.Field.Field1);
                }

                // Now verify results
                Assert.AreEqual(3, count);
            }
        }
        [TestMethod]
        public void ExecuteReader_FromXmlToInstance()
        {
            string embeddedScriptName = "ExecuteT_ScriptOnly.sql";

            var result = new Script()
                .Database()
                .UsingScript(embeddedScriptName)
                .ExecuteReader()
                .FromXmlToInstance<List<TestTable>>();

            Assert.AreEqual(27, result.Count);
        }

        [TestMethod]
        public void ExecuteReader_ToInstance()
        {
            string embeddedScriptName = "ExecuteReader_SelectAll.sql";

            var result = new Script()
                .Database()
                .UsingScript(embeddedScriptName)
                .ExecuteReader().ToInstance<TestTable>();

            Assert.AreEqual(3, result.Count);
        }

        [TestMethod]
        public void ExecuteReader_ToInstance_WithNullableTypes()
        {
            string script = "SELECT * FROM DataTypeCheckTable";

            var result = new Script()
                .Database()
                .UsingCommand(script)
                .ExecuteReader()
                .ToInstance<DataTypeCheckTable>();

            Assert.AreEqual(2, result.Count);
            Assert.IsNotNull(result[0].NonNullDateTime);
            Assert.IsNotNull(result[0].NonNullNumeric);
            Assert.IsNotNull(result[0].NonNullString);
            Assert.IsNull(result[0].NullableDateTime);
            Assert.IsNull(result[0].NullableNumeric);
            Assert.IsNull(result[0].NullableString);
        }

        [TestMethod]
        public void Execute_allows_multiple_scripts_to_be_executed()
        {

            using (var tx = new TransactionScope())
            {
                // Execute the two different scripts
                var actual = new Script()
                    .Database()
                    .UsingScript("Execute_InsertRecord.sql") // Add one new record
                    .WithIn(new {Id = 12})
                    .UsingScript("Execute_InsertRecord.sql") // Add one new record
                    .WithIn(new { Id = 13 })
                    .Execute();

                // Now verify that the update was successful
                var result = new Script()
                    .Database()
                    .UsingCommand("SELECT @RowCount = COUNT(*) FROM TestTable WHERE Id >= 10")
                    .WithOut(new {RowCount = DbType.Int32})
                    .Execute();

                // Verify that the results and properties have been applied correctly
                Assert.AreEqual(2, actual.Results.Count);
                Assert.AreEqual(12, actual.Property.Id);
                Assert.AreEqual(12, actual.Results[0].Property.Id);
                Assert.AreEqual(13, actual.Results[1].Property.Id);

                // Verify that the two scripts wer executed
                Assert.AreEqual(2, result.Property.RowCount);
            }
        }

        [TestMethod]
        public void Execute_allows_multiple_scripts_to_be_executed_in_parallel()
        {
            try
            {


            // Execute the two different scripts
            var actual = new Script()
                .Database()
                .EnableParallelExecution()
                .UsingScript("Execute_InsertRecord.sql") // Add one new record
                .WithIn(new {Id = 12})
                .UsingScript("Execute_InsertRecord.sql") // Add one new record
                .WithIn(new {Id = 13})
                .Execute();

            // Now verify that the update was successful
            var result = new Script()
                .Database()
                .UsingCommand("SELECT @RowCount = COUNT(*) FROM TestTable WHERE Id >= 10")
                .WithOut(new {RowCount = DbType.Int32})
                .Execute();

            // Verify that the results and properties have been applied correctly
            Assert.AreEqual(2, actual.Results.Count);

            // Unable to verify the properties as the order is no longer guaranteed
            //Assert.AreEqual(12, actual.Properties.Id);
            //Assert.AreEqual(12, actual.Results[0].Properties.Id);
            //Assert.AreEqual(13, actual.Results[1].Properties.Id);

            // Verify that the two scripts wer executed
            Assert.AreEqual(2, result.Property.RowCount);
            }
            finally
            {
                // This is a compensating script to remove the effects of the previous script
                // a transaction wasn't able to be used due to the use of EnableParallelExecution
                new Script()
                    .Database()
                    .UsingCommand("delete from TestTable where Id > 10")
                    .Execute();
            }
        }

        [TestMethod]
        public void Execute_Specifying_timeout_adjusts_time_allowed_for_command_execution()
        {
            try
            {

            // Specify a wait of 1 second and make script run for 2 seconds. This should fail!
            new Script()
                .Database()
                .WithTimeout(1)
                .UsingCommand("WAITFOR DELAY '00:00:02'")
                .Execute();
                
                Assert.Fail("A timeout excepetion should have been raised");
            }
            catch (SqlScriptException ex)
            {
                Assert.AreEqual("Timeout expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.", ex.InnerException.Message);
            }
        }

        [TestMethod]
        public void ExecuteReader_Specifying_timeout_adjusts_time_allowed_for_command_execution()
        {
            try
            {

                // Specify a wait of 1 second and make script run for 2 seconds. This should fail!
                new Script()
                    .Database()
                    .WithTimeout(1)
                    .UsingCommand("WAITFOR DELAY '00:00:02'")
                    .ExecuteReader();

                Assert.Fail("A timeout excepetion should have been raised");
            }
            catch (SqlScriptException ex)
            {
                ex.InnerException.Message.Should().Be("Timeout expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.");
            }
        }

        [TestMethod]
        public void Execute_Result_has_properties_list()
        {
            using (var tx = new TransactionScope())
            {
                // Execute the two different scripts
                var actual = new Script()
                    .Database()
                    .UsingScript("Execute_InsertRecord.sql") // Add one new record
                    .WithIn(new {Id = 12})
                    .UsingScript("Execute_InsertRecord.sql") // Add one new record
                    .WithIn(new {Id = 13})
                    .Execute();

                Assert.AreEqual("Id", actual.Properties["Id"].Name);
                Assert.AreEqual(12, actual.Properties["Id"].Value);
            }
        }
    }
}