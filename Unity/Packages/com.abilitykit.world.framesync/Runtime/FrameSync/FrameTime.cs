using System;
using AbilityKit.Deterministic;

namespace AbilityKit.Ability.FrameSync
{
    /// <summary>
    /// 帧时钟。累计时刻以 Q32.32 定点（raw long 整数累加，无精度漂移）；
    /// float 成员是表现/接口边界的单次换算视图。
    /// </summary>
    public sealed class FrameTime : IFrameTime
    {
        private Fixed64 _fixedDelta;
        private Fixed64 _time;

        public FrameIndex Frame { get; private set; }

        public float DeltaTime { get; private set; }

        public float Time => _time.ToSingle();

        /// <summary>累计时刻的 Q32.32 raw 值（回滚快照与位断言用）。</summary>
        internal long TimeRaw => _time.RawValue;

        /// <summary>锁定固定步长的 Q32.32 raw 值。</summary>
        internal long FixedDeltaRaw => _fixedDelta.RawValue;

        public float FrameToTime(FrameIndex frame)
        {
            if (_fixedDelta <= Fixed64.Zero) return 0f;
            return (Fixed64.FromInt32(frame.Value) * _fixedDelta).ToSingle();
        }

        public FrameIndex TimeToFrame(float time)
        {
            if (_fixedDelta <= Fixed64.Zero) return new FrameIndex(0);
            if (!float.IsFinite(time)) return new FrameIndex(0);
            // Q32.32 原码算术右移 32 位即 floor(raw / 2^32)，正负输入都是下取整。
            var v = (Fixed64.FromSingle(time) / _fixedDelta).RawValue >> 32;
            return new FrameIndex((int)v);
        }

        public void StepTo(FrameIndex frame, float deltaTime)
        {
            if (_fixedDelta <= Fixed64.Zero && deltaTime > 0f)
            {
                _fixedDelta = ToFixed(deltaTime);
            }

            Frame = frame;
            DeltaTime = deltaTime;
            _time += ToFixed(deltaTime);
        }

        /// <summary>
        /// 整数对齐到目标帧：时间按 帧号×固定步长 的整数乘重建，
        /// 与逐帧 StepTo 累加（N 步 = N×单步 raw）位一致——客户端预测对齐必须用它，
        /// 不要经 float（Time/FrameToTime）中转重建，否则与累加路径产生亚毫秒偏差。
        /// 步长未锁定时用 fallbackDeltaTime 锁定。返回是否成功对齐。
        /// </summary>
        public bool AlignTo(FrameIndex targetFrame, float fallbackDeltaTime)
        {
            if (_fixedDelta <= Fixed64.Zero)
            {
                if (fallbackDeltaTime <= 0f) return false;
                _fixedDelta = ToFixed(fallbackDeltaTime);
            }

            _time = Fixed64.FromInt32(targetFrame.Value) * _fixedDelta;
            Frame = targetFrame;
            DeltaTime = _fixedDelta.ToSingle();
            return true;
        }

        /// <summary>从当前帧起算 seconds 秒后的帧号（定点除法取整，不经 float 中转）。</summary>
        public FrameIndex FrameAfterSeconds(float seconds)
        {
            if (_fixedDelta <= Fixed64.Zero) return Frame;
            var frames = (ToFixed(seconds) / _fixedDelta).RawValue >> 32;
            return new FrameIndex(Frame.Value + (int)frames);
        }

        /// <summary>累计时刻的整数毫秒（(raw×1000)>>32，纯整数域，无舍入边界）。</summary>
        public long TimeMilliseconds => (_time.RawValue * 1000L) >> 32;

        public void Reset(FrameIndex frame, float time, float fixedDelta)
        {
            Restore(frame, time, deltaTime: 0f, fixedDelta);
        }

        internal void Restore(FrameIndex frame, float time, float deltaTime, float fixedDelta)
        {
            RestoreRaw(frame, ToFixed(time).RawValue, deltaTime, ToFixed(fixedDelta).RawValue);
        }

        internal void RestoreRaw(FrameIndex frame, long timeRaw, float deltaTime, long fixedDeltaRaw)
        {
            Frame = frame;
            _time = Fixed64.FromRaw(timeRaw);
            DeltaTime = deltaTime;
            _fixedDelta = Fixed64.FromRaw(fixedDeltaRaw);
        }

        private static Fixed64 ToFixed(float value)
        {
            return Core.Mathematics.DeterministicMathBridge.ToFixed(value);
        }
    }
}
