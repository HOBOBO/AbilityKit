using System;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleMoveInputState
    {
        private float _lastDx;
        private float _lastDz;
        private int _stopRepeatTicks;
        private int _lastSubmittedFrame = int.MinValue;
        private bool _hasSubmitted;

        public bool TryGetMoveToSubmit(int targetFrame, float dx, float dz, out float submitDx, out float submitDz)
        {
            if (targetFrame == _lastSubmittedFrame)
            {
                submitDx = 0f;
                submitDz = 0f;
                return false;
            }

            if (!_hasSubmitted)
            {
                _hasSubmitted = true;
                _lastDx = dx;
                _lastDz = dz;
                _lastSubmittedFrame = targetFrame;
                submitDx = dx;
                submitDz = dz;
                return true;
            }

            var wasMoving = Math.Abs(_lastDx) > 0.0001f || Math.Abs(_lastDz) > 0.0001f;
            var isMoving = Math.Abs(dx) > 0.0001f || Math.Abs(dz) > 0.0001f;

            if (isMoving || (wasMoving && !isMoving))
            {
                if (!isMoving && wasMoving)
                {
                    _stopRepeatTicks = 2;
                }

                _lastDx = dx;
                _lastDz = dz;
                _lastSubmittedFrame = targetFrame;
                submitDx = dx;
                submitDz = dz;
                return true;
            }

            _lastDx = dx;
            _lastDz = dz;

            if (_stopRepeatTicks > 0)
            {
                _stopRepeatTicks--;
                _lastSubmittedFrame = targetFrame;
                submitDx = 0f;
                submitDz = 0f;
                return true;
            }

            submitDx = 0f;
            submitDz = 0f;
            return false;
        }

        public void Reset()
        {
            _lastDx = 0f;
            _lastDz = 0f;
            _stopRepeatTicks = 0;
            _lastSubmittedFrame = int.MinValue;
            _hasSubmitted = false;
        }
    }
}
