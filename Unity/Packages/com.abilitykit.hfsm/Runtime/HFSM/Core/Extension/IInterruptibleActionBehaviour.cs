using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    /// <summary>
    /// Optional lifecycle contract for actions that own work which must be cancelled when their
    /// state or parent composite is interrupted. Implementations must remain resettable after abort.
    /// </summary>
    public interface IInterruptibleActionBehaviour : IActionBehaviour
    {
        void Abort(in ActionBehaviourContext ctx);
    }
}
