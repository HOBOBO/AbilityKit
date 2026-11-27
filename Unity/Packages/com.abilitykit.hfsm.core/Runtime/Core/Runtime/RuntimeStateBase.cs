#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public abstract class RuntimeStateBase<TOwner> : IRuntimeState<TOwner>
    {
        public virtual void OnEnter(TOwner owner, in TickContext context)
        {
        }

        public virtual void OnTick(TOwner owner, in TickContext context)
        {
        }

        public virtual void OnExitRequested(TOwner owner, in TickContext context)
        {
        }

        public virtual bool CanExit(TOwner owner, in TickContext context) => true;

        public virtual void OnExit(TOwner owner, in TickContext context)
        {
        }
    }
}
