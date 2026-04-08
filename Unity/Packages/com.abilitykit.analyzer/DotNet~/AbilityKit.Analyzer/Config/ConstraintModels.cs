using System;
using System.Collections.Generic;

namespace AbilityKit.Analyzer.Config
{
    public enum AKDiagnosticSeverity
    {
        Error = 0,
        Warning = 1,
        Info = 2,
        Hidden = 3
    }

    [Serializable]
    public sealed class PackageConstraint
    {
        public string PackageName { get; set; }

        public List<string> ForbiddenNamespaces { get; set; } = new();

        public List<string> ForbiddenAssemblies { get; set; } = new();

        public bool IsEnabled { get; set; } = true;

        public AKDiagnosticSeverity Severity { get; set; } = AKDiagnosticSeverity.Error;

        public bool CheckUsingAliases { get; set; } = true;

        public string Description { get; set; }

        public bool IsNamespaceForbidden(string @namespace)
        {
            if (string.IsNullOrEmpty(@namespace) || !IsEnabled)
                return false;

            foreach (var forbidden in ForbiddenNamespaces)
            {
                if (@namespace == forbidden || @namespace.StartsWith(forbidden + "."))
                    return true;
            }
            return false;
        }

        public bool IsAssemblyForbidden(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName) || !IsEnabled)
                return false;

            foreach (var forbidden in ForbiddenAssemblies)
            {
                if (assemblyName == forbidden || assemblyName.StartsWith(forbidden))
                    return true;
            }
            return false;
        }
    }

    [Serializable]
    public sealed class PackageConstraintsConfig
    {
        public Dictionary<string, PackageConstraint> Constraints { get; set; } = new();

        public GlobalConstraintDefaults GlobalDefaults { get; set; } = new();

        public PackageConstraint GetConstraint(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return null;

            if (Constraints.TryGetValue(packageName, out var constraint))
                return constraint;

            foreach (var key in Constraints.Keys)
            {
                if (key.EndsWith(".*") && packageName.StartsWith(key.TrimEnd('*')))
                    return Constraints[key];
            }

            return null;
        }

        public PackageConstraint GetEffectiveConstraint(string packageName)
        {
            var constraint = GetConstraint(packageName);
            if (constraint != null)
                return constraint;

            if (!GlobalDefaults.ApplyToUnlistedPackages)
                return null;

            if (!GlobalDefaults.Enabled)
                return null;

            return new PackageConstraint
            {
                PackageName = packageName,
                ForbiddenNamespaces = GlobalDefaults.ForbiddenNamespaces,
                ForbiddenAssemblies = GlobalDefaults.ForbiddenAssemblies,
                IsEnabled = GlobalDefaults.Enabled,
                Severity = GlobalDefaults.Severity,
                CheckUsingAliases = GlobalDefaults.CheckUsingAliases
            };
        }
    }

    [Serializable]
    public sealed class GlobalConstraintDefaults
    {
        public bool Enabled { get; set; } = false;

        public List<string> ForbiddenNamespaces { get; set; } = new();

        public List<string> ForbiddenAssemblies { get; set; } = new();

        public AKDiagnosticSeverity Severity { get; set; } = AKDiagnosticSeverity.Error;

        public bool CheckUsingAliases { get; set; } = true;

        public bool ApplyToUnlistedPackages { get; set; } = false;
    }
}
