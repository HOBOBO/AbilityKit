#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using AbilityKit.Network.Sdk;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 基于 MOBA 原生 Gateway API 的正式多人房间会话适配器。
    /// 每个写命令完成后补拉权威快照并写入 <see cref="ClientRoomStore"/>。
    /// See also: <c>GatewayRoomPreparationController</c> (BattleSessionFeature auto-create
    /// path) for headless/demo scenarios that skip the full formal lobby flow.
    /// </summary>
    public sealed class GatewayMultiplayerRoomSession :
        IMultiplayerRoomSession,
        IMobaReliableBattleEventCheckpointStore,
        IDisposable
    {
        private readonly IGatewayRoomClient _client;
        private readonly RoomGatewaySessionFlow _flow;
        private readonly IDisposable _sessionClient;
        private readonly ClientRoomStore _store;
        private readonly GatewayRoomMembership _membership = new GatewayRoomMembership();
        private readonly MobaReliableBattleEventCheckpointStore _checkpointStore;
        private readonly uint _guestLoginOpCode;
        private readonly TimeSpan _requestTimeout;
        private readonly TimeSpan _pollInterval;
        private readonly TimeSpan _battleStartTimeout;
        private string _sessionToken = string.Empty;

        public string SessionToken => _sessionToken;
        public string CurrentRoomId => _membership.RoomId;
        public ulong CurrentNumericRoomId => _membership.NumericRoomId;
        public uint CurrentPlayerId => _membership.PlayerId;

        public bool TryLoad(
            string battleId,
            out MobaReliableBattleEventCheckpoint checkpoint)
        {
            return _checkpointStore.TryLoad(battleId, out checkpoint);
        }

        public void Save(in MobaReliableBattleEventCheckpoint checkpoint)
        {
            _checkpointStore.Save(in checkpoint);
        }

        public GatewayMultiplayerRoomSession(
            IGatewayRoomClient client,
            ClientRoomStore store,
            uint guestLoginOpCode = 100u,
            TimeSpan? requestTimeout = null,
            TimeSpan? pollInterval = null,
            TimeSpan? battleStartTimeout = null,
            IReliableEventCheckpointStore reliableEventCheckpointStore = null,
            bool ownsReliableEventCheckpointStore = false,
            ReliableEventCheckpointLifecycleOptions reliableEventCheckpointLifecycleOptions = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _checkpointStore = new MobaReliableBattleEventCheckpointStore(
                reliableEventCheckpointStore,
                ownsReliableEventCheckpointStore,
                reliableEventCheckpointLifecycleOptions);

            IRoomGatewaySessionClientBase sessionClient;
            if (client is IRoomGatewayRequestTransport requestTransport)
            {
                var wireClient = new RoomGatewayWireSessionClient(
                    requestTransport,
                    client as IRoomGatewayPushSource);
                sessionClient = wireClient;
                _sessionClient = wireClient;
            }
            else
            {
                var compatibilityClient = new MobaRoomGatewaySessionClient(
                    _client,
                    _client,
                    _store);
                sessionClient = compatibilityClient;
                _sessionClient = compatibilityClient;
            }

            _flow = new RoomGatewaySessionFlow(sessionClient);
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

                var result = GatewayRoomProtocolMapper.ToRestoreResult(in restored);
                if (!result.HasActiveRoom)
                {
                    ClearMembership();
                    _store.Reset();
                    return result;
                }

                if (restored.Snapshot == null)
                {
                    return new MultiplayerRoomRestoreResult(
                        restored.RoomId,
                        restored.NumericRoomId,
                        restored.PlayerId,
                        (MultiplayerRoomPhase)restored.Phase,
                        MultiplayerRoomRestoreNextStep.None,
                        GatewayRoomProtocolMapper.ToEntryKind(restored.EntryKind),
                        restored.CanStart,
                        "Room restore did not produce an authoritative snapshot.",
                        MultiplayerRoomRestoreStatus.Failed,
                        MultiplayerRoomRestoreErrorCode.InternalError);
                }

                var authoritativePlayerId = ResolveAuthoritativeRestoredPlayerId(
                    restored.Snapshot,
                    spec.AccountId,
                    result.PlayerId);
                var restoredSnapshot = GatewayRoomProtocolMapper.ToClientSnapshot(
                    restored.Snapshot,
                    restored.NumericRoomId);
                var candidateMembership = new GatewayRoomMembership();
                candidateMembership.Commit(result.RoomId, result.NumericRoomId, authoritativePlayerId);

                var current = _store.Current;
                var replacesCurrentRoom = current != null &&
                    !string.Equals(current.RoomId, result.RoomId, StringComparison.Ordinal);
                ApplyMembership(
                    candidateMembership.RoomId,
                    candidateMembership.NumericRoomId,
                    candidateMembership.PlayerId);
                if (replacesCurrentRoom)
                {
                    _store.Reset();
                }
                ApplyAuthoritativeSnapshot(restoredSnapshot, restored.NumericRoomId);
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                return GatewayRoomProtocolMapper.CreateRestoreFailure(
                    fallbackPlayerId,
                    ex.Message,
                    MultiplayerRoomRestoreStatus.Timeout,
                    MultiplayerRoomRestoreErrorCode.Timeout);
            }
            catch (TimeoutException ex)
            {
                return GatewayRoomProtocolMapper.CreateRestoreFailure(
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
                GatewayRoomProtocolMapper.ToLaunchSpec(spec),
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
            _membership.Commit(roomId, numericRoomId, playerId);
        }

        private void ClearMembership()
        {
            _membership.Clear();
        }

        public async Task LeaveRoomAsync(string roomId, CancellationToken cancellationToken)
        {
            ValidateActiveSession(roomId);
            var current = RequireCurrentSnapshot(roomId);
            var completedBattleId = current.BattleId;
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
            if (!string.IsNullOrWhiteSpace(completedBattleId))
            {
                _checkpointStore.Remove(completedBattleId);
            }
            // 远端离房已不可逆完成，后续本地持久化不能再被原操作取消令牌中断。
            await _checkpointStore.FlushAsync(
                ReliableEventCheckpointFlushTrigger.RoomLeave,
                CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>等待可靠战斗事件检查点完成持久化。</summary>
        public Task FlushReliableEventCheckpointsAsync(
            CancellationToken cancellationToken = default)
        {
            return _checkpointStore.FlushAsync(cancellationToken);
        }

        /// <summary>按指定生命周期原因等待可靠战斗事件检查点完成持久化。</summary>
        public Task<ReliableEventCheckpointFlushResult> FlushReliableEventCheckpointsAsync(
            ReliableEventCheckpointFlushTrigger trigger,
            CancellationToken cancellationToken = default)
        {
            return _checkpointStore.FlushAsync(trigger, cancellationToken);
        }

        /// <summary>获取可靠事件检查点生命周期的累计诊断。</summary>
        public ReliableEventCheckpointLifecycleDiagnostics ReliableEventCheckpointLifecycleDiagnostics =>
            _checkpointStore.LifecycleDiagnostics;

        /// <summary>可靠事件检查点 flush 失败且完成诊断记录后触发。</summary>
        public event Action<ReliableEventCheckpointLifecycleFailure> ReliableEventCheckpointLifecycleFailure
        {
            add => _checkpointStore.LifecycleFailure += value;
            remove => _checkpointStore.LifecycleFailure -= value;
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
                    GatewayRoomProtocolMapper.ToClientSnapshot(result.Snapshot, current.NumericRoomId),
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
                _battleStartTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSucceeded(result.Success, result.Message, "report assets loaded", result.ErrorCode);
            if (result.Snapshot != null && !string.IsNullOrWhiteSpace(result.Snapshot.RoomId))
            {
                ApplyAuthoritativeSnapshot(
                    GatewayRoomProtocolMapper.ToClientSnapshot(result.Snapshot, current.NumericRoomId),
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
                GatewayRoomProtocolMapper.ToClientSnapshot(result.Snapshot, result.NumericRoomId),
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
                    GatewayRoomProtocolMapper.ToClientSnapshot(result.Snapshot, current.NumericRoomId),
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
            var result = await _flow.GetSnapshotAsync(
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

            var snapshot = GatewayRoomProtocolMapper.ToClientSnapshot(
                result.Snapshot,
                result.NumericRoomId);
            ApplyAuthoritativeSnapshot(snapshot, result.NumericRoomId);
            return snapshot;
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
                    GatewayRoomProtocolMapper.ToClientSnapshot(result.Snapshot, current.NumericRoomId),
                    current.NumericRoomId);
            }
            else
            {
                await RefreshSnapshotAsync(roomId, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            try
            {
                _checkpointStore.Dispose();
            }
            finally
            {
                _sessionClient.Dispose();
            }
        }

        internal static IReadOnlyDictionary<string, string> BuildLaunchTags(
            MultiplayerRoomLaunchSpec spec)
        {
            return GatewayRoomProtocolMapper.BuildLaunchTags(spec);
        }

        internal static uint ResolveAuthoritativeRestoredPlayerId(
            RoomGatewaySnapshot snapshot,
            string accountId,
            uint serverPlayerId)
        {
            if (snapshot?.Players == null || string.IsNullOrWhiteSpace(accountId))
            {
                throw new InvalidOperationException(
                    "Active room restore requires an authenticated account and an authoritative player snapshot.");
            }

            uint snapshotPlayerId = 0u;
            for (var i = 0; i < snapshot.Players.Count; i++)
            {
                var player = snapshot.Players[i];
                if (player != null && string.Equals(
                        player.AccountId,
                        accountId,
                        StringComparison.Ordinal))
                {
                    snapshotPlayerId = player.PlayerId;
                    break;
                }
            }

            if (snapshotPlayerId == 0u)
            {
                throw new InvalidOperationException(
                    $"Active room restore snapshot does not contain a player identity for account '{accountId}'.");
            }

            if (serverPlayerId == 0u || serverPlayerId != snapshotPlayerId)
            {
                throw new InvalidOperationException(
                    $"Active room restore player identity mismatch for account '{accountId}': " +
                    $"server={serverPlayerId}, snapshot={snapshotPlayerId}.");
            }

            return snapshotPlayerId;
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
