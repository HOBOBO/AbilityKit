using System;
using System.Threading;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Impl.Moba.Systems;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Impl.Moba.EntitasAdapters;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Entitas;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Game.Flow.Snapshot;
using AbilityKit.Ability.Share.Common.Record.Lockstep;
using AbilityKit.Game.Flow.Battle.Replay;
using AbilityKit.Game.Battle.Transport;
using AbilityKit.Network.Runtime;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Abstractions;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Flow.Battle.ViewEvents.Snapshot;
using AbilityKit.Game.Flow.Battle.ViewEvents.Triggering;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Flow;
using UnityEngine;
using System.Threading.Tasks;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Rollback;
using System.Diagnostics;
using AbilityKit.Game.EntityCreation;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature : IGamePhaseFeature
    {
        private const int MaxRemoteDrivenCatchUpStepsPerUpdate = 5;
        private const int StateHashRecordIntervalFrames = 10;
        private const int ReplaySeekChunkFrames = 300;
        private const int RollbackSeekProbeFrames = 120;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool DebugForceClientHashMismatch { get; set; }
#endif

        private readonly IBattleBootstrapper _bootstrapper;

        private BattleLogicSession _session;
        private BattleStartPlan _plan;

        private BattleContext _ctx;

        private AbilityKit.Ability.EC.Entity _root;

        private FrameSnapshotDispatcher _snapshots;
        private BattleSnapshotPipeline _pipeline;
        private BattleCmdHandler _cmdHandler;

        private LockstepReplayDriver _replay;

        private AbilityKit.Ability.World.Management.IWorldManager _remoteDrivenWorlds;
        private AbilityKit.Ability.Host.Framework.HostRuntime _remoteDrivenRuntime;
        private AbilityKit.Ability.World.Abstractions.IWorld _remoteDrivenWorld;
        private int _remoteDrivenLastTickedFrame;
        private int _remoteDrivenLastLoggedFrame;
        private bool _remoteDrivenFirstSnapshotLogged;
        private bool _remoteDrivenFirstSpawnLogged;

        private IRemoteFrameSource<PlayerInputCommand[]> _remoteDrivenInputSource;
        private IConsumableRemoteFrameSource<PlayerInputCommand[]> _remoteDrivenConsumable;
        private IRemoteFrameSink<PlayerInputCommand[]> _remoteDrivenSink;

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
        private BattleSnapshotPipeline _confirmedViewPipeline;
        private BattleCmdHandler _confirmedViewCmdHandler;
        private ConfirmedBattleViewFeature _confirmedViewFeature;

        private IDisposable _confirmedViewSubLobby;
        private IDisposable _confirmedViewSubActorTransform;
        private IDisposable _confirmedViewSubStateHash;

        private AbilityKit.Network.Abstractions.IDispatcher _unityDispatcher;
        private AbilityKit.Network.Abstractions.DedicatedThreadDispatcher _networkIoDispatcher;

        private int _lastFrame;
        private float _tickAcc;

        private bool _firstFrameReceived;

        private ConnectionManager _gatewayRoomConn;
        private GatewayRoomClient _gatewayRoomClient;
        private Task _gatewayRoomTask;

        private readonly Dictionary<WorldId, GatewayWorldStartAnchor> _gatewayWorldStartAnchors = new Dictionary<WorldId, GatewayWorldStartAnchor>();

        private CancellationTokenSource _timeSyncCts;
        private Task _timeSyncTask;

        private bool _hasClockSync;
        private double _clockOffsetSecondsEwma;
        private double _rttSecondsEwma;
        private int _timeSyncSamples;

        private bool _tickEnteredLogged;
        private bool _autoPlanLogged;

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
            ctx.Root.TryGetComponent(out _ctx);
            _root = ctx.Root;
            _flow = ctx.Entry != null ? ctx.Entry.Get<GameFlowDomain>() : null;

            _unityDispatcher = AbilityKit.Network.Abstractions.UnityMainThreadDispatcher.CaptureCurrent();
            _networkIoDispatcher ??= new AbilityKit.Network.Abstractions.DedicatedThreadDispatcher("GatewayNetworkThread");

#if UNITY_EDITOR
            TryInstallEditorPlayModeStopHook();
#endif

            _plan = _bootstrapper?.Build() ?? default;

            Log.Info($"[BattleSessionFeature] OnAttach Plan: HostMode={_plan.HostMode}, UseGatewayTransport={_plan.UseGatewayTransport}, Gateway={_plan.GatewayHost}:{_plan.GatewayPort}, NumericRoomId={_plan.NumericRoomId}, AutoConnect={_plan.AutoConnect}, AutoCreateWorld={_plan.AutoCreateWorld}, AutoJoin={_plan.AutoJoin}, AutoReady={_plan.AutoReady}, WorldId={_plan.WorldId}, PlayerId={_plan.PlayerId}");

            if (ShouldPrepareGatewayRoom())
            {
                StartGatewayRoomPreparation();
            }
            else
            {
                try
                {
                    StartSession();
                    SessionStarted?.Invoke();
                    ApplyAutoPlanActions();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[BattleSessionFeature] StartSession failed in OnAttach");
                    StopSession();
                    SessionFailed?.Invoke(ex);
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
            StopGatewayRoomPreparation();
            StopSession();

            _firstFrameReceived = false;

            if (_ctx != null)
            {
                _ctx.Session = null;
            }

            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_gatewayRoomConn != null)
            {
                _gatewayRoomConn.Tick(deltaTime);

                if (_gatewayRoomTask != null && _gatewayRoomTask.IsCompleted)
                {
                    if (_gatewayRoomTask.IsFaulted)
                    {
                        var ex = _gatewayRoomTask.Exception != null ? _gatewayRoomTask.Exception.GetBaseException() : null;
                        var wrapped = new InvalidOperationException("Gateway room preparation failed.", ex);
                        Log.Exception(wrapped, "[BattleSessionFeature] Gateway room preparation failed");
                        StopGatewayRoomPreparation();
                        SessionFailed?.Invoke(wrapped);
                        return;
                    }

                    StopGatewayRoomPreparation();

                    try
                    {
                        StartSession();
                        SessionStarted?.Invoke();
                        ApplyAutoPlanActions();
                    }
                    catch (Exception ex)
                    {
                        Log.Exception(ex, "[BattleSessionFeature] StartSession failed after gateway room preparation");
                        StopSession();
                        SessionFailed?.Invoke(ex);
                        return;
                    }

                    if (_ctx != null)
                    {
                        _ctx.Plan = _plan;
                        _ctx.Session = _session;
                        _ctx.LastFrame = _lastFrame;
                    }
                }
            }

            if (_session == null) return;

            if (!_tickEnteredLogged)
            {
                _tickEnteredLogged = true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_replay != null)
            {
                if (Input.GetKeyDown(KeyCode.P))
                {
                    if (_replay.IsPlaying) _replay.Pause();
                    else _replay.Play();
                }

                if (Input.GetKeyDown(KeyCode.R))
                {
                    _replay.SeekToStart();
                }

                if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                {
                    var target = Math.Max(0, _lastFrame + ReplaySeekChunkFrames);
                    SeekReplayToFrame(target);
                }

                if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                {
                    var target = Math.Max(0, _lastFrame - ReplaySeekChunkFrames);
                    SeekReplayToFrame(target);
                }
            }
#endif

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

            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)
            {
                IBattleLogicTransport transport;

                if (!uint.TryParse(_plan.PlayerId, out var localPlayerId))
                {
                    throw new InvalidOperationException($"GatewayRemote requires numeric PlayerId. playerId='{_plan.PlayerId}'");
                }

                var roomId = _plan.NumericRoomId;
                if (roomId == 0 && !ulong.TryParse(_plan.WorldId, out roomId))
                {
                    throw new InvalidOperationException($"GatewayRemote requires numeric WorldId(roomId). worldId='{_plan.WorldId}'");
                }

                var gatewayOptions = GatewayRemoteBattleTransportOptionsFactory.Create(
                    host: _plan.GatewayHost,
                    port: _plan.GatewayPort,
                    transportFactory: () => new TcpTransport(),
                    playerIdToUInt: pid => uint.TryParse(pid.Value, out var n) ? n : localPlayerId,
                    playerIdFromUInt: n => new PlayerId(n.ToString()),
                    worldIdToUlong: wid => ulong.TryParse(wid.Value, out var n) ? n : roomId,
                    worldIdFromUlong: n => new WorldId(n.ToString()),
                    roomId: roomId,
                    sessionToken: _plan.GatewaySessionToken);

                transport = new GatewayBattleLogicTransport(gatewayOptions, _unityDispatcher, _networkIoDispatcher);
                _session = BattleLogicSessionHost.Start(opts, remoteTransport: transport);
            }
            else
            {
                _session = BattleLogicSessionHost.Start(opts);
            }
            _session.FrameReceived += OnFrame;

            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)
            {
                StartRemoteDrivenLocalWorld();
            }

            if (_plan.EnableConfirmedAuthorityWorld)
            {
                StartConfirmedAuthorityWorld();
            }

            var subscribeToSessionFrames = !(_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport);
            _snapshots = new FrameSnapshotDispatcher(_session, subscribeToSessionFrames);
            _pipeline = new BattleSnapshotPipeline(_ctx, _snapshots);
            _cmdHandler = new BattleCmdHandler(_ctx, _snapshots);
            BattleSnapshotRegistry.RegisterAll(_snapshots, _pipeline, _pipeline, _cmdHandler);

            _lastFrame = 0;
            _tickAcc = 0f;
            _firstFrameReceived = false;

            if (runMode == BattleStartConfig.BattleRunMode.Replay)
            {
                var file = LockstepJsonInputRecordReader.Load(_plan.InputReplayPath);
                _replay = new LockstepReplayDriver(new WorldId(_plan.WorldId), file);
            }

            if (_ctx != null)
            {
                _ctx.Session = _session;
                _ctx.LastFrame = _lastFrame;
                _ctx.FrameSnapshots = _snapshots;
                _ctx.SnapshotPipeline = _pipeline;
                _ctx.CmdHandler = _cmdHandler;

                if (runMode == BattleStartConfig.BattleRunMode.Record)
                {
                    _ctx.InputRecordWriter?.Dispose();
                    _ctx.InputRecordWriter = new LockstepJsonInputRecordWriter(
                        _plan.InputRecordOutputPath,
                        new LockstepInputRecordMeta
                        {
                            WorldId = _plan.WorldId,
                            WorldType = _plan.WorldType,
                            TickRate = 30,
                            RandomSeed = 0,
                            PlayerId = _plan.PlayerId,
                            StartedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        });
                }
            }
        }

        private void StopSession()
        {
            if (_session == null) return;

            try
            {
                _session.FrameReceived -= OnFrame;
                _pipeline?.Dispose();
                _cmdHandler?.Dispose();
                _snapshots?.Dispose();
                BattleLogicSessionHost.Stop();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
            finally
            {
                if (_ctx != null)
                {
                    _ctx.SnapshotPipeline = null;
                    _ctx.CmdHandler = null;
                    _ctx.FrameSnapshots = null;
                }

                _replay = null;
                _cmdHandler = null;
                _pipeline = null;
                _snapshots = null;

                try
                {
                    _remoteDrivenRuntime?.DestroyWorld(new WorldId(_plan.WorldId));
                    _confirmedRuntime?.DestroyWorld(new WorldId((_plan.WorldId ?? "room_1") + "__confirmed"));
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
                finally
                {
                    if (_flow != null && _confirmedViewFeature != null)
                    {
                        _flow.Detach(_confirmedViewFeature);
                        _confirmedViewFeature = null;
                    }

                    _confirmedViewSubLobby?.Dispose();
                    _confirmedViewSubLobby = null;
                    _confirmedViewSubActorTransform?.Dispose();
                    _confirmedViewSubActorTransform = null;
                    _confirmedViewSubStateHash?.Dispose();
                    _confirmedViewSubStateHash = null;

                    _confirmedViewCmdHandler?.Dispose();
                    _confirmedViewPipeline?.Dispose();
                    _confirmedViewSnapshots?.Dispose();
                    _confirmedViewCmdHandler = null;
                    _confirmedViewPipeline = null;
                    _confirmedViewSnapshots = null;

                    if (_confirmedViewCtx != null)
                    {
                        if (_confirmedViewCtx.EntityNode.IsValid)
                        {
                            DestroyEntityTree(_confirmedViewCtx.EntityNode);
                        }
                        _confirmedViewCtx.EntityLookup?.Clear();
                        BattleContext.Return(_confirmedViewCtx);
                        _confirmedViewCtx = null;
                    }

                    _remoteDrivenWorld = null;
                    _remoteDrivenRuntime = null;
                    _remoteDrivenWorlds = null;
                    _remoteDrivenLastTickedFrame = 0;
                    _remoteDrivenInputSource?.Dispose();
                    _remoteDrivenInputSource = null;
                    _remoteDrivenConsumable = null;
                    _remoteDrivenSink = null;

                    _confirmedWorld = null;
                    _confirmedRuntime = null;
                    _confirmedWorlds = null;
                    _confirmedLastTickedFrame = 0;
                    _confirmedInputSource?.Dispose();
                    _confirmedInputSource = null;
                    _confirmedConsumable = null;
                    _confirmedSink = null;

                    _confirmedSnapshotViewAdapter?.Dispose();
                    _confirmedSnapshotViewAdapter = null;

                    _confirmedTriggerBridge?.Dispose();
                    _confirmedTriggerBridge = null;

                    _confirmedViewEventSink = null;
                    _confirmedSnapshots = null;

                    BattleFlowDebugProvider.ConfirmedAuthorityWorldStats = null;

                    if (_ctx != null)
                    {
                        _ctx.PredictionStats = null;
                    }
                }

                try
                {
                    _networkIoDispatcher?.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
                finally
                {
                    _networkIoDispatcher = null;
                }

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

        private void SeekReplayToFrame(int targetFrame)
        {
            if (!_plan.EnableInputReplay) return;
            if (targetFrame < 0) targetFrame = 0;

            var fixedDelta = GetFixedDeltaSeconds();

            // Fast path: seek forward by fast-forwarding within the same session.
            if (_session != null && _replay != null && targetFrame > _lastFrame)
            {
                _tickAcc = 0f;

                for (int f = _lastFrame + 1; f <= targetFrame; f++)
                {
                    _replay.Pump(_session, f);
                    _session.Tick(fixedDelta);
                }

                _lastFrame = targetFrame;
                if (_ctx != null) _ctx.LastFrame = _lastFrame;
                return;
            }

            // Fast path: seek backward within rollback history without restarting.
            if (_session != null && _session.RollbackModule != null && targetFrame <= _lastFrame)
            {
                var worldId = new WorldId(_plan.WorldId);
                var probeStart = Math.Max(0, targetFrame - RollbackSeekProbeFrames);
                for (int f = targetFrame; f >= probeStart; f--)
                {
                    if (_session.RollbackModule.TryRollbackAndReplay(worldId, new FrameIndex(f), new FrameIndex(targetFrame), fixedDelta))
                    {
                        _lastFrame = targetFrame;
                        if (_ctx != null) _ctx.LastFrame = _lastFrame;
                        return;
                    }
                }
            }

            StopSession();
            StartSession();
            ApplyAutoPlanActions();

            if (_replay == null) return;
            _replay.SeekToStart();

            _tickAcc = 0f;

            for (int f = 1; f <= targetFrame; f++)
            {
                _replay.Pump(_session, f);
                _session.Tick(fixedDelta);
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
            if (_remoteDrivenWorld != null || _confirmedWorld != null)
            {
                try
                {
                    var frame = packet.Frame.Value;
                    var worldId = _remoteDrivenWorld != null ? _remoteDrivenWorld.Id : packet.WorldId;

                    var inputs = packet.Inputs == null || packet.Inputs.Count == 0
                        ? Array.Empty<PlayerInputCommand>()
                        : (packet.Inputs is PlayerInputCommand[] arr ? arr : new List<PlayerInputCommand>(packet.Inputs).ToArray());

                    if (_remoteDrivenInputSource == null)
                    {
                        var delay = _plan.InputDelayFrames < 0 ? 0 : _plan.InputDelayFrames;
                        var buf = new FrameJitterBuffer<PlayerInputCommand[]>(delay, MissingFrameMode.FillDefault, Array.Empty<PlayerInputCommand>);
                        _remoteDrivenInputSource = buf;
                        _remoteDrivenConsumable = buf;
                        _remoteDrivenSink = buf;

                        AbilityKit.Game.Flow.BattleFlowDebugProvider.JitterBufferStats = new AbilityKit.Game.Flow.JitterBufferStatsSnapshot
                        {
                            DelayFrames = buf.DelayFrames,
                            MissingMode = buf.MissingMode.ToString(),
                            TargetFrame = buf.TargetFrame,
                            MaxReceivedFrame = buf.MaxReceivedFrame,
                            LastConsumedFrame = buf.LastConsumedFrame,
                            BufferedCount = buf.Count,
                            MinBufferedFrame = buf.MinBufferedFrame,

                            AddedCount = buf.AddedCount,
                            DuplicateCount = buf.DuplicateCount,
                            LateCount = buf.LateCount,
                            ConsumedCount = buf.ConsumedCount,
                            FilledDefaultCount = buf.FilledDefaultCount,
                        };
                    }

                    _remoteDrivenSink?.Add(frame, inputs);

                    if (_confirmedInputSource == null)
                    {
                        var buf = new FrameJitterBuffer<PlayerInputCommand[]>(delayFrames: 0, missingMode: MissingFrameMode.FillDefault, missingFrameFactory: Array.Empty<PlayerInputCommand>, initialCapacity: 256);
                        _confirmedInputSource = buf;
                        _confirmedConsumable = buf;
                        _confirmedSink = buf;
                    }

                    _confirmedSink?.Add(frame, inputs);

                    if (_remoteDrivenInputSource is FrameJitterBuffer<PlayerInputCommand[]> jb)
                    {
                        AbilityKit.Game.Flow.BattleFlowDebugProvider.JitterBufferStats = new AbilityKit.Game.Flow.JitterBufferStatsSnapshot
                        {
                            DelayFrames = jb.DelayFrames,
                            MissingMode = jb.MissingMode.ToString(),
                            TargetFrame = jb.TargetFrame,
                            MaxReceivedFrame = jb.MaxReceivedFrame,
                            LastConsumedFrame = jb.LastConsumedFrame,
                            BufferedCount = jb.Count,
                            MinBufferedFrame = jb.MinBufferedFrame,

                            AddedCount = jb.AddedCount,
                            DuplicateCount = jb.DuplicateCount,
                            LateCount = jb.LateCount,
                            ConsumedCount = jb.ConsumedCount,
                            FilledDefaultCount = jb.FilledDefaultCount,
                        };
                    }

                    packet = new FramePacket(worldId, new FrameIndex(frame), packet.Inputs, default);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
            }

            _lastFrame = packet.Frame.Value;

            if (!_firstFrameReceived)
            {
                _firstFrameReceived = true;
                FirstFrameReceived?.Invoke();
            }

            if (_ctx != null)
            {
                _ctx.LastFrame = _lastFrame;

                if (_replay != null)
                {
                    if (_ctx.EntityNode.IsValid && _ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent hs) && hs != null)
                    {
                        if (!_replay.TryValidateStateHashOnce(hs.Frame, hs.Version, hs.Hash, out var expected))
                        {
                            Log.Error($"[BattleReplay] State hash mismatch at frame={hs.Frame}, expected(version={expected.Version}, hash={expected.Hash}), actual(version={hs.Version}, hash={hs.Hash})");
                            _replay.Pause();
                        }
                    }
                }

                if (_plan.EnableInputRecording && _ctx.InputRecordWriter != null)
                {
                    if (packet.Snapshot.HasValue)
                    {
                        var s = packet.Snapshot.Value;
                        _ctx.InputRecordWriter.AppendSnapshot(_lastFrame, s.OpCode, s.Payload);
                    }

                    var interval = StateHashRecordIntervalFrames;
                    if (interval <= 0) interval = 10;

                    if ((_lastFrame % interval) == 0)
                    {
                        if (_ctx.EntityNode.IsValid && _ctx.EntityNode.TryGetComponent(out BattleStateHashSnapshotComponent h) && h != null)
                        {
                            _ctx.InputRecordWriter.AppendStateHash(h.Frame, h.Version, h.Hash);
                        }
                    }
                }
            }
        }

#if UNITY_EDITOR
        private void TryInstallEditorPlayModeStopHook()
        {
            if (_editorPlayModeHookActive) return;

            if (!_editorPlayModeHookInstalled)
            {
                UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
                _editorPlayModeHookInstalled = true;
            }

            _editorPlayModeHookActive = true;
        }

        private void TryUninstallEditorPlayModeStopHook()
        {
            _editorPlayModeHookActive = false;
        }

        private void OnEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (!_editorPlayModeHookActive) return;

            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                try
                {
                    StopGatewayRoomPreparation();
                    StopSession();
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, "[BattleSessionFeature] Stop on play mode exit failed");
                }
            }
        }
#endif
    }
}
