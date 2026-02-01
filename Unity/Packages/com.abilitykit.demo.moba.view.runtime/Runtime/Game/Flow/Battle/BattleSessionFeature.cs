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
using UnityEngine;
using System.Threading.Tasks;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Management;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Impl.Moba.Rollback;

namespace AbilityKit.Game.Flow
{
    public sealed class BattleSessionFeature : IGamePhaseFeature
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

        private AbilityKit.Network.Abstractions.IDispatcher _unityDispatcher;
        private AbilityKit.Network.Abstractions.DedicatedThreadDispatcher _networkIoDispatcher;

        private int _lastFrame;
        private float _tickAcc;

        private bool _firstFrameReceived;

        private ConnectionManager _gatewayRoomConn;
        private GatewayRoomClient _gatewayRoomClient;
        private Task _gatewayRoomTask;

        private bool _tickEnteredLogged;
        private bool _autoPlanLogged;

#if UNITY_EDITOR
        private static bool _editorPlayModeHookInstalled;
        private bool _editorPlayModeHookActive;
#endif

        public event Action SessionStarted;
        public event Action FirstFrameReceived;
        public event Action<Exception> SessionFailed;

        public BattleSessionFeature(IBattleBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper;
        }

        private static void AddByte(ref uint h, byte v)
        {
            h ^= v;
            h *= 16777619u;
        }

        private static void AddUInt(ref uint h, uint v)
        {
            AddByte(ref h, (byte)(v & 0xFF));
            AddByte(ref h, (byte)((v >> 8) & 0xFF));
            AddByte(ref h, (byte)((v >> 16) & 0xFF));
            AddByte(ref h, (byte)((v >> 24) & 0xFF));
        }

        private static void AddInt(ref uint h, int v)
        {
            unchecked
            {
                AddUInt(ref h, (uint)v);
            }
        }

        private static void AddFloat(ref uint h, float v)
        {
            var bits = BitConverter.SingleToInt32Bits(v);
            AddInt(ref h, bits);
        }

        private bool ShouldPrepareGatewayRoom()
        {
            if (_plan.HostMode != BattleStartConfig.BattleHostMode.GatewayRemote) return false;
            if (!_plan.UseGatewayTransport) return false;
            if (!_plan.GatewayAutoCreateRoom && !_plan.GatewayAutoJoinRoom) return false;
            return true;
        }

        private void StartGatewayRoomPreparation()
        {
            StopGatewayRoomPreparation();

            var connOptions = new ConnectionOptions
            {
                FrameCodec = LengthPrefixedFrameCodec.Instance,
                KickPushOpCode = 9000
            };

            _gatewayRoomConn = new ConnectionManager(() => new TcpTransport(), connOptions, _unityDispatcher, _networkIoDispatcher);
            _gatewayRoomConn.Open(_plan.GatewayHost, _plan.GatewayPort);

            var opCodes = new GatewayRoomOpCodes(_plan.GatewayCreateRoomOpCode, _plan.GatewayJoinRoomOpCode);
            _gatewayRoomClient = new GatewayRoomClient(_gatewayRoomConn, opCodes);

            _gatewayRoomTask = PrepareRoomAsync();
        }

