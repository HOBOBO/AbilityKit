using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;

namespace AbilityKit.Ability.Share.Effect
{
    /// <summary>
    /// 效果实例。计时字段（已流逝/剩余/距下次 Tick 秒数）以 Q32.32 raw long 累加
    /// （整数运算无漂移），float 属性是触发事件/表现边界的单次换算视图。
    /// </summary>
    public sealed class EffectInstance
    {
        internal EffectInstance(int id, GameplayEffectSpec spec)
        {
            Id = id;
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));

            _elapsedRaw = 0L;
            _remainingRaw = spec.DurationPolicy == EffectDurationPolicy.Duration
                ? Fixed64.FromSingle(System.Math.Max(0f, spec.DurationSeconds)).RawValue
                : Fixed64.FromSingle(-1f).RawValue;
            _nextTickRaw = spec.PeriodSeconds > 0f
                ? Fixed64.FromSingle(System.Math.Max(0f, spec.PeriodSeconds)).RawValue
                : Fixed64.FromSingle(-1f).RawValue;

            StackCount = 1;
            State = new Dictionary<object, object>();
        }

        public int Id { get; }
        public GameplayEffectSpec Spec { get; }

        /// <summary>已流逝秒数（Q32.32 raw，内部累加用）。</summary>
        internal long ElapsedRaw
        {
            get => _elapsedRaw;
            set => _elapsedRaw = value;
        }

        /// <summary>剩余秒数（Q32.32 raw；非持续时间为 -1）。</summary>
        internal long RemainingRaw
        {
            get => _remainingRaw;
            set => _remainingRaw = value;
        }

        /// <summary>距下次周期 Tick 的秒数（Q32.32 raw；非周期为 -1）。</summary>
        internal long NextTickRaw
        {
            get => _nextTickRaw;
            set => _nextTickRaw = value;
        }

        public float ElapsedSeconds => Fixed64.FromRaw(_elapsedRaw).ToSingle();
        public float RemainingSeconds => Fixed64.FromRaw(_remainingRaw).ToSingle();
        public float NextTickInSeconds => Fixed64.FromRaw(_nextTickRaw).ToSingle();

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

        private long _elapsedRaw;
        private long _remainingRaw;
        private long _nextTickRaw;
    }
}
