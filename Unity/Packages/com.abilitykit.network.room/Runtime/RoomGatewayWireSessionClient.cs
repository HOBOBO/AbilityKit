#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Network.Room
{
    public interface IRoomGatewayRequestTransport
    {
        Task<ArraySegment<byte>> SendRequestAsync(
            uint opCode,
            ArraySegment<byte> payload,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default);
    }

    public interface IRoomGatewayPushSource
    {
        event Action<uint, ArraySegment<byte>>? ServerPushReceived;
    }

    public readonly struct RoomGatewayWireOpCodes
    {
        public static RoomGatewayWireOpCodes Default => new RoomGatewayWireOpCodes(
            RequestOpCode<WireCreateRoomReq>(),
            RequestOpCode<WireJoinRoomReq>(),
            RequestOpCode<WireLeaveRoomReq>(),
            RequestOpCode<WireRoomReadyReq>(),
            RequestOpCode<WireStartRoomBattleReq>(),
            RequestOpCode<WireSubscribeStateSyncReq>(),
            RequestOpCode<WireRestoreRoomReq>(),
            RequestOpCode<WireRoomPickHeroReq>(),
            RequestOpCode<WireBeginLoadingReq>(),
            RequestOpCode<WireReportLoadingProgressReq>(),
            RequestOpCode<WireReportAssetsLoadedReq>(),
            RequestOpCode<WireCancelLoadingReq>(),
            RequestOpCode<WireGetSnapshotReq>(),
            ProtocolMessageDescriptor<WireRoomStateChangedPush>.RequireOpCode(
                ProtocolDirection.ServerToClient));

        public readonly uint CreateRoom;
        public readonly uint JoinRoom;
        public readonly uint LeaveRoom;
        public readonly uint SetReady;
        public readonly uint StartBattle;
        public readonly uint SubscribeStateSync;
        public readonly uint RestoreRoom;
        public readonly uint PickHero;
        public readonly uint BeginLoading;
        public readonly uint ReportLoadingProgress;
        public readonly uint ReportAssetsLoaded;
        public readonly uint CancelLoading;
        public readonly uint GetSnapshot;
        public readonly uint RoomStateChanged;

        public RoomGatewayWireOpCodes(
            uint createRoom,
            uint joinRoom,
            uint leaveRoom,
            uint setReady,
            uint startBattle,
            uint subscribeStateSync,
            uint restoreRoom,
            uint pickHero,
            uint beginLoading,
            uint reportLoadingProgress,
            uint reportAssetsLoaded,
            uint cancelLoading,
            uint getSnapshot,
            uint roomStateChanged)
        {
            CreateRoom = createRoom;
            JoinRoom = joinRoom;
            LeaveRoom = leaveRoom;
            SetReady = setReady;
            StartBattle = startBattle;
            SubscribeStateSync = subscribeStateSync;
            RestoreRoom = restoreRoom;
            PickHero = pickHero;
            BeginLoading = beginLoading;
            ReportLoadingProgress = reportLoadingProgress;
            ReportAssetsLoaded = reportAssetsLoaded;
            CancelLoading = cancelLoading;
            GetSnapshot = getSnapshot;
            RoomStateChanged = roomStateChanged;
        }

        private static uint RequestOpCode<TRequest>()
        {
            return ProtocolMessageDescriptor<TRequest>.RequireOpCode(
                ProtocolDirection.ClientToServer);
        }
    }

    /// <summary>
    /// Default Room gateway client backed by the shared Room wire protocol.
    /// The caller owns the connection or injected transport.
    /// </summary>
    public sealed class RoomGatewayWireSessionClient : IRoomGatewaySessionClient, IRoomGatewaySnapshotFeed, IDisposable
    {
        private readonly IRoomGatewayRequestTransport _requestTransport;
        private readonly IRoomGatewayPushSource? _pushSource;
        private readonly IDisposable? _ownedRequestTransport;
        private readonly RoomGatewayWireOpCodes _opCodes;
        private readonly object _snapshotGate = new object();
        private RoomGatewaySnapshot? _current;
        private bool _disposed;

        public RoomGatewayWireSessionClient(IConnection connection)
            : this(connection, RoomGatewayWireOpCodes.Default)
        {
        }

        public RoomGatewayWireSessionClient(
            IConnection connection,
            RoomGatewayWireOpCodes opCodes)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));

            var transport = new ConnectionRequestTransport(connection);
            _requestTransport = transport;
            _pushSource = transport;
            _ownedRequestTransport = transport;
            _opCodes = opCodes;
            _pushSource.ServerPushReceived += OnServerPushReceived;
        }

        public RoomGatewayWireSessionClient(
            IRoomGatewayRequestTransport requestTransport,
            IRoomGatewayPushSource? pushSource = null,
            RoomGatewayWireOpCodes? opCodes = null)
            : this(requestTransport, pushSource, null, opCodes)
        {
        }

        internal RoomGatewayWireSessionClient(
            IRoomGatewayRequestTransport requestTransport,
            IRoomGatewayPushSource? pushSource,
            IDisposable? ownedRequestTransport,
            RoomGatewayWireOpCodes? opCodes)
        {
            _requestTransport = requestTransport
                ?? throw new ArgumentNullException(nameof(requestTransport));
            _pushSource = pushSource;
            _ownedRequestTransport = ownedRequestTransport;
            _opCodes = opCodes ?? RoomGatewayWireOpCodes.Default;
            if (_pushSource != null)
            {
                _pushSource.ServerPushReceived += OnServerPushReceived;
            }
        }

        public RoomGatewaySnapshot? Current
        {
            get
            {
                lock (_snapshotGate)
                {
                    return _current;
                }
            }
        }

        public event Action<RoomGatewaySnapshot>? SnapshotChanged;

        public async Task<RoomGatewayCreateResult> CreateRoomAsync(
            RoomGatewayCreateRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireCreateRoomReq
            {
                SessionToken = request.SessionToken,
                Region = request.Region,
                ServerId = request.ServerId,
                RoomType = request.RoomType,
                Title = request.Title,
                IsPublic = request.IsPublic,
                MaxPlayers = request.MaxPlayers,
                Tags = ToDictionary(request.Tags)
            };
            var wire = await SendAsync<WireCreateRoomReq, WireCreateRoomRes>(
                _opCodes.CreateRoom,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new RoomGatewayCreateResult(
                wire.Success,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                wire.Message ?? string.Empty);
        }

        public async Task<RoomGatewayJoinResult> JoinRoomAsync(
            RoomGatewayJoinRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireJoinRoomReq
            {
                SessionToken = request.SessionToken,
                Region = request.Region,
                ServerId = request.ServerId,
                RoomId = request.RoomId
            };
            var wire = await SendAsync<WireJoinRoomReq, WireJoinRoomRes>(
                _opCodes.JoinRoom,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wireSnapshot = wire.Snapshot;
            var snapshot = ToSnapshot(in wireSnapshot);
            if (wire.Success)
            {
                PublishSnapshot(snapshot);
            }

            var wireAnchor = wire.WorldStartAnchor;
            return new RoomGatewayJoinResult(
                wire.Success,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                ToWorldStartAnchor(in wireAnchor),
                wire.Message ?? string.Empty,
                snapshot.BattleId,
                snapshot.CanStart,
                ToEntryKind(wire.JoinKind),
                wire.ServerNowTicks,
                snapshot.WorldId,
                wire.CurrentPlayerId);
        }

        public async Task<RoomGatewayLeaveResult> LeaveRoomAsync(
            RoomGatewayLeaveRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireLeaveRoomReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                ExpectedRevision = request.ExpectedRevision,
                CommandId = request.CommandId
            };
            var wire = await SendOperationAsync(
                _opCodes.LeaveRoom,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var snapshot = PublishOperationSnapshot(in wire);
            return new RoomGatewayLeaveResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.Message ?? string.Empty,
                wire.RoomRevision,
                snapshot);
        }

        public async Task<RoomGatewayReadyResult> SetReadyAsync(
            RoomGatewayReadyRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireRoomReadyReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                Ready = request.Ready
            };
            var wire = await SendAsync<WireRoomReadyReq, WireRoomSnapshotRes>(
                _opCodes.SetReady,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wireSnapshot = wire.Snapshot;
            var snapshot = ToSnapshot(in wireSnapshot);
            if (wire.Success)
            {
                PublishSnapshot(snapshot);
            }

            return new RoomGatewayReadyResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                snapshot.BattleId,
                snapshot.CanStart,
                wire.Message ?? string.Empty);
        }

        public async Task<RoomGatewayStartBattleResult> StartBattleAsync(
            RoomGatewayStartBattleRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireStartRoomBattleReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                GameplayId = request.GameplayId,
                RuleSetId = request.RuleSetId,
                ConfigVersion = request.ConfigVersion,
                ProtocolVersion = request.ProtocolVersion,
                WorldType = request.WorldType,
                ClientId = request.ClientId,
                SyncTemplateId = request.SyncTemplateId,
                SyncModel = request.SyncModel,
                NetworkEnvironmentId = request.NetworkEnvironmentId,
                CarrierName = request.CarrierName,
                EnableAuthoritativeWorld = request.EnableAuthoritativeWorld,
                InterpolationEnabled = request.InterpolationEnabled,
                InputDelayFrames = request.InputDelayFrames
            };
            var wire = await SendAsync<WireStartRoomBattleReq, WireStartRoomBattleRes>(
                _opCodes.StartBattle,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wireAnchor = wire.WorldStartAnchor;
            return new RoomGatewayStartBattleResult(
                wire.Success,
                wire.BattleId ?? string.Empty,
                wire.WorldId,
                wire.Started,
                ToWorldStartAnchor(in wireAnchor),
                wire.ServerNowTicks,
                wire.Message ?? string.Empty,
                RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(wire.SyncCapabilities));
        }

        public async Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            RoomGatewayStateSyncSubscriptionRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireSubscribeStateSyncReq
            {
                SessionToken = request.SessionToken,
                BattleId = request.BattleId,
                RoomId = request.RoomId,
                EventEpoch = request.EventEpoch,
                LastEventAck = request.LastEventAck
            };
            var wire = await SendAsync<WireSubscribeStateSyncReq, WireSubscribeStateSyncRes>(
                _opCodes.SubscribeStateSync,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new RoomGatewayStateSyncSubscriptionResult(
                wire.Success,
                wire.Message ?? string.Empty);
        }

        public async Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(
            RoomGatewayRestoreRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireRestoreRoomReq
            {
                SessionToken = request.SessionToken,
                Region = request.Region,
                ServerId = request.ServerId
            };
            var wire = await SendAsync<WireRestoreRoomReq, WireRestoreRoomRes>(
                _opCodes.RestoreRoom,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wireSnapshot = wire.Snapshot;
            var snapshot = ToSnapshot(in wireSnapshot);
            if (wire.Success && wire.HasActiveRoom)
            {
                PublishSnapshot(snapshot);
            }

            var wireAnchor = wire.WorldStartAnchor;
            return new RoomGatewayRestoreRoomResult(
                wire.Success,
                wire.HasActiveRoom,
                wire.IsInBattle,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                ToWorldStartAnchor(in wireAnchor),
                wire.Message ?? string.Empty,
                snapshot.BattleId,
                snapshot.CanStart,
                ToEntryKind(wire.JoinKind),
                wire.ServerNowTicks,
                snapshot.WorldId,
                ToRestoreStatus(wire.Status),
                ToRestoreErrorCode(wire.ErrorCode),
                wire.CurrentPlayerId,
                wire.Success && wire.HasActiveRoom ? snapshot : null);
        }

        public async Task<RoomGatewayPickHeroResult> PickHeroAsync(
            RoomGatewayPickHeroRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireRoomPickHeroReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                HeroId = request.HeroId,
                TeamId = request.TeamId,
                SpawnPointId = request.SpawnPointId,
                Level = request.Level,
                AttributeTemplateId = request.AttributeTemplateId,
                BasicAttackSkillId = request.BasicAttackSkillId,
                SkillIds = ToList(request.SkillIds)
            };
            var wire = await SendAsync<WireRoomPickHeroReq, WireRoomSnapshotRes>(
                _opCodes.PickHero,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wireSnapshot = wire.Snapshot;
            var snapshot = ToSnapshot(in wireSnapshot);
            if (wire.Success)
            {
                PublishSnapshot(snapshot);
            }

            return new RoomGatewayPickHeroResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                snapshot,
                wire.Message ?? string.Empty);
        }

        public async Task<RoomGatewayBeginLoadingResult> BeginLoadingAsync(
            RoomGatewayBeginLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireBeginLoadingReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                ExpectedRevision = request.ExpectedRevision,
                CommandId = request.CommandId
            };
            var wire = await SendOperationAsync(
                _opCodes.BeginLoading,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new RoomGatewayBeginLoadingResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.Message ?? string.Empty,
                wire.RoomRevision,
                PublishOperationSnapshot(in wire));
        }

        public async Task<RoomGatewayReportLoadingProgressResult> ReportLoadingProgressAsync(
            RoomGatewayReportLoadingProgressRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireReportLoadingProgressReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                LaunchGeneration = request.LaunchGeneration,
                ManifestVersion = request.ManifestVersion,
                ManifestHash = request.ManifestHash,
                Progress = request.Progress
            };
            var wire = await SendOperationAsync(
                _opCodes.ReportLoadingProgress,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new RoomGatewayReportLoadingProgressResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.Message ?? string.Empty,
                wire.RoomRevision,
                PublishOperationSnapshot(in wire));
        }

        public async Task<RoomGatewayReportAssetsLoadedResult> ReportAssetsLoadedAsync(
            RoomGatewayReportAssetsLoadedRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireReportAssetsLoadedReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                LaunchGeneration = request.LaunchGeneration,
                ManifestVersion = request.ManifestVersion,
                ManifestHash = request.ManifestHash,
                CommandId = request.CommandId
            };
            var wire = await SendOperationAsync(
                _opCodes.ReportAssetsLoaded,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new RoomGatewayReportAssetsLoadedResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.Message ?? string.Empty,
                wire.RoomRevision,
                PublishOperationSnapshot(in wire));
        }

        public async Task<RoomGatewayCancelLoadingResult> CancelLoadingAsync(
            RoomGatewayCancelLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireCancelLoadingReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId,
                ExpectedRevision = request.ExpectedRevision,
                CommandId = request.CommandId
            };
            var wire = await SendOperationAsync(
                _opCodes.CancelLoading,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new RoomGatewayCancelLoadingResult(
                wire.Success,
                wire.Applied,
                wire.ErrorCode,
                wire.Message ?? string.Empty,
                wire.RoomRevision,
                PublishOperationSnapshot(in wire));
        }

        public async Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(
            RoomGatewayGetSnapshotRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var wireRequest = new WireGetSnapshotReq
            {
                SessionToken = request.SessionToken,
                RoomId = request.RoomId
            };
            var wire = await SendAsync<WireGetSnapshotReq, WireRoomSnapshotRes>(
                _opCodes.GetSnapshot,
                wireRequest,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var wireSnapshot = wire.Snapshot;
            var snapshot = ToSnapshot(in wireSnapshot);
            if (wire.Success)
            {
                PublishSnapshot(snapshot);
            }

            return new RoomGatewayGetSnapshotResult(
                wire.Success,
                wire.RoomId ?? string.Empty,
                wire.NumericRoomId,
                wire.Success ? snapshot : null,
                wire.Message ?? string.Empty,
                wire.ServerNowTicks);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_pushSource != null)
            {
                _pushSource.ServerPushReceived -= OnServerPushReceived;
            }
            _ownedRequestTransport?.Dispose();
            lock (_snapshotGate)
            {
                _current = null;
            }
            SnapshotChanged = null;
        }

        private async Task<TResponse> SendAsync<TRequest, TResponse>(
            uint opCode,
            TRequest request,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var payload = WireRoomGatewayBinary.Serialize(in request);
            var response = await _requestTransport.SendRequestAsync(
                opCode,
                payload,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return WireRoomGatewayBinary.Deserialize<TResponse>(response);
        }

        private Task<WireRoomOperationRes> SendOperationAsync<TRequest>(
            uint opCode,
            TRequest request,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            return SendAsync<TRequest, WireRoomOperationRes>(
                opCode,
                request,
                timeout,
                cancellationToken);
        }

        private sealed class ConnectionRequestTransport :
            IRoomGatewayRequestTransport,
            IRoomGatewayPushSource,
            IDisposable
        {
            private readonly IConnection _connection;
            private readonly RequestClient _requestClient;
            private bool _disposed;

            public ConnectionRequestTransport(IConnection connection)
            {
                _connection = connection;
                _requestClient = new RequestClient(connection);
                _connection.ServerPushReceived += ForwardServerPush;
            }

            public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

            public Task<ArraySegment<byte>> SendRequestAsync(
                uint opCode,
                ArraySegment<byte> payload,
                TimeSpan? timeout = null,
                CancellationToken cancellationToken = default)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ConnectionRequestTransport));
                }

                return _requestClient.SendRequestAsync(
                    opCode,
                    payload,
                    timeout,
                    cancellationToken);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _connection.ServerPushReceived -= ForwardServerPush;
                _requestClient.Dispose();
                ServerPushReceived = null;
            }

            private void ForwardServerPush(uint opCode, ArraySegment<byte> payload)
            {
                ServerPushReceived?.Invoke(opCode, payload);
            }
        }

        private void OnServerPushReceived(uint opCode, ArraySegment<byte> payload)
        {
            if (_disposed || opCode != _opCodes.RoomStateChanged)
            {
                return;
            }

            try
            {
                var push = WireRoomGatewayBinary.Deserialize<WireRoomStateChangedPush>(payload);
                var wireSnapshot = push.Snapshot;
                PublishSnapshot(ToSnapshot(in wireSnapshot));
            }
            catch
            {
                // Push payload validation belongs to the transport diagnostics path; malformed pushes do not break dispatch.
            }
        }

        private RoomGatewaySnapshot? PublishOperationSnapshot(in WireRoomOperationRes wire)
        {
            if (!wire.Success)
            {
                return null;
            }

            var wireSnapshot = wire.Snapshot;
            var snapshot = ToSnapshot(in wireSnapshot);
            PublishSnapshot(snapshot);
            return snapshot;
        }

        private void PublishSnapshot(RoomGatewaySnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(snapshot.RoomId))
            {
                return;
            }

            Action<RoomGatewaySnapshot>? changed;
            lock (_snapshotGate)
            {
                if (_disposed)
                {
                    return;
                }

                if (_current != null &&
                    string.Equals(_current.RoomId, snapshot.RoomId, StringComparison.Ordinal))
                {
                    if (snapshot.RoomRevision < _current.RoomRevision)
                    {
                        return;
                    }

                    var completesSyncCapabilities =
                        snapshot.RoomRevision == _current.RoomRevision &&
                        _current.SyncCapabilities == null &&
                        snapshot.SyncCapabilities != null;
                    if (snapshot.SyncCapabilities == null)
                    {
                        snapshot.SyncCapabilities = _current.SyncCapabilities;
                    }
                    if (snapshot.RoomRevision == _current.RoomRevision &&
                        !completesSyncCapabilities)
                    {
                        return;
                    }
                }

                _current = snapshot;
                changed = SnapshotChanged;
            }

            changed?.Invoke(snapshot);
        }

        private static RoomGatewaySnapshot ToSnapshot(in WireRoomSnapshot wire)
        {
            var members = wire.Members == null || wire.Members.Count == 0
                ? Array.Empty<string>()
                : wire.Members.ToArray();
            var sourcePlayers = wire.Players;
            var players = sourcePlayers == null || sourcePlayers.Count == 0
                ? Array.Empty<RoomGatewayPlayerSnapshot>()
                : new RoomGatewayPlayerSnapshot[sourcePlayers.Count];
            for (var i = 0; i < players.Length; i++)
            {
                var player = sourcePlayers![i];
                players[i] = new RoomGatewayPlayerSnapshot
                {
                    AccountId = player.AccountId ?? string.Empty,
                    PlayerId = player.PlayerId,
                    TeamId = player.TeamId,
                    Ready = player.Ready,
                    HeroId = player.HeroId,
                    SpawnPointId = player.SpawnPointId,
                    Level = player.Level,
                    AttributeTemplateId = player.AttributeTemplateId,
                    BasicAttackSkillId = player.BasicAttackSkillId,
                    SkillIds = player.SkillIds == null || player.SkillIds.Count == 0
                        ? Array.Empty<int>()
                        : player.SkillIds.ToArray(),
                    LobbyReady = player.LobbyReady,
                    AssetsLoaded = player.AssetsLoaded,
                    LoadingProgress = player.LoadingProgress,
                    IsOnline = player.IsOnline,
                    JoinOrdinal = player.JoinOrdinal,
                    LoadedManifestVersion = player.LoadedManifestVersion,
                    LoadedManifestHash = player.LoadedManifestHash ?? string.Empty,
                    LastSeenTicks = player.LastSeenTicks,
                    OfflineSinceTicks = player.OfflineSinceTicks
                };
            }

            var summary = wire.Summary;
            var wireAnchor = wire.WorldStartAnchor;
            return new RoomGatewaySnapshot
            {
                RoomId = summary.RoomId ?? string.Empty,
                OwnerAccountId = summary.OwnerAccountId ?? string.Empty,
                Phase = ToSessionPhase(wire.Phase),
                PhaseReason = wire.PhaseReason ?? string.Empty,
                LaunchGeneration = wire.LaunchGeneration,
                LoadingDeadlineUnixMs = wire.LoadingDeadlineUnixMs,
                LaunchManifestHash = wire.LaunchManifestHash ?? string.Empty,
                LaunchManifestVersion = wire.LaunchManifestVersion,
                LastStartFailureCode = wire.LastStartFailureCode ?? string.Empty,
                RoomRevision = wire.RoomRevision,
                LastEventSequence = wire.LastEventSequence,
                CanStart = wire.CanStart,
                BattleId = wire.BattleId ?? string.Empty,
                WorldId = wire.WorldId,
                SyncCapabilities = RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(wire.SyncCapabilities),
                Members = members,
                Players = players,
                WorldStartAnchor = ToWorldStartAnchor(in wireAnchor)
            };
        }

        private static RoomGatewaySessionPhase ToSessionPhase(int phase)
        {
            switch (phase)
            {
                case 1: return RoomGatewaySessionPhase.Loading;
                case 2: return RoomGatewaySessionPhase.Starting;
                case 3: return RoomGatewaySessionPhase.InBattle;
                case 4: return RoomGatewaySessionPhase.Closing;
                case 5: return RoomGatewaySessionPhase.Closed;
                case 6: return RoomGatewaySessionPhase.Expired;
                default: return RoomGatewaySessionPhase.Lobby;
            }
        }

        private static RoomGatewaySessionEntryKind ToEntryKind(WireRoomJoinKind kind)
        {
            switch (kind)
            {
                case WireRoomJoinKind.Reconnect: return RoomGatewaySessionEntryKind.Reconnect;
                case WireRoomJoinKind.LateJoin: return RoomGatewaySessionEntryKind.LateJoin;
                default: return RoomGatewaySessionEntryKind.TeamLobby;
            }
        }

        private static RoomGatewaySessionRestoreStatus ToRestoreStatus(WireRoomRestoreStatus status)
        {
            switch (status)
            {
                case WireRoomRestoreStatus.Restored: return RoomGatewaySessionRestoreStatus.Restored;
                case WireRoomRestoreStatus.NoActiveRoom: return RoomGatewaySessionRestoreStatus.NoActiveRoom;
                case WireRoomRestoreStatus.NotMember: return RoomGatewaySessionRestoreStatus.NotMember;
                case WireRoomRestoreStatus.RoomClosed: return RoomGatewaySessionRestoreStatus.RoomClosed;
                case WireRoomRestoreStatus.RoomExpired: return RoomGatewaySessionRestoreStatus.RoomExpired;
                case WireRoomRestoreStatus.InvalidSession: return RoomGatewaySessionRestoreStatus.InvalidSession;
                default: return RoomGatewaySessionRestoreStatus.Failed;
            }
        }

        private static RoomGatewaySessionRestoreErrorCode ToRestoreErrorCode(WireRoomRestoreErrorCode errorCode)
        {
            switch (errorCode)
            {
                case WireRoomRestoreErrorCode.None: return RoomGatewaySessionRestoreErrorCode.None;
                case WireRoomRestoreErrorCode.NoAccountRoomMapping: return RoomGatewaySessionRestoreErrorCode.NoAccountRoomMapping;
                case WireRoomRestoreErrorCode.AccountNotInRoom: return RoomGatewaySessionRestoreErrorCode.AccountNotInRoom;
                case WireRoomRestoreErrorCode.RoomClosed: return RoomGatewaySessionRestoreErrorCode.RoomClosed;
                case WireRoomRestoreErrorCode.RoomExpired: return RoomGatewaySessionRestoreErrorCode.RoomExpired;
                case WireRoomRestoreErrorCode.InvalidSession: return RoomGatewaySessionRestoreErrorCode.InvalidSession;
                default: return RoomGatewaySessionRestoreErrorCode.InternalError;
            }
        }

        private static RoomGatewayWorldStartAnchor ToWorldStartAnchor(in WireWorldStartAnchor wire)
        {
            return new RoomGatewayWorldStartAnchor(
                wire.StartServerTicks,
                wire.ServerTickFrequency,
                wire.StartFrame,
                wire.FixedDeltaSeconds);
        }

        private static Dictionary<string, string>? ToDictionary(IReadOnlyDictionary<string, string>? source)
        {
            if (source == null || source.Count == 0)
            {
                return null;
            }

            var result = new Dictionary<string, string>(source.Count);
            foreach (var pair in source)
            {
                result[pair.Key ?? string.Empty] = pair.Value ?? string.Empty;
            }
            return result;
        }

        private static List<int>? ToList(IReadOnlyList<int>? source)
        {
            if (source == null || source.Count == 0)
            {
                return null;
            }

            var result = new List<int>(source.Count);
            for (var i = 0; i < source.Count; i++)
            {
                result.Add(source[i]);
            }
            return result;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RoomGatewayWireSessionClient));
            }
        }
    }
}
