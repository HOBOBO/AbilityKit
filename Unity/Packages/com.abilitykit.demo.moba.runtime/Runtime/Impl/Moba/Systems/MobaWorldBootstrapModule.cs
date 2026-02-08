using System;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO;
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
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

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

        private sealed class PlanTextLoaderAdapter : TriggerPlanJsonDatabase.ITextLoader
        {
            private readonly ITextLoader _inner;

            public PlanTextLoaderAdapter(ITextLoader inner)
            {
                _inner = inner;
            }

            public bool TryLoad(string id, out string text)
            {
                if (_inner == null)
                {
                    text = null;
                    return false;
                }

                return _inner.TryLoad(id, out text);
            }
        }

        private sealed class WorldResolverContextSource : AbilityKit.Triggering.Runtime.ITriggerContextSource<IWorldResolver>
        {
            private readonly IWorldResolver _services;

            public WorldResolverContextSource(IWorldResolver services)
            {
                _services = services;
            }

            public IWorldResolver GetContext() => _services;
        }

        private static void RegisterStubActionsFromPlans(TriggerPlanJsonDatabase db, ActionRegistry actions)
        {
            if (db == null || actions == null) return;

            var arityById = new System.Collections.Generic.Dictionary<int, byte>();
            var records = db.Records;
            if (records == null) return;

            for (int i = 0; i < records.Count; i++)
            {
                var plan = records[i].Plan;
                var calls = plan.Actions;
                if (calls == null) continue;

                for (int j = 0; j < calls.Length; j++)
                {
                    var call = calls[j];
                    var id = call.Id.Value;
                    if (id == 0) continue;

                    if (arityById.TryGetValue(id, out var existing))
                    {
                        if (existing != call.Arity)
                        {
                            arityById[id] = byte.MaxValue;
                        }
                    }
                    else
                    {
                        arityById[id] = call.Arity;
                    }
                }
            }

            foreach (var kv in arityById)
            {
                var actionId = new ActionId(kv.Key);
                var arity = kv.Value;
                if (arity == byte.MaxValue) continue;

                switch (arity)
                {
                    case 0:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action0>(
                            actionId,
                            static (args, ctx) => { },
                            isDeterministic: true);
                        break;
                    case 1:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action1>(
                            actionId,
                            static (args, a0, ctx) => { },
                            isDeterministic: true);
                        break;
                    case 2:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                            actionId,
                            static (args, a0, a1, ctx) => { },
                            isDeterministic: true);
                        break;
                }
            }
        }

        public void Configure(WorldContainerBuilder builder)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            builder.AddModule(new EntitasEcsWorldModule());
            builder.AddModule(new TriggeringWorldModule());

            builder.TryRegister<AbilityKit.Ability.Triggering.IEventBus>(WorldLifetime.Scoped, _ => new AbilityKit.Ability.Triggering.EventBus());
            builder.TryRegister<AbilityKit.Triggering.Eventing.IEventBus>(WorldLifetime.Scoped, _ => new AbilityKit.Triggering.Eventing.EventBus());

            builder.TryRegister<FunctionRegistry>(WorldLifetime.Scoped, _ => new FunctionRegistry());
            builder.TryRegister<ActionRegistry>(WorldLifetime.Scoped, _ => new ActionRegistry());
            builder.TryRegister<AbilityKit.Triggering.Runtime.ITriggerContextSource<IWorldResolver>>(WorldLifetime.Scoped, r => new WorldResolverContextSource(r));
            builder.TryRegister<AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver>>(WorldLifetime.Scoped, r =>
            {
                var planBus = r.Resolve<AbilityKit.Triggering.Eventing.IEventBus>();
                var funcs = r.Resolve<FunctionRegistry>();
                var acts = r.Resolve<ActionRegistry>();
                var ctxSource = r.Resolve<AbilityKit.Triggering.Runtime.ITriggerContextSource<IWorldResolver>>();
                return new AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver>(planBus, funcs, acts, contextSource: ctxSource);
            });

            builder.RegisterService<BattleTriggersService, BattleTriggersService>();

            builder.RegisterService<MobaLobbyStateService, MobaLobbyStateService>();

            builder.TryRegister<MobaConfigDatabase>(WorldLifetime.Singleton, _ =>
            {
                var db = new MobaConfigDatabase();
                try
                {
                    /*
                     * 职责边界/数据流：
                     * - MobaConfigDatabase 是逻辑层“读表后的配置数据库”，供技能/BUFF/召唤/初始化等系统查询。
                     * - 接入方只需提供 IMobaConfigTextSink（表数据读取 sink），逻辑层负责解析与建库。
                     * - 若未提供 sink，则 fallback 到 Unity Resources（保持旧行为）。
                     */
                    if (_.TryResolve<IMobaConfigTextSink>(out var sink) && sink != null)
                    {
                        db.LoadFromTextSink(sink, MobaConfigPaths.DefaultResourcesDir);
                    }
                    else
                    {
                        db.LoadFromResources(MobaConfigPaths.DefaultResourcesDir);
                    }

                    return db;
                }
                catch (Exception ex)
                {
                    /* 建库失败属于启动期关键错误，需要可观测（Log）且不能静默吞掉（rethrow）。 */
                    Log.Exception(ex, "[MobaWorldBootstrapModule] MobaConfigDatabase load failed");
                    throw;
                }
            });

            builder.TryRegister<ITagTemplateRegistry>(WorldLifetime.Singleton, r => new MobaTagTemplateRegistry(r.Resolve<MobaConfigDatabase>()));
            builder.TryRegisterType<IGameplayTagService, GameplayTagService>(WorldLifetime.Scoped);
            builder.TryRegisterType<IDurableRegistry, DurableRegistry>(WorldLifetime.Scoped);
            builder.TryRegisterType<ITagEffectRouter, TagEffectRouter>(WorldLifetime.Scoped);

            builder.TryRegister<ITextLoader>(WorldLifetime.Singleton, _ => new UnityResourcesTextLoader());

            builder.TryRegister<TriggerPlanJsonDatabase>(WorldLifetime.Singleton, r =>
            {
                var loader = r.Resolve<ITextLoader>();
                var db = new TriggerPlanJsonDatabase();
                Log.Info("[MobaWorldBootstrapModule] TriggerPlanJsonDatabase.Load begin");
                db.Load(new PlanTextLoaderAdapter(loader), "ability/ability_trigger_plans");
                Log.Info($"[MobaWorldBootstrapModule] TriggerPlanJsonDatabase.Load end. records={db.Records?.Count ?? 0}");
                return db;
            });

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

            builder.RegisterService<AbilityKit.Ability.Impl.Moba.Util.Generator.ActorEntityInitPipeline, AbilityKit.Ability.Impl.Moba.Util.Generator.ActorEntityInitPipeline>();

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

            try
            {
                if (services.TryResolve<TriggerPlanJsonDatabase>(out var db) && db != null
                    && services.TryResolve<ActionRegistry>(out var acts) && acts != null
                    && services.TryResolve<AbilityKit.Triggering.Runtime.TriggerRunner<IWorldResolver>>(out var runner) && runner != null)
                {
                    RegisterStubActionsFromPlans(db, acts);

                    var debugLogId = new ActionId(AbilityKit.Triggering.Eventing.StableStringId.Get("action:debug_log"));
                    acts.Register<PlannedTrigger<object, IWorldResolver>.Action0>(
                        debugLogId,
                        static (args, ctx) =>
                        {
                            var ctxType = ctx.Context != null ? ctx.Context.GetType().Name : "<null>";
                            var argsType = args != null ? args.GetType().Name : "<null>";
                            Log.Info($"[Plan] debug_log executed. argsType={argsType}, ctxType={ctxType}");
                        },
                        isDeterministic: true);

                    acts.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                        debugLogId,
                        static (args, a0, a1, ctx) =>
                        {
                            var msgId = (int)a0;
                            var dump = a1 >= 0.5;
                            var msg = string.Empty;
                            if (ctx.Context != null && ctx.Context.TryResolve<TriggerPlanJsonDatabase>(out var db) && db != null)
                            {
                                if (!db.TryGetString(msgId, out msg)) msg = string.Empty;
                            }
                            Log.Info($"[Plan] debug_log: {msg}");
                            if (dump)
                            {
                                var argsType = args != null ? args.GetType().Name : "<null>";
                                var ctxType = ctx.Context != null ? ctx.Context.GetType().Name : "<null>";
                                Log.Info($"[Plan] debug_log dump. argsType={argsType}, ctxType={ctxType}");
                            }
                        },
                        isDeterministic: true);

                    var shootProjectileId = new ActionId(AbilityKit.Triggering.Eventing.StableStringId.Get("action:shoot_projectile"));
                    acts.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                        shootProjectileId,
                        static (args, a0, a1, ctx) =>
                        {
                            try
                            {
                                if (ctx.Context == null) return;

                                var launcherId = (int)a0;
                                var projectileId = (int)a1;
                                if (launcherId <= 0 || projectileId <= 0) return;

                                if (!ctx.Context.TryResolve<MobaProjectileService>(out var projectileSvc) || projectileSvc == null) return;
                                if (!ctx.Context.TryResolve<MobaConfigDatabase>(out var configs) || configs == null) return;

                                var casterActorId = 0;
                                var aimPos = Vec3.Zero;
                                var aimDir = Vec3.Zero;
                                if (args is SkillCastContext scc)
                                {
                                    casterActorId = scc.CasterActorId;
                                    aimPos = scc.AimPos;
                                    aimDir = scc.AimDir;
                                }

                                if (casterActorId <= 0) return;

                                ProjectileLauncherMO launcher = null;
                                ProjectileMO projectile = null;
                                if (launcherId > 0) configs.TryGetProjectileLauncher(launcherId, out launcher);
                                if (projectileId > 0) configs.TryGetProjectile(projectileId, out projectile);
                                if (launcher == null || projectile == null) return;

                                var casterPos = Vec3.Zero;
                                if (ctx.Context.TryResolve<MobaActorRegistry>(out var actorRegistry)
                                    && actorRegistry != null
                                    && actorRegistry.TryGet(casterActorId, out var casterEntity)
                                    && casterEntity != null
                                    && casterEntity.hasTransform)
                                {
                                    casterPos = casterEntity.transform.Value.Position;
                                }

                                // Keep legacy behavior: aimPos is offset from caster when provided.
                                if (!aimPos.Equals(Vec3.Zero)) aimPos = casterPos + aimPos;
                                if (!aimDir.Equals(Vec3.Zero)) aimDir = aimDir.Normalized;

                                if (!projectileSvc.Launch(casterActorId, launcher, projectile, in aimPos, in aimDir))
                                {
                                    Log.Warning($"[Plan] shoot_projectile launch failed. launcherId={launcherId} projectileId={projectileId}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Exception(ex, "[Plan] shoot_projectile executed failed");
                            }
                        },
                        isDeterministic: true);

                    db.RegisterAll(runner);
                    Log.Info($"[MobaWorldBootstrapModule] PlanTriggering initialized. records={db.Records?.Count ?? 0}");
                }
                else
                {
                    Log.Info("[MobaWorldBootstrapModule] PlanTriggering init skipped (missing deps)");
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[MobaWorldBootstrapModule] PlanTriggering init exception");
            }

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

