using UnityEngine;

namespace AbilityKit.Game.Flow
{
    internal struct BattleViewPositionSampleBuffer
    {
        private PresentationPositionSampleBuffer _samples;

        public void Clear() => _samples.Clear();

        public void Add(double time, in Vector3 pos)
        {
            var value = new PresentationPosition(pos.x, pos.y, pos.z);
            _samples.Add(time, in value);
        }

        public bool TryEvaluate(double time, out Vector3 position)
        {
            if (!_samples.TryEvaluate(time, out var value))
            {
                position = default;
                return false;
            }

            position = new Vector3(value.X, value.Y, value.Z);
            return true;
        }
    }
}
