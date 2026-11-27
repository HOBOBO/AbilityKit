#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public sealed class RuntimeFaultedException : InvalidOperationException
    {
        public RuntimeFaultedException()
            : base("The HFSM runtime is faulted. Restore a valid snapshot or create a new runtime before continuing.")
        {
        }
    }
}
