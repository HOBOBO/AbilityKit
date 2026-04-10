namespace AbilityKit.Core.Math
{
    public readonly struct Ray3
    {
        public readonly Vec3 Origin;
        public readonly Vec3 Direction;

        public Ray3(in Vec3 origin, in Vec3 direction)
        {
            Origin = origin;
            Direction = direction;
        }

        public Vec3 GetPoint(float distance)
        {
            return Origin + Direction * distance;
        }
    }
}
