#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    /// <summary>Metadata-only catalog shared by validation and editor pickers.</summary>
    public sealed class BindingCatalog
    {
        private readonly Dictionary<BindingIdentity, BindingDescriptor> _descriptors =
            new Dictionary<BindingIdentity, BindingDescriptor>();
        private readonly List<BindingCatalogIssue> _issues = new List<BindingCatalogIssue>();

        public IReadOnlyList<BindingDescriptor> Descriptors => _descriptors.Values
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();

        public IReadOnlyList<BindingCatalogIssue> Issues => _issues.AsReadOnly();

        /// <summary>Allows editor-owned metadata sources to preserve diagnostics without exposing storage.</summary>
        public void AddIssue(BindingCatalogIssue issue)
        {
            if (issue == null) throw new ArgumentNullException(nameof(issue));
            _issues.Add(issue);
        }

        public void Register(BindingDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            var identity = new BindingIdentity(descriptor.Kind, descriptor.Key);
            if (!_descriptors.TryAdd(identity, descriptor))
                throw new InvalidOperationException(
                    $"HFSM {descriptor.Kind} binding '{descriptor.Key}' is already described.");
        }

        public bool Contains(BindingKind kind, string key)
        {
            return !string.IsNullOrEmpty(key) && _descriptors.ContainsKey(new BindingIdentity(kind, key));
        }

        public bool TryGetDescriptor(BindingKind kind, string key, out BindingDescriptor descriptor)
        {
            return _descriptors.TryGetValue(new BindingIdentity(kind, key), out descriptor!);
        }

        public int ScanAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            var count = 0;
            foreach (var type in assembly.GetTypes())
            {
                foreach (var attribute in type.GetCustomAttributes<BindingAttribute>(false))
                {
                    try
                    {
                        Register(new BindingDescriptor(
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
                        AddIssue(new BindingCatalogIssue(
                            "HFSMBIND001", attribute.Kind, attribute.Key, exception.Message));
                    }
                    catch (ArgumentException exception)
                    {
                        AddIssue(new BindingCatalogIssue(
                            "HFSMBIND004", attribute.Kind, attribute.Key, exception.Message));
                    }
                }
            }

            return count;
        }

        private readonly struct BindingIdentity : IEquatable<BindingIdentity>
        {
            public BindingIdentity(BindingKind kind, string key)
            {
                Kind = kind;
                Key = key ?? string.Empty;
            }

            private BindingKind Kind { get; }

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
