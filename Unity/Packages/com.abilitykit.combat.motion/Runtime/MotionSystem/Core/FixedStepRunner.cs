using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.MotionSystem.Core
{
    /// <summary>
    /// 固定步长累加器：Q32.32 raw 整数累减（无 float 累计精度损耗），
    /// 步数判定为整数除法截断（正数域与旧 float (int) 截断语义一致）。
    /// </summary>
    public struct FixedStepRunner
    {
        private long _accumulatorRaw;
        private readonly long _stepRaw;

        public FixedStepRunner(float step)
        {
            _stepRaw = DeterministicMathBridge.ToFixed(step <= 0f ? 0.02f : step).RawValue;
            _accumulatorRaw = 0L;
        }

        public float Step => Deterministic.Fixed64.FromRaw(_stepRaw).ToSingle();

        public int Accumulate(float dt)
        {
            if (dt <= 0f) return 0;
            _accumulatorRaw += DeterministicMathBridge.ToFixed(dt).RawValue;
            if (_accumulatorRaw < _stepRaw) return 0;

            var count = (int)(_accumulatorRaw / _stepRaw);
            if (count > 0) _accumulatorRaw -= count * _stepRaw;
            return count;
        }

        public float ConsumeOneStep()
        {
            return Deterministic.Fixed64.FromRaw(_stepRaw).ToSingle();
        }

        public void Reset()
        {
            _accumulatorRaw = 0L;
        }
    }
}
