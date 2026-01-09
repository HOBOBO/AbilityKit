using System;
using AbilityKit.Ability.Share.Common.MotionSystem.Core;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Share.Common.MotionSystem.Generic
{
    public sealed class ScaledMotionSource : IMotionSource
    {
        private readonly IMotionSource _inner;
        private float _scale;
        private readonly int _groupId;
        private readonly MotionStacking _stacking;
        private readonly int _priority;

        public ScaledMotionSource(IMotionSource inner, float scale, int groupId = 0, MotionStacking stacking = MotionStacking.Additive, int priority = int.MinValue)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _scale = scale;

            _groupId = groupId != 0 ? groupId : inner.GroupId;
            _stacking = stacking;
            _priority = priority != int.MinValue ? priority : inner.Priority;
        }

        public int GroupId => _groupId;

        public MotionStacking Stacking => _stacking;

        public int Priority => _priority;

        public bool IsActive => _inner != null && _inner.IsActive;

        public float Scale
        {
            get => _scale;
            set => _scale = value;
        }

        public void Tick(int id, ref MotionState state, float dt, ref Vec3 outDesiredDelta)
        {
            if (_inner == null) return;
            if (_scale == 0f) return;

            var tmp = Vec3.Zero;
            _inner.Tick(id, ref state, dt, ref tmp);
            outDesiredDelta = outDesiredDelta + tmp * _scale;
        }

        public void Cancel()
        {
            _inner?.Cancel();
        }
    }
}