        private async Task PrepareRoomAsync()
        {
            // Wait until connected.
            while (_gatewayRoomConn != null && _gatewayRoomConn.State == ConnectionState.Connecting)
            {
                await Task.Yield();
            }

            if (_gatewayRoomConn == null || _gatewayRoomConn.State != ConnectionState.Connected)
            {
                throw new InvalidOperationException($"Gateway room connection not connected. state={_gatewayRoomConn?.State}");
            }

            Log.Info($"[BattleSessionFeature] GatewayRoom connected: {_plan.GatewayHost}:{_plan.GatewayPort}");

            const uint GuestLoginOpCode = 100;
            var sessionToken = _plan.GatewaySessionToken;
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                Log.Info("[BattleSessionFeature] GatewayRoom GuestLogin...");
                sessionToken = await _gatewayRoomClient.GuestLoginAsync(GuestLoginOpCode);
                if (string.IsNullOrWhiteSpace(sessionToken))
                {
                    throw new InvalidOperationException("Gateway guest login failed: sessionToken is empty.");
                }

                Log.Info("[BattleSessionFeature] GatewayRoom GuestLogin ok.");

                _plan = new BattleStartPlan(
                    worldId: _plan.WorldId,
                    worldType: _plan.WorldType,
                    clientId: _plan.ClientId,
                    playerId: _plan.PlayerId,
                    tickRate: _plan.TickRate,
                    inputDelayFrames: _plan.InputDelayFrames,
                    hostMode: _plan.HostMode,
                    useGatewayTransport: _plan.UseGatewayTransport,
                    gatewayHost: _plan.GatewayHost,
                    gatewayPort: _plan.GatewayPort,
                    numericRoomId: _plan.NumericRoomId,
                    gatewaySessionToken: sessionToken,
                    gatewayRegion: _plan.GatewayRegion,
                    gatewayServerId: _plan.GatewayServerId,
                    gatewayAutoCreateRoom: _plan.GatewayAutoCreateRoom,
                    gatewayAutoJoinRoom: _plan.GatewayAutoJoinRoom,
                    gatewayJoinRoomId: _plan.GatewayJoinRoomId,
                    gatewayCreateRoomOpCode: _plan.GatewayCreateRoomOpCode,
                    gatewayJoinRoomOpCode: _plan.GatewayJoinRoomOpCode,
                    autoConnect: _plan.AutoConnect,
                    autoCreateWorld: _plan.AutoCreateWorld,
                    autoJoin: _plan.AutoJoin,
                    autoReady: _plan.AutoReady,
                    syncMode: _plan.SyncMode,
                    viewEventSourceMode: _plan.ViewEventSourceMode,
                    enableInputRecording: _plan.EnableInputRecording,
                    inputRecordOutputPath: _plan.InputRecordOutputPath,
                    enableInputReplay: _plan.EnableInputReplay,
                    inputReplayPath: _plan.InputReplayPath,
                    runMode: _plan.RunMode,
                    createWorldOpCode: _plan.CreateWorldOpCode,
                    createWorldPayload: _plan.CreateWorldPayload);
            }

            if (_plan.GatewayAutoCreateRoom)
            {
                Log.Info("[BattleSessionFeature] GatewayRoom CreateRoom...");
                var result = await _gatewayRoomClient.CreateRoomAsync(
                    sessionToken: _plan.GatewaySessionToken,
                    region: _plan.GatewayRegion,
                    serverId: _plan.GatewayServerId,
                    roomType: string.IsNullOrEmpty(_plan.WorldType) ? "battle" : _plan.WorldType,
                    title: string.Empty,
                    isPublic: true,
                    maxPlayers: 10,
                    tags: null);

                Log.Info($"[BattleSessionFeature] GatewayRoom CreateRoom ok. roomId='{result.RoomId}' numericRoomId={result.NumericRoomId}");

                if (result.NumericRoomId == 0)
                {
                    throw new InvalidOperationException($"Gateway CreateRoom returned invalid NumericRoomId. roomId='{result.RoomId}'");
                }

                var worldId = result.NumericRoomId.ToString();

                _plan = new BattleStartPlan(
                    worldId: worldId,
                    worldType: _plan.WorldType,
                    clientId: _plan.ClientId,
                    playerId: _plan.PlayerId,
                    tickRate: _plan.TickRate,
                    inputDelayFrames: _plan.InputDelayFrames,
                    hostMode: _plan.HostMode,
                    useGatewayTransport: _plan.UseGatewayTransport,
                    gatewayHost: _plan.GatewayHost,
                    gatewayPort: _plan.GatewayPort,
                    numericRoomId: result.NumericRoomId,
                    gatewaySessionToken: _plan.GatewaySessionToken,
                    gatewayRegion: _plan.GatewayRegion,
                    gatewayServerId: _plan.GatewayServerId,
                    gatewayAutoCreateRoom: _plan.GatewayAutoCreateRoom,
                    gatewayAutoJoinRoom: _plan.GatewayAutoJoinRoom,
                    gatewayJoinRoomId: _plan.GatewayJoinRoomId,
                    gatewayCreateRoomOpCode: _plan.GatewayCreateRoomOpCode,
                    gatewayJoinRoomOpCode: _plan.GatewayJoinRoomOpCode,
                    autoConnect: _plan.AutoConnect,
                    autoCreateWorld: _plan.AutoCreateWorld,
                    autoJoin: _plan.AutoJoin,
                    autoReady: _plan.AutoReady,
                    syncMode: _plan.SyncMode,
                    viewEventSourceMode: _plan.ViewEventSourceMode,
                    enableInputRecording: _plan.EnableInputRecording,
                    inputRecordOutputPath: _plan.InputRecordOutputPath,
                    enableInputReplay: _plan.EnableInputReplay,
                    inputReplayPath: _plan.InputReplayPath,
                    runMode: _plan.RunMode,
                    createWorldOpCode: _plan.CreateWorldOpCode,
                    createWorldPayload: _plan.CreateWorldPayload);

                await _gatewayRoomClient.JoinRoomAsync(
                    sessionToken: _plan.GatewaySessionToken,
                    region: _plan.GatewayRegion,
                    serverId: _plan.GatewayServerId,
                    roomId: string.IsNullOrWhiteSpace(result.RoomId) ? _plan.NumericRoomId.ToString() : result.RoomId);

                Log.Info($"[BattleSessionFeature] GatewayRoom JoinRoom ok. numericRoomId={_plan.NumericRoomId}");
                return;
            }

