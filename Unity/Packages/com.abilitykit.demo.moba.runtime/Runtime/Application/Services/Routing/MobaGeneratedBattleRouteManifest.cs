namespace AbilityKit.Demo.Moba.Services
{
    internal static partial class MobaGeneratedBattleRouteManifest
    {
        public static int Register(MobaBattleRouteRegistry registry)
        {
            var count = 0;
            AddGenerated(registry, ref count);
            return count;
        }

        static partial void AddGenerated(MobaBattleRouteRegistry registry, ref int count);
    }
}
