using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace xpf.Scripting.SqlServer.Test
{
    [TestClass]
    public class TracingTest
    {
        [TestMethod]
        public void Starting_Tracing_then_executing_a_script_captures_the_script_execution_details()
        {
            Script.Tracing.StartTracingWithReset();

            var result = new Script().Database()
                .UsingCommand("Select * from TestTable")
                .Execute();

            Script.Tracing.StopTracing();

            var entries = Script.Tracing.Entries;

            entries.Count.Should().Be(1);
            entries[0].Script.Should().Be("Select * from TestTable");

        }

        [TestMethod]
        public void Starting_Tracing_then_executing_ExecuteReader_a_script_captures_the_script_execution_details()
        {
            Script.Tracing.StartTracingWithReset();

            using (var result = new Script().Database()
                .UsingCommand("Select * from TestTable")
                .ExecuteReader())
            {
                
            }

            Script.Tracing.StopTracing();

            var entries = Script.Tracing.Entries;

            entries.Count.Should().Be(1);

        }
    }
}
