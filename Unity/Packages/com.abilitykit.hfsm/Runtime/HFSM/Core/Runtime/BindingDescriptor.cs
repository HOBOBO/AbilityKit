#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public sealed class BindingDescriptor
    {
        public BindingDescriptor(
            BindingKind kind,
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

        public BindingKind Kind { get; }

        public string Key { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; }

        public Type? ImplementationType { get; }
    }
}
