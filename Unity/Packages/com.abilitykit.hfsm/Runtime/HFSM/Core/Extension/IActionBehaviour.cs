using System.Collections.Generic;


namespace AbilityKit.HFSM.Extension
{
    public interface IActionBehaviour
    {
        void Reset();

        ActionBehaviourStatus Tick(in ActionBehaviourContext ctx);
    }
}
