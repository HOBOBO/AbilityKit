#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public sealed class BindingCatalogIssue
    {
        public BindingCatalogIssue(string code, BindingKind kind, string key, string message)
        {
            Code = code ?? string.Empty;
            Kind = kind;
            Key = key ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; }

        public BindingKind Kind { get; }

        public string Key { get; }

        public string Message { get; }

        public override string ToString() => $"{Code} {Kind} '{Key}': {Message}";
    }
}
