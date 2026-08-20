using System;
using System.Reflection;

namespace AbilityKit.Core.Reflection
{
    /// <summary>
    /// Provides compatibility helpers for broad runtime type lookup and static invocation.
    /// </summary>
    [Obsolete("Broad reflection invocation no longer belongs in Core. Use a narrow owner-specific bootstrap contract before the next major version.")]
    public static class ReflectionInvokeUtils
    {
        /// <summary>
        /// Finds a loaded type by its full name or assembly-qualified name.
        /// </summary>
        /// <param name="typeNameOrAssemblyQualified">The full or assembly-qualified type name.</param>
        /// <returns>The resolved type, or <see langword="null"/> when the name cannot be resolved.</returns>
        public static Type? FindType(string typeNameOrAssemblyQualified)
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

        /// <summary>
        /// Attempts to invoke a public parameterless static method.
        /// </summary>
        /// <param name="typeNameOrAssemblyQualified">The full or assembly-qualified type name.</param>
        /// <param name="methodName">The static method name.</param>
        /// <returns><see langword="true"/> when the method was found and invoked; otherwise, <see langword="false"/>.</returns>
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

        /// <summary>
        /// Attempts to invoke a public parameterless static method and reports invocation failures.
        /// </summary>
        /// <param name="typeNameOrAssemblyQualified">The full or assembly-qualified type name.</param>
        /// <param name="methodName">The static method name.</param>
        /// <param name="exception">The invocation exception, or <see langword="null"/> when no exception was thrown.</param>
        /// <returns><see langword="true"/> when the method was found and invoked; otherwise, <see langword="false"/>.</returns>
        public static bool TryInvokeStaticMethod(string typeNameOrAssemblyQualified, string methodName, out Exception? exception)
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
