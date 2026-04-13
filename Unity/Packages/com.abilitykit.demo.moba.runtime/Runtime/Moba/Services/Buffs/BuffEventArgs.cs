using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Components;

namespace AbilityKit.Demo.Moba.Services
{
    public struct BuffEventArgs
    {
        public string EventId;

        public int SourceActorId;
        public int TargetActorId;

        public int BuffId;
        public int EffectId;

        public string Stage;

        public int StackCount;
        public float DurationSeconds;

        public EffectSourceEndReason RemoveReason;

        public long SourceContextId;

        public BuffRuntime Runtime;
    }
}
