using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Console.Battle.Context;
using AbilityKit.Demo.Moba.Console.Battle.ECS.Components;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Client;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Battle.Requests;
using BattleWorldId = AbilityKit.Ability.World.Abstractions.WorldId;

namespace AbilityKit.Demo.Moba.Console.Battle.Sync
{
    /// <summary>
    /// State-sync adapter on the shared two-connection client host
    /// (<see cref="GatewayBattleClientHost"/>: room control plane + battle data plane on its own
    /// NetworkTransport connection). This adapter keeps only demo-specific pieces: the full
    /// battle-start flow hooks (hero-pick/loading), the snapshot apply, and the one-shot-host
    /// rebuild reconnect policy.
    /// </summary>
    public sealed class StateSyncAdapter : IBattleSyncAdapter
    {
        private ConsoleBattleContext _context;
        private BattleStartConfig _config;
        private GatewayBattleClientHost _host;
        private bool _entering;
        private bool _enterRequested;
        private string _host_ = "localhost";
        private int _port = 4000;
        private bool _initialized;
        private bool _connected;
        private int _currentFrame;
        private double _logicTimeSeconds;
        private double _renderTimeSeconds;
        private int _localActorId;

        private string _roomId = string.Empty;
        private ulong _numericRoomId;
        private string _playerId = string.Empty;

        private readonly List<ActorStateSnapshot> _actorStates = new();
        private readonly Dictionary<int, ActorStateSnapshot> _latestActorStates = new();
        private readonly object _statesLock = new();

        private NetworkConfig _networkConfig = new();
        private readonly System.Diagnostics.Stopwatch _networkTickClock = new();

        public SyncMode Mode => SyncMode.SnapshotAuthority;
        public bool IsConnected => _connected && (_host?.RoomConnection.IsConnected ?? false);
        public int CurrentFrame => _currentFrame;
        public double LogicTimeSeconds => _logicTimeSeconds;
        public double RenderTimeSeconds => _renderTimeSeconds;
        public int LocalActorId => _localActorId;

        public event Action<bool> OnConnectionChanged;
        public event Action<int, double> OnFrameSync;
        public event Action<ActorStateSnapshot[]> OnActorStateSnapshot;

        public void Initialize(ConsoleBattleContext context, BattleStartConfig config)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _initialized = true;
            _connected = false;
            _currentFrame = 0;
            _logicTimeSeconds = 0;
            _renderTimeSeconds = 0;
            _networkConfig = config.Network ?? new NetworkConfig();
            _localActorId = config.Players?.Count > 0
                ? DeterministicHash.StringToActorId(_config.Players[0].PlayerId)
                : 1;

            Platform.Log.Sync($"[StateSync] Initialized - Mode: {Mode}, LocalActorId: {_localActorId}");
        }

        public void Connect(string host, int port, string roomId, string playerId)
        {
            if (!_initialized)
                throw new InvalidOperationException("StateSyncAdapter not initialized. Call Initialize first.");

            _roomId = roomId;
            _playerId = playerId;
            _host_ = host;
            _port = port;

            Platform.Log.Sync($"[StateSync] Connecting to {host}:{port}...");
            _networkTickClock.Restart();
            _ = EnterHostAsync();
        }

        public void Connect()
        {
            // Parameterless entry used by bootstrapper/flow when config.Network is set:
            // fall back to config identity when no explicit room/player was provided.
            Connect(
                _networkConfig.Host,
                _networkConfig.Port,
                string.IsNullOrEmpty(_roomId) ? _config.WorldId : _roomId,
                string.IsNullOrEmpty(_playerId) ? _config.PlayerId : _playerId);
        }

        /// <summary>
        /// One full entry: room connection + login + room flow (join-or-create → hero-pick → ready →
        /// loading → battle start) + battle data-plane attach, all via the shared host. The host is
        /// one-shot, so a connection loss rebuilds it (see <see cref="OnRoomConnectionLost"/>).
        /// </summary>
        private async Task EnterHostAsync()
        {
            if (_entering) { _enterRequested = true; return; }
            _entering = true;
            try
            {
                var previous = _host;
                _host = null;
                if (previous != null)
                {
                    previous.RoomConnection.Disconnected -= OnRoomConnectionLost;
                    previous.Dispose();
                }

                var launchSpec = new RoomGatewayLaunchSpec(
                    region: "dev",
                    serverId: "local",
                    roomType: "moba",
                    roomTitle: string.IsNullOrEmpty(_roomId) ? "console-battle" : _roomId,
                    maxPlayers: 4,
                    gameplayId: 1,
                    ruleSetId: 0,
                    configVersion: 0,
                    protocolVersion: 0,
                    worldType: "moba",
                    clientId: _playerId,
                    // SnapshotAuthority needs the state-sync battle template; without the tag the
                    // room boots the default frame-sync battle and no snapshots are ever pushed.
                    tags: new Dictionary<string, string>
                    {
                        ["mapId"] = "1",
                        ["gameplayId"] = "1",
                        ["minPlayers"] = "1",
                        ["tickRate"] = _config.TickRate.ToString(CultureInfo.InvariantCulture),
                        ["syncTemplateId"] = "state-sync-authority",
                    });

                var host = await GatewayBattleClientHost.EnterAsync(
                    _host_, _port, _playerId, launchSpec,
                    configureBattle: ConfigureBattle,
                    battleDispatcher: InlineDispatcher.Instance,
                    joinRoomId: string.IsNullOrEmpty(_roomId) ? null : _roomId,
                    joinFallbackToCreate: true,
                    waitForBattleStart: true,
                    afterJoinAndBeforeReady: PickHeroForLocalPlayerAsync,
                    afterReadyAndBeforeBattleStart: DriveLoadingAsync);

                host.RoomConnection.Disconnected += OnRoomConnectionLost;
                host.RoomConnection.ServerPushReceived += OnServerPush;
                host.Battle.StateSyncSnapshotPushed += OnBattleSnapshotPushed;

                _host = host;
                _roomId = host.Session.RoomId;
                _numericRoomId = host.Session.NumericRoomId;

                _connected = true;
                OnConnectionChanged?.Invoke(true);
                Platform.Log.Sync($"[StateSync] Entered room: {_roomId} (battle={host.Session.BattleId})");
            }
            catch (Exception ex)
            {
                Platform.Log.Sync($"[StateSync] Connection error: {ex.Message}");
                OnConnectionChanged?.Invoke(false);
            }
            finally
            {
                _entering = false;
            }
        }