            if (_plan.GatewayAutoJoinRoom)
            {
                var joinRoomId = _plan.GatewayJoinRoomId;
                if (string.IsNullOrWhiteSpace(joinRoomId))
                {
                    joinRoomId = _plan.NumericRoomId != 0 ? _plan.NumericRoomId.ToString() : _plan.WorldId;
                }
                if (string.IsNullOrWhiteSpace(joinRoomId))
                {
                    throw new InvalidOperationException("GatewayAutoJoinRoom requires JoinRoomId or WorldId.");
                }

                Log.Info($"[BattleSessionFeature] GatewayRoom JoinRoom... roomId='{joinRoomId}'");
                var result = await _gatewayRoomClient.JoinRoomAsync(
                    sessionToken: _plan.GatewaySessionToken,
                    region: _plan.GatewayRegion,
                    serverId: _plan.GatewayServerId,
                    roomId: joinRoomId);

                Log.Info($"[BattleSessionFeature] GatewayRoom JoinRoom ok. numericRoomId={result.NumericRoomId}");

                if (result.NumericRoomId == 0)
                {
                    throw new InvalidOperationException($"Gateway JoinRoom returned invalid NumericRoomId. roomId='{joinRoomId}'");
                }

                var worldId = result.NumericRoomId.ToString();

                _plan = new BattleStartPlan(
                    worldId: worldId,
                    worldType: _plan.WorldType,
                    clientId: _plan.ClientId,
                    playerId: _plan.PlayerId,
                    tickRate: _plan.TickRate,
                    inputDelayFrames: _plan.InputDelayFrames,
                    hostMode: _plan.HostMode,
                    useGatewayTransport: _plan.UseGatewayTransport,
                    gatewayHost: _plan.GatewayHost,
                    gatewayPort: _plan.GatewayPort,
                    numericRoomId: result.NumericRoomId,
                    gatewaySessionToken: _plan.GatewaySessionToken,
                    gatewayRegion: _plan.GatewayRegion,
                    gatewayServerId: _plan.GatewayServerId,
                    gatewayAutoCreateRoom: _plan.GatewayAutoCreateRoom,
                    gatewayAutoJoinRoom: _plan.GatewayAutoJoinRoom,
                    gatewayJoinRoomId: _plan.GatewayJoinRoomId,
                    gatewayCreateRoomOpCode: _plan.GatewayCreateRoomOpCode,
                    gatewayJoinRoomOpCode: _plan.GatewayJoinRoomOpCode,
                    autoConnect: _plan.AutoConnect,
                    autoCreateWorld: _plan.AutoCreateWorld,
                    autoJoin: _plan.AutoJoin,
                    autoReady: _plan.AutoReady,
                    syncMode: _plan.SyncMode,
                    viewEventSourceMode: _plan.ViewEventSourceMode,
                    enableInputRecording: _plan.EnableInputRecording,
                    inputRecordOutputPath: _plan.InputRecordOutputPath,
                    enableInputReplay: _plan.EnableInputReplay,
                    inputReplayPath: _plan.InputReplayPath,
                    runMode: _plan.RunMode,
                    createWorldOpCode: _plan.CreateWorldOpCode,
                    createWorldPayload: _plan.CreateWorldPayload);
                return;
            }
        }

        private void StopGatewayRoomPreparation()
        {
            _gatewayRoomTask = null;
            _gatewayRoomClient = null;

            if (_gatewayRoomConn != null)
            {
                _gatewayRoomConn.Dispose();
                _gatewayRoomConn = null;
            }
        }

        public BattleLogicSession Session => _session;
        public int LastFrame => _lastFrame;
        public BattleStartPlan Plan => _plan;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetComponent(out _ctx);

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

        private void TickRemoteDrivenLocalSim(float deltaTime)
        {
            if (_remoteDrivenWorld == null || _remoteDrivenRuntime == null) return;
            if (_remoteDrivenInputSource == null) return;

            _remoteDrivenInputSource.DelayFrames = _plan.InputDelayFrames < 0 ? 0 : _plan.InputDelayFrames;

            var targetFrame = _remoteDrivenInputSource.TargetFrame;
            if (targetFrame <= 0) return;

            var fixedDelta = GetFixedDeltaSeconds();
            var stepsBudget = MaxRemoteDrivenCatchUpStepsPerUpdate;
            if (stepsBudget <= 0) return;

            var worldId = _remoteDrivenWorld.Id;
            AbilityKit.Ability.Host.IWorldStateSnapshotProvider provider = null;

            try
            {
                if (_remoteDrivenWorld.Services != null)
                {
                    _remoteDrivenWorld.Services.TryResolve(out provider);
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                provider = null;
            }

            var steps = 0;
            while (steps < stepsBudget && _remoteDrivenLastTickedFrame < targetFrame)
            {
                var nextFrame = _remoteDrivenLastTickedFrame + 1;
                var frameIndex = new FrameIndex(nextFrame);

                _remoteDrivenRuntime.Tick(fixedDelta);

                if (provider != null)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        if (!provider.TryGetSnapshot(frameIndex, out var s))
                        {
                            break;
                        }

                        var synthesized = new FramePacket(worldId, frameIndex, Array.Empty<PlayerInputCommand>(), s);
                        _snapshots?.Feed(synthesized);
                    }
                }

                _remoteDrivenLastTickedFrame = nextFrame;
                steps++;
            }

            _remoteDrivenInputSource.TrimBefore(_remoteDrivenLastTickedFrame - 120);
        }

        private void StartSession()
        {
            StopSession();

            var syncMode = _plan.SyncMode;

            var runMode = _plan.RunMode;
            if (_plan.EnableInputReplay) runMode = BattleStartConfig.BattleRunMode.Replay;
            else if (_plan.EnableInputRecording) runMode = BattleStartConfig.BattleRunMode.Record;

            var logicMode = syncMode == BattleSyncMode.SnapshotAuthority ? BattleLogicMode.Remote : BattleLogicMode.Local;
            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote) logicMode = BattleLogicMode.Remote;

            var opts = new BattleLogicSessionOptions
            {
                Mode = logicMode,
                WorldId = new WorldId(string.IsNullOrEmpty(_plan.WorldId) ? "room_1" : _plan.WorldId),
                WorldType = string.IsNullOrEmpty(_plan.WorldType) ? "battle" : _plan.WorldType,
                ClientId = string.IsNullOrEmpty(_plan.ClientId) ? "battle_client" : _plan.ClientId,
                PlayerId = string.IsNullOrEmpty(_plan.PlayerId) ? "p1" : _plan.PlayerId,
                AutoConnect = false,
                AutoCreateWorld = false,
                AutoJoin = false,
                ScanAssemblies = new[]
                {
                    typeof(WorldServiceContainerFactory).Assembly,
                    typeof(BattleLogicSession).Assembly,
                    typeof(AbilityKit.Ability.Impl.Moba.Systems.MobaWorldBootstrapModule).Assembly,
                    typeof(BattleSessionFeature).Assembly
                },
                NamespacePrefixes = new[] { "AbilityKit" }
            };

            if (runMode == BattleStartConfig.BattleRunMode.Replay)
            {
                opts.EnableRollback = true;
                opts.RollbackHistoryFrames = 1200;
                opts.RollbackCaptureEveryNFrames = 30;
            }

            if (logicMode == BattleLogicMode.Remote)
            {
                IBattleLogicTransport transport;

                if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)
                {
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
                }
                else
                {
                    transport = new NullBattleLogicTransport();
                }

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
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }
                finally
                {
                    _remoteDrivenWorld = null;
                    _remoteDrivenRuntime = null;
                    _remoteDrivenWorlds = null;
                    _remoteDrivenLastTickedFrame = 0;
                    _remoteDrivenInputSource?.Dispose();
                    _remoteDrivenInputSource = null;
                    _remoteDrivenConsumable = null;
                    _remoteDrivenSink = null;

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

        private void StartRemoteDrivenLocalWorld()
        {
            if (_remoteDrivenWorld != null) return;

            var typeRegistry = new WorldTypeRegistry()
                .RegisterEntitasWorld(AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaLobbyWorldBlueprint.Type)
                .RegisterEntitasWorld(AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaBattleWorldBlueprint.Type);

            var blueprints = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintRegistry();
            AbilityKit.Ability.Impl.Moba.Worlds.Blueprints.MobaWorldBlueprintsRegistration.RegisterAll(blueprints);

            var baseFactory = new AbilityKit.Ability.World.Management.RegistryWorldFactory(typeRegistry);
            var factory = new AbilityKit.Ability.Host.WorldBlueprints.WorldBlueprintWorldFactory(baseFactory, blueprints);
            _remoteDrivenWorlds = new AbilityKit.Ability.World.Management.WorldManager(factory);

            var serverOptions = new AbilityKit.Ability.Host.Framework.HostRuntimeOptions();
            _remoteDrivenRuntime = new AbilityKit.Ability.Host.Framework.HostRuntime(_remoteDrivenWorlds, serverOptions);

            var fixedDelta = GetFixedDeltaSeconds();

            var modules = new AbilityKit.Ability.Host.Framework.HostRuntimeModuleHost()
                .Add(new AbilityKit.Ability.Host.Extensions.FrameSync.ClientPredictionDriverModule(
                    resolveRemoteInputs: _ => _remoteDrivenConsumable,
                    resolveLocalInputs: _ => _ctx != null ? _ctx.LocalInputQueue : null,
                    inputDelayFrames: _plan.InputDelayFrames < 0 ? 0 : _plan.InputDelayFrames,
                    enableRollback: true,
                    rollbackHistoryFrames: 240,
                    rollbackCaptureEveryNFrames: 1,
                    buildRollbackRegistry: world =>
                    {
                        var reg = new AbilityKit.Ability.FrameSync.Rollback.RollbackRegistry();
                        if (world?.Services == null) return reg;

                        if (world.Services.TryResolve<MobaActorRegistry>(out var actorReg) && actorReg != null)
                        {
                            reg.Register(new AbilityKit.Ability.Share.Impl.Moba.Rollback.MobaActorTransformRollbackProvider(actorReg));
                        }

                        if (world.Services.TryResolve<AbilityKit.Ability.Share.Impl.Moba.Move.MobaMoveService>(out var move) && move != null)
                        {
                            reg.Register(new MobaMoveRollbackProvider(move));
                        }

                        return reg;
                    },
                    buildComputeHash: world =>
                    {
                        if (world?.Services == null) return null;

                        if (!world.Services.TryResolve<AbilityKit.Ability.Share.Impl.Moba.Services.MobaLobbyStateService>(out var lobby) || lobby == null)
                        {
                            return null;
                        }

                        if (!world.Services.TryResolve<AbilityKit.Ability.Share.Impl.Moba.Services.MobaActorRegistry>(out var registry) || registry == null)
                        {
                            return null;
                        }

                        return _ => new AbilityKit.Ability.FrameSync.Rollback.WorldStateHash(ComputeStateHash(lobby, registry));
                    }))
                .Add(new AbilityKit.Ability.Host.Extensions.Time.ServerFrameTimeModule(fixedDelta));
            modules.InstallAll(_remoteDrivenRuntime, serverOptions);

            if (_ctx != null)
            {
                if (_remoteDrivenRuntime.Features.TryGetFeature<AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionDriverStats>(out var stats) && stats != null)
                {
                    _ctx.PredictionStats = stats;
                }
                else
                {
                    _ctx.PredictionStats = null;
                }

                if (_remoteDrivenRuntime.Features.TryGetFeature<AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionReconcileTarget>(out var target) && target != null)
                {
                    _ctx.PredictionReconcileTarget = target;
                }
                else
                {
                    _ctx.PredictionReconcileTarget = null;
                }

                if (_remoteDrivenRuntime.Features.TryGetFeature<AbilityKit.Ability.Host.Extensions.FrameSync.IClientPredictionReconcileControl>(out var control) && control != null)
                {
                    _ctx.PredictionReconcileControl = control;
                }
                else
                {
                    _ctx.PredictionReconcileControl = null;
                }
            }

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

            var options = new WorldCreateOptions(new WorldId(_plan.WorldId), _plan.WorldType)
            {
                ServiceBuilder = builder,
            };
            options.SetEntitasContextsFactory(new MobaEntitasContextsFactory());

            _remoteDrivenWorld = _remoteDrivenRuntime.CreateWorld(options);

            try
            {
                if (_remoteDrivenWorld?.Services == null)
                {
                    Log.Error("[BattleSessionFeature] RemoteDrivenLocalWorld bootstrap failed: world.Services is null");
                }
                else
                {
                    var p = new PlayerId(_plan.PlayerId);

                    if (_remoteDrivenWorld.Services.TryResolve<AbilityKit.Ability.Share.Impl.Moba.Services.MobaLobbyStateService>(out var lobby) && lobby != null)
                    {
                        lobby.OnPlayerJoined(p);
                    }
                    else
                    {
                        Log.Error("[BattleSessionFeature] RemoteDrivenLocalWorld bootstrap failed: MobaLobbyStateService not found");
                    }

                    if (_remoteDrivenWorld.Services.TryResolve<AbilityKit.Ability.Host.IWorldInputSink>(out var sink) && sink != null)
                    {
                        var frame0 = new FrameIndex(0);
                        var ready = new PlayerInputCommand(frame0, p, (int)AbilityKit.Ability.Share.Impl.Moba.Services.MobaOpCode.Ready, Array.Empty<byte>());
                        sink.Submit(frame0, new[] { ready });
                    }
                    else
                    {
                        Log.Error("[BattleSessionFeature] RemoteDrivenLocalWorld bootstrap failed: IWorldInputSink not found");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "[BattleSessionFeature] RemoteDrivenLocalWorld bootstrap threw");
            }

            _remoteDrivenLastTickedFrame = 0;
            _remoteDrivenLastLoggedFrame = -1;
            _remoteDrivenFirstSnapshotLogged = false;
            _remoteDrivenFirstSpawnLogged = false;

            var delay = _plan.InputDelayFrames;
            if (delay < 0) delay = 0;
            var buf = new FrameJitterBuffer<PlayerInputCommand[]>(delayFrames: delay, missingMode: MissingFrameMode.FillDefault, missingFrameFactory: Array.Empty<PlayerInputCommand>, initialCapacity: 256);
            _remoteDrivenInputSource = buf;
            _remoteDrivenConsumable = buf;
            _remoteDrivenSink = buf;
        }

        private static uint ComputeStateHash(
            AbilityKit.Ability.Share.Impl.Moba.Services.MobaLobbyStateService lobby,
            AbilityKit.Ability.Share.Impl.Moba.Services.MobaActorRegistry registry)
        {
            var entries = new List<(int actorId, float x, float y, float z)>(16);
            foreach (var kv in registry.Entries)
            {
                var actorId = kv.Key;
                var e = kv.Value;
                if (e == null) continue;
                if (!e.hasTransform) continue;
                var p = e.transform.Value.Position;
                entries.Add((actorId, p.X, p.Y, p.Z));
            }

            entries.Sort((a, b) => a.actorId.CompareTo(b.actorId));

            uint h = 2166136261u;
            AddByte(ref h, lobby.Started ? (byte)1 : (byte)0);
            AddInt(ref h, entries.Count);

            for (int i = 0; i < entries.Count; i++)
            {
                var it = entries[i];
                AddInt(ref h, it.actorId);
                AddFloat(ref h, it.x);
                AddFloat(ref h, it.y);
                AddFloat(ref h, it.z);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugForceClientHashMismatch)
            {
                h ^= 1u;
            }
#endif

            return h;
        }

        private void ApplyAutoPlanActions()
        {
            if (!_autoPlanLogged)
            {
                _autoPlanLogged = true;
            }

            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)
            {
                Log.Info("[BattleSessionFeature] GatewayRemote transport active. Skipping AutoCreateWorld/AutoJoin (not applicable). Use GatewayAutoCreateRoom/GatewayAutoJoinRoom for room lifecycle. AutoConnect/AutoReady are supported.");
                if (_plan.AutoConnect)
                {
                    Log.Info($"[BattleSessionFeature] GatewayRemote AutoConnect -> Connect() to {_plan.GatewayHost}:{_plan.GatewayPort}");
                    _session?.Connect();
                }

                if (_plan.AutoReady)
                {
                    Log.Info($"[BattleSessionFeature] GatewayRemote AutoReady -> SubmitInput(Ready). worldId='{_plan.WorldId}' playerId={_plan.PlayerId} frame={_lastFrame + 1}");
                    var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_plan.PlayerId), opCode: (int)MobaOpCode.Ready, payload: Array.Empty<byte>());
                    _session?.SubmitInput(new SubmitInputRequest(new WorldId(_plan.WorldId), cmd));
                }
                return;
            }

            var isLocal = _plan.SyncMode != BattleSyncMode.SnapshotAuthority && _plan.HostMode == BattleStartConfig.BattleHostMode.Local;
            if (isLocal) _session?.Connect();
            else if (_plan.AutoConnect) _session?.Connect();

            if (_plan.AutoCreateWorld) CreateWorld();
            if (_plan.AutoJoin)
            {
                _session?.Join(new JoinWorldRequest(new WorldId(_plan.WorldId), new PlayerId(_plan.PlayerId)));
            }
            if (_plan.AutoReady)
            {
                var cmd = new PlayerInputCommand(new FrameIndex(_lastFrame + 1), new PlayerId(_plan.PlayerId), opCode: (int)MobaOpCode.Ready, payload: Array.Empty<byte>());
                _session?.SubmitInput(new SubmitInputRequest(new WorldId(_plan.WorldId), cmd));
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
            if (_remoteDrivenWorld != null)
            {
                try
                {
                    var frame = packet.Frame.Value;
                    var worldId = _remoteDrivenWorld.Id;

                    var inputCount = packet.Inputs != null ? packet.Inputs.Count : 0;
                    var logThisFrame = false;

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
                    }

                    _remoteDrivenSink?.Add(frame, inputs);

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
