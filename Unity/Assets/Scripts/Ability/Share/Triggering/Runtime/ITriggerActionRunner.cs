namespace AbilityKit.Ability.Triggering.Runtime
{
    public interface ITriggerActionRunner
    {
        int RunningCount { get; }

        void Add(IRunningAction action, object owner = null);
        void Tick(float deltaTime);

        int CancelByOwner(object owner);
        int CancelAll();
    }
}
