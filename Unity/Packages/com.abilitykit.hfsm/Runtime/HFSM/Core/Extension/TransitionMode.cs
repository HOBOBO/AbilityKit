#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{
    public enum TransitionMode
    {
        Condition = 0,
        OnSucceeded = 1,
        OnFailed = 2,
        OnFinished = 3,
    }
}
