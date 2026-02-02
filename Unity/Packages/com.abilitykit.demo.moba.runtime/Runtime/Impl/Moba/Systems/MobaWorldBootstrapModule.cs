using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Share.Impl.Moba.Services.Projectile;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.Impl.Moba.EffectSource;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.Share.ECS.Entitas;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Share.Common.Projectile;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Share.Impl.Moba.Move;
using AbilityKit.Ability.Share.Common.TagSystem;
using AbilityKit.Ability.Tags;

namespace AbilityKit.Ability.Share.Impl.Moba.Services.Projectile
{
    public sealed class MobaAreaTriggerRegistry : AbilityKit.Ability.World.Services.IService
    {
        private readonly System.Collections.Generic.Dictionary<int, Entry> _entries = new System.Collections.Generic.Dictionary<int, Entry>();

        public void Register(
            AbilityKit.Ability.Share.Common.Projectile.AreaId areaId,
            int templateId,
            int ownerId,
            in AbilityKit.Ability.Share.Math.Vec3 center,
            float radius,
            int collisionLayerMask,
            int maxTargets,
            int onEnterTriggerId,
            int onExitTriggerId,
            int[] onExpireTriggerIds)
        {
            if (areaId.Value <= 0) return;
            _entries[areaId.Value] = new Entry(templateId, ownerId, center, radius, collisionLayerMask, maxTargets, onEnterTriggerId, onExitTriggerId, onExpireTriggerIds);
        }

        public void Register(
            AbilityKit.Ability.Share.Common.Projectile.AreaId areaId,
            int templateId,
            int ownerId,
            in AbilityKit.Ability.Share.Math.Vec3 center,
            float radius,
            int collisionLayerMask,
            int maxTargets,
            int onEnterTriggerId,
            int onExitTriggerId,
            int onExpireTriggerId)
        {
            Register(areaId, templateId, ownerId, in center, radius, collisionLayerMask, maxTargets, onEnterTriggerId, onExitTriggerId, onExpireTriggerId > 0 ? new[] { onExpireTriggerId } : null);
        }

        public void Unregister(AbilityKit.Ability.Share.Common.Projectile.AreaId areaId)
        {
            if (areaId.Value <= 0) return;
            _entries.Remove(areaId.Value);
        }

        public bool TryGet(AbilityKit.Ability.Share.Common.Projectile.AreaId areaId, out Entry entry)
        {
            if (areaId.Value <= 0)
            {
                entry = default;
                return false;
            }

            return _entries.TryGetValue(areaId.Value, out entry);
        }

        public void Dispose()
        {
            _entries.Clear();
        }

