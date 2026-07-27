using System;
using System.Collections.Generic;
using System.Reflection;

public static class AssemblyUtility
{
    static readonly List<Assembly> AssemblyList = new List<Assembly>();
    
    static AssemblyUtility()
    {
        var        assemblyName = Assembly.GetExecutingAssembly().GetName();
        Assembly[] assemblies   = AppDomain.CurrentDomain.GetAssemblies();

        foreach (Assembly assembly in assemblies)
        { 
            AssemblyName[] referencedAssemblies = assembly.GetReferencedAssemblies();
            if (Array.Exists(referencedAssemblies, (a) => a.Name == assemblyName.Name))
            {
                AssemblyList.Add(assembly);
            }
        }
    }

    public static IEnumerable<Type> GetTypesSafe(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types;
        }

        foreach (Type type in types)
        {
            if (type != null)
                yield return type;
        }
    }

    public static List<Assembly> GetCompatibleAssemblies() { return AssemblyList; }
}