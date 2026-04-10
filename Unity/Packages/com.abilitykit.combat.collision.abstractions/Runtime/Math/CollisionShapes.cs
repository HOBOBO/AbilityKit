namespace AbilityKit.Core.Math
{
    public readonly struct Sphere
    {
        public readonly Vec3 Center;
        public readonly float Radius;

        public Sphere(in Vec3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }
    }

    public readonly struct Aabb
    {
        public readonly Vec3 Min;
        public readonly Vec3 Max;

        public Aabb(in Vec3 min, in Vec3 max)
        {
            Min = min;
            Max = max;
        }

        public Vec3 Center => (Min + Max) * 0.5f;
        public Vec3 Extents => (Max - Min) * 0.5f;
    }

    public readonly struct Capsule
    {
        public readonly Vec3 A;
        public readonly Vec3 B;
        public readonly float Radius;

        public Capsule(in Vec3 a, in Vec3 b, float radius)
        {
            A = a;
            B = b;
            Radius = radius;
        }
    }

    public enum ColliderShapeType
    {
        Sphere = 1,
        Aabb = 2,
        Capsule = 3
    }

    public readonly struct ColliderShape
    {
        public readonly ColliderShapeType Type;
        public readonly Sphere Sphere;
        public readonly Aabb Aabb;
        public readonly Capsule Capsule;

        private ColliderShape(ColliderShapeType type, in Sphere sphere, in Aabb aabb, in Capsule capsule)
        {
            Type = type;
            Sphere = sphere;
            Aabb = aabb;
            Capsule = capsule;
        }

        public static ColliderShape CreateSphere(in Sphere sphere) => new ColliderShape(ColliderShapeType.Sphere, sphere, default, default);
        public static ColliderShape CreateAabb(in Aabb aabb) => new ColliderShape(ColliderShapeType.Aabb, default, aabb, default);
        public static ColliderShape CreateCapsule(in Capsule capsule) => new ColliderShape(ColliderShapeType.Capsule, default, default, capsule);
    }
}
