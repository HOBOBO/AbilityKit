namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow
{
    internal static partial class MobaGeneratedBootstrapStageManifest
    {
        public static int RegisterAll()
        {
            var count = 0;
            AddGenerated(ref count);
            return count;
        }

        private static void Register(
            global::System.Func<MobaBootstrapStageBase> factory,
            string stageTypeName,
            ref int count)
        {
            try
            {
                MobaBootstrapStageRegistry.Register(factory());
                count++;
            }
            catch (global::System.Exception ex)
            {
                throw new global::System.InvalidOperationException(
                    $"Failed to register generated MOBA bootstrap stage: {stageTypeName}", ex);
            }
        }

        static partial void AddGenerated(ref int count);
    }
}
