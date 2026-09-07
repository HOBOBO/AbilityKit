using System.Collections;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Actions
{

    /// <summary>
    /// Implemented by states that expose their instrumented action tree.
    /// </summary>
    public interface IActionRuntimeStateProvider
    {
        IEnumerable<IActionRuntimeStateSource> GetActionRuntimeStates();
    }
}
