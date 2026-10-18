using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Core.Mathematics;
using AbilityKit.Deterministic;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// MOBA 持续运行时实现共享的状态机基类。
    /// 计时状态（已流逝/间隔剩余）以 Q32.32 raw long 累加（整数运算无漂移），
    /// float 属性是 IContinuous 接口/表现边界的单次换算视图。
    /// </summary>
    public abstract class MobaContinuousRuntimeBase : IContinuous
    {
        private long _elapsedRaw;
        private long _intervalRemainingRaw;
        private int _allocatedModifierSourceId;
        private readonly Dictionary<int, MobaModifierCapturedValue> _modifierCaptures = new Dictionary<int, MobaModifierCapturedValue>();
        private readonly Dictionary<MobaContinuousModifierCaptureKey, MobaModifierCapturedValue> _stableModifierCaptures =
            new Dictionary<MobaContinuousModifierCaptureKey, MobaModifierCapturedValue>();

        public IReadOnlyDictionary<int, MobaModifierCapturedValue> ModifierCaptures => _modifierCaptures;

        public IReadOnlyDictionary<MobaContinuousModifierCaptureKey, MobaModifierCapturedValue> StableModifierCaptures =>
            _stableModifierCaptures;

        internal int AllocatedModifierSourceId => _allocatedModifierSourceId;

        internal void AssignModifierSourceId(int sourceId)
        {
            if (sourceId <= 0 || _allocatedModifierSourceId != 0 && _allocatedModifierSourceId != sourceId)
                throw new InvalidOperationException("Continuous modifier source identity cannot be reassigned.");
            _allocatedModifierSourceId = sourceId;
        }

        public bool TryGetModifierCapture(int index, int stack, int op, out float value)
        {
            return TryGetModifierCaptureCore(
                _stableModifierCaptures.TryGetValue(new MobaContinuousModifierCaptureKey(0, index), out var stable)
                    ? stable : (_modifierCaptures.TryGetValue(index, out var legacy) ? legacy : (MobaModifierCapturedValue?)null),
                stack, op, out value);
        }

        public bool TryGetModifierCapture(IMobaContinuousModifierSpec spec, int ordinal, int stack, int op,
            out float value)
        {
            var key = MobaContinuousModifierCaptureKey.From(spec, ordinal);
            if (_stableModifierCaptures.TryGetValue(key, out var capture))
            {
                return TryGetModifierCaptureCore(capture, stack, op, out value);
            }
            value = 0f;
            return false;
        }

        private static bool TryGetModifierCaptureCore(MobaModifierCapturedValue? optionalCapture, int stack, int op,
            out float value)
        {
            if (!optionalCapture.HasValue)
            {
                value = 0f;
                return false;
            }

            var capture = optionalCapture.Value;
            if (capture.Stack == stack || op == (int)AbilityKit.Modifiers.ModifierOp.Override)
                value = capture.Value;
            else if (op == (int)AbilityKit.Modifiers.ModifierOp.Mul)
            {
                var scaled = capture.HasUnitValue
                    ? MobaContinuousModifierMath.IntegerPow(capture.UnitValue, stack)
                    : DeterministicMathBridge.Pow(capture.Value, (float)stack / capture.Stack);
                if (double.IsNaN(scaled) || double.IsInfinity(scaled) ||
                    scaled < float.MinValue || scaled > float.MaxValue)
                    throw new System.InvalidOperationException("Captured multiplier cannot be rescaled for the new stack count.");
                value = (float)scaled;
            }
            else value = capture.Value * stack / capture.Stack;
            return true;
        }

        public void SetModifierCapture(int index, int stack, float value)
        {
            if (!_modifierCaptures.ContainsKey(index))
                _modifierCaptures.Add(index, new MobaModifierCapturedValue(stack, value));
            var key = new MobaContinuousModifierCaptureKey(0, index);
            if (!_stableModifierCaptures.ContainsKey(key))
                _stableModifierCaptures.Add(key, new MobaModifierCapturedValue(stack, value));
        }

        public void SetModifierCapture(int index, int stack, float value, float unitValue)
        {
            if (!_modifierCaptures.ContainsKey(index))
                _modifierCaptures.Add(index, new MobaModifierCapturedValue(stack, value, unitValue));
            var key = new MobaContinuousModifierCaptureKey(0, index);
            if (!_stableModifierCaptures.ContainsKey(key))
                _stableModifierCaptures.Add(key, new MobaModifierCapturedValue(stack, value, unitValue));
        }

        public void SetModifierCapture(IMobaContinuousModifierSpec spec, int ordinal, int stack, float value,
            float? unitValue = null)
        {
            var key = MobaContinuousModifierCaptureKey.From(spec, ordinal);
            var capture = new MobaModifierCapturedValue(stack, value, unitValue);
            if (!_stableModifierCaptures.ContainsKey(key))
                _stableModifierCaptures.Add(key, capture);
            if (!_modifierCaptures.ContainsKey(ordinal))
                _modifierCaptures.Add(ordinal, capture);
        }

        public void RestoreModifierCaptures(IReadOnlyDictionary<int, MobaModifierCapturedValue> captures)
        {
            _modifierCaptures.Clear();
            _stableModifierCaptures.Clear();
            if (captures == null) return;
            foreach (var pair in captures)
            {
                _modifierCaptures.Add(pair.Key, pair.Value);
                _stableModifierCaptures.Add(new MobaContinuousModifierCaptureKey(0, pair.Key), pair.Value);
            }
        }

        public void RestoreStableModifierCaptures(
            IReadOnlyDictionary<MobaContinuousModifierCaptureKey, MobaModifierCapturedValue> captures)
        {
            _modifierCaptures.Clear();
            _stableModifierCaptures.Clear();
            if (captures == null) return;
            foreach (var pair in captures)
            {
                _stableModifierCaptures.Add(pair.Key, pair.Value);
                if (!_modifierCaptures.ContainsKey(pair.Key.Ordinal))
                    _modifierCaptures.Add(pair.Key.Ordinal, pair.Value);
            }
        }

        public void ClearModifierCaptures()
        {
            _modifierCaptures.Clear();
            _stableModifierCaptures.Clear();
        }

        public abstract IContinuousConfig Config { get; }

        public ContinuousState State { get; private set; } = ContinuousState.Inactive;
        public bool IsActive => State == ContinuousState.Active;
        public bool IsTerminated => State == ContinuousState.Expired || State == ContinuousState.Aborted;
        public bool IsPaused => State == ContinuousState.Paused;

        public float ElapsedSeconds => Fixed64.FromRaw(_elapsedRaw).ToSingle();

        /// <summary>已流逝秒数的 Q32.32 raw（内部累加与回滚恢复用）。</summary>
        internal long ElapsedRaw => _elapsedRaw;

        /// <summary>距下次间隔触发的剩余秒数（Q32.32 raw；TickProcessor 整数运算用）。</summary>
        internal long IntervalRemainingRaw
        {
            get => _intervalRemainingRaw;
            set => _intervalRemainingRaw = value;
        }

        /// <summary>
        /// 间隔剩余的 float 视图（IMobaContinuousIntervalState 落点）。
        /// setter 单次换算——只应在初始化/配置回填时写，逐帧推进走 raw 路径。
        /// </summary>
        public float IntervalRemainingSeconds
        {
            get => Fixed64.FromRaw(_intervalRemainingRaw).ToSingle();
            set => _intervalRemainingRaw = DeterministicMathBridge.ToFixed(value).RawValue;
        }

        public event Action<IContinuous, ContinuousEndReason> OnEnded;

        public void Activate()
        {
            if (State == ContinuousState.Active) return;
            if (IsTerminated) return;

            State = ContinuousState.Activating;
            if (!OnActivating())
            {
                CompleteEnd(ContinuousEndReason.Interrupted);
                return;
            }

            State = ContinuousState.Active;
            OnActivated();
        }

        public void Pause()
        {
            if (State != ContinuousState.Active) return;
            State = ContinuousState.Paused;
            OnPaused();
        }

        public void Resume()
        {
            if (State != ContinuousState.Paused) return;
            State = ContinuousState.Active;
            OnResumed();
        }

        public void End(ContinuousEndReason reason)
        {
            if (IsTerminated) return;
            OnEnding(reason);
            CompleteEnd(reason);
        }

        public void Abort(string reason)
        {
            End(ContinuousEndReason.Interrupted);
        }

        protected void AdvanceElapsed(float deltaTimeSeconds)
        {
            AdvanceElapsedRaw(DeterministicMathBridge.ToFixed(deltaTimeSeconds).RawValue);
        }

        protected internal void AdvanceElapsedRaw(long deltaTimeRaw)
        {
            if (deltaTimeRaw > 0L)
            {
                _elapsedRaw += deltaTimeRaw;
            }
        }

        /// <summary>直接设置累计 raw（回滚/恢复路径，绕过加法）。</summary>
        protected internal void SetElapsedRaw(long elapsedRaw)
        {
            _elapsedRaw = elapsedRaw;
        }

        protected void ResetElapsed()
        {
            _elapsedRaw = 0L;
        }

        protected virtual bool OnActivating() => true;
        protected virtual void OnActivated() { }
        protected virtual void OnPaused() { }
        protected virtual void OnResumed() { }
        protected virtual void OnEnding(ContinuousEndReason reason) { }

        private void CompleteEnd(ContinuousEndReason reason)
        {
            if (IsTerminated) return;
            State = reason == ContinuousEndReason.Completed ? ContinuousState.Expired : ContinuousState.Aborted;
            OnEnded?.Invoke(this, reason);
            ClearModifierCaptures();
        }
    }

    public readonly struct MobaModifierCapturedValue
    {
        public MobaModifierCapturedValue(int stack, float value, float? unitValue = null)
        {
            if (stack < 1) throw new ArgumentOutOfRangeException(nameof(stack));
            Stack = stack;
            Value = value;
            HasUnitValue = unitValue.HasValue;
            UnitValue = unitValue.GetValueOrDefault();
        }

        public int Stack { get; }
        public float Value { get; }
        public bool HasUnitValue { get; }
        public float UnitValue { get; }
    }

    public readonly struct MobaContinuousModifierCaptureKey : IEquatable<MobaContinuousModifierCaptureKey>
    {
        public MobaContinuousModifierCaptureKey(int definitionId, int ordinal)
        {
            DefinitionId = definitionId > 0 ? definitionId : 0;
            Ordinal = ordinal < 0 ? 0 : ordinal;
        }

        public int DefinitionId { get; }
        public int Ordinal { get; }

        public static MobaContinuousModifierCaptureKey From(IMobaContinuousModifierSpec spec, int ordinal) =>
            new MobaContinuousModifierCaptureKey(spec?.DefinitionId ?? 0, ordinal);

        public bool Equals(MobaContinuousModifierCaptureKey other) =>
            DefinitionId == other.DefinitionId && Ordinal == other.Ordinal;

        public override bool Equals(object obj) => obj is MobaContinuousModifierCaptureKey other && Equals(other);

        public override int GetHashCode() => unchecked((DefinitionId * 397) ^ Ordinal);

        public override string ToString() => $"{DefinitionId}:{Ordinal}";
    }
}
