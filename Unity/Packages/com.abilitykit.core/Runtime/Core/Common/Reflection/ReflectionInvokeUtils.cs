using System;
using System.Reflection;

namespace AbilityKit.Core.Common.Reflection
{
    public static class ReflectionInvokeUtils
    {
        public static Type FindType(string typeNameOrAssemblyQualified)
        {
            if (string.IsNullOrEmpty(typeNameOrAssemblyQualified)) return null;

            var t = Type.GetType(typeNameOrAssemblyQualified, throwOnError: false);
            if (t != null) return t;

            try
            {
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    var tt = asms[i].GetType(typeNameOrAssemblyQualified, throwOnError: false);
                    if (tt != null) return tt;
                }
            }
            catch
            {
            }

            return null;
        }

        public static bool TryInvokeStaticMethod(string typeNameOrAssemblyQualified, string methodName)
        {
            try
            {
                var t = FindType(typeNameOrAssemblyQualified);
                if (t == null) return false;

                var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (m == null) return false;

                m.Invoke(null, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryInvokeStaticMethod(string typeNameOrAssemblyQualified, string methodName, out Exception exception)
        {
            try
            {
                exception = null;
                var t = FindType(typeNameOrAssemblyQualified);
                if (t == null) return false;

                var m = t.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (m == null) return false;

                m.Invoke(null, null);
                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }
        }
    }
}
