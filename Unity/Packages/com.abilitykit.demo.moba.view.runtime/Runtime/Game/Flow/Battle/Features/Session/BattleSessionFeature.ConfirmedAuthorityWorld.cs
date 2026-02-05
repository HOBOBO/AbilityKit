using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Management;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.EntityCreation;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Ability.Share.Common.SnapshotRouting;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private AbilityKit.Ability.World.Management.IWorldManager _confirmedWorlds;
        private AbilityKit.Ability.Host.Framework.HostRuntime _confirmedRuntime;
        private AbilityKit.Ability.World.Abstractions.IWorld _confirmedWorld;
        private int _confirmedLastTickedFrame;
        private IRemoteFrameSource<PlayerInputCommand[]> _confirmedInputSource;
        private IConsumableRemoteFrameSource<PlayerInputCommand[]> _confirmedConsumable;
        private IRemoteFrameSink<PlayerInputCommand[]> _confirmedSink;

        private FrameSnapshotDispatcher _confirmedSnapshots;
        private DebugBattleViewEventSink _confirmedViewEventSink;
        private BattleSnapshotViewAdapter _confirmedSnapshotViewAdapter;
        private BattleTriggerEventViewBridge _confirmedTriggerBridge;

        private BattleContext _confirmedViewCtx;
        private FrameSnapshotDispatcher _confirmedViewSnapshots;
        private SnapshotPipeline _confirmedViewPipeline;
        private SnapshotCmdHandler _confirmedViewCmdHandler;
        private ConfirmedBattleViewFeature _confirmedViewFeature;

        private IDisposable _confirmedViewSubLobby;
        private IDisposable _confirmedViewSubActorTransform;
        private IDisposable _confirmedViewSubStateHash;

        private void TickConfirmedAuthorityWorldSim(float deltaTime)
        {
            if (_confirmedWorld == null || _confirmedRuntime == null) return;
            if (_confirmedInputSource == null) return;

            var inputTargetFrame = _confirmedInputSource.TargetFrame;
            if (inputTargetFrame <= 0) return;

            var driveTargetFrame = inputTargetFrame;
            var confirmedFrame = 0;
            var predictedFrame = 0;

            // Drive only up to confirmed frame from prediction driver stats (if available).
            var stats = _ctx != null ? _ctx.PredictionStats : null;
            if (stats != null)
            {
                var wid = new WorldId(_plan.WorldId);
                if (stats.TryGetFrames(wid, out var confirmed, out var predicted))
                {
                    confirmedFrame = confirmed.Value;
                    predictedFrame = predicted.Value;

                    // Only cap by confirmedFrame after it becomes available (>0).
                    // Early in the session stats may report 0, which would otherwise stall the confirmed world.
                    if (confirmedFrame > 0)
                    {
                        driveTargetFrame = Math.Min(inputTargetFrame, confirmedFrame);
                    }
                }
            }

            if (driveTargetFrame <= 0) return;

            var fixedDelta = GetFixedDeltaSeconds();
            var stepsBudget = MaxRemoteDrivenCatchUpStepsPerUpdate;
            if (stepsBudget <= 0) return;

            var worldId = _confirmedWorld.Id;
            IWorldStateSnapshotProvider provider = null;

            try
            {
                if (_confirmedWorld.Services != null)
                {
                    _confirmedWorld.Services.TryResolve(out provider);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                provider = null;
            }

            var steps = 0;
            while (steps < stepsBudget && _confirmedLastTickedFrame < driveTargetFrame)
            {
                var nextFrame = _confirmedLastTickedFrame + 1;
                var frameIndex = new FrameIndex(nextFrame);

                _confirmedRuntime.Tick(fixedDelta);

                if (provider != null)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        if (!provider.TryGetSnapshot(frameIndex, out var s))
                        {
                            break;
                        }

                        var synthesized = new FramePacket(worldId, frameIndex, Array.Empty<PlayerInputCommand>(), s);
                        _confirmedSnapshots?.Feed(synthesized);
                        _confirmedViewSnapshots?.Feed(synthesized);
                    }
                }

                _confirmedLastTickedFrame = nextFrame;
                steps++;
            }

            _confirmedInputSource.TrimBefore(_confirmedLastTickedFrame - 120);

            if (BattleFlowDebugProvider.ConfirmedAuthorityWorldStats != null)
            {
                var s = BattleFlowDebugProvider.ConfirmedAuthorityWorldStats;
                s.ConfirmedFrame = confirmedFrame;
                s.PredictedFrame = predictedFrame;
                s.AuthorityInputTargetFrame = inputTargetFrame;
                s.AuthorityDriveTargetFrame = driveTargetFrame;
                s.AuthorityLastTickedFrame = _confirmedLastTickedFrame;

                if (_confirmedViewEventSink != null)
                {
                    s.ViewEventTotal = _confirmedViewEventSink.Total;
                    s.RecentViewEvents = _confirmedViewEventSink.GetRecentLines();
                }
            }
        }

        private void StartConfirmedAuthorityWorld()
        {
            if (_confirmedWorld != null) return;

            var typeRegistry = new WorldTypeRegistry()
                .RegisterEntitasWorld(AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaLobbyWorldBlueprint.Type)
                .RegisterEntitasWorld(AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaBattleWorldBlueprint.Type);

            var blueprints = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintRegistry();
            AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaWorldBlueprintsRegistration.RegisterAll(blueprints);

            var baseFactory = new RegistryWorldFactory(typeRegistry);
            var factory = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintWorldFactory(baseFactory, blueprints);
            _confirmedWorlds = new WorldManager(factory);

            var serverOptions = new AbilityKit.Ability.Host.Framework.HostRuntimeOptions();
            _confirmedRuntime = new AbilityKit.Ability.Host.Framework.HostRuntime(_confirmedWorlds, serverOptions);

            var fixedDelta = GetFixedDeltaSeconds();

            // Confirmed-authority world: only driven by remote inputs, up to ConfirmedFrame.
            var modules = new AbilityKit.Ability.Host.Framework.HostRuntimeModuleHost()
                .Add(new AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionDriverModule(
                    resolveRemoteInputs: _ => _confirmedConsumable,
                    resolveLocalInputs: _ => null,
                    resolveIdealFrameLimit: _ => ResolveIdealFrameLimit(_),
                    inputDelayFrames: 0,
                    maxPredictionAheadFrames: 0,
                    minPredictionWindow: 0,
                    backlogEwmaAlpha: 0.20f,
                    enableRollback: false,
                    rollbackHistoryFrames: 0,
                    rollbackCaptureEveryNFrames: 0,
                    buildRollbackRegistry: _ => new AbilityKit.Ability.FrameSync.Rollback.RollbackRegistry(),
                    buildComputeHash: _ => null))
                .Add(new AbilityKit.Ability.Host.Extensions.Time.ServerFrameTimeModule(fixedDelta));

            modules.InstallAll(_confirmedRuntime, serverOptions);

            var builder = WorldServiceContainerFactory.CreateWithAttributes(
                AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile.All,
                new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Ability.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly
                },
                new[] { "AbilityKit" }
            );
            builder.AddModule(new MobaConfigWorldModule());
            builder.RegisterInstance(new WorldInitData(_plan.CreateWorldOpCode, _plan.CreateWorldPayload));
            builder.TryRegister<IFrameTime>(WorldLifetime.Singleton, _ => new AbilityKit.Ability.FrameSync.FrameTime());

            var authWorldId = new WorldId((_plan.WorldId ?? "room_1") + "__confirmed");
            var options = new WorldCreateOptions(authWorldId, _plan.WorldType)
            {
                ServiceBuilder = builder,
            };
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());

            _confirmedWorld = _confirmedRuntime.CreateWorld(options);

            _confirmedLastTickedFrame = 0;

            var buf = new FrameJitterBuffer<PlayerInputCommand[]>(delayFrames: 0, missingMode: MissingFrameMode.FillDefault, missingFrameFactory: Array.Empty<PlayerInputCommand>, initialCapacity: 256);
            _confirmedInputSource = buf;
            _confirmedConsumable = buf;
            _confirmedSink = buf;

            try
            {
                if (_confirmedWorld?.Services == null)
                {
                    Log.Error("[BattleSessionFeature] ConfirmedAuthorityWorld bootstrap failed: world.Services is null");
                }
                else
                {
                    var p = new PlayerId(_plan.PlayerId);

                    if (_confirmedWorld.Services.TryResolve<AbilityKit.Ability.Share.Impl.Moba.Services.MobaLobbyStateService>(out var lobby) && lobby != null)
                    {
                        lobby.OnPlayerJoined(p);
                    }
                    else
                    {
                        Log.Error("[BattleSessionFeature] ConfirmedAuthorityWorld bootstrap failed: MobaLobbyStateService not found");
                    }

                    if (_confirmedWorld.Services.TryResolve<AbilityKit.Ability.Host.IWorldInputSink>(out var sink) && sink != null)
                    {
                        var frame0 = new FrameIndex(0);
                        var ready = new PlayerInputCommand(frame0, p, (int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.Ready, Array.Empty<byte>());
                        sink.Submit(frame0, new[] { ready });
                    }
                    else
                    {
                        Log.Error("[BattleSessionFeature] ConfirmedAuthorityWorld bootstrap failed: IWorldInputSink not found");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }

            // Build an independent snapshot/view-event pipeline for confirmed authority world.
            if (_session != null)
            {
                _confirmedSnapshots = new FrameSnapshotDispatcher();
                // Debug-only: register decoders so BattleSnapshotViewAdapter can decode payloads.
                // Do NOT register cmd handlers here to avoid applying snapshots to the main battle entity world.
                AbilityKit.Game.Flow.Snapshot.BattleSnapshotRegistry.RegisterAll(
                    dispatcherDecoders: _confirmedSnapshots,
                    pipelineDecoders: _confirmedSnapshots,
                    pipeline: new NullSnapshotPipelineStageRegistry(),
                    cmd: new NullSnapshotCmdHandlerRegistry());

                AbilityKit.Game.Flow.Snapshot.LobbySnapshotRegistry.RegisterAll(
                    dispatcherDecoders: _confirmedSnapshots,
                    pipelineDecoders: _confirmedSnapshots,
                    pipeline: new NullSnapshotPipelineStageRegistry(),
                    cmd: new NullSnapshotCmdHandlerRegistry());

                AbilityKit.Game.Flow.Snapshot.SharedSnapshotRegistry.RegisterAll(
                    dispatcherDecoders: _confirmedSnapshots,
                    pipelineDecoders: _confirmedSnapshots,
                    pipeline: new NullSnapshotPipelineStageRegistry(),
                    cmd: new NullSnapshotCmdHandlerRegistry());

                _confirmedViewEventSink = new DebugBattleViewEventSink(maxLines: 32);

                var mode = _plan.ViewEventSourceMode;
                if (mode == BattleViewEventSourceMode.SnapshotOnly || mode == BattleViewEventSourceMode.Hybrid)
                {
                    _confirmedSnapshotViewAdapter = new BattleSnapshotViewAdapter(_confirmedSnapshots, _confirmedViewEventSink);
                }

                if (mode == BattleViewEventSourceMode.TriggerOnly || mode == BattleViewEventSourceMode.Hybrid)
                {
                    if (_confirmedWorld?.Services != null && _confirmedWorld.Services.TryResolve(out AbilityKit.Ability.Triggering.IEventBus bus) && bus != null)
                    {
                        _confirmedTriggerBridge = new BattleTriggerEventViewBridge(bus, _confirmedViewEventSink);
                    }
                }

            }

            // Build a dedicated view context for confirmed authority world and attach an extra view feature.
            // This context owns its own EC.EntityWorld and view binder mappings, isolated from the main battle context.
            if (_flow != null && _confirmedViewFeature == null && _plan.EnableConfirmedAuthorityWorld)
            {
                _confirmedViewCtx = BattleContext.Rent();
                _confirmedViewCtx.Plan = _ctx != null ? _ctx.Plan : default;
                _confirmedViewCtx.Session = null;

                var viewWorld = new AbilityKit.Ability.EC.EntityWorld();
                var lookup = new BattleEntityLookup();
                var node = viewWorld.Create("BattleEntity__confirmed");
                var entityFactory = new BattleEntityFactory(viewWorld, lookup, node);
                var query = new BattleEntityQuery(viewWorld, lookup);

                if (node.IsValid)
                {
                    node.AddComponent(lookup);
                    node.AddComponent(entityFactory);
                    node.AddComponent(query);
                }

                _confirmedViewCtx.EntityNode = node;
                _confirmedViewCtx.EntityWorld = viewWorld;
                _confirmedViewCtx.EntityLookup = lookup;
                _confirmedViewCtx.EntityFactory = entityFactory;
                _confirmedViewCtx.EntityQuery = query;
                _confirmedViewCtx.DirtyEntities = new List<AbilityKit.Ability.EC.EntityId>(128);

                _confirmedViewSnapshots = new FrameSnapshotDispatcher();
                _confirmedViewPipeline = new SnapshotPipeline(_confirmedViewCtx, _confirmedViewSnapshots);
                _confirmedViewCmdHandler = new SnapshotCmdHandler(_confirmedViewCtx, _confirmedViewSnapshots);
                AbilityKit.Game.Flow.Snapshot.BattleSnapshotRegistry.RegisterAll(_confirmedViewSnapshots, _confirmedViewPipeline, _confirmedViewPipeline, _confirmedViewCmdHandler);

                AbilityKit.Game.Flow.Snapshot.LobbySnapshotRegistry.RegisterAll(_confirmedViewSnapshots, _confirmedViewPipeline, _confirmedViewPipeline, _confirmedViewCmdHandler);

                AbilityKit.Game.Flow.Snapshot.SharedSnapshotRegistry.RegisterAll(_confirmedViewSnapshots, _confirmedViewPipeline, _confirmedViewPipeline, _confirmedViewCmdHandler);

                // Apply snapshots to confirmed view-side entity world (same logic as BattleSyncFeature subscriptions).
                _confirmedViewSubLobby = _confirmedViewSnapshots.Subscribe<AbilityKit.Ability.Share.Impl.Moba.Services.LobbySnapshot>(
                    (int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.LobbySnapshot,
                    (packet, snap) => ApplyConfirmedViewLobbySnapshot(snap));
                _confirmedViewSubActorTransform = _confirmedViewSnapshots.Subscribe<(int actorId, float x, float y, float z)[]>(
                    (int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.ActorTransformSnapshot,
                    (packet, entries) => ApplyConfirmedViewTransformSnapshot(entries));
                _confirmedViewSubStateHash = _confirmedViewSnapshots.Subscribe<AbilityKit.Ability.Share.Impl.Moba.Services.MobaStateHashSnapshotCodec.SnapshotPayload>(
                    (int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.StateHashSnapshot,
                    (packet, snap) => ApplyConfirmedViewStateHashSnapshot(snap));

                _confirmedViewCtx.FrameSnapshots = _confirmedViewSnapshots;
                _confirmedViewCtx.SnapshotPipeline = _confirmedViewPipeline;
                _confirmedViewCtx.CmdHandler = _confirmedViewCmdHandler;

                _confirmedViewFeature = new ConfirmedBattleViewFeature(_confirmedViewCtx);
                _flow.Attach(_confirmedViewFeature);
            }

            BattleFlowDebugProvider.ConfirmedAuthorityWorldStats = new ConfirmedAuthorityWorldStatsSnapshot
            {
                WorldId = authWorldId.Value,
                ConfirmedFrame = 0,
                PredictedFrame = 0,
                AuthorityInputTargetFrame = 0,
                AuthorityDriveTargetFrame = 0,
                AuthorityLastTickedFrame = 0,
                ViewEventTotal = 0,
                RecentViewEvents = null,
            };
        }

        private sealed class DebugBattleViewEventSink : IBattleViewEventSink
        {
            private readonly string[] _lines;
            private int _next;
            private int _count;

            public int Total { get; private set; }

            public DebugBattleViewEventSink(int maxLines)
            {
                if (maxLines <= 0) maxLines = 16;
                _lines = new string[maxLines];
            }

            public string[] GetRecentLines()
            {
                if (_count <= 0) return Array.Empty<string>();

                var n = Math.Min(_count, _lines.Length);
                var arr = new string[n];
                var start = (_next - n + _lines.Length) % _lines.Length;
                for (int i = 0; i < n; i++)
                {
                    arr[i] = _lines[(start + i) % _lines.Length];
                }
                return arr;
            }

            private void Push(string line)
            {
                if (string.IsNullOrWhiteSpace(line)) return;
                _lines[_next] = line;
                _next = (_next + 1) % _lines.Length;
                if (_count < _lines.Length) _count++;
                Total++;
            }

            public void OnTriggerEvent(in AbilityKit.Ability.Triggering.TriggerEvent evt)
            {
                var id = evt.Id != null ? evt.Id.ToString() : "<null>";
                Push($"Trigger:{id}");
            }

            public void OnEnterGameSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, EnterMobaGameRes res)
            {
                Push($"EnterGame: tickRate={res.TickRate}");
            }

            public void OnActorTransformSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, (int actorId, float x, float y, float z)[] entries)
            {
                if (entries == null) return;
                Push($"Transform: n={entries.Length}");
            }

            public void OnProjectileEventSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, MobaProjectileEventSnapshotCodec.Entry[] entries)
            {
                if (entries == null) return;
                Push($"Projectile: n={entries.Length}");
            }

            public void OnAreaEventSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, MobaAreaEventSnapshotCodec.Entry[] entries)
            {
                if (entries == null) return;
                Push($"Area: n={entries.Length}");
            }

            public void OnDamageEventSnapshot(AbilityKit.Ability.Host.ISnapshotEnvelope packet, MobaDamageEventSnapshotCodec.Entry[] entries)
            {
                if (entries == null) return;
                Push($"Damage: n={entries.Length}");
            }
        }

        private void ApplyConfirmedViewLobbySnapshot(AbilityKit.Ability.Share.Impl.Moba.Services.LobbySnapshot snap)
        {
            if (_confirmedViewCtx == null) return;
            var node = _confirmedViewCtx.EntityNode;
            if (!node.IsValid) return;

            var comp = node.TryGetComponent(out BattleLobbySnapshotComponent existing) ? existing : null;
            if (comp == null)
            {
                comp = new BattleLobbySnapshotComponent();
                node.AddComponent(comp);
            }

            comp.Started = snap.Started;
            comp.Version = snap.Version;
            comp.Players = snap.Players;
        }

        private void ApplyConfirmedViewStateHashSnapshot(AbilityKit.Ability.Share.Impl.Moba.Services.MobaStateHashSnapshotCodec.SnapshotPayload p)
        {
            if (_confirmedViewCtx == null) return;
            var node = _confirmedViewCtx.EntityNode;
            if (!node.IsValid) return;

            var comp = node.TryGetComponent(out BattleStateHashSnapshotComponent existing) ? existing : null;
            if (comp == null)
            {
                comp = new BattleStateHashSnapshotComponent();
                node.AddComponent(comp);
            }

            comp.Version = p.Version;
            comp.Frame = p.Frame;
            comp.Hash = p.Hash;
        }

        private void ApplyConfirmedViewTransformSnapshot((int actorId, float x, float y, float z)[] entries)
        {
            if (_confirmedViewCtx == null) return;

            var world = _confirmedViewCtx.EntityWorld;
            var lookup = _confirmedViewCtx.EntityLookup;
            var entityFactory = _confirmedViewCtx.EntityFactory;
            if (world == null || lookup == null || entityFactory == null) return;

            var dirty = _confirmedViewCtx.DirtyEntities;
            if (dirty == null)
            {
                dirty = new List<AbilityKit.Ability.EC.EntityId>(64);
                _confirmedViewCtx.DirtyEntities = dirty;
            }
            else
            {
                dirty.Clear();
            }

            if (entries == null || entries.Length == 0) return;
            for (int i = 0; i < entries.Length; i++)
            {
                var en = entries[i];
                var netId = new BattleNetId(en.actorId);

                if (!lookup.TryResolve(world, netId, out var e))
                {
                    continue;
                }

                if (!e.TryGetComponent(out BattleTransformComponent t) || t == null)
                {
                    t = new BattleTransformComponent();
                    e.AddComponent(t);
                }

                t.Position.x = en.x;
                t.Position.y = en.y;
                t.Position.z = en.z;
                if (t.Forward == default) t.Forward = UnityEngine.Vector3.forward;

                dirty.Add(e.Id);
            }
        }
    }
}