        /// <summary>
        /// Fills the game-specific callbacks on the host-prepared battle config (gateway address,
        /// session identity, room-gateway protocol preset are already applied). Engine retry off;
        /// per-submit results are not consumed by this demo.
        /// </summary>
        private void ConfigureBattle(NetworkBattleConfig config, GatewaySessionResult session)
        {
            var playerId = (uint)Math.Max(1, _localActorId);
            config
                .UseRoomGatewayStateSyncInput(
                    session.BattleId,
                    playerIdToUInt: p => uint.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : playerId,
                    worldIdToUlong: w => ulong.TryParse(w.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : session.NumericRoomId)
                .WithSnapshotDeserializer(payload => WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload));
        }

        /// <summary>
        /// Facade hook (after join, before ready): pick the local player's hero with the full
        /// loadout from battle config — the server requires a complete loadout before it can start.
        /// </summary>
        private async Task PickHeroForLocalPlayerAsync(
            RoomGatewaySessionFlow flow, string sessionToken, string roomId, TimeSpan? timeout, CancellationToken cancellationToken)
        {
            PlayerConfig local = null;
            if (_config.Players != null)
            {
                foreach (var p in _config.Players)
                {
                    if (p != null && p.PlayerId == _config.PlayerId) { local = p; break; }
                }
                local ??= _config.Players.Count > 0 ? _config.Players[0] : null;
            }
            if (local == null || local.HeroId <= 0)
            {
                Platform.Log.Sync("[StateSync] No local player loadout in config; skipping hero pick");
                return;
            }

            var pick = await flow.ConfigureLoadoutAsync(new RoomGatewayPickHeroRequest(
                sessionToken,
                roomId,
                heroId: local.HeroId,
                teamId: local.TeamId,
                spawnPointId: 0,
                level: local.Level,
                attributeTemplateId: local.AttributeTemplateId,
                basicAttackSkillId: local.BasicAttackSkillId,
                skillIds: local.SkillIds), timeout, cancellationToken);
            Platform.Log.Sync(pick.Success
                ? $"[StateSync] Hero picked: {local.HeroId}"
                : $"[StateSync] Pick hero not applied: {pick.Message}");
        }

        /// <summary>
        /// Facade hook (after ready, before battle-start wait): drive the loading stage so the room
        /// commits a battle world. Only the owner can begin loading; a joiner reads the manifest
        /// from the room snapshot instead.
        /// </summary>
        private async Task DriveLoadingAsync(
            RoomGatewaySessionFlow flow, string sessionToken, string roomId, TimeSpan? timeout, CancellationToken cancellationToken)
        {
            RoomGatewaySnapshot loadingSnapshot = null;
            var begin = await flow.BeginLoadingAsync(
                new RoomGatewayBeginLoadingRequest(sessionToken, roomId, expectedRevision: null, Guid.NewGuid().ToString("N")),
                timeout, cancellationToken);
            if (begin.Success && begin.Applied && begin.Snapshot != null)
            {
                loadingSnapshot = begin.Snapshot;
                Platform.Log.Sync("[StateSync] Begin loading (owner)");
            }
            else
            {
                var snapshot = await flow.GetSnapshotAsync(sessionToken, roomId, timeout, cancellationToken);
                if (snapshot.Success && snapshot.Snapshot != null && snapshot.Snapshot.Phase >= RoomGatewaySessionPhase.Loading)
                {
                    loadingSnapshot = snapshot.Snapshot;
                    Platform.Log.Sync($"[StateSync] Loading already started (phase={snapshot.Snapshot.Phase})");
                }
                else
                {
                    Platform.Log.Sync($"[StateSync] Begin loading not applied ({begin.Message}); waiting for the owner to start");
                    return;
                }
            }

            var loaded = await flow.ReportAssetsLoadedAsync(
                new RoomGatewayReportAssetsLoadedRequest(
                    sessionToken,
                    roomId,
                    loadingSnapshot.LaunchGeneration,
                    loadingSnapshot.LaunchManifestVersion,
                    loadingSnapshot.LaunchManifestHash,
                    Guid.NewGuid().ToString("N")),
                timeout, cancellationToken);
            Platform.Log.Sync(loaded.Success
                ? "[StateSync] Assets loaded reported"
                : $"[StateSync] Report assets loaded not applied: {loaded.Message}");
        }

