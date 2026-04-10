using System;

namespace AbilityKit.Core.Common.AttributeSystem
{
    public sealed class RangeAttributeConstraint : IAttributeConstraint
    {
        private readonly float _min;
        private readonly float _max;

        public RangeAttributeConstraint(float min, float max)
        {
            _min = min;
            _max = max;
        }

        public float Apply(AttributeId id, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return System.Math.Clamp(value, _min, _max);
        }
    }
}
