#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Extensions.Session;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 将权威 <see cref="ClientRoomStore"/> 投影为多人流程使用的稳定视图。
    /// </summary>
    public sealed class ClientRoomSnapshotProvider : IRoomSnapshotProvider, IDisposable
    {
        private readonly ClientRoomStore _store;

        public ClientRoomSnapshotProvider(ClientRoomStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _store.OnSnapshotChanged += HandleSnapshotChanged;
        }

        public MultiplayerRoomSnapshot? Current => Project(_store.Current);

        public event Action<MultiplayerRoomSnapshot>? OnSnapshotChanged;

        public void Dispose()
        {
            _store.OnSnapshotChanged -= HandleSnapshotChanged;
        }

        private void HandleSnapshotChanged(ClientRoomSnapshot snapshot)
        {
            var projected = Project(snapshot);
            if (projected != null)
            {
                OnSnapshotChanged?.Invoke(projected);
            }
        }

        private static MultiplayerRoomSnapshot? Project(ClientRoomSnapshot? snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            return new MultiplayerRoomSnapshot
            {
                RoomId = snapshot.RoomId,
                OwnerAccountId = snapshot.OwnerAccountId,
                NumericRoomId = snapshot.NumericRoomId,
                Phase = (MultiplayerRoomPhase)snapshot.Phase,
                PhaseReason = snapshot.PhaseReason,
                CanStart = snapshot.CanStart,
                BattleId = snapshot.BattleId,
                WorldId = snapshot.WorldId,
                LaunchGeneration = snapshot.LaunchGeneration,
                LaunchManifestVersion = snapshot.LaunchManifestVersion,
                LaunchManifestHash = snapshot.LaunchManifestHash,
                LoadingDeadlineUnixMs = snapshot.LoadingDeadlineUnixMs,
                RoomRevision = snapshot.RoomRevision,
                LastEventSequence = snapshot.LastEventSequence,
                LastStartFailureCode = snapshot.LastStartFailureCode,
                Members = CopyStrings(snapshot.Members),
                Players = ToMultiplayerPlayers(snapshot.Players)
            };
        }

        private static IReadOnlyList<MultiplayerRoomPlayerSnapshot> ToMultiplayerPlayers(
            IReadOnlyList<ClientRoomPlayer> players)
        {
            if (players == null || players.Count == 0)
            {
                return Array.Empty<MultiplayerRoomPlayerSnapshot>();
            }

            var result = new MultiplayerRoomPlayerSnapshot[players.Count];
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                result[i] = new MultiplayerRoomPlayerSnapshot
                {
                    AccountId = player.AccountId,
                    PlayerId = player.PlayerId,
                    TeamId = player.TeamId,
                    HeroId = player.HeroId,
                    SpawnPointId = player.SpawnPointId,
                    Level = player.Level,
                    AttributeTemplateId = player.AttributeTemplateId,
                    BasicAttackSkillId = player.BasicAttackSkillId,
                    SkillIds = CopyInts(player.SkillIds),
                    LobbyReady = player.LobbyReady,
                    AssetsLoaded = player.AssetsLoaded,
                    LoadingProgress = player.LoadingProgress,
                    IsOnline = player.IsOnline,
                    JoinOrdinal = player.JoinOrdinal,
                    LoadedManifestVersion = player.LoadedManifestVersion,
                    LoadedManifestHash = player.LoadedManifestHash,
                    LastSeenTicks = player.LastSeenTicks,
                    OfflineSinceTicks = player.OfflineSinceTicks
                };
            }

            return result;
        }

        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
            var result = new string[source.Count];
            for (var i = 0; i < source.Count; i++) result[i] = source[i] ?? string.Empty;
            return result;
        }

        private static IReadOnlyList<int> CopyInts(IReadOnlyList<int> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<int>();
            var result = new int[source.Count];
            for (var i = 0; i < source.Count; i++) result[i] = source[i];
            return result;
        }
    }

    /// <summary>
    /// 基于 MOBA 原生 Gateway API 的正式多人房间会话适配器。
    /// 每个写命令完成后补拉权威快照并写入 <see cref="ClientRoomStore"/>。
    /// See also: <c>GatewayRoomPreparationController</c> (BattleSessionFeature auto-create
    /// path) for headless/demo scenarios that skip the full formal lobby flow.
    /// </summary>
    public sealed class GatewayMultiplayerRoomSession :
        IMultiplayerRoomSession,
        IMobaReliableBattleEventCheckpointStore
    {
        private readonly IGatewayRoomClient _client;
        private readonly RoomGatewaySessionFlow _flow;
        private readonly ClientRoomStore _store;
        private readonly uint _guestLoginOpCode;
        private readonly TimeSpan _requestTimeout;
        private readonly TimeSpan _pollInterval;
        private readonly TimeSpan _battleStartTimeout;
        private readonly object _checkpointGate = new object();
        private string _sessionToken = string.Empty;
        private string _currentRoomId = string.Empty;
        private ulong _currentNumericRoomId;
        private uint _currentPlayerId;
        private MobaReliableBattleEventCheckpoint _reliableEventCheckpoint;

        public string SessionToken => _sessionToken;
        public string CurrentRoomId => _currentRoomId;
        public ulong CurrentNumericRoomId => _currentNumericRoomId;
        public uint CurrentPlayerId => _currentPlayerId;

        public bool TryLoad(
            string battleId,
            out MobaReliableBattleEventCheckpoint checkpoint)
        {
            lock (_checkpointGate)
            {
                checkpoint = _reliableEventCheckpoint;
                return checkpoint.IsValid &&
                       string.Equals(
                           checkpoint.BattleId,
                           battleId,
                           StringComparison.Ordinal);
            }
        }

        public void Save(in MobaReliableBattleEventCheckpoint checkpoint)
        {
            if (!checkpoint.IsValid)
            {
                return;
            }

            lock (_checkpointGate)
            {
                _reliableEventCheckpoint = checkpoint;
            }
        }

        public GatewayMultiplayerRoomSession(
            IGatewayRoomClient client,
            ClientRoomStore store,
            uint guestLoginOpCode = 100u,
            TimeSpan? requestTimeout = null,
            TimeSpan? pollInterval = null,
            TimeSpan? battleStartTimeout = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _flow = new RoomGatewaySessionFlow(new MobaRoomGatewaySessionClient(_client, _store));
            _guestLoginOpCode = guestLoginOpCode;
            _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(10);
            _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(500);
            _battleStartTimeout = battleStartTimeout ?? TimeSpan.FromSeconds(135);
        }

        public async Task<MultiplayerRoomRestoreResult> RestoreAsync(
            MultiplayerRoomLaunchSpec spec,
            uint fallbackPlayerId,
            CancellationToken cancellationToken)
        {
            ValidateSpec(spec);
            if (fallbackPlayerId == 0u) throw new ArgumentOutOfRangeException(nameof(fallbackPlayerId));

            try
            {
                var token = await EnsureSessionTokenAsync(
                    spec.SessionToken,
                    cancellationToken).ConfigureAwait(false);
                var restored = await _flow.RestoreAsync(
                    token,
                    spec.Region,
                    spec.ServerId,
                    fallbackPlayerId,
                    _requestTimeout,
                    cancellationToken).ConfigureAwait(false);

                var result = ToRestoreResult(in restored);
                if (!result.HasActiveRoom)
                {
                    ClearMembership();
                    _store.Reset();
                    return result;
                }

                if (restored.Snapshot == null)
                {
                    ClearMembership();
                    _store.Reset();
                    return new MultiplayerRoomRestoreResult(
                        restored.RoomId,
                        restored.NumericRoomId,
                        restored.PlayerId,
                        (MultiplayerRoomPhase)restored.Phase,
                        MultiplayerRoomRestoreNextStep.None,
                        ToEntryKind(restored.EntryKind),
                        restored.CanStart,
                        "Room restore did not produce an authoritative snapshot.",
                        MultiplayerRoomRestoreStatus.Failed,
                        MultiplayerRoomRestoreErrorCode.InternalError);
                }

                var current = _store.Current;
                if (current != null &&
                    !string.Equals(current.RoomId, restored.RoomId, StringComparison.Ordinal))
                {
                    _store.Reset();
                }

                ApplyMembership(result.RoomId, result.NumericRoomId, result.PlayerId);
                ApplyAuthoritativeSnapshot(
                    ToClientSnapshot(restored.Snapshot, restored.NumericRoomId),
                    restored.NumericRoomId);
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                return CreateRestoreFailure(
                    fallbackPlayerId,
                    ex.Message,
                    MultiplayerRoomRestoreStatus.Timeout,
                    MultiplayerRoomRestoreErrorCode.Timeout);
            }
            catch (TimeoutException ex)
            {
                return CreateRestoreFailure(
                    fallbackPlayerId,
                    ex.Message,
                    MultiplayerRoomRestoreStatus.Timeout,
                    MultiplayerRoomRestoreErrorCode.Timeout);
            }
        }

        public async Task<string> CreateRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            CancellationToken cancellationToken)
        {
            ValidateSpec(spec);
            var token = await EnsureSessionTokenAsync(spec.SessionToken, cancellationToken).ConfigureAwait(false);
            return await _flow.CreateRoomAsync(
                token,
                ToLaunchSpec(spec),
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<MultiplayerRoomJoinResult> JoinRoomAsync(
            MultiplayerRoomLaunchSpec spec,
            string roomId,
            CancellationToken cancellationToken)
        {
            ValidateSpec(spec);
            ValidateRoomId(roomId);
            var token = await EnsureSessionTokenAsync(spec.SessionToken, cancellationToken).ConfigureAwait(false);
            var result = await _flow.JoinRoomAsync(
                token,
                spec.Region,
                spec.ServerId,
                roomId,
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(result.Success, result.Message, "join room");
            if (result.CurrentPlayerId == 0u)
            {
                throw new InvalidOperationException(
                    "Gateway join room did not return an authoritative player id.");
            }
            await RefreshSnapshotAsync(roomId, cancellationToken).ConfigureAwait(false);
            var joinedRoomId = string.IsNullOrWhiteSpace(result.RoomId) ? roomId : result.RoomId;
            ApplyMembership(joinedRoomId, result.NumericRoomId, result.CurrentPlayerId);
            return new MultiplayerRoomJoinResult(
                joinedRoomId,
                result.NumericRoomId,
                result.CurrentPlayerId);
        }

        private void ApplyMembership(string roomId, ulong numericRoomId, uint playerId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new InvalidOperationException("Authoritative room membership has no room id.");
            }
            if (numericRoomId == 0UL)
            {
                throw new InvalidOperationException("Authoritative room membership has no numeric room id.");
            }
            if (playerId == 0u)
            {
                throw new InvalidOperationException("Authoritative room membership has no player id.");
            }

            _currentRoomId = roomId;
            _currentNumericRoomId = numericRoomId;
            _currentPlayerId = playerId;
        }

        private void ClearMembership()
        {
            _currentRoomId = string.Empty;
            _currentNumericRoomId = 0UL;
            _currentPlayerId = 0u;
        }

        public async Task LeaveRoomAsync(string roomId, CancellationToken cancellationToken)
        {
            ValidateActiveSession(roomId);
            var current = RequireCurrentSnapshot(roomId);
            var result = await _flow.LeaveRoomAsync(
                new RoomGatewayLeaveRequest(
                    _sessionToken,
                    roomId,
                    current.RoomRevision,
                    NewCommandId("leave-room")),
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(result.Success, result.Message, "leave room", result.ErrorCode);

            ClearMembership();
            _store.Reset();
        }

        public async Task ConfigureLoadoutAsync(
            string roomId,
            MultiplayerLoadoutSpec loadout,
            CancellationToken cancellationToken)
        {
            ValidateActiveSession(roomId);
            var result = await _flow.ConfigureLoadoutAsync(
                new RoomGatewayPickHeroRequest(
                    _sessionToken,
                    roomId,
                    loadout.HeroId,
                    loadout.TeamId,
                    loadout.SpawnPointId,
                    loadout.Level,
                    loadout.AttributeTemplateId,
                    loadout.BasicAttackSkillId,
                    loadout.SkillIds),
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(
                result.Success && result.Applied,
                result.Message,
                "configure loadout",
                result.ErrorCode);
            await RefreshSnapshotAsync(roomId, cancellationToken).ConfigureAwait(false);
        }

        public async Task SetReadyAsync(
            string roomId,
            bool ready,
            CancellationToken cancellationToken)
        {
            ValidateActiveSession(roomId);
            var result = await _flow.SetReadyAsync(
                _sessionToken,
                roomId,
                ready,
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(result.Success, result.Message, "set ready");
            await RefreshSnapshotAsync(roomId, cancellationToken).ConfigureAwait(false);
        }

        public async Task BeginLoadingAsync(string roomId, CancellationToken cancellationToken)
        {
            ValidateActiveSession(roomId);
            var current = RequireCurrentSnapshot(roomId);
            var result = await _flow.BeginLoadingAsync(
                new RoomGatewayBeginLoadingRequest(
                    _sessionToken,
                    roomId,
                    current.RoomRevision,
                    NewCommandId("begin-loading")),
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(result.Success, result.Message, "begin loading", result.ErrorCode);
            if (result.Snapshot != null && !string.IsNullOrWhiteSpace(result.Snapshot.RoomId))
            {
                ApplyAuthoritativeSnapshot(
                    ToClientSnapshot(result.Snapshot, current.NumericRoomId),
                    current.NumericRoomId);
            }
            else
            {
                await RefreshSnapshotAsync(roomId, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task ReportAssetsLoadedAsync(string roomId, CancellationToken cancellationToken)
        {
            ValidateActiveSession(roomId);
            var current = RequireCurrentSnapshot(roomId);
            if (current.Phase != ClientRoomPhase.Loading)
            {
                throw new InvalidOperationException($"Cannot report assets in room phase {current.Phase}.");
            }

            var result = await _flow.ReportAssetsLoadedAsync(
                new RoomGatewayReportAssetsLoadedRequest(
                    _sessionToken,
                    roomId,
                    current.LaunchGeneration,
                    current.LaunchManifestVersion,
                    current.LaunchManifestHash,
                    NewCommandId("assets-loaded")),
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(result.Success, result.Message, "report assets loaded", result.ErrorCode);
            if (result.Snapshot != null && !string.IsNullOrWhiteSpace(result.Snapshot.RoomId))
            {
                ApplyAuthoritativeSnapshot(
                    ToClientSnapshot(result.Snapshot, current.NumericRoomId),
                    current.NumericRoomId);
            }
            else
            {
                await RefreshSnapshotAsync(roomId, cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task WaitForBattleStartAsync(string roomId, CancellationToken cancellationToken)
        {
            ValidateActiveSession(roomId);
            var result = await _flow.WaitForBattleStartAsync(
                _sessionToken,
                roomId,
                _pollInterval,
                _battleStartTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(result.Success, result.Message, "wait for battle start");
            if (result.Snapshot == null || result.Snapshot.WorldId == 0UL)
            {
                throw new InvalidOperationException(
                    $"Gateway wait for battle start returned no committed world for room {roomId}.");
            }

            ApplyAuthoritativeSnapshot(
                ToClientSnapshot(result.Snapshot, result.NumericRoomId),
                result.NumericRoomId);
        }

        public async Task ReportLoadingProgressAsync(
            string roomId,
            int progress,
            CancellationToken cancellationToken)
        {
            ValidateActiveSession(roomId);
            var current = RequireCurrentSnapshot(roomId);
            if (current.Phase != ClientRoomPhase.Loading)
            {
                throw new InvalidOperationException($"Cannot report loading progress in room phase {current.Phase}.");
            }

            var result = await _flow.ReportLoadingProgressAsync(
                new RoomGatewayReportLoadingProgressRequest(
                    _sessionToken,
                    roomId,
                    current.LaunchGeneration,
                    current.LaunchManifestVersion,
                    current.LaunchManifestHash,
                    progress),
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(result.Success, result.Message, "report loading progress", result.ErrorCode);
            if (result.Snapshot != null && !string.IsNullOrWhiteSpace(result.Snapshot.RoomId))
            {
                ApplyAuthoritativeSnapshot(
                    ToClientSnapshot(result.Snapshot, current.NumericRoomId),
                    current.NumericRoomId);
            }
        }

        private async Task<string> EnsureSessionTokenAsync(
            string requestedToken,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(requestedToken))
            {
                _sessionToken = requestedToken;
                return _sessionToken;
            }

            if (!string.IsNullOrWhiteSpace(_sessionToken))
            {
                return _sessionToken;
            }

            _sessionToken = await _client.GuestLoginAsync(
                _guestLoginOpCode,
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(_sessionToken))
            {
                throw new InvalidOperationException("Gateway guest login did not return a session token.");
            }

            return _sessionToken;
        }

        internal async Task<ClientRoomSnapshot> RefreshSnapshotAsync(
            string roomId,
            CancellationToken cancellationToken)
        {
            var result = await _client.GetSnapshotAsync(
                _sessionToken,
                roomId,
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            if (!result.Success || result.Snapshot == null)
            {
                throw new InvalidOperationException(
                    $"Gateway get snapshot failed: {result.Message}");
            }

            if (result.NumericRoomId == 0UL)
            {
                throw new InvalidOperationException(
                    $"Gateway get snapshot returned an invalid numeric room id for room {roomId}.");
            }

            result.Snapshot.NumericRoomId = result.NumericRoomId;
            ApplyAuthoritativeSnapshot(result.Snapshot, result.NumericRoomId);
            return result.Snapshot;
        }

        private void ApplyAuthoritativeSnapshot(ClientRoomSnapshot snapshot, ulong numericRoomId)
        {
            if (snapshot == null) return;
            if (numericRoomId != 0UL) snapshot.NumericRoomId = numericRoomId;
            _store.ApplySnapshot(snapshot);
            _store.MarkRefreshed();
        }

        private ClientRoomSnapshot RequireCurrentSnapshot(string roomId)
        {
            var current = _store.Current;
            if (current == null || !string.Equals(current.RoomId, roomId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Current authoritative room snapshot is unavailable.");
            }

            return current;
        }

        private static void EnsureSucceeded(bool success, string message, string operation, int errorCode = 0)
        {
            if (!success)
            {
                throw new InvalidOperationException(
                    $"Gateway {operation} failed ({errorCode}): {message}");
            }
        }

        public async Task CancelLoadingAsync(string roomId, CancellationToken cancellationToken)
        {
            ValidateActiveSession(roomId);
            var current = RequireCurrentSnapshot(roomId);
            if (current.Phase != ClientRoomPhase.Loading && current.Phase != ClientRoomPhase.Starting)
            {
                throw new InvalidOperationException($"Cannot cancel loading in room phase {current.Phase}.");
            }

            var result = await _flow.CancelLoadingAsync(
                new RoomGatewayCancelLoadingRequest(
                    _sessionToken,
                    roomId,
                    current.RoomRevision,
                    NewCommandId("cancel-loading")),
                _requestTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(result.Success, result.Message, "cancel loading", result.ErrorCode);
            if (result.Snapshot != null && !string.IsNullOrWhiteSpace(result.Snapshot.RoomId))
            {
                ApplyAuthoritativeSnapshot(
                    ToClientSnapshot(result.Snapshot, current.NumericRoomId),
                    current.NumericRoomId);
            }
            else
            {
                await RefreshSnapshotAsync(roomId, cancellationToken).ConfigureAwait(false);
            }
        }

        private static RoomGatewayLaunchSpec ToLaunchSpec(MultiplayerRoomLaunchSpec spec)
        {
            return new RoomGatewayLaunchSpec(
                spec.Region,
                spec.ServerId,
                spec.RoomType,
                spec.RoomTitle,
                spec.MaxPlayers,
                spec.GameplayId,
                spec.RuleSetId,
                spec.ConfigVersion,
                spec.ProtocolVersion,
                spec.WorldType,
                spec.ClientId,
                tags: BuildLaunchTags(spec));
        }

        internal static IReadOnlyDictionary<string, string> BuildLaunchTags(
            MultiplayerRoomLaunchSpec spec)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RoomTagKeys.Gameplay] = spec.RoomType,
                [RoomTagKeys.GameplayId] = spec.GameplayId.ToString(CultureInfo.InvariantCulture),
                [RoomTagKeys.RuleSetId] = spec.RuleSetId.ToString(CultureInfo.InvariantCulture),
                [RoomTagKeys.ConfigVersion] = spec.ConfigVersion.ToString(CultureInfo.InvariantCulture),
                [RoomTagKeys.ProtocolVersion] = spec.ProtocolVersion.ToString(CultureInfo.InvariantCulture),
                [RoomTagKeys.WorldType] = spec.WorldType,
                [RoomTagKeys.ClientId] = spec.ClientId,
                [RoomTagKeys.MinPlayers] = spec.MinPlayers.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static MultiplayerRoomRestoreResult ToRestoreResult(
            in RoomGatewayStagedRestoreResult restored)
        {
            return new MultiplayerRoomRestoreResult(
                restored.RoomId,
                restored.NumericRoomId,
                restored.PlayerId,
                (MultiplayerRoomPhase)restored.Phase,
                ToNextStep(restored.NextStep),
                ToEntryKind(restored.EntryKind),
                restored.CanStart,
                restored.Message,
                ToRestoreStatus(restored.RestoreStatus),
                ToRestoreErrorCode(restored.RestoreErrorCode));
        }

        private static MultiplayerRoomRestoreResult CreateRestoreFailure(
            uint playerId,
            string message,
            MultiplayerRoomRestoreStatus status,
            MultiplayerRoomRestoreErrorCode errorCode)
        {
            return new MultiplayerRoomRestoreResult(
                string.Empty,
                0UL,
                playerId,
                MultiplayerRoomPhase.Closed,
                MultiplayerRoomRestoreNextStep.None,
                MultiplayerRoomEntryKind.TeamLobby,
                false,
                message,
                status,
                errorCode);
        }

        private static ClientRoomSnapshot ToClientSnapshot(
            RoomGatewaySnapshot snapshot,
            ulong numericRoomId)
        {
            var anchor = snapshot.WorldStartAnchor;
            return new ClientRoomSnapshot
            {
                RoomId = snapshot.RoomId,
                OwnerAccountId = snapshot.OwnerAccountId,
                NumericRoomId = numericRoomId,
                Phase = (ClientRoomPhase)snapshot.Phase,
                PhaseReason = snapshot.PhaseReason,
                LaunchGeneration = snapshot.LaunchGeneration,
                LoadingDeadlineUnixMs = snapshot.LoadingDeadlineUnixMs,
                LaunchManifestHash = snapshot.LaunchManifestHash,
                LaunchManifestVersion = snapshot.LaunchManifestVersion,
                LastStartFailureCode = snapshot.LastStartFailureCode,
                RoomRevision = snapshot.RoomRevision,
                LastEventSequence = snapshot.LastEventSequence,
                CanStart = snapshot.CanStart,
                BattleId = snapshot.BattleId,
                WorldId = snapshot.WorldId,
                Members = CopyStrings(snapshot.Members),
                Players = ToClientPlayers(snapshot.Players),
                WorldStartAnchor = new GatewayWorldStartAnchor(
                    anchor.StartServerTicks,
                    anchor.ServerTickFrequency,
                    anchor.StartFrame,
                    anchor.FixedDeltaSeconds)
            };
        }

        private static IReadOnlyList<ClientRoomPlayer> ToClientPlayers(
            IReadOnlyList<RoomGatewayPlayerSnapshot> players)
        {
            if (players == null || players.Count == 0) return Array.Empty<ClientRoomPlayer>();
            var result = new ClientRoomPlayer[players.Count];
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                result[i] = new ClientRoomPlayer
                {
                    AccountId = player.AccountId,
                    PlayerId = player.PlayerId,
                    TeamId = player.TeamId,
                    HeroId = player.HeroId,
                    SpawnPointId = player.SpawnPointId,
                    Level = player.Level,
                    AttributeTemplateId = player.AttributeTemplateId,
                    BasicAttackSkillId = player.BasicAttackSkillId,
                    SkillIds = CopyInts(player.SkillIds),
                    LobbyReady = player.LobbyReady,
                    Ready = player.LobbyReady,
                    AssetsLoaded = player.AssetsLoaded,
                    LoadingProgress = player.LoadingProgress,
                    IsOnline = player.IsOnline,
                    JoinOrdinal = player.JoinOrdinal,
                    LoadedManifestVersion = player.LoadedManifestVersion,
                    LoadedManifestHash = player.LoadedManifestHash,
                    LastSeenTicks = player.LastSeenTicks,
                    OfflineSinceTicks = player.OfflineSinceTicks
                };
            }

            return result;
        }

        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<string>();
            var result = new string[source.Count];
            for (var i = 0; i < source.Count; i++) result[i] = source[i] ?? string.Empty;
            return result;
        }

        private static IReadOnlyList<int> CopyInts(IReadOnlyList<int> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<int>();
            var result = new int[source.Count];
            for (var i = 0; i < source.Count; i++) result[i] = source[i];
            return result;
        }

        private static MultiplayerRoomRestoreNextStep ToNextStep(
            RoomGatewayStagedRestoreNextStep nextStep)
        {
            switch (nextStep)
            {
                case RoomGatewayStagedRestoreNextStep.SetReadyAndBeginLoading:
                    return MultiplayerRoomRestoreNextStep.SetReadyAndBeginLoading;
                case RoomGatewayStagedRestoreNextStep.ReportAssetsLoaded:
                    return MultiplayerRoomRestoreNextStep.ReportAssetsLoaded;
                case RoomGatewayStagedRestoreNextStep.WaitForBattleStart:
                    return MultiplayerRoomRestoreNextStep.WaitForBattleStart;
                case RoomGatewayStagedRestoreNextStep.SubscribeStateSync:
                    return MultiplayerRoomRestoreNextStep.EnterBattle;
                default:
                    return MultiplayerRoomRestoreNextStep.None;
            }
        }

        private static MultiplayerRoomEntryKind ToEntryKind(RoomGatewaySessionEntryKind entryKind)
        {
            return entryKind switch
            {
                RoomGatewaySessionEntryKind.Reconnect => MultiplayerRoomEntryKind.Reconnect,
                RoomGatewaySessionEntryKind.LateJoin => MultiplayerRoomEntryKind.LateJoin,
                _ => MultiplayerRoomEntryKind.TeamLobby
            };
        }

        private static MultiplayerRoomRestoreStatus ToRestoreStatus(
            RoomGatewaySessionRestoreStatus status)
        {
            return status switch
            {
                RoomGatewaySessionRestoreStatus.NoActiveRoom => MultiplayerRoomRestoreStatus.NoActiveRoom,
                RoomGatewaySessionRestoreStatus.NotMember => MultiplayerRoomRestoreStatus.NotMember,
                RoomGatewaySessionRestoreStatus.RoomClosed => MultiplayerRoomRestoreStatus.RoomClosed,
                RoomGatewaySessionRestoreStatus.RoomExpired => MultiplayerRoomRestoreStatus.RoomExpired,
                RoomGatewaySessionRestoreStatus.InvalidSession => MultiplayerRoomRestoreStatus.InvalidSession,
                RoomGatewaySessionRestoreStatus.Timeout => MultiplayerRoomRestoreStatus.Timeout,
                RoomGatewaySessionRestoreStatus.Failed => MultiplayerRoomRestoreStatus.Failed,
                _ => MultiplayerRoomRestoreStatus.Restored
            };
        }

        private static MultiplayerRoomRestoreErrorCode ToRestoreErrorCode(
            RoomGatewaySessionRestoreErrorCode errorCode)
        {
            return errorCode switch
            {
                RoomGatewaySessionRestoreErrorCode.NoAccountRoomMapping => MultiplayerRoomRestoreErrorCode.NoAccountRoomMapping,
                RoomGatewaySessionRestoreErrorCode.AccountNotInRoom => MultiplayerRoomRestoreErrorCode.AccountNotInRoom,
                RoomGatewaySessionRestoreErrorCode.RoomClosed => MultiplayerRoomRestoreErrorCode.RoomClosed,
                RoomGatewaySessionRestoreErrorCode.RoomExpired => MultiplayerRoomRestoreErrorCode.RoomExpired,
                RoomGatewaySessionRestoreErrorCode.InvalidSession => MultiplayerRoomRestoreErrorCode.InvalidSession,
                RoomGatewaySessionRestoreErrorCode.Timeout => MultiplayerRoomRestoreErrorCode.Timeout,
                RoomGatewaySessionRestoreErrorCode.InternalError => MultiplayerRoomRestoreErrorCode.InternalError,
                _ => MultiplayerRoomRestoreErrorCode.None
            };
        }

        private void ValidateActiveSession(string roomId)
        {
            ValidateRoomId(roomId);
            if (string.IsNullOrWhiteSpace(_sessionToken))
            {
                throw new InvalidOperationException("Gateway session is not authenticated.");
            }
        }

        private static void ValidateSpec(MultiplayerRoomLaunchSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (string.IsNullOrWhiteSpace(spec.Region)) throw new ArgumentException("Region is required.", nameof(spec));
            if (string.IsNullOrWhiteSpace(spec.ServerId)) throw new ArgumentException("ServerId is required.", nameof(spec));
            if (spec.MaxPlayers <= 0) throw new ArgumentOutOfRangeException(nameof(spec));
            if (spec.MinPlayers <= 0 || spec.MinPlayers > spec.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spec),
                    "MinPlayers must be between 1 and MaxPlayers.");
            }
            if (spec.GameplayId <= 0) throw new ArgumentOutOfRangeException(nameof(spec), "GameplayId must be positive.");
            if (string.IsNullOrWhiteSpace(spec.WorldType)) throw new ArgumentException("WorldType is required.", nameof(spec));
        }

        private static void ValidateRoomId(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                throw new ArgumentException("RoomId is required.", nameof(roomId));
            }
        }

        private static string NewCommandId(string operation)
        {
            return operation + ":" + Guid.NewGuid().ToString("N");
        }
    }
}
