namespace AbilityKit.Triggering.Blackboard
{
    public interface IBlackboard
    {
        bool TryGetInt(int keyId, out int value);
        void SetInt(int keyId, int value);
    }
}
