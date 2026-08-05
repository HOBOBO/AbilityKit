namespace AbilityKit.Demo.Moba.Services
{
    internal static partial class MobaGeneratedSnapshotEmitterManifest
    {
        public static int Register(MobaSnapshotEmitterRegistry registry)
        {
            var count = 0;
            AddGenerated(registry, ref count);
            return count;
        }

        static partial void AddGenerated(MobaSnapshotEmitterRegistry registry, ref int count);
    }
}
