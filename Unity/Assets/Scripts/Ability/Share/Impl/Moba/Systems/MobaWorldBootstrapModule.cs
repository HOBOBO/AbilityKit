using System;
using AbilityKit.Ability.Server;
using AbilityKit.Ability.Share.Impl.Moba.Move;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed class MobaWorldBootstrapModule : IWorldModule, IEntitasSystemsInstaller
    {
        public const int InitOpCode = 2000;

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.AddModule(new TriggeringWorldModule());

            builder.RegisterService<MobaLobbyStateService, MobaLobbyStateService>();

            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ =>
            {
                var db = new MobaConfigDatabase();
                db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir);
                return db;
            });

            builder.TryRegister<ITextLoader>(WorldLifetime.Singleton, _ => new UnityResourcesTextLoader());

            builder.TryRegisterService<EffectSourceRegistry, EffectSourceRegistry>();

            builder.RegisterService<ActorIdAllocator, ActorIdAllocator>();
            builder.RegisterService<MobaActorRegistry, MobaActorRegistry>();
            builder.RegisterService<ActorIdIndex, ActorIdIndex>();
            builder.RegisterService<MobaActorLookupService, MobaActorLookupService>();

            builder.RegisterService<MobaEntityManager, MobaEntityManager>();

            builder.RegisterService<MobaPlayerActorMapService, MobaPlayerActorMapService>();
            builder.RegisterService<MobaActorTransformSnapshotService, MobaActorTransformSnapshotService>();
            builder.RegisterService<MobaActorSpawnSnapshotService, MobaActorSpawnSnapshotService>();
            builder.RegisterService<MobaStateHashSnapshotService, MobaStateHashSnapshotService>();

            builder.RegisterService<MobaProjectileEventSnapshotService, MobaProjectileEventSnapshotService>();

            builder.RegisterService<MobaEnterGameSnapshotService, MobaEnterGameSnapshotService>();

            builder.RegisterService<MobaLobbySnapshotService, MobaLobbySnapshotService>();
            builder.RegisterService<MobaSnapshotRouter, MobaSnapshotRouter>();
            builder.RegisterServiceAlias<IWorldStateSnapshotProvider, MobaSnapshotRouter>();

            builder.RegisterService<MobaEnterGameFlowService, MobaEnterGameFlowService>();
            builder.RegisterService<IWorldInputSink, MobaLobbyInputSink>();

            builder.RegisterService<MobaActorEntityGenerator, MobaActorEntityGenerator>();

            builder.RegisterService<MobaMoveService, MobaMoveService>();

            builder.RegisterService<ICollisionService, CollisionService>();

            builder.TryRegisterService<IProjectileService, ProjectileService>();

            builder.RegisterService<AbilityKit.Ability.Share.Impl.Moba.Services.Projectile.MobaProjectileLinkService, AbilityKit.Ability.Share.Impl.Moba.Services.Projectile.MobaProjectileLinkService>();
            builder.RegisterService<AbilityKit.Ability.Share.Impl.Moba.Services.Projectile.MobaProjectileService, AbilityKit.Ability.Share.Impl.Moba.Services.Projectile.MobaProjectileService>();

            builder.RegisterService<MobaSkillLoadoutService, MobaSkillLoadoutService>();
            builder.TryRegister<MobaTriggerIndexService>(WorldLifetime.Singleton, _ =>
            {
                var loader = _.Resolve<ITextLoader>();
                var s = new MobaTriggerIndexService(loader);
                s.LoadFromResources();
                return s;
            });
            builder.RegisterService<MobaEffectExecutionService, MobaEffectExecutionService>();
            builder.RegisterService<MobaEffectExecuteSubscriber, MobaEffectExecuteSubscriber>();
            builder.RegisterService<MobaEffectExecuteDemoSubscriber, MobaEffectExecuteDemoSubscriber>();
            builder.RegisterService<MobaEffectApplyDemoSubscriber, MobaEffectApplyDemoSubscriber>();

            builder.TryRegisterService<MobaBuffService, MobaBuffService>();
            builder.RegisterService<IMobaSkillPipelineLibrary, TableDrivenMobaSkillPipelineLibrary>();
            builder.RegisterService<SkillExecutor, SkillExecutor>();
        }

        public void Install(global::Contexts contexts, global::Entitas.Systems systems, IWorldServices services)
        {
            if (contexts == null) throw new ArgumentNullException(nameof(contexts));
            if (systems == null) throw new ArgumentNullException(nameof(systems));
            if (services == null) throw new ArgumentNullException(nameof(services));

            AutoSystemInstaller.Install(
                contexts,
                systems,
                services,
                assemblies: new[] { typeof(MobaWorldBootstrapModule).Assembly },
                namespacePrefixes: new[]
                {
                    "AbilityKit.Ability.Share.Impl.Moba",
                    "AbilityKit.Ability.Share.Common.Projectile",
                }
            );

            if (!services.TryGet<WorldInitData>(out var init) || init.Payload == null || init.Payload.Length == 0)
            {
                return;
            }

            // CreateWorld stage: store EnterGame request for later StartGame (server adjudication)
            var req = EnterMobaGameCodec.DeserializeReq(init.Payload);
            if (services.TryGet<MobaLobbyStateService>(out var lobby2) && lobby2 != null)
            {
                lobby2.SetEnterGameReq(req);
            }
        }
    }
}
