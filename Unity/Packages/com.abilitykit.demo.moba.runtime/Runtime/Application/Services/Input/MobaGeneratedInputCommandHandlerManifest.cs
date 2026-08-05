namespace AbilityKit.Demo.Moba.Services
{
    internal static partial class MobaGeneratedInputCommandHandlerManifest
    {
        public static int Register(MobaInputCommandHandlerRegistry registry)
        {
            var count = 0;
            AddGenerated(registry, ref count);
            return count;
        }

        static partial void AddGenerated(MobaInputCommandHandlerRegistry registry, ref int count);
    }
}
