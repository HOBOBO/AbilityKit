namespace AbilityKit.Demo.Moba
{
    public static class MobaCollisionLayers
    {
        public const int UnitId = 0;
        public const int ProjectileId = 1;
        public const int WorldId = 2;

        public const int UnitMask = 1 << UnitId;
        public const int ProjectileMask = 1 << ProjectileId;
        public const int WorldMask = 1 << WorldId;
    }
}
