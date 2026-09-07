using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{

    public interface IRollbackActionBehaviour : IActionBehaviour
    {
        ActionBehaviourSnapshot CaptureSnapshot();

        void RestoreSnapshot(ActionBehaviourSnapshot snapshot);
    }
}
