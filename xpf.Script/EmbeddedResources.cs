using System.IO;
using System.Reflection;

namespace xpf.Scripting
{
    internal static class EmbeddedResources
    {
        public static Stream GetResourceStream(string resourceName, Assembly assembly)
        {
            string strFullResourceName = "";
            foreach (string r in assembly.GetManifestResourceNames())
            {
                if (r.EndsWith(resourceName))
                {
                    strFullResourceName = r;
                    break;
                }
            }

            if (strFullResourceName != "")
                return assembly.GetManifestResourceStream(strFullResourceName);
            else
                return null;
        }

        /// <summary>
        /// Returns the contents of an embedded resource file as a string
        /// </summary>
        /// <param name="assembly"></param>
        /// <param name="resourceName">The name of the resource file to return. The filename is matched using EndsWith allowing for partial filenames to be used.</param>
        /// <returns></returns>
        public static string GetResourceString(Assembly assembly, string resourceName)
        {
            StreamReader objStream;
            string strText = "";

            var resource = GetResourceStream(resourceName, assembly);
            if(resource == null)
                throw new FileNotFoundException(string.Format("Unable to locate the resource: {0} in assembly {1}. Check that the file has been marked as an embedded resource.", resourceName, assembly.GetName().Name));

            using (objStream = new StreamReader(resource))
            {
                strText = objStream.ReadToEnd();
            }

            return strText;
        }
    }
}