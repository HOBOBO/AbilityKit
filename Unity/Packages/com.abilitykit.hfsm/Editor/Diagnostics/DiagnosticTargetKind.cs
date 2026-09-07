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
    public enum DiagnosticTargetKind
    {
        None = 0,
        Node = 1,
        Transition = 2,
    }
}
