using System;

namespace AbilityKit.Ability.FrameSync
{
    public sealed class FrameTime : IFrameTime
    {
        private float _fixedDelta;

        public FrameIndex Frame { get; private set; }

        public float DeltaTime { get; private set; }

        public float Time { get; private set; }

        public float FrameToTime(FrameIndex frame)
        {
            if (_fixedDelta <= 0f) return 0f;
            return frame.Value * _fixedDelta;
        }

        public FrameIndex TimeToFrame(float time)
        {
            if (_fixedDelta <= 0f) return new FrameIndex(0);
            var v = (int)Math.Floor(time / _fixedDelta);
            return new FrameIndex(v);
        }

        public void StepTo(FrameIndex frame, float deltaTime)
        {
            if (_fixedDelta <= 0f && deltaTime > 0f)
            {
                _fixedDelta = deltaTime;
            }

            Frame = frame;
            DeltaTime = deltaTime;
            Time += deltaTime;
        }

        public void Reset(FrameIndex frame, float time, float fixedDelta)
        {
            Frame = frame;
            Time = time;
            _fixedDelta = fixedDelta;
            DeltaTime = 0f;
        }
    }
}
