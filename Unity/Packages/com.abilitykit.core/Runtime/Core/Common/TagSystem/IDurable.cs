namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public interface IDurable
    {
        int OwnerId { get; }
        string Kind { get; }

        bool IsPaused { get; }
        bool IsStopped { get; }
        bool IsRemoved { get; }

        void Pause();
        void Resume();
        void Stop();
        void Remove();
    }
}
