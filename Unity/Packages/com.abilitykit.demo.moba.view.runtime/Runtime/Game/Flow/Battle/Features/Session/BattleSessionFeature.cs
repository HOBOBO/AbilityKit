using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Ability.Share.Common.SnapshotRouting;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Flow.Modules;
using AbilityKit.Network.Abstractions;
using UnityEngine;

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

        private Exception _pendingModuleValidationFailure;

        private List<ISessionSubFeature<BattleSessionFeature>> _subFeatures;
        private ModuleHost<FeatureModuleContext<BattleSessionFeature>, ISessionSubFeature<BattleSessionFeature>> _subFeatureHost;

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

            EnsureModulesCreated();
            _subFeatureHost?.Attach(new FeatureModuleContext<BattleSessionFeature>(ctx, this));
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _subFeatureHost?.Detach(new FeatureModuleContext<BattleSessionFeature>(ctx, this));

            StopSession();

            Events?.Dispose();
            Events = null;

            Hooks = null;

            _firstFrameReceived = false;

            if (_ctx != null)
            {
                _ctx.Session = null;
                _ctx.Events = null;
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

            InvokeMainTickSubFeatures(ctx, deltaTime);

            _subFeatureHost?.Tick(new FeatureModuleContext<BattleSessionFeature>(ctx, this), deltaTime);
            Hooks?.PostTick.Invoke(deltaTime);
            Events?.Flush();
        }

        private void InvokeMainTickSubFeatures(in GamePhaseContext ctx, float deltaTime)
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(ctx, this);
            _subFeatureHost.ForEach<ISessionMainTickSubFeature<BattleSessionFeature>>(m => m.MainTick(fctx, deltaTime));
        }

        private void EnsureModulesCreated()
        {
            _subFeatures ??= new List<ISessionSubFeature<BattleSessionFeature>>(capacity: 8);
            if (_subFeatureHost != null && _subFeatures.Count > 0) return;

            void Fail(string message)
            {
                Log.Error($"[BattleSessionFeature] Module dependency validation failed: {message}");

                var ex = new InvalidOperationException(message);
                if (Events != null)
                {
                    Events.Publish(new SessionFailedEvent(ex));
                    Events.Flush();
                }
                else
                {
                    _pendingModuleValidationFailure = ex;
                }
            }

            _subFeatureHost = SessionSubFeaturePipeline.CreateModuleHost(_subFeatures, Fail);

            if (_subFeatures.Count == 0)
            {
                SessionSubFeaturePipeline.AddStandardSessionSubFeatures(_subFeatures);
                var raw = new List<IBattleSessionModule>(capacity: 8);
                CreateModules(raw);
                SessionSubFeaturePipeline.AddLegacySessionModules(_subFeatures, raw);
                SessionSubFeaturePipeline.AddPostLegacySessionSubFeatures(_subFeatures);

                if (!_subFeatureHost.TrySortByDependencies())
                {
                    return;
                }
            }
        }

        private void CreateModules(List<IBattleSessionModule> modules)
        {
            var cfg = (_bootstrapper as IBattleStartConfigProvider)?.Config;
            var set = cfg != null ? cfg.EffectiveSessionModuleSet : null;
            if (set == null || set.ModuleIds == null || set.ModuleIds.Count == 0)
            {
                modules.Add(new GatewayRoomModule(this));
                modules.Add(new SnapshotRoutingModule(this));
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
                }
            }
        }

        private void OnStartSessionRequested()
        {
            try
            {
                StartSession();
                Events?.Publish(new SessionStartedEvent(_plan));
                Events?.Flush();
                ApplyAutoPlanActions();
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] StartSession failed after gateway room preparation");
                StopSession();
                Events?.Publish(new SessionFailedEvent(ex));
                Events?.Flush();
                return;
            }

            if (_ctx != null)
            {
                _ctx.Plan = _plan;
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
            }
        }

        private void InvokeModulesPreTick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(ctx, this);
            _subFeatureHost.ForEach<ISessionPreTickSubFeature<BattleSessionFeature>>(m => m.PreTick(fctx, deltaTime));
        }

        private bool InvokeModulesPlanBuilt()
        {
            if (_subFeatureHost == null) return false;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
            var handled = false;
            _subFeatureHost.ForEach<ISessionPlanSubFeature<BattleSessionFeature>>(m =>
            {
                if (!handled && m.OnPlanBuilt(fctx)) handled = true;
            });
            return handled;
        }

        private void InvokeSessionStartingPipeline()
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
            _subFeatureHost.ForEach<ISessionLifecycleNotifySubFeature<BattleSessionFeature>>(m => m.NotifySessionStarting(fctx));
            _subFeatureHost.ForEach<ISessionLifecycleSubFeature<BattleSessionFeature>>(m => m.OnSessionStarting(fctx));
        }

        private void InvokeSessionStoppingPipeline()
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
            _subFeatureHost.ForEach<ISessionLifecycleNotifySubFeature<BattleSessionFeature>>(m => m.NotifySessionStopping(fctx));
            _subFeatureHost.ForEachReverse<ISessionLifecycleSubFeature<BattleSessionFeature>>(m => m.OnSessionStopping(fctx));
        }

        private void InvokeReplaySetupPipeline()
        {
            if (_subFeatureHost == null) return;
            var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
            _subFeatureHost.ForEach<ISessionReplaySetupSubFeature<BattleSessionFeature>>(m => m.SetupReplayOrRecord(fctx));
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

            InvokeSessionStartingPipeline();

            _lastFrame = 0;
            _tickAcc = 0f;
            _firstFrameReceived = false;

            if (_ctx != null)
            {
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
            }

            InvokeReplaySetupPipeline();
        }

        private void StopSession()
        {
            if (_session == null) return;

            try
            {
                _session.FrameReceived -= OnFrame;

                InvokeSessionStoppingPipeline();
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
            if (_subFeatureHost != null)
            {
                var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
                _subFeatureHost.ForEach<ISessionFramePacketTransformSubFeature<BattleSessionFeature>>(m => packet = m.TransformFramePacket(fctx, packet));
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
            }

            if (_subFeatureHost != null)
            {
                var fctx = new FeatureModuleContext<BattleSessionFeature>(_phaseCtx, this);
                _subFeatureHost.ForEach<ISessionFrameReceivedSubFeature<BattleSessionFeature>>(m => m.OnFrameReceived(fctx, packet));
            }
        }

    }
}
