using System;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Explicitly enables the optional trace projection and its diagnostic adapters for a world.
    /// </summary>
    public sealed class MobaTraceAdapterModule : IWorldModule
    {
        private static readonly string[] NamespacePrefixes =
        {
            "AbilityKit.Demo.Moba.Services",
        };

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.AddModule(new AttributeWorldServicesModule(
                WorldServiceProfile.All,
                new[] { typeof(MobaTraceAdapterModule).Assembly },
                NamespacePrefixes));
        }
    }
}
