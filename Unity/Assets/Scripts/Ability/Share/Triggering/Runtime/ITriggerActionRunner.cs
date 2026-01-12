namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface ITriggerActionRunner
    {
        int RunningCount { get; }

        void Add(IRunningAction action, object owner = null);
        void Add(IRunningAction action, long ownerKey);
        void Tick(float deltaTime);

        int CancelByOwner(object owner);
        int CancelByOwnerKey(long ownerKey);
        int CancelAll();
    }
}
