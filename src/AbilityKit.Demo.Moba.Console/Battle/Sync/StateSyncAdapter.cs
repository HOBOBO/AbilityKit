using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Console.Battle.Context;
using AbilityKit.Demo.Moba.Console.Battle.ECS.Components;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Demo.Moba.Console.Battle.Sync
{
    /// <summary>
    /// State-sync adapter using the unified AbilityKit network SDK (network.sdk + protocol.room).
    /// Replaces the legacy TcpNetworkClient/NetworkProtocol/NetworkOpCodes with NetworkSdkClient +
    /// WireRoomGatewayBinary + RoomGatewayOpCodes. Same IBattleSyncAdapter interface.
    /// </summary>
    public sealed class StateSyncAdapter : IBattleSyncAdapter
    {
        private ConsoleBattleContext _context;
        private BattleStartConfig _config;
        private NetworkSdkClient? _sdkClient;
        private bool _initialized;
        private bool _connected;
        private int _currentFrame;
        private double _logicTimeSeconds;
        private double _renderTimeSeconds;
        private int _localActorId;

        private string _roomId = string.Empty;
        private string _sessionToken = string.Empty;
        private ulong _numericRoomId;
        private string _playerId = string.Empty;

        private readonly List<ActorStateSnapshot> _actorStates = new();
        private readonly Dictionary<int, ActorStateSnapshot> _latestActorStates = new();
        private readonly object _statesLock = new();

        private NetworkConfig _networkConfig = new();

        public SyncMode Mode => SyncMode.SnapshotAuthority;
        public bool IsConnected => _connected && (_sdkClient?.IsConnected ?? false);
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

            _sdkClient = new NetworkSdkBuilder()
                .UseTransportFactory(() => new TcpTransport())
                .Build();
            _sdkClient.Connected += OnGatewayConnected;
            _sdkClient.Disconnected += OnGatewayDisconnected;
            _sdkClient.ServerPushReceived += OnServerPush;
            _sdkClient.Error += OnGatewayError;

            Platform.Log.Sync($"[StateSync] Connecting to {host}:{port}...");
            _sdkClient.Open(host, port);
        }

        public void Connect()
        {
            Connect(_networkConfig.Host, _networkConfig.Port, _roomId, _playerId);
        }

        private async void OnGatewayConnected()
        {
            Platform.Log.Sync("[StateSync] Connected to server, logging in...");

            try
            {
                // 1. Guest Login (gateway protocol: opCode 100)
                var loginPayload = WireRoomGatewayBinary.Serialize(
                    new WireRoomGuestLoginReq { GuestId = _playerId });
                var loginRespBytes = await _sdkClient!.SendRawRequestAsync(
                    RoomGatewayOpCodes.GuestLogin, loginPayload);
                var loginResult = WireRoomGatewayBinary.Deserialize<WireRoomGuestLoginRes>(loginRespBytes);
                if (!loginResult.Success)
                {
                    Platform.Log.Sync($"[StateSync] Login failed: {loginResult.Message}");
                    return;
                }
                _sessionToken = loginResult.SessionToken;
                _playerId = loginResult.SessionToken;
                Platform.Log.Sync($"[StateSync] Logged in: {_sessionToken}");

                // 2. Create or Join Room (gateway protocol: opCode 101/102)
                bool roomJoined = false;
                if (!string.IsNullOrEmpty(_roomId))
                {
                    var joinPayload = WireRoomGatewayBinary.Serialize(
                        new WireJoinRoomReq
                        {
                            SessionToken = _sessionToken,
                            Region = "dev",
                            ServerId = "local",
                            RoomId = _roomId
                        });
                    var joinRespBytes = await _sdkClient.SendRawRequestAsync(
                        RoomGatewayOpCodes.JoinRoom, joinPayload);
                    var joinResult = WireRoomGatewayBinary.Deserialize<WireJoinRoomRes>(joinRespBytes);
                    if (joinResult.Success)
                    {
                        _roomId = joinResult.RoomId;
                        Platform.Log.Sync($"[StateSync] Joined room: {_roomId}");
                        roomJoined = true;
                    }
                }

                if (!roomJoined)
                {
                    var createPayload = WireRoomGatewayBinary.Serialize(
                        new WireCreateRoomReq
                        {
                            SessionToken = _sessionToken,
                            Region = "dev",
                            ServerId = "local",
                            RoomType = "moba",
                            Title = _roomId,
                            IsPublic = true,
                            MaxPlayers = 4
                        });
                    var createRespBytes = await _sdkClient.SendRawRequestAsync(
                        RoomGatewayOpCodes.CreateRoom, createPayload);
                    var createResult = WireRoomGatewayBinary.Deserialize<WireCreateRoomRes>(createRespBytes);
                    if (createResult.Success)
                    {
                        _roomId = createResult.RoomId;
                        Platform.Log.Sync($"[StateSync] Created room: {_roomId}");
                        roomJoined = true;
                    }
                }

                if (!roomJoined)
                {
                    Platform.Log.Sync("[StateSync] Failed to create/join room");
                    return;
                }

                // 3. Subscribe State Sync (gateway protocol: opCode 103 — required before pushes)
                var subPayload = WireRoomGatewayBinary.Serialize(
                    new WireSubscribeStateSyncReq
                    {
                        SessionToken = _sessionToken,
                        BattleId = _roomId,
                        RoomId = _roomId
                    });
                _ = await _sdkClient.SendRawRequestAsync(
                    RoomGatewayOpCodes.SubscribeStateSync, subPayload);
                Platform.Log.Sync("[StateSync] Subscribed to state sync");

                _connected = true;
                OnConnectionChanged?.Invoke(true);
            }
            catch (Exception ex)
            {
                Platform.Log.Sync($"[StateSync] Connection error: {ex.Message}");
                OnConnectionChanged?.Invoke(false);
            }
        }

        private void OnGatewayDisconnected()
        {
            _connected = false;
            OnConnectionChanged?.Invoke(false);
            Platform.Log.Sync("[StateSync] Disconnected from server");
        }

        private void OnGatewayError(Exception ex)
        {
            Platform.Log.Sync($"[StateSync] Server error: {ex.Message}");
        }

        private void OnServerPush(uint opCode, ArraySegment<byte> payload)
        {
            switch (opCode)
            {
                case RoomGatewayOpCodes.SnapshotPushed:
                case RoomGatewayOpCodes.DeltaSnapshotPushed:
                    HandleSnapshotPushed(payload);
                    break;

                default:
                    Platform.Log.Sync($"[StateSync] Push OpCode {opCode} ({payload.Count} bytes)");
                    break;
            }
        }

        private void HandleSnapshotPushed(ArraySegment<byte> payload)
        {
            try
            {
                var snapshot = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(payload);

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
            _sdkClient?.Dispose();
            _sdkClient = null;

            _connected = false;
            OnConnectionChanged?.Invoke(false);
            Platform.Log.Sync("[StateSync] Disconnected");
        }

        public void SubmitInput(PlayerInput input)
        {
            if (!_connected || _sdkClient == null) return;

            var payload = WireRoomGatewayBinary.Serialize(
                new WireSubmitBattleInputReq
                {
                    SessionToken = _sessionToken,
                    BattleId = _roomId,
                    WorldId = _numericRoomId,
                    Frame = _currentFrame,
                    PlayerId = (uint)LocalActorId,
                    InputOpCode = (int)input.OpCode,
                    Payload = input.Payload ?? Array.Empty<byte>()
                });

            _ = _sdkClient.SendRawRequestAsync(RoomGatewayOpCodes.SubmitBattleInput, payload);
        }

        public void Tick(float deltaTime)
        {
            if (!_initialized) return;
            _sdkClient?.Tick(deltaTime);

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
