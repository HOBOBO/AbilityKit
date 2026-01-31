using System;
using System.Threading.Tasks;
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

namespace AbilityKit.Game.Flow
{
    public sealed class BattleSessionFeature : IGamePhaseFeature
    {
        private const float FixedDelta = 1f / 30f;
        private const int StateHashRecordIntervalFrames = 10;
        private const int ReplaySeekChunkFrames = 300;
        private const int RollbackSeekProbeFrames = 120;

        private readonly IBattleBootstrapper _bootstrapper;

        private BattleLogicSession _session;
        private BattleStartPlan _plan;

        private BattleContext _ctx;

        private FrameSnapshotDispatcher _snapshots;
        private BattleSnapshotPipeline _pipeline;
        private BattleCmdHandler _cmdHandler;

        private LockstepReplayDriver _replay;

        private int _lastFrame;
        private float _tickAcc;

        private bool _firstFrameReceived;

        private ConnectionManager _gatewayRoomConn;
        private GatewayRoomClient _gatewayRoomClient;
        private Task _gatewayRoomTask;

        private bool _tickEnteredLogged;
        private bool _autoPlanLogged;

        public event Action SessionStarted;
        public event Action FirstFrameReceived;
        public event Action<Exception> SessionFailed;

        public BattleSessionFeature(IBattleBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper;
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

            _gatewayRoomConn = new ConnectionManager(() => new TcpTransport(), connOptions);
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

            Debug.Log($"[BattleSessionFeature] GatewayRoom connected: {_plan.GatewayHost}:{_plan.GatewayPort}");

            const uint GuestLoginOpCode = 100;
            var sessionToken = _plan.GatewaySessionToken;
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                Debug.Log("[BattleSessionFeature] GatewayRoom GuestLogin...");
                sessionToken = await _gatewayRoomClient.GuestLoginAsync(GuestLoginOpCode);
                if (string.IsNullOrWhiteSpace(sessionToken))
                {
                    throw new InvalidOperationException("Gateway guest login failed: sessionToken is empty.");
                }

                Debug.Log("[BattleSessionFeature] GatewayRoom GuestLogin ok.");

                _plan = new BattleStartPlan(
                    worldId: _plan.WorldId,
                    worldType: _plan.WorldType,
                    clientId: _plan.ClientId,
                    playerId: _plan.PlayerId,
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
                Debug.Log("[BattleSessionFeature] GatewayRoom CreateRoom...");
                var result = await _gatewayRoomClient.CreateRoomAsync(
                    sessionToken: _plan.GatewaySessionToken,
                    region: _plan.GatewayRegion,
                    serverId: _plan.GatewayServerId,
                    roomType: string.IsNullOrEmpty(_plan.WorldType) ? "battle" : _plan.WorldType,
                    title: string.Empty,
                    isPublic: true,
                    maxPlayers: 10,
                    tags: null);

                Debug.Log($"[BattleSessionFeature] GatewayRoom CreateRoom ok. roomId='{result.RoomId}' numericRoomId={result.NumericRoomId}");

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

                Debug.Log($"[BattleSessionFeature] GatewayRoom JoinRoom ok. numericRoomId={_plan.NumericRoomId}");
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

                Debug.Log($"[BattleSessionFeature] GatewayRoom JoinRoom... roomId='{joinRoomId}'");
                var result = await _gatewayRoomClient.JoinRoomAsync(
                    sessionToken: _plan.GatewaySessionToken,
                    region: _plan.GatewayRegion,
                    serverId: _plan.GatewayServerId,
                    roomId: joinRoomId);

                Debug.Log($"[BattleSessionFeature] GatewayRoom JoinRoom ok. numericRoomId={result.NumericRoomId}");

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

            _plan = _bootstrapper?.Build() ?? default;

            Debug.Log($"[BattleSessionFeature] OnAttach Plan: HostMode={_plan.HostMode}, UseGatewayTransport={_plan.UseGatewayTransport}, Gateway={_plan.GatewayHost}:{_plan.GatewayPort}, NumericRoomId={_plan.NumericRoomId}, AutoConnect={_plan.AutoConnect}, AutoCreateWorld={_plan.AutoCreateWorld}, AutoJoin={_plan.AutoJoin}, AutoReady={_plan.AutoReady}, WorldId={_plan.WorldId}, PlayerId={_plan.PlayerId}");

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
            while (_tickAcc >= FixedDelta)
            {
                var nextFrame = _lastFrame + 1;
                _replay?.Pump(_session, nextFrame);
                _session.Tick(FixedDelta);
                _tickAcc -= FixedDelta;
            }
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

                    transport = new GatewayBattleLogicTransport(gatewayOptions);
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

            _snapshots = new FrameSnapshotDispatcher(_session);
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
                _session = null;
            }
        }

        private void ApplyAutoPlanActions()
        {
            if (!_autoPlanLogged)
            {
                _autoPlanLogged = true;
            }

            if (_plan.HostMode == BattleStartConfig.BattleHostMode.GatewayRemote && _plan.UseGatewayTransport)
            {
                Debug.Log("[BattleSessionFeature] GatewayRemote transport active. Skipping AutoCreateWorld/AutoJoin (not applicable). Use GatewayAutoCreateRoom/GatewayAutoJoinRoom for room lifecycle. AutoConnect/AutoReady are supported.");
                if (_plan.AutoConnect)
                {
                    Debug.Log($"[BattleSessionFeature] GatewayRemote AutoConnect -> Connect() to {_plan.GatewayHost}:{_plan.GatewayPort}");
                    _session?.Connect();
                }

                if (_plan.AutoReady)
                {
                    Debug.Log($"[BattleSessionFeature] GatewayRemote AutoReady -> SubmitInput(Ready). worldId='{_plan.WorldId}' playerId={_plan.PlayerId} frame={_lastFrame + 1}");
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

            // Fast path: seek forward by fast-forwarding within the same session.
            if (_session != null && _replay != null && targetFrame > _lastFrame)
            {
                _tickAcc = 0f;

                for (int f = _lastFrame + 1; f <= targetFrame; f++)
                {
                    _replay.Pump(_session, f);
                    _session.Tick(FixedDelta);
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
                    if (_session.RollbackModule.TryRollbackAndReplay(worldId, new FrameIndex(f), new FrameIndex(targetFrame), FixedDelta))
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
                _session.Tick(FixedDelta);
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
                            Debug.LogError($"[BattleReplay] State hash mismatch at frame={hs.Frame}, expected(version={expected.Version}, hash={expected.Hash}), actual(version={hs.Version}, hash={hs.Hash})");
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
    }
}
