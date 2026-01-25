using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Effect
{
    public sealed class EffectInstance
    {
        internal EffectInstance(int id, GameplayEffectSpec spec)
        {
            Id = id;
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            ElapsedSeconds = 0f;
            RemainingSeconds = spec.DurationPolicy == EffectDurationPolicy.Duration ? System.Math.Max(0f, spec.DurationSeconds) : -1f;
            NextTickInSeconds = spec.PeriodSeconds > 0f ? System.Math.Max(0f, spec.PeriodSeconds) : -1f;

            StackCount = 1;
            State = new Dictionary<object, object>();
        }

        public int Id { get; }
        public GameplayEffectSpec Spec { get; }

        public float ElapsedSeconds { get; internal set; }
        public float RemainingSeconds { get; internal set; }
        public float NextTickInSeconds { get; internal set; }

        public int StackCount { get; internal set; }

        public Dictionary<object, object> State { get; }

        public bool TryGetState<T>(object key, out T value)
        {
            if (key != null && State.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public void SetState(object key, object value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            State[key] = value;
        }

        public bool RemoveState(object key)
        {
            if (key == null) return false;
            return State.Remove(key);
        }
    }
}
