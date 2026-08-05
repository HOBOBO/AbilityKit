namespace AbilityKit.Demo.Moba.Services.Projectile.Launch
{
    internal static partial class MobaGeneratedProjectileEmitterManifest
    {
        public static int Register(MobaProjectileEmitterRegistry registry)
        {
            var count = 0;
            AddGenerated(registry, ref count);
            return count;
        }

        static partial void AddGenerated(MobaProjectileEmitterRegistry registry, ref int count);
    }
}
