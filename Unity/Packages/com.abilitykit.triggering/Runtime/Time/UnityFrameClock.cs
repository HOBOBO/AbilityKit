using System;
using System.Threading;

namespace AbilityKit.Triggering.Runtime.Time
{
    /// <summary>
    /// 帧时钟实现（Unity 版）
    /// 基于 Unity 的 Time.deltaTime，提供帧计数转换
    /// </summary>
    public sealed class UnityFrameClock : IFrameClock
    {
        private float _accumulatedTime;
        private long _currentFrame;
        private readonly float _fixedFrameDurationMs;

        public UnityFrameClock(float fixedFrameDurationMs = 16.667f)
        {
            _fixedFrameDurationMs = fixedFrameDurationMs;
            _currentFrame = 0;
            _accumulatedTime = 0f;
        }

        /// <summary>当前帧号</summary>
        public long CurrentFrame => _currentFrame;

        /// <summary>每帧的毫秒数（固定值）</summary>
        public float FrameDurationMs => _fixedFrameDurationMs;

        /// <summary>推进一帧（每帧调用）</summary>
        public void AdvanceFrame(float deltaTimeMs)
        {
            _accumulatedTime += deltaTimeMs;
            while (_accumulatedTime >= _fixedFrameDurationMs)
            {
                _accumulatedTime -= _fixedFrameDurationMs;
                _currentFrame++;
            }
        }

        /// <summary>强制设置帧号（用于回滚/同步）</summary>
        public void SetFrame(long frame)
        {
            _currentFrame = frame;
            _accumulatedTime = 0f;
        }

        /// <summary>重置时钟</summary>
        public void Reset()
        {
            _currentFrame = 0;
            _accumulatedTime = 0f;
        }

        public long MsToFrame(float ms)
        {
            var frames = (long)Math.Round(ms / _fixedFrameDurationMs);
            return frames < 0 ? 0 : frames;
        }

        public float FrameToMs(long frame)
        {
            return frame * _fixedFrameDurationMs;
        }
    }
}
