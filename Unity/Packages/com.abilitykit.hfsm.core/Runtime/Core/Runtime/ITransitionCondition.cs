#nullable enable
using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

using AbilityKit.HFSM.Definition;


namespace AbilityKit.HFSM.Runtime
{

    /// <summary>Transition conditions must be deterministic and side-effect free.</summary>
    public interface ITransitionCondition<in TOwner>
    {
        bool Evaluate(TOwner owner, in TransitionContext context);
    }
}