        private void OnBattleSnapshotPushed(object snapshot)
        {
            if (snapshot is WireStateSyncSnapshotPush push)
            {
                HandleSnapshotPushed(in push);
            }
        }

        private void OnRoomConnectionLost()
        {
            _connected = false;
            OnConnectionChanged?.Invoke(false);
            // The host is one-shot; rebuild it (fresh login + flow + battle attach) on the next Tick
            // rather than on this event thread.
            _enterRequested = true;
            Platform.Log.Sync("[StateSync] Disconnected from server");
        }

        private void OnGatewayError(Exception ex)
        {
            Platform.Log.Sync($"[StateSync] Server error: {ex.Message}");
        }

        private void OnServerPush(uint opCode, ArraySegment<byte> payload)
        {
            // Snapshot pushes arrive on the battle data plane (typed StateSyncSnapshotPushed event);
            // the room connection only carries control-plane pushes.
            Platform.Log.Sync($"[StateSync] Push OpCode {opCode} ({payload.Count} bytes)");
        }

        private void HandleSnapshotPushed(in WireStateSyncSnapshotPush snapshot)
        {
            try
            {
                lock (_statesLock)
                {
                    _currentFrame = snapshot.Frame;

                    if (snapshot.Actors != null)
                    {
                        foreach (var actor in snapshot.Actors)
                        {
                            var state = new ActorStateSnapshot
                            {
                                ActorId = actor.ActorId,
                                X = actor.X,
                                Y = actor.Y,
                                Z = actor.Z,
                                Rotation = actor.Rotation,
                                VelocityX = actor.VelocityX,
                                VelocityZ = actor.VelocityZ,
                                Hp = actor.Hp,
                                HpMax = actor.HpMax,
                                TeamId = actor.TeamId
                            };
                            _latestActorStates[state.ActorId] = state;
                        }

                        _actorStates.Clear();
                        foreach (var kvp in _latestActorStates)
                        {
                            _actorStates.Add(kvp.Value);
                        }

                        OnActorStateSnapshot?.Invoke(_actorStates.ToArray());
                    }

                    OnFrameSync?.Invoke(_currentFrame, _logicTimeSeconds);
                    Platform.Log.Sync($"[StateSync] Snapshot - Frame:{snapshot.Frame}, Actors:{snapshot.Actors?.Count ?? 0}");
                }
            }
            catch (Exception ex)
            {
                Platform.Log.Sync($"[StateSync] Error handling snapshot: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            var host = _host;
            _host = null;
            if (host != null)
            {
                host.RoomConnection.Disconnected -= OnRoomConnectionLost;
                host.Dispose();
            }

            _connected = false;
            OnConnectionChanged?.Invoke(false);
            Platform.Log.Sync("[StateSync] Disconnected");
        }

        public void SubmitInput(PlayerInput input)
        {
            var battle = _host?.Battle;
            if (!_connected || battle == null) return;

            // Engine handles request/response + retry; per-submit results are not consumed by this demo.
            battle.SendInput(new SubmitInputRequest(
                new BattleWorldId(_numericRoomId.ToString(CultureInfo.InvariantCulture)),
                new PlayerInputCommand(
                    new FrameIndex(_currentFrame),
                    new PlayerId(((uint)Math.Max(1, _localActorId)).ToString(CultureInfo.InvariantCulture)),
                    (int)input.OpCode,
                    input.Payload ?? Array.Empty<byte>())));
        }

        public void Tick(float deltaTime)
        {
            if (!_initialized) return;

            if (_enterRequested && !_entering)
            {
                _enterRequested = false;
                _ = EnterHostAsync();
            }

            // The console battle can fast-forward (game loop spins faster than wall-clock), so the
            // SDK Tick must be fed real elapsed time — heartbeat/reconnect liveness timers run on
            // the supplied delta, and fake deltas would trip the heartbeat timeout within ~150ms.
            var realDelta = (float)_networkTickClock.Elapsed.TotalSeconds;
            _networkTickClock.Restart();
            _host?.Tick(realDelta);

            _renderTimeSeconds = _logicTimeSeconds - (1.0 / _config.TickRate);
            _logicTimeSeconds += deltaTime;
        }

        public ActorStateSnapshot[] GetAllActorStates()
        {
            lock (_statesLock)
            {
                _actorStates.Clear();
                foreach (var kvp in _latestActorStates)
                {
                    _actorStates.Add(kvp.Value);
                }
                return _actorStates.ToArray();
            }
        }

        public void Dispose() => Disconnect();
    }
}
