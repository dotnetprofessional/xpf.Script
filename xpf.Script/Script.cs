using System.Reflection;

namespace xpf.Scripting
{
    public class Script
    {
        public Script()
        {
            // This is a bit of a hack due to an issue of exposing GetCallingAssembly in a PCL
            // This method should only have an issue with WinRT apps and combining with native code
            // which is going to be rare. 
            CallingAssembly = (Assembly)typeof(Assembly).GetTypeInfo()
                .GetDeclaredMethod("GetCallingAssembly")
                .Invoke(null, new object[0]);
        }

        internal static Assembly CallingAssembly { get; private set; }
    }
}