#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public interface IRuntimeState<in TOwner>
    {
        void OnEnter(TOwner owner, in TickContext context);

        void OnTick(TOwner owner, in TickContext context);

        void OnExitRequested(TOwner owner, in TickContext context);

        bool CanExit(TOwner owner, in TickContext context);

        void OnExit(TOwner owner, in TickContext context);
    }
}
