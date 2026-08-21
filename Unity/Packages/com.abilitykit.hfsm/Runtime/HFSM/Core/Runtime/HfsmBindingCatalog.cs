#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AbilityKit.HFSM
{
    public enum HfsmBindingKind
    {
        State = 0,
        Condition = 1,
        Action = 2,
    }

    /// <summary>
    /// Declares editor-facing metadata for a stable runtime binding key. Discovery never creates
    /// the annotated type; runtime factories remain explicitly registered in HfsmRuntimeBindings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class HfsmBindingAttribute : Attribute
    {
        public HfsmBindingAttribute(HfsmBindingKind kind, string key, string displayName, string category = "")
        {
            Kind = kind;
            Key = key ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category ?? string.Empty;
        }

        public HfsmBindingKind Kind { get; }

        public string Key { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; set; } = string.Empty;
    }

    public sealed class HfsmBindingDescriptor
    {
        public HfsmBindingDescriptor(
            HfsmBindingKind kind,
            string key,
            string displayName,
            string category = "",
            string description = "",
            Type? implementationType = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("HFSM binding key is required.", nameof(key));
            Kind = kind;
            Key = key;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? key : displayName;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            ImplementationType = implementationType;
        }

        public HfsmBindingKind Kind { get; }

        public string Key { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; }

        public Type? ImplementationType { get; }
    }

    public sealed class HfsmBindingCatalogIssue
    {
        public HfsmBindingCatalogIssue(string code, HfsmBindingKind kind, string key, string message)
        {
            Code = code ?? string.Empty;
            Kind = kind;
            Key = key ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public HfsmBindingKind Kind { get; }

        public string Key { get; }

        public string Message { get; }

        public override string ToString() => $"{Code} {Kind} '{Key}': {Message}";
    }

    /// <summary>Metadata-only catalog shared by validation and editor pickers.</summary>
    public sealed class HfsmBindingCatalog
    {
        private readonly Dictionary<BindingIdentity, HfsmBindingDescriptor> _descriptors =
            new Dictionary<BindingIdentity, HfsmBindingDescriptor>();
        private readonly List<HfsmBindingCatalogIssue> _issues = new List<HfsmBindingCatalogIssue>();

        public IReadOnlyList<HfsmBindingDescriptor> Descriptors => _descriptors.Values
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();

        public IReadOnlyList<HfsmBindingCatalogIssue> Issues => _issues.AsReadOnly();

        /// <summary>Allows editor-owned metadata sources to preserve diagnostics without exposing storage.</summary>
        public void AddIssue(HfsmBindingCatalogIssue issue)
        {
            if (issue == null) throw new ArgumentNullException(nameof(issue));
            _issues.Add(issue);
        }

        public void Register(HfsmBindingDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            var identity = new BindingIdentity(descriptor.Kind, descriptor.Key);
            if (!_descriptors.TryAdd(identity, descriptor))
                throw new InvalidOperationException(
                    $"HFSM {descriptor.Kind} binding '{descriptor.Key}' is already described.");
        }

        public bool Contains(HfsmBindingKind kind, string key)
        {
            return !string.IsNullOrEmpty(key) && _descriptors.ContainsKey(new BindingIdentity(kind, key));
        }

        public bool TryGetDescriptor(HfsmBindingKind kind, string key, out HfsmBindingDescriptor descriptor)
        {
            return _descriptors.TryGetValue(new BindingIdentity(kind, key), out descriptor!);
        }

        public int ScanAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var count = 0;
            foreach (var type in assembly.GetTypes())
            {
                foreach (var attribute in type.GetCustomAttributes<HfsmBindingAttribute>(false))
                {
                    try
                    {
                        Register(new HfsmBindingDescriptor(
                            attribute.Kind,
                            attribute.Key,
                            attribute.DisplayName,
                            attribute.Category,
                            attribute.Description,
                            type));
                        count++;
                    }
                    catch (InvalidOperationException exception)
                    {
                        AddIssue(new HfsmBindingCatalogIssue(
                            "HFSMBIND001", attribute.Kind, attribute.Key, exception.Message));
                    }
                    catch (ArgumentException exception)
                    {
                        AddIssue(new HfsmBindingCatalogIssue(
                            "HFSMBIND004", attribute.Kind, attribute.Key, exception.Message));
                    }
                }
            }

            return count;
        }

        private readonly struct BindingIdentity : IEquatable<BindingIdentity>
        {
            public BindingIdentity(HfsmBindingKind kind, string key)
            {
                Kind = kind;
                Key = key ?? string.Empty;
            }

            private HfsmBindingKind Kind { get; }

            private string Key { get; }

            public bool Equals(BindingIdentity other) =>
                Kind == other.Kind && string.Equals(Key, other.Key, StringComparison.Ordinal);

            public override bool Equals(object? obj) => obj is BindingIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Kind * 397) ^ StringComparer.Ordinal.GetHashCode(Key);
                }
            }
        }
    }
}
