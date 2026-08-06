using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

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
    [DataContract]
    public sealed class PackageConstraint
    {
        [DataMember(Name = "packageName", EmitDefaultValue = false)]
        public string PackageName { get; set; }

        [DataMember(Name = "forbiddenNamespaces", EmitDefaultValue = false)]
        public List<string> ForbiddenNamespaces { get; set; } = new();

        [DataMember(Name = "forbiddenAssemblies", EmitDefaultValue = false)]
        public List<string> ForbiddenAssemblies { get; set; } = new();

        [DataMember(Name = "isEnabled", EmitDefaultValue = false)]
        public bool IsEnabled { get; set; } = true;

        [DataMember(Name = "severity", EmitDefaultValue = false)]
        public AKDiagnosticSeverity Severity { get; set; } = AKDiagnosticSeverity.Error;

        [DataMember(Name = "checkUsingAliases", EmitDefaultValue = false)]
        public bool CheckUsingAliases { get; set; } = true;

        [DataMember(Name = "description", EmitDefaultValue = false)]
        public string Description { get; set; }

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            IsEnabled = true;
            CheckUsingAliases = true;
        }

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
    [DataContract]
    public sealed class PackageConstraintsConfig
    {
        [DataMember(Name = "constraints", EmitDefaultValue = false)]
        public Dictionary<string, PackageConstraint> Constraints { get; set; } = new();

        [DataMember(Name = "globalDefaults", EmitDefaultValue = false)]
        public GlobalConstraintDefaults GlobalDefaults { get; set; } = new();

        internal void Normalize()
        {
            Constraints ??= new Dictionary<string, PackageConstraint>();
            GlobalDefaults ??= new GlobalConstraintDefaults();
            GlobalDefaults.Normalize();

            foreach (var pair in Constraints)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                pair.Value.PackageName ??= pair.Key;
                pair.Value.ForbiddenNamespaces ??= new List<string>();
                pair.Value.ForbiddenAssemblies ??= new List<string>();
            }
        }

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
    [DataContract]
    public sealed class GlobalConstraintDefaults
    {
        [DataMember(Name = "enabled", EmitDefaultValue = false)]
        public bool Enabled { get; set; } = false;

        [DataMember(Name = "forbiddenNamespaces", EmitDefaultValue = false)]
        public List<string> ForbiddenNamespaces { get; set; } = new();

        [DataMember(Name = "forbiddenAssemblies", EmitDefaultValue = false)]
        public List<string> ForbiddenAssemblies { get; set; } = new();

        [DataMember(Name = "severity", EmitDefaultValue = false)]
        public AKDiagnosticSeverity Severity { get; set; } = AKDiagnosticSeverity.Error;

        [DataMember(Name = "checkUsingAliases", EmitDefaultValue = false)]
        public bool CheckUsingAliases { get; set; } = true;

        [DataMember(Name = "applyToUnlistedPackages", EmitDefaultValue = false)]
        public bool ApplyToUnlistedPackages { get; set; } = false;

        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            CheckUsingAliases = true;
        }

        internal void Normalize()
        {
            ForbiddenNamespaces ??= new List<string>();
            ForbiddenAssemblies ??= new List<string>();
        }
    }
}
