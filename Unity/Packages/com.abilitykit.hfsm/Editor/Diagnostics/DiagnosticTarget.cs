using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Editor.Platform.Diagnostics;
using AbilityKit.HFSM.Migration;
using AbilityKit.HFSM.Editor.Export;
using AbilityKit.HFSM.Graph;
using AbilityKit.HFSM.Graph.Compilation;

namespace AbilityKit.HFSM.Editor.Diagnostics
{

    public readonly struct DiagnosticTarget
    {
        public DiagnosticTarget(DiagnosticTargetKind kind, string id)
        {
            Kind = kind;
            Id = id ?? string.Empty;
        }

        public DiagnosticTargetKind Kind { get; }

        public string Id { get; }

        public bool IsValid => Kind != DiagnosticTargetKind.None && !string.IsNullOrEmpty(Id);
    }
}
