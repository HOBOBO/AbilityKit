using System;
using AbilityKit.Ability.Host.WorldBlueprints;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Combat.Collision;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.EntitasAdapters;
using AbilityKit.Demo.Moba.Systems;

namespace AbilityKit.Demo.Moba.Worlds.Blueprints
{
    public abstract class MobaLogicWorldBlueprintBase : IWorldBlueprint
    {
        public abstract string WorldType { get; }

        protected abstract MobaLogicWorldProfile Profile { get; }

        protected virtual MobaLogicWorldFeatures Features => MobaLogicWorldFeatures.EntitasContexts;

        public void Configure(WorldCreateOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            ConfigureCommon(options);
            ConfigureBlueprintOptions(options);
            ConfigureModules(options);
        }

        protected virtual void ConfigureCommon(WorldCreateOptions options)
        {
            options.ServiceBuilder ??= WorldServiceContainerFactory.CreateDefaultOnly();
            options.ServiceBuilder.Register<ICollisionService>(WorldLifetime.Singleton, _ => new CollisionService(new CollisionWorldOptions { BroadphaseType = BroadphaseType.Grid, GridCellSize = 4f }));

            if ((Features & MobaLogicWorldFeatures.EntitasContexts) != 0)
            {
                options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());
            }
        }

        protected virtual void ConfigureBlueprintOptions(WorldCreateOptions options)
        {
            options.SetMobaLogicWorldBlueprintOptions(CreateBlueprintOptions());
        }

        protected virtual MobaLogicWorldBlueprintOptions CreateBlueprintOptions()
        {
            return new MobaLogicWorldBlueprintOptions(WorldType, Profile, Features);
        }

        protected virtual void ConfigureModules(WorldCreateOptions options)
        {
        }

        protected void EnsureModule<TModule>(WorldCreateOptions options, Func<TModule> factory) where TModule : class, IWorldModule
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            for (int i = 0; i < options.Modules.Count; i++)
            {
                if (options.Modules[i] != null && options.Modules[i].GetType() == typeof(TModule))
                {
                    return;
                }
            }

            options.Modules.Add(factory());
        }
    }

    public enum MobaLogicWorldProfile
    {
        Unknown = 0,
        Lobby = 1,
        Battle = 2,
    }

    [Flags]
    public enum MobaLogicWorldFeatures
    {
        None = 0,
        EntitasContexts = 1 << 0,
        BootstrapFlow = 1 << 1,
        InputPort = 1 << 2,
        SnapshotOutput = 1 << 3,
        StateSync = 1 << 4,
        Config = 1 << 5,
        Skills = 1 << 6,
        Projectiles = 1 << 7,
        Triggering = 1 << 8,

        BattleRuntime = BootstrapFlow | InputPort | SnapshotOutput | StateSync | Config | Skills | Projectiles | Triggering,
    }

    public sealed class MobaLogicWorldBlueprintOptions
    {
        public MobaLogicWorldBlueprintOptions(string worldType, MobaLogicWorldProfile profile, MobaLogicWorldFeatures features)
        {
            WorldType = worldType;
            Profile = profile;
            Features = features;
        }

        public string WorldType { get; }

        public MobaLogicWorldProfile Profile { get; }

        public MobaLogicWorldFeatures Features { get; }

        public bool HasFeature(MobaLogicWorldFeatures feature)
        {
            return (Features & feature) == feature;
        }
    }

    public static class MobaLogicWorldBlueprintOptionsExtensions
    {
        public static void SetMobaLogicWorldBlueprintOptions(this WorldCreateOptions options, MobaLogicWorldBlueprintOptions value)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.Extensions[typeof(MobaLogicWorldBlueprintOptions)] = value;
        }

        public static bool TryGetMobaLogicWorldBlueprintOptions(this WorldCreateOptions options, out MobaLogicWorldBlueprintOptions value)
        {
            if (options != null && options.Extensions.TryGetValue(typeof(MobaLogicWorldBlueprintOptions), out var raw) && raw is MobaLogicWorldBlueprintOptions typed)
            {
                value = typed;
                return true;
            }

            value = null;
            return false;
        }
    }
}
