using System.Collections;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// Lifecycle state exposed by the optional action runtime instrumentation layer.
    /// </summary>
    public enum ActionRuntimeStatus
    {
        Inactive,
        Running,
        Success,
        Failure,
        Cancelled
    }
}