        public readonly struct Entry
        {
            public readonly int TemplateId;
            public readonly int OwnerId;
            public readonly AbilityKit.Ability.Share.Math.Vec3 Center;
            public readonly float Radius;
            public readonly int CollisionLayerMask;
            public readonly int MaxTargets;
            public readonly int OnEnterTriggerId;
            public readonly int OnExitTriggerId;
            public readonly int[] OnExpireTriggerIds;

            public Entry(int templateId, int ownerId, in AbilityKit.Ability.Share.Math.Vec3 center, float radius, int collisionLayerMask, int maxTargets, int onEnterTriggerId, int onExitTriggerId, int[] onExpireTriggerIds)
            {
                TemplateId = templateId;
                OwnerId = ownerId;
                Center = center;
                Radius = radius;
                CollisionLayerMask = collisionLayerMask;
                MaxTargets = maxTargets;
                OnEnterTriggerId = onEnterTriggerId;
                OnExitTriggerId = onExitTriggerId;
                OnExpireTriggerIds = onExpireTriggerIds;
            }
        }
    }
}

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed class MobaProjectileReturnTargetProvider : IProjectileReturnTargetProvider
    {
        private readonly MobaActorRegistry _registry;

        public MobaProjectileReturnTargetProvider(MobaActorRegistry registry)
        {
            _registry = registry;
        }

        public bool TryGetReturnTargetPosition(int launcherActorId, out Vec3 position)
        {
            position = Vec3.Zero;
            if (launcherActorId <= 0) return false;
            if (_registry == null) return false;
            if (!_registry.TryGet(launcherActorId, out var e) || e == null) return false;
            if (!e.hasTransform) return false;
            position = e.transform.Value.Position;
            return true;
        }

        public void Dispose()
        {
        }
    }

    public sealed class MobaWorldBootstrapModule : IWorldModule, IEntitasSystemsInstaller
    {
        public const int InitOpCode = 2000;

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.AddModule(new EntitasEcsWorldModule());
            builder.AddModule(new TriggeringWorldModule());

            builder.TryRegister<AbilityKit.Ability.Triggering.IEventBus>(WorldLifetime.Scoped, _ => new AbilityKit.Ability.Triggering.EventBus());
            builder.TryRegister<AbilityKit.Triggering.Eventing.IEventBus>(WorldLifetime.Scoped, _ => new AbilityKit.Triggering.Eventing.EventBus());

            builder.RegisterService<BattleTriggersService, BattleTriggersService>();

            builder.RegisterService<MobaLobbyStateService, MobaLobbyStateService>();

            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ =>
            {
                var db = new MobaConfigDatabase();
                db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir);
                return db;
            });

            builder.TryRegister<ITagTemplateRegistry>(WorldLifetime.Singleton, r => new MobaTagTemplateRegistry(r.Resolve<MobaConfigDatabase>()));
            builder.TryRegisterType<IGameplayTagService, GameplayTagService>(WorldLifetime.Scoped);
            builder.TryRegisterType<IDurableRegistry, DurableRegistry>(WorldLifetime.Scoped);
            builder.TryRegisterType<ITagEffectRouter, TagEffectRouter>(WorldLifetime.Scoped);

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
            builder.RegisterService<MobaActorDespawnSnapshotService, MobaActorDespawnSnapshotService>();
            builder.RegisterService<MobaStateHashSnapshotService, MobaStateHashSnapshotService>();

            builder.RegisterService<MobaProjectileEventSnapshotService, MobaProjectileEventSnapshotService>();

            builder.RegisterService<MobaAreaEventSnapshotService, MobaAreaEventSnapshotService>();

            builder.RegisterService<MobaDamageEventSnapshotService, MobaDamageEventSnapshotService>();

            builder.RegisterService<MobaDamageService, MobaDamageService>();

            builder.RegisterService<DamagePipelineService, DamagePipelineService>();

            builder.RegisterService<MobaSummonService, MobaSummonService>();
            builder.RegisterService<MobaSummonDeathSubscriber, MobaSummonDeathSubscriber>();

            builder.RegisterService<MobaComponentTemplateService, MobaComponentTemplateService>();

            builder.RegisterService<MobaEnterGameSnapshotService, MobaEnterGameSnapshotService>();

            builder.RegisterService<MobaLobbySnapshotService, MobaLobbySnapshotService>();
            builder.RegisterService<MobaSnapshotRouter, MobaSnapshotRouter>();
            builder.RegisterServiceAlias<IWorldStateSnapshotProvider, MobaSnapshotRouter>();

            builder.RegisterService<MobaEnterGameFlowService, MobaEnterGameFlowService>();
            builder.RegisterService<IWorldInputSink, MobaLobbyInputSink>();

            builder.RegisterService<MobaActorEntityGenerator, MobaActorEntityGenerator>();

            builder.RegisterService<MobaMoveService, MobaMoveService>();

            builder.RegisterService<global::AbilityKit.Ability.Share.Math.CollisionService, global::AbilityKit.Ability.Share.Math.CollisionService>();
            builder.RegisterServiceAlias<global::AbilityKit.Ability.Share.Math.ICollisionService, global::AbilityKit.Ability.Share.Math.CollisionService>();

            builder.TryRegisterService<IProjectileService, ProjectileService>();

            builder.RegisterService<IProjectileReturnTargetProvider, MobaProjectileReturnTargetProvider>();

            builder.RegisterService<MobaProjectileLinkService, MobaProjectileLinkService>();
            builder.RegisterService<MobaProjectileService, MobaProjectileService>();

            builder.RegisterService<MobaAreaTriggerRegistry, MobaAreaTriggerRegistry>();

            builder.RegisterService<SearchTargetService, SearchTargetService>();

            builder.RegisterService<MobaSkillLoadoutService, MobaSkillLoadoutService>();
            builder.TryRegister<MobaTriggerIndexService>(WorldLifetime.Singleton, _ =>
            {
                var loader = _.Resolve<ITextLoader>();
                var s = new MobaTriggerIndexService(loader);
                Log.Info("[MobaWorldBootstrapModule] MobaTriggerIndexService.LoadFromResources begin");
                s.LoadFromResources();
                Log.Info("[MobaWorldBootstrapModule] MobaTriggerIndexService.LoadFromResources end");
                return s;
            });
            builder.RegisterService<MobaEffectExecutionService, MobaEffectExecutionService>();

            builder.RegisterService<MobaOngoingEffectService, MobaOngoingEffectService>();
            builder.RegisterService<MobaEffectExecuteSubscriber, MobaEffectExecuteSubscriber>();
            builder.RegisterService<MobaEffectExecuteDemoSubscriber, MobaEffectExecuteDemoSubscriber>();
            builder.RegisterService<MobaEffectApplyDemoSubscriber, MobaEffectApplyDemoSubscriber>();

            builder.TryRegisterService<MobaBuffService, MobaBuffService>();
            builder.RegisterService<IMobaSkillPipelineLibrary, TableDrivenMobaSkillPipelineLibrary>();
            builder.RegisterService<SkillExecutor, SkillExecutor>();
        }

        public void Install(global::Entitas.IContexts contexts, global::Entitas.Systems systems, IWorldResolver services)
        {
            Log.Info("[MobaWorldBootstrapModule] Install: begin");
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

            Log.Info("[MobaWorldBootstrapModule] Install: AutoSystemInstaller.Install done");

            if (!services.TryResolve<WorldInitData>(out var init))
            {
                Log.Info("[MobaWorldBootstrapModule] Install: WorldInitData not found; skip SetEnterGameReq");
                return;
            }

            var payloadLen = init.Payload != null ? init.Payload.Length : 0;
            Log.Info($"[MobaWorldBootstrapModule] Install: WorldInitData found. opCode={init.OpCode}, payloadLen={payloadLen}");

            if (payloadLen == 0)
            {
                Log.Info("[MobaWorldBootstrapModule] Install: WorldInitData payload is empty; skip SetEnterGameReq");
                return;
            }

            // CreateWorld stage: store EnterGame request for later StartGame (server adjudication)
            var req = EnterMobaGameCodec.DeserializeReq(init.Payload);
            if (services.TryResolve<MobaLobbyStateService>(out var lobby2) && lobby2 != null)
            {
                lobby2.SetEnterGameReq(req);
                Log.Info("[MobaWorldBootstrapModule] Install: SetEnterGameReq success");
            }
            else
            {
                Log.Info("[MobaWorldBootstrapModule] Install: MobaLobbyStateService not found; cannot SetEnterGameReq");
            }
        }
    }
}

