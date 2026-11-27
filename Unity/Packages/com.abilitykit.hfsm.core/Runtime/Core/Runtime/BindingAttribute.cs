#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    /// <summary>
    /// Declares editor-facing metadata for a stable runtime binding key. Discovery never creates
    /// the annotated type; runtime factories remain explicitly registered in RuntimeBindings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class BindingAttribute : Attribute
    {
        public BindingAttribute(BindingKind kind, string key, string displayName, string category = "")
        {
            Kind = kind;
            Key = key ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category ?? string.Empty;
        }

        public BindingKind Kind { get; }

        public string Key { get; }

        public string DisplayName { get; }

        public string Category { get; }

        public string Description { get; set; } = string.Empty;
    }
}
