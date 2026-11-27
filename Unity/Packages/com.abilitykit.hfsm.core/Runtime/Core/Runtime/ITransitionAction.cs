#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    public interface ITransitionAction<in TOwner>
    {
        void BeforeTransition(TOwner owner, in TransitionContext context);

        void AfterTransition(TOwner owner, in TransitionContext context);
    }
}
