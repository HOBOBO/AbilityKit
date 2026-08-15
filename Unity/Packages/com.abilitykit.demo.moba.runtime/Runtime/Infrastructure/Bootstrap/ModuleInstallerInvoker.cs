using System;
using System.Reflection;

namespace AbilityKit.Demo.Moba.Bootstrap
{
    public static class ModuleInstallerInvoker
    {
        public static bool TryInvoke(ModuleInstallerConfig installer)
        {
            return TryInvoke(installer, out _);
        }

        public static bool TryInvoke(ModuleInstallerConfig installer, out Exception exception)
        {
            try
            {
                exception = null;
                if (installer == null || !installer.IsValid) return false;

                var t = FindType(installer.InstallerType);
                if (t == null) return false;

                var m = t.GetMethod(installer.GetEffectiveMethod(), BindingFlags.Public | BindingFlags.Static);
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

        private static Type FindType(string typeNameOrAssemblyQualified)
        {
            if (string.IsNullOrEmpty(typeNameOrAssemblyQualified)) return null;

            var type = Type.GetType(typeNameOrAssemblyQualified, throwOnError: false);
            if (type != null) return type;

            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    var candidate = assemblies[i].GetType(typeNameOrAssemblyQualified, throwOnError: false);
                    if (candidate != null) return candidate;
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
