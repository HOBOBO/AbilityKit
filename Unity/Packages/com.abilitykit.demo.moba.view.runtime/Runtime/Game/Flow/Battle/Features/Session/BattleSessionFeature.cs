using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Battle.Modules;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature : IGamePhaseFeature
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool DebugForceClientHashMismatch { get; set; }
#endif

        private readonly IBattleBootstrapper _bootstrapper;

        private GamePhaseContext _phaseCtx;

        private BattleLogicSession _session;
        private BattleStartPlan _plan;

        private BattleContext _ctx;

        private AbilityKit.Ability.EC.Entity _root;

        private FrameSnapshotDispatcher _snapshots;
        private SnapshotPipeline _pipeline;
        private SnapshotCmdHandler _cmdHandler;

        private LockstepReplayDriver _replay;

        private BattleSessionNetAdapter _netAdapter;
        private BattleSessionNetAdapterContext _netAdapterCtx;

        private AbilityKit.Network.Abstractions.IDispatcher _unityDispatcher;
        private AbilityKit.Network.Abstractions.DedicatedThreadDispatcher _networkIoDispatcher;

        private int _lastFrame;
        private float _tickAcc;

        private bool _firstFrameReceived;

        private bool _tickEnteredLogged;
        private bool _autoPlanLogged;

        private List<IBattleSessionModule> _modules;

        internal BattleEventBus Events { get; private set; }
        internal BattleSessionHooks Hooks { get; private set; }

#if UNITY_EDITOR
        private static bool _editorPlayModeHookInstalled;
        private bool _editorPlayModeHookActive;
