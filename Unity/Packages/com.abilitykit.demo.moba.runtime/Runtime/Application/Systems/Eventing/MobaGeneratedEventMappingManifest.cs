namespace AbilityKit.Demo.Moba.Systems
{
    internal static partial class MobaGeneratedEventMappingManifest
    {
        public static int Register(MobaEventSubscriptionRegistry registry)
        {
            var count = 0;
            AddGenerated(registry, ref count);
            return count;
        }

        static partial void AddGenerated(MobaEventSubscriptionRegistry registry, ref int count);

        private static void AddExact<TArgs>(
            MobaEventSubscriptionRegistry registry,
            string eventId,
            ref int count)
        {
            registry.RegisterExact<TArgs>(eventId);
            count++;
        }

        private static void AddPrefix<TArgs>(
            MobaEventSubscriptionRegistry registry,
            string prefix,
            ref int count)
        {
            registry.RegisterPrefix<TArgs>(prefix);
            count++;
        }
    }
}
