namespace UnityHFSM.Extension
{
    public interface IActionBehaviour
    {
        void Reset();

        ActionBehaviourStatus Tick(in ActionBehaviourContext ctx);
    }
}