#endif

        public event Action SessionStarted;
        public event Action FirstFrameReceived;
        public event Action<Exception> SessionFailed;

        private GameFlowDomain _flow;

        public BattleSessionFeature(IBattleBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper;
        }

        public BattleLogicSession Session => _session;
        public int LastFrame => _lastFrame;
        public BattleStartPlan Plan => _plan;

        public void OnAttach(in GamePhaseContext ctx)
        {
            _phaseCtx = ctx;
            ctx.Root.TryGetComponent(out _ctx);
            _root = ctx.Root;
            _flow = ctx.Entry != null ? ctx.Entry.Get<GameFlowDomain>() : null;

            Hooks = new BattleSessionHooks();
            Events = new BattleEventBus();
            Events.Subscribe<StartSessionRequested>(_ => OnStartSessionRequested());
            Events.Subscribe<SessionFailedEvent>(e =>
            {
                SessionFailed?.Invoke(e.Exception);
                Hooks?.SessionFailed.Invoke(e.Exception);
            });
            Events.Subscribe<FirstFrameReceivedEvent>(_ =>
            {
                FirstFrameReceived?.Invoke();
                Hooks?.FirstFrameReceived.Invoke();
            });

            EnsureModulesCreated();
            InvokeModulesAttach(ctx);

            _unityDispatcher = AbilityKit.Network.Abstractions.UnityMainThreadDispatcher.CaptureCurrent();
            _networkIoDispatcher ??= new AbilityKit.Network.Abstractions.DedicatedThreadDispatcher("GatewayNetworkThread");

#if UNITY_EDITOR
            TryInstallEditorPlayModeStopHook();
#endif

            _plan = _bootstrapper?.Build() ?? default;

            Events?.Publish(new PlanBuiltEvent(_plan));
            var planBuiltHandled = Hooks != null && Hooks.PlanBuilt.Invoke(_plan);

            Log.Info($"[BattleSessionFeature] OnAttach Plan: HostMode={_plan.HostMode}, UseGatewayTransport={_plan.UseGatewayTransport}, Gateway={_plan.GatewayHost}:{_plan.GatewayPort}, NumericRoomId={_plan.NumericRoomId}, AutoConnect={_plan.AutoConnect}, AutoCreateWorld={_plan.AutoCreateWorld}, AutoJoin={_plan.AutoJoin}, AutoReady={_plan.AutoReady}, WorldId={_plan.WorldId}, PlayerId={_plan.PlayerId}");

            if (!(planBuiltHandled || InvokeModulesPlanBuilt()))
            {
                try
                {
                    StartSession();
                    SessionStarted?.Invoke();
                    Events?.Publish(new SessionStartedEvent(_plan));
                    Hooks?.SessionStarted.Invoke(_plan);
                    ApplyAutoPlanActions();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[BattleSessionFeature] StartSession failed in OnAttach");
                    StopSession();
                    Events?.Publish(new SessionFailedEvent(ex));
                    return;
                }
            }

            if (_ctx != null)
            {
                _ctx.Plan = _plan;
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
            }

        }

        public void OnDetach(in GamePhaseContext ctx)
        {
#if UNITY_EDITOR
            TryUninstallEditorPlayModeStopHook();
#endif
            InvokeModulesDetach(ctx);
            StopSession();

            Events?.Dispose();
            Events = null;

            Hooks = null;

            _firstFrameReceived = false;

            if (_ctx != null)
            {
                _ctx.Session = null;
            }

            _ctx = null;
            _phaseCtx = default;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            Hooks?.PreTick.Invoke(deltaTime);
            InvokeModulesPreTick(ctx, deltaTime);
            Events?.Flush();

            if (_session == null) return;

            if (!_tickEnteredLogged)
            {
                _tickEnteredLogged = true;
            }

            _tickAcc += deltaTime;
            var fixedDelta = GetFixedDeltaSeconds();
            while (_tickAcc >= fixedDelta)
            {
                var nextFrame = _lastFrame + 1;
                _replay?.Pump(_session, nextFrame);
                _session.Tick(fixedDelta);
                _tickAcc -= fixedDelta;
            }

            TickRemoteDrivenLocalSim(deltaTime);
            TickConfirmedAuthorityWorldSim(deltaTime);

            InvokeModulesTick(ctx, deltaTime);
            Hooks?.PostTick.Invoke(deltaTime);
        }

        private void EnsureModulesCreated()
        {
            _modules ??= new List<IBattleSessionModule>(capacity: 8);
            if (_modules.Count == 0)
            {
                CreateModules(_modules);
                if (!TrySortModulesByDependencies(_modules))
                {
                    return;
                }
            }
        }

        private bool TrySortModulesByDependencies(List<IBattleSessionModule> modules)
        {
            if (modules == null || modules.Count <= 1) return true;

            void Fail(string message)
            {
                Log.Error($"[BattleSessionFeature] Module dependency validation failed: {message}");
                Events?.Publish(new SessionFailedEvent(new InvalidOperationException(message)));
            }

            var ids = new Dictionary<string, IBattleSessionModule>(StringComparer.Ordinal);
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i] is not IBattleSessionModuleId mid || string.IsNullOrEmpty(mid.Id))
                {
                    Fail($"Module at index {i} ({modules[i]?.GetType().Name ?? "<null>"}) does not implement IBattleSessionModuleId or Id is empty.");
                    return false;
                }

                if (ids.ContainsKey(mid.Id))
                {
                    Fail($"Duplicate module id '{mid.Id}'.");
                    return false;
                }

                ids[mid.Id] = modules[i];
            }

            var deps = new Dictionary<IBattleSessionModule, List<IBattleSessionModule>>();
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                if (m is not IBattleSessionModuleDependencies d) continue;

                var list = new List<IBattleSessionModule>();
                if (d.Dependencies != null)
                {
                    foreach (var depId in d.Dependencies)
                    {
                        if (string.IsNullOrEmpty(depId))
                        {
                            Fail($"Module '{((IBattleSessionModuleId)m).Id}' declares an empty dependency id.");
                            return false;
                        }

                        if (!ids.TryGetValue(depId, out var dep))
                        {
                            Fail($"Module '{((IBattleSessionModuleId)m).Id}' depends on missing module '{depId}'.");
                            return false;
                        }
                        list.Add(dep);
                    }
                }

                deps[m] = list;
            }

            var inDegree = new Dictionary<IBattleSessionModule, int>();
            for (int i = 0; i < modules.Count; i++) inDegree[modules[i]] = 0;
            foreach (var kv in deps)
            {
                var m = kv.Key;
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    inDegree[m] = inDegree[m] + 1;
                }
            }

            var queue = new List<IBattleSessionModule>(modules.Count);
            for (int i = 0; i < modules.Count; i++)
            {
                var m = modules[i];
                if (inDegree[m] == 0) queue.Add(m);
            }

            var sorted = new List<IBattleSessionModule>(modules.Count);
            while (queue.Count > 0)
            {
                var n = queue[0];
                queue.RemoveAt(0);
                sorted.Add(n);

                for (int i = 0; i < modules.Count; i++)
                {
                    var m = modules[i];
                    if (!deps.TryGetValue(m, out var mdeps) || mdeps == null || mdeps.Count == 0) continue;
                    var removed = false;
                    for (int j = 0; j < mdeps.Count; j++)
                    {
                        if (ReferenceEquals(mdeps[j], n))
                        {
                            removed = true;
                            break;
                        }
                    }
                    if (!removed) continue;

                    inDegree[m] = inDegree[m] - 1;
                    if (inDegree[m] == 0)
                    {
                        if (!queue.Contains(m)) queue.Add(m);
                    }
                }
            }

            if (sorted.Count != modules.Count)
            {
                Fail("Cyclic module dependencies detected.");
                return false;
            }

            modules.Clear();
            modules.AddRange(sorted);
            return true;
        }

        private void CreateModules(List<IBattleSessionModule> modules)
        {
            var cfg = (_bootstrapper as IBattleStartConfigProvider)?.Config;
            var set = cfg != null ? cfg.EffectiveSessionModuleSet : null;
            if (set == null || set.ModuleIds == null || set.ModuleIds.Count == 0)
            {
                modules.Add(new GatewayRoomModule(this));
                modules.Add(new SnapshotRoutingModule(this));
                modules.Add(new ReplaySeekModule(this));
                return;
            }

            for (int i = 0; i < set.ModuleIds.Count; i++)
            {
                var id = set.ModuleIds[i];
                if (string.IsNullOrEmpty(id)) continue;

                switch (id)
                {
                    case "gateway_room":
                        modules.Add(new GatewayRoomModule(this));
                        break;
                    case "snapshot_routing":
                        modules.Add(new SnapshotRoutingModule(this));
                        break;
                    case "replay_seek":
                        modules.Add(new ReplaySeekModule(this));
                        break;
                }
            }
        }

        private void OnStartSessionRequested()
        {
            try
            {
                StartSession();
                SessionStarted?.Invoke();
                Events?.Publish(new SessionStartedEvent(_plan));
                ApplyAutoPlanActions();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] StartSession failed after gateway room preparation");
                StopSession();
                Events?.Publish(new SessionFailedEvent(ex));
                return;
            }

            if (_ctx != null)
            {
                _ctx.Plan = _plan;
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
            }
        }

        private void InvokeModulesAttach(in GamePhaseContext ctx)
        {
            if (_modules == null || _modules.Count == 0) return;
            var mctx = new BattleSessionModuleContext(ctx, this);
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.OnAttach(mctx);
            }
        }

        private void InvokeModulesDetach(in GamePhaseContext ctx)
        {
            if (_modules == null || _modules.Count == 0) return;
            var mctx = new BattleSessionModuleContext(ctx, this);
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                _modules[i]?.OnDetach(mctx);
            }
        }

        private void InvokeModulesTick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_modules == null || _modules.Count == 0) return;
            var mctx = new BattleSessionModuleContext(ctx, this);
            for (int i = 0; i < _modules.Count; i++)
            {
                _modules[i]?.Tick(mctx, deltaTime);
            }
        }

        private void InvokeModulesPreTick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_modules == null || _modules.Count == 0) return;
            var mctx = new BattleSessionModuleContext(ctx, this);
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IBattleSessionPreTickModule preTick)
                {
                    preTick.PreTick(mctx, deltaTime);
                }
            }
        }

        private bool InvokeModulesPlanBuilt()
        {
            if (_modules == null || _modules.Count == 0) return false;
            var mctx = new BattleSessionModuleContext(_phaseCtx, this);
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IBattleSessionPlanModule plan)
                {
                    if (plan.OnPlanBuilt(mctx)) return true;
                }
            }

            return false;
        }

        private void InvokeModulesSessionStarting()
        {
            if (_modules == null || _modules.Count == 0) return;
            var mctx = new BattleSessionModuleContext(_phaseCtx, this);
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i] is IBattleSessionLifecycleModule lifecycle)
                {
                    lifecycle.OnSessionStarting(mctx);
                }
            }
        }

        private void InvokeModulesSessionStopping()
        {
            if (_modules == null || _modules.Count == 0) return;
            var mctx = new BattleSessionModuleContext(_phaseCtx, this);
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                if (_modules[i] is IBattleSessionLifecycleModule lifecycle)
                {
                    lifecycle.OnSessionStopping(mctx);
                }
            }
        }

        private float GetFixedDeltaSeconds()
        {
            var tickRate = _plan.TickRate;
            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)
            {
                tickRate = 30;
            }
            if (tickRate <= 0) tickRate = 30;
            return 1f / tickRate;
        }

        private void StartSession()
        {
            StopSession();

            var runMode = _plan.RunMode;

            var useRemote = _plan.SyncMode == BattleSyncMode.SnapshotAuthority || (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport);

            var opts = new BattleLogicSessionOptions
            {
                Mode = useRemote ? BattleLogicMode.Remote : BattleLogicMode.Local,
                WorldId = new WorldId(_plan.WorldId),
                WorldType = _plan.WorldType,
                ClientId = _plan.ClientId,
                PlayerId = _plan.PlayerId,

                ScanAssemblies = new[]
                {
                    typeof(AbilityKit.Ability.World.Services.WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Ability.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly,
                },
                NamespacePrefixes = new[] { "AbilityKit" },

                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
            };

            _session = StartBattleLogicSession(opts);
            _session.FrameReceived += OnFrame;

            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)
            {
                StartRemoteDrivenLocalWorld();
            }

            if (_plan.EnableConfirmedAuthorityWorld)
            {
                StartConfirmedAuthorityWorld();
            }

            Hooks?.SessionStarting.Invoke();
            InvokeModulesSessionStarting();

            _lastFrame = 0;
            _tickAcc = 0f;
            _firstFrameReceived = false;

            if (_ctx != null)
            {
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
            }

            BuildReplayOrRecord(runMode);
        }

        private void StopSession()
        {
            if (_session == null) return;

            try
            {
                _session.FrameReceived -= OnFrame;
                Hooks?.SessionStopping.Invoke();
                InvokeModulesSessionStopping();
                BattleLogicSessionHost.Stop();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] StopSession failed");
            }
            finally
            {
                _replay = null;

                TryDestroyBattleWorlds();
                DisposeConfirmedView();
                DisposeRemoteDrivenWorld();
                DisposeConfirmedWorld();
                DisposeNetworkIoDispatcher();

                _session = null;
            }
        }

        private static void DestroyEntityTree(AbilityKit.Ability.EC.Entity root)
        {
            if (!root.IsValid) return;

            var list = new List<AbilityKit.Ability.EC.Entity>(16);
            var stack = new Stack<AbilityKit.Ability.EC.Entity>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var e = stack.Pop();
                if (!e.IsValid) continue;
                list.Add(e);

                var count = e.ChildCount;
                for (int i = 0; i < count; i++)
                {
                    stack.Push(e.GetChild(i));
                }
            }

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e.IsValid) e.Destroy();
            }
        }

        private void CreateWorld()
        {
            if (_session == null) return;

            var builder = WorldServiceContainerFactory.CreateWithAttributes(
                AbilityKit.Ability.World.Services.Attributes.WorldServiceProfile.All,
                new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(AbilityKit.Ability.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly
                },
                new[] { "AbilityKit" }
            );

            builder.AddModule(new MobaConfigWorldModule());

            var options = new WorldCreateOptions(new WorldId(_plan.WorldId), _plan.WorldType)
            {
                ServiceBuilder = builder,
            };
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());

            var req = new CreateWorldRequest(options, _plan.CreateWorldOpCode, _plan.CreateWorldPayload);
            _session.CreateWorld(req);
        }

        private void OnFrame(FramePacket packet)
        {
            if (_netAdapter != null && _session != null)
            {
                var frame = packet.Frame.Value;
                if (_session.RemoteInputFrames != null
                    && _session.RemoteSnapshotFrames != null
                    && _session.RemoteInputFrames.TryGet(frame, out var inputFrame)
                    && _session.RemoteSnapshotFrames.TryGet(frame, out var snapshotFrame))
                {
                    packet = _netAdapter.ProcessAndFeed(packet.WorldId, inputFrame, snapshotFrame);
                }
                else
                {
                    packet = _netAdapter.ProcessAndFeed(packet);
                }
            }

            _lastFrame = packet.Frame.Value;

            if (!_firstFrameReceived)
            {
                _firstFrameReceived = true;
                Events?.Publish(new FirstFrameReceivedEvent());
            }

            if (_ctx != null)
            {
                _ctx.LastFrame = _lastFrame;

                OnFrameReplayAndRecording(packet);
            }
        }

    }
}
