using System.Collections;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// Read-only runtime information for one action instance.
    /// </summary>
    public interface IActionRuntimeStateSource
    {
        string RuntimeId { get; }
        string ParentRuntimeId { get; }
        string Name { get; }
        string TypeName { get; }
        ActionRuntimeStatus RuntimeStatus { get; }
        bool IsActive { get; }
        int ExecutionCount { get; }
        float ElapsedTime { get; }
    }
}
