#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{
    public enum BehaviourKind
    {
        Action = 0,
        Sequence = 1,
        Selector = 2,
        Parallel = 3,
        Invert = 4,
        Repeat = 5,
        Timeout = 6,
        Condition = 7,
        Delay = 8,
    }
}
