using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Share.Effect
{
    public readonly struct EffectEventArgs
    {
        public EffectEventArgs(object source, object target, EffectInstance instance)
        {
            Source = source;
            Target = target;
            Spec = instance?.Spec;
            Instance = instance;
            InstanceId = instance != null ? instance.Id : 0;
            StackCount = instance != null ? instance.StackCount : 0;
            ElapsedSeconds = instance != null ? instance.ElapsedSeconds : 0f;
            RemainingSeconds = instance != null ? instance.RemainingSeconds : 0f;
        }

        public object Source { get; }
        public object Target { get; }
        public GameplayEffectSpec Spec { get; }
        public EffectInstance Instance { get; }
        public int InstanceId { get; }
        public int StackCount { get; }
        public float ElapsedSeconds { get; }
        public float RemainingSeconds { get; }
    }

    public static class EffectTriggering
    {
        public static class EventNames
        {
            public const string Apply = "effect.apply";
            public const string Tick = "effect.tick";
            public const string Remove = "effect.remove";
        }

        public static class Events
        {
            public static readonly EventKey<EffectEventArgs> Apply = Create(EventNames.Apply);
            public static readonly EventKey<EffectEventArgs> Tick = Create(EventNames.Tick);
            public static readonly EventKey<EffectEventArgs> Remove = Create(EventNames.Remove);

            private static EventKey<EffectEventArgs> Create(string eventName)
            {
                return new EventKey<EffectEventArgs>(StableStringId.Get("event:" + eventName));
            }
        }
    }
}
