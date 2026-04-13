using System;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Demo.Moba.Worlds.Blueprints
{
    public sealed class MobaLobbyWorldBlueprint : IWorldBlueprint
    {
        public const string Type = "lobby";

        public string WorldType => Type;

        public void Configure(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();

            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());

            var hasBootstrap = false;
            for (int i = 0; i < options.Modules.Count; i++)
            {
                if (options.Modules[i] != null && options.Modules[i].GetType() == typeof(MobaWorldBootstrapModule))
                {
                    hasBootstrap = true;
                    break;
                }
            }

            if (!hasBootstrap) options.Modules.Add(new MobaWorldBootstrapModule());
        }
    }
}
