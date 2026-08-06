using System;
using System.Collections.Generic;
using System.Reflection;

namespace AbilityKit.Demo.Moba.Services
{
    internal static class MobaRegistryAssemblyDiscovery
    {
        public static Assembly[] GetExternalAssemblies(Assembly runtimeAssembly)
        {
            var assemblies = new List<Assembly>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == null || assembly == runtimeAssembly || assembly.IsDynamic)
                {
                    continue;
                }

                assemblies.Add(assembly);
            }

            assemblies.Sort((left, right) => string.CompareOrdinal(left.FullName, right.FullName));
            return assemblies.ToArray();
        }
    }
}
