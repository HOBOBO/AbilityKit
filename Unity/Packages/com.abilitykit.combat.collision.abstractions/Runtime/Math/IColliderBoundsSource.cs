using System.Collections.Generic;

namespace AbilityKit.Core.Mathematics
{
    /// <summary>Authoritative collision envelopes, using the same padding as shape overlap queries.</summary>
    public interface IColliderBoundsSource
    {
        void CopyColliderBounds(List<ColliderBounds> results);
    }

    public readonly struct ColliderBounds
    {
        public readonly ColliderId Collider;
        public readonly Vec3 Center;
        public readonly float Radius;
        public readonly int LayerId;
        public ColliderBounds(ColliderId collider, Vec3 center, float radius, int layerId)
        { Collider = collider; Center = center; Radius = radius; LayerId = layerId; }
    }
}
