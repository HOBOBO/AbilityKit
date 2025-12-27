using System.Reflection;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Ability.World.Services
{
    public static class WorldServiceContainerFactory
    {
        public static WorldContainerBuilder CreateDefaultOnly()
        {
            var builder = new WorldContainerBuilder();
            builder.AddModule(new DefaultWorldServicesModule());
            return builder;
        }

        public static WorldContainerBuilder CreateWithAttributes(WorldServiceProfile profile, Assembly[] scanAssemblies, string[] namespacePrefixes = null)
        {
            var builder = CreateDefaultOnly();
            builder.AddModule(new AttributeWorldServicesModule(profile, scanAssemblies, namespacePrefixes));
            return builder;
        }

        public static WorldContainerBuilder CreateWithAttributes(WorldServiceProfile profile, bool scanAllLoadedAssemblies, string[] namespacePrefixes = null)
        {
            var builder = CreateDefaultOnly();
            builder.AddModule(new AttributeWorldServicesModule(profile, scanAllLoadedAssemblies, namespacePrefixes));
            return builder;
        }

        public static WorldContainerBuilder Create(WorldServiceProfile profile, params Assembly[] scanAssemblies)
        {
            return CreateWithAttributes(profile, scanAssemblies, null);
        }

        public static WorldContainerBuilder Create(WorldServiceProfile profile, bool scanAllLoadedAssemblies)
        {
            return CreateWithAttributes(profile, scanAllLoadedAssemblies, null);
        }
    }
}
