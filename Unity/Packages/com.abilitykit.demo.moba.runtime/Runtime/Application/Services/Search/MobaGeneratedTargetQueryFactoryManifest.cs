namespace AbilityKit.Demo.Moba.Services.Search
{
    internal static partial class MobaGeneratedTargetQueryFactoryManifest
    {
        public static int Register(MobaTargetQueryFactoryRegistry registry)
        {
            var count = 0;
            AddGenerated(registry, ref count);
            return count;
        }

        static partial void AddGenerated(MobaTargetQueryFactoryRegistry registry, ref int count);
    }
}
