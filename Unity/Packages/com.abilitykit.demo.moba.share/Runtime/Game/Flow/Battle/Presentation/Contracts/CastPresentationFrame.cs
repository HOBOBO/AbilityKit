using System;

namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>Optional, immutable world frame owned by the enclosing action's original Cast.</summary>
    public readonly struct CastPresentationFrame
    {
        public readonly bool HasValue;
        public readonly float OriginX, OriginY, OriginZ, ForwardX, ForwardY, ForwardZ;

        public CastPresentationFrame(float originX, float originY, float originZ,
            float forwardX, float forwardY, float forwardZ)
        {
            HasValue = true;
            OriginX = originX; OriginY = originY; OriginZ = originZ;
            ForwardX = forwardX; ForwardY = forwardY; ForwardZ = forwardZ;
        }

        public bool IsValid => !HasValue ||
            Finite(OriginX) && Finite(OriginY) && Finite(OriginZ) &&
            Finite(ForwardX) && Finite(ForwardY) && Finite(ForwardZ) &&
            Math.Abs(ForwardY) < .0001f &&
            Math.Abs(ForwardX * ForwardX + ForwardZ * ForwardZ - 1f) < .001f;

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
