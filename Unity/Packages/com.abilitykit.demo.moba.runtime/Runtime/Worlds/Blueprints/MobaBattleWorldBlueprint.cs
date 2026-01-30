using System;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Impl.Moba.Worlds.Blueprints
{
    public sealed class MobaBattleWorldBlueprint : IWorldBlueprint
    {
        public const string Type = "battle";

        public string WorldType => Type;

        public void Configure(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();

            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());

            options.Modules.Add(new MobaWorldBootstrapModule());
        }
    }
}
