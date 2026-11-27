#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    /// <summary>
    /// Read-only diagnostics hook. Observer exceptions are isolated and never affect simulation.
    /// </summary>
    public interface IRuntimeObserver
    {
        void OnRuntimeEvent(in RuntimeEvent runtimeEvent);
    }
}
