using AbilityKit.Core.Mathematics;

namespace AbilityKit.Combat.Projectile
{
    public readonly struct AreaSpawnEvent
    {
        public readonly AreaId Area;
        public readonly int OwnerId;
        public readonly int Frame;
        public readonly Vec3 Center;
        public readonly float Radius;

        public AreaSpawnEvent(AreaId area, int ownerId, int frame, in Vec3 center, float radius)
        {
            Area = area;
            OwnerId = ownerId;
            Frame = frame;
            Center = center;
            Radius = radius;
        }
    }

    public readonly struct AreaEnterEvent
    {
        public readonly AreaId Area;
        public readonly int OwnerId;
        public readonly ColliderId Collider;
        public readonly int Frame;
        // Nonzero only for a complete swept crossing missed by overlap Enter.
        // Rectangle right normal defines outward (+1); inward is -1.
        public readonly int CrossingDirection;

        public AreaEnterEvent(AreaId area, int ownerId, ColliderId collider, int frame, int crossingDirection = 0)
        {
            Area = area;
            OwnerId = ownerId;
            Collider = collider;
            Frame = frame;
            CrossingDirection = crossingDirection;
        }
    }

    public readonly struct AreaStayEvent
    {
        public readonly AreaId Area;
        public readonly int OwnerId;
        public readonly ColliderId Collider;
        public readonly int Frame;

        public AreaStayEvent(AreaId area, int ownerId, ColliderId collider, int frame)
        {
            Area = area;
            OwnerId = ownerId;
            Collider = collider;
            Frame = frame;
        }
    }

    public readonly struct AreaExitEvent
    {
        public readonly AreaId Area;
        public readonly int OwnerId;
        public readonly ColliderId Collider;
        public readonly int Frame;

        public AreaExitEvent(AreaId area, int ownerId, ColliderId collider, int frame)
        {
            Area = area;
            OwnerId = ownerId;
            Collider = collider;
            Frame = frame;
        }
    }

    public readonly struct AreaExpireEvent
    {
        public readonly AreaId Area;
        public readonly int OwnerId;
        public readonly int Frame;

        public AreaExpireEvent(AreaId area, int ownerId, int frame)
        {
            Area = area;
            OwnerId = ownerId;
            Frame = frame;
        }
    }
}
