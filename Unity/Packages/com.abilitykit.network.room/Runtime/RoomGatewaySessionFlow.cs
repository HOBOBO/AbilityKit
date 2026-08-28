#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Network.Room
{

    public sealed class RoomGatewaySessionFlow
    {
        private readonly IRoomGatewaySessionClientBase _client;
        private readonly IRoomGatewayHeroPickCapability? _heroPick;
        private readonly IRoomGatewayStagedLoadingCapability? _stagedLoading;
        private readonly IRoomGatewayStateSyncSubscriptionCapability? _stateSyncSubscription;

        public RoomGatewaySessionFlow(IRoomGatewaySessionClientBase client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _heroPick = client as IRoomGatewayHeroPickCapability;
            _stagedLoading = client as IRoomGatewayStagedLoadingCapability;
            _stateSyncSubscription = client as IRoomGatewayStateSyncSubscriptionCapability;
        }

        // ===== 阶段 5：阶段化资源加载流程（每步独立可恢复） =====

        /// <summary>
        /// 阶段 1：创建房间。返回 roomId。
        /// </summary>
        public async Task<string> CreateRoomAsync(
            string sessionToken,
            RoomGatewayLaunchSpec launchSpec,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);

            var create = await _client.CreateRoomAsync(
                new RoomGatewayCreateRequest(sessionToken, launchSpec.Region, launchSpec.ServerId, launchSpec.RoomType, launchSpec.RoomTitle, true, launchSpec.MaxPlayers, launchSpec.Tags),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(create.Success, create.Message, "create room");
            return create.RoomId;
        }

        /// <summary>
        /// 阶段 2：加入房间。返回 join 结果（含 snapshot / battleId）。
        /// </summary>
        public Task<RoomGatewayJoinResult> JoinRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            return _client.JoinRoomAsync(new RoomGatewayJoinRequest(sessionToken, region, serverId, roomId), timeout, cancellationToken);
        }

        public Task<RoomGatewayLeaveResult> LeaveRoomAsync(
            RoomGatewayLeaveRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(request.SessionToken);
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
            return _client.LeaveRoomAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// 阶段 3：配置出战（PickHero）。
        /// </summary>
        public Task<RoomGatewayPickHeroResult> ConfigureLoadoutAsync(
            RoomGatewayPickHeroRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(request.SessionToken);
            return RequireCapability(_heroPick, "hero pick").PickHeroAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// 阶段 4：设置准备状态。
        /// </summary>
        public Task<RoomGatewayReadyResult> SetReadyAsync(
            string sessionToken,
            string roomId,
            bool ready,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            return _client.SetReadyAsync(new RoomGatewayReadyRequest(sessionToken, roomId, ready), timeout, cancellationToken);
        }

        /// <summary>
        /// 阶段 5：Owner 发起资源加载阶段（Lobby -> Loading）。
        /// </summary>
        public Task<RoomGatewayBeginLoadingResult> BeginLoadingAsync(
            RoomGatewayBeginLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(request.SessionToken);
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
            return RequireCapability(_stagedLoading, "staged loading").BeginLoadingAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// 阶段 6：成员上报资源加载完成。
        /// </summary>
        public Task<RoomGatewayReportAssetsLoadedResult> ReportAssetsLoadedAsync(
            RoomGatewayReportAssetsLoadedRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(request.SessionToken);
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
            return RequireCapability(_stagedLoading, "staged loading").ReportAssetsLoadedAsync(request, timeout, cancellationToken);
        }

        public Task<RoomGatewayReportLoadingProgressResult> ReportLoadingProgressAsync(
            RoomGatewayReportLoadingProgressRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(request.SessionToken);
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
            if (request.Progress < 0 || request.Progress > 100) throw new ArgumentOutOfRangeException(nameof(request));
            return RequireCapability(_stagedLoading, "staged loading").ReportLoadingProgressAsync(request, timeout, cancellationToken);
        }

        public Task<RoomGatewayCancelLoadingResult> CancelLoadingAsync(
            RoomGatewayCancelLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(request.SessionToken);
            if (string.IsNullOrWhiteSpace(request.RoomId)) throw new ArgumentException("roomId is required.", nameof(request));
            return RequireCapability(_stagedLoading, "staged loading").CancelLoadingAsync(request, timeout, cancellationToken);
        }

        public Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(
            string sessionToken,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            return _client.GetSnapshotAsync(
                new RoomGatewayGetSnapshotRequest(sessionToken, roomId),
                timeout,
                cancellationToken);
        }

        /// <summary>
        /// 阶段 7：等待战斗提交完成。
        /// 通过 GetSnapshot 轮询，直到 Phase=InBattle 且 BattleId 已回填或超时。
        /// Starting 仅表示服务端正在初始化战斗运行时，尚不可订阅状态同步。
        /// </summary>
        public Task<RoomGatewayGetSnapshotResult> WaitForBattleStartAsync(
            string sessionToken,
            string roomId,
            TimeSpan pollInterval,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return WaitForBattleStartAsync(
                sessionToken,
                roomId,
                pollInterval,
                timeout,
                progress: null,
                cancellationToken);
        }

        public async Task<RoomGatewayGetSnapshotResult> WaitForBattleStartAsync(
            string sessionToken,
            string roomId,
            TimeSpan pollInterval,
            TimeSpan timeout,
            IProgress<RoomGatewaySnapshot>? progress,
            CancellationToken cancellationToken = default)
        {
            return await WaitForSnapshotAsync(
                sessionToken,
                roomId,
                snapshot => snapshot.Phase == RoomGatewaySessionPhase.InBattle &&
                            !string.IsNullOrWhiteSpace(snapshot.BattleId),
                pollInterval,
                timeout,
                progress,
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<RoomGatewayGetSnapshotResult> WaitForSnapshotAsync(
            string sessionToken,
            string roomId,
            Func<RoomGatewaySnapshot, bool> predicate,
            TimeSpan pollInterval,
            TimeSpan timeout,
            IProgress<RoomGatewaySnapshot>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (pollInterval <= TimeSpan.Zero) pollInterval = TimeSpan.FromMilliseconds(500);
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

            var deadline = DateTime.UtcNow + timeout;
            var observedLoading = false;
            var lastPhase = (RoomGatewaySessionPhase)(-1);
            var lastBattleId = string.Empty;
            var feed = _client as IRoomGatewaySnapshotFeed;
            if (feed != null && pollInterval < TimeSpan.FromSeconds(2))
            {
                pollInterval = TimeSpan.FromSeconds(2);
            }

            TaskCompletionSource<bool>? signal = null;
            Action<RoomGatewaySnapshot>? onSnapshotChanged = null;
            if (feed != null)
            {
                signal = CreateSignal();
                onSnapshotChanged = snapshot =>
                {
                    if (snapshot != null && string.Equals(snapshot.RoomId, roomId, StringComparison.Ordinal))
                    {
                        progress?.Report(snapshot);
                        signal?.TrySetResult(true);
                    }
                };
                feed.SnapshotChanged += onSnapshotChanged;
            }

            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pushed = feed?.Current;
                    if (pushed != null && string.Equals(pushed.RoomId, roomId, StringComparison.Ordinal))
                    {
                        ExtendDeadlineFromSnapshot(pushed, ref deadline);
                        EnsureWaitablePhase(pushed, roomId, ref observedLoading);
                    }

                    var remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero) break;
                    var requestTimeout = remaining < TimeSpan.FromSeconds(10)
                        ? remaining
                        : TimeSpan.FromSeconds(10);
                    var snapshot = await GetSnapshotAsync(
                        sessionToken,
                        roomId,
                        requestTimeout,
                        cancellationToken).ConfigureAwait(false);

                    if (!snapshot.Success)
                    {
                        return snapshot;
                    }

                    if (snapshot.Snapshot != null)
                    {
                        progress?.Report(snapshot.Snapshot);
                        lastPhase = snapshot.Snapshot.Phase;
                        lastBattleId = snapshot.Snapshot.BattleId ?? string.Empty;
                        ExtendDeadlineFromSnapshot(snapshot.Snapshot, ref deadline);
                        EnsureWaitablePhase(snapshot.Snapshot, roomId, ref observedLoading);
                        if (predicate(snapshot.Snapshot))
                        {
                            return snapshot;
                        }
                    }

                    remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero) break;
                    var delay = pollInterval < remaining ? pollInterval : remaining;
                    if (feed == null || signal == null)
                    {
                        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var observedSignal = signal;
                    var delayTask = Task.Delay(delay, cancellationToken);
                    await Task.WhenAny(observedSignal.Task, delayTask).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (observedSignal.Task.IsCompleted)
                    {
                        signal = CreateSignal();
                    }
                }
            }
            finally
            {
                if (feed != null && onSnapshotChanged != null)
                {
                    feed.SnapshotChanged -= onSnapshotChanged;
                }
            }

            throw new TimeoutException($"Room snapshot wait timed out after {timeout} for room {roomId}. lastPhase={lastPhase} battleId={lastBattleId}");
        }

        private static TaskCompletionSource<bool> CreateSignal() =>
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static TCapability RequireCapability<TCapability>(TCapability? capability, string name)
            where TCapability : class
        {
            return capability ?? throw new NotSupportedException($"Room session client does not support {name}.");
        }

        private static void ExtendDeadlineFromSnapshot(RoomGatewaySnapshot snapshot, ref DateTime deadline)
        {
            if (snapshot.LoadingDeadlineUnixMs <= 0L) return;
            DateTime authoritativeDeadline;
            try
            {
                authoritativeDeadline = DateTimeOffset
                    .FromUnixTimeMilliseconds(snapshot.LoadingDeadlineUnixMs)
                    .UtcDateTime
                    .AddSeconds(15);
            }
            catch (ArgumentOutOfRangeException)
            {
                return;
            }

            if (authoritativeDeadline > deadline) deadline = authoritativeDeadline;
        }

        private static void EnsureWaitablePhase(
            RoomGatewaySnapshot snapshot,
            string roomId,
            ref bool observedLoading)
        {
            if (snapshot.Phase == RoomGatewaySessionPhase.Loading ||
                snapshot.Phase == RoomGatewaySessionPhase.Starting)
            {
                observedLoading = true;
            }
            else if (observedLoading && snapshot.Phase == RoomGatewaySessionPhase.Lobby)
            {
                throw new InvalidOperationException(
                    $"Room {roomId} returned to Lobby while waiting: {snapshot.PhaseReason}");
            }

            switch (snapshot.Phase)
            {
                case RoomGatewaySessionPhase.Closing:
                case RoomGatewaySessionPhase.Closed:
                case RoomGatewaySessionPhase.Expired:
                    throw new InvalidOperationException(
                        $"Room {roomId} entered terminal phase {snapshot.Phase}: {snapshot.PhaseReason}");
            }
        }

        /// <summary>
        /// 阶段 8：订阅战斗状态同步（Phase=InBattle 后调用）。
        /// </summary>
        public Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            string sessionToken,
            string battleId,
            string roomId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return SubscribeStateSyncAsync(
                sessionToken,
                battleId,
                roomId,
                string.Empty,
                0L,
                timeout,
                cancellationToken);
        }

        public Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            string sessionToken,
            string battleId,
            string roomId,
            string eventEpoch,
            long lastEventAck,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(battleId)) throw new ArgumentException("battleId is required.", nameof(battleId));
            return RequireCapability(_stateSyncSubscription, "state-sync subscription").SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(
                    sessionToken,
                    battleId,
                    roomId,
                    eventEpoch,
                    lastEventAck),
                timeout,
                cancellationToken);
        }

        /// <summary>
        /// 阶段化恢复：支持 Lobby/Loading/Starting/InBattle 任意阶段 restore，
        /// 根据 snapshot.Phase 决定下一步。
        /// </summary>
        public async Task<RoomGatewayStagedRestoreResult> RestoreAsync(
            string sessionToken,
            string region,
            string serverId,
            uint playerId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));
            ValidatePlayerId(playerId);

            RoomGatewayRestoreRoomResult restored;
            try
            {
                restored = await _client.RestoreRoomAsync(
                    new RoomGatewayRestoreRoomRequest(sessionToken, region, serverId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return CreateRestoreTimeoutResult(playerId, "Room restore request timed out.");
            }
            catch (TimeoutException ex)
            {
                return CreateRestoreTimeoutResult(playerId, ex.Message);
            }

            // Restore failures are part of the session protocol. Preserve their status and
            // error code so callers can decide whether to retry, re-authenticate, or return
            // to the lobby instead of collapsing every outcome into InvalidOperationException.
            if (!restored.Success)
            {
                return new RoomGatewayStagedRestoreResult(
                    restored.RoomId,
                    restored.NumericRoomId,
                    null,
                    RoomGatewaySessionPhase.Closed,
                    RoomGatewayStagedRestoreNextStep.None,
                    SelectPlayerId(restored.CurrentPlayerId, playerId),
                    restored.ServerNowTicks,
                    restored.Message,
                    restored.JoinKind,
                    restored.CanStart,
                    restored.Status,
                    restored.ErrorCode);
            }

            if (!restored.HasActiveRoom || string.IsNullOrWhiteSpace(restored.RoomId))
            {
                return new RoomGatewayStagedRestoreResult(
                    restored.RoomId,
                    restored.NumericRoomId,
                    null,
                    RoomGatewaySessionPhase.Closed,
                    RoomGatewayStagedRestoreNextStep.None,
                    SelectPlayerId(restored.CurrentPlayerId, playerId),
                    restored.ServerNowTicks,
                    restored.Message,
                    restored.JoinKind,
                    restored.CanStart,
                    restored.Status,
                    restored.ErrorCode);
            }

            RoomGatewayGetSnapshotResult current;
            try
            {
                current = await _client.GetSnapshotAsync(
                    new RoomGatewayGetSnapshotRequest(sessionToken, restored.RoomId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                current = default;
            }
            catch (TimeoutException)
            {
                current = default;
            }
            var snapshot = current.Success && current.Snapshot != null
                ? MergeRestoreSnapshot(current.Snapshot, in restored)
                : CreateRestoreSnapshot(in restored);
            var phase = ResolvePhase(snapshot);
            var nextStep = ResolveNextStep(
                phase,
                restored.IsInBattle && !string.IsNullOrWhiteSpace(snapshot.BattleId));
            var numericRoomId = restored.NumericRoomId != 0ul
                ? restored.NumericRoomId
                : current.NumericRoomId;
            var serverNowTicks = current.Success && current.ServerNowTicks != 0L
                ? current.ServerNowTicks
                : restored.ServerNowTicks;

            return new RoomGatewayStagedRestoreResult(
                restored.RoomId,
                numericRoomId,
                snapshot,
                phase,
                nextStep,
                SelectPlayerId(restored.CurrentPlayerId, playerId),
                serverNowTicks,
                restored.Message,
                restored.JoinKind,
                restored.CanStart,
                restored.Status,
                restored.ErrorCode);
        }

        private static RoomGatewayStagedRestoreResult CreateRestoreTimeoutResult(
            uint playerId,
            string message)
        {
            return new RoomGatewayStagedRestoreResult(
                string.Empty,
                0UL,
                null,
                RoomGatewaySessionPhase.Closed,
                RoomGatewayStagedRestoreNextStep.None,
                playerId,
                0L,
                message,
                RoomGatewaySessionEntryKind.TeamLobby,
                false,
                RoomGatewaySessionRestoreStatus.Timeout,
                RoomGatewaySessionRestoreErrorCode.Timeout);
        }

        private static RoomGatewaySnapshot CreateRestoreSnapshot(in RoomGatewayRestoreRoomResult restored)
        {
            return new RoomGatewaySnapshot
            {
                RoomId = restored.RoomId,
                Phase = restored.IsInBattle
                    ? RoomGatewaySessionPhase.InBattle
                    : RoomGatewaySessionPhase.Lobby,
                CanStart = restored.CanStart,
                BattleId = restored.BattleId,
                WorldId = restored.WorldId,
                WorldStartAnchor = restored.WorldStartAnchor
            };
        }

        private static RoomGatewaySnapshot MergeRestoreSnapshot(
            RoomGatewaySnapshot snapshot,
            in RoomGatewayRestoreRoomResult restored)
        {
            return new RoomGatewaySnapshot
            {
                RoomId = string.IsNullOrWhiteSpace(snapshot.RoomId)
                    ? restored.RoomId
                    : snapshot.RoomId,
                OwnerAccountId = snapshot.OwnerAccountId,
                Phase = snapshot.Phase,
                PhaseReason = snapshot.PhaseReason,
                LaunchGeneration = snapshot.LaunchGeneration,
                LoadingDeadlineUnixMs = snapshot.LoadingDeadlineUnixMs,
                LaunchManifestHash = snapshot.LaunchManifestHash,
                LaunchManifestVersion = snapshot.LaunchManifestVersion,
                LastStartFailureCode = snapshot.LastStartFailureCode,
                RoomRevision = snapshot.RoomRevision,
                LastEventSequence = snapshot.LastEventSequence,
                CanStart = snapshot.CanStart || restored.CanStart,
                BattleId = string.IsNullOrWhiteSpace(snapshot.BattleId)
                    ? restored.BattleId
                    : snapshot.BattleId,
                WorldId = snapshot.WorldId == 0ul ? restored.WorldId : snapshot.WorldId,
                Members = snapshot.Members,
                Players = snapshot.Players,
                SyncCapabilities = snapshot.SyncCapabilities,
                WorldStartAnchor = snapshot.WorldStartAnchor.IsValid
                    ? snapshot.WorldStartAnchor
                    : restored.WorldStartAnchor
            };
        }

        private static RoomGatewaySessionPhase ResolvePhase(RoomGatewaySnapshot? snapshot)
        {
            if (snapshot == null)
            {
                return RoomGatewaySessionPhase.Lobby;
            }

            return snapshot.Phase;
        }

        private static RoomGatewayStagedRestoreNextStep ResolveNextStep(
            RoomGatewaySessionPhase phase,
            bool canSubscribeRunningBattle)
        {
            switch (phase)
            {
                case RoomGatewaySessionPhase.Lobby:
                    return RoomGatewayStagedRestoreNextStep.SetReadyAndBeginLoading;
                case RoomGatewaySessionPhase.Loading:
                    return RoomGatewayStagedRestoreNextStep.ReportAssetsLoaded;
                case RoomGatewaySessionPhase.Starting:
                    return RoomGatewayStagedRestoreNextStep.WaitForBattleStart;
                case RoomGatewaySessionPhase.InBattle:
                    return canSubscribeRunningBattle
                        ? RoomGatewayStagedRestoreNextStep.SubscribeStateSync
                        : RoomGatewayStagedRestoreNextStep.WaitForBattleStart;
                default:
                    return RoomGatewayStagedRestoreNextStep.None;
            }
        }

        private static uint SelectPlayerId(uint serverPlayerId, uint fallbackPlayerId)
        {
            return serverPlayerId == 0u ? fallbackPlayerId : serverPlayerId;
        }

        private static void ValidateSessionToken(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) throw new ArgumentException("sessionToken is required.", nameof(sessionToken));
        }

        private static void ValidatePlayerId(uint playerId)
        {
            if (playerId == 0) throw new ArgumentOutOfRangeException(nameof(playerId));
        }

        private static void EnsureSuccess(bool success, string message, string operation)
        {
            if (!success) throw new InvalidOperationException($"Room gateway {operation} failed: {message}");
        }
    }

    public enum RoomGatewaySessionEntryKind
    {
        TeamLobby = 0,
        Reconnect = 1,
        LateJoin = 2
    }

    public enum RoomGatewaySessionRestoreStatus
    {
        Restored = 0,
        NoActiveRoom = 1,
        NotMember = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        Failed = 6,
        Timeout = 7
    }

    public enum RoomGatewaySessionRestoreErrorCode
    {
        None = 0,
        NoAccountRoomMapping = 1,
        AccountNotInRoom = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        InternalError = 6,
        Timeout = 7
    }

    public readonly struct RoomGatewayWorldStartAnchor
    {
        public readonly long StartServerTicks;
        public readonly long ServerTickFrequency;
        public readonly int StartFrame;
        public readonly double FixedDeltaSeconds;

        public RoomGatewayWorldStartAnchor(long startServerTicks, long serverTickFrequency, int startFrame, double fixedDeltaSeconds)
        {
            StartServerTicks = startServerTicks;
            ServerTickFrequency = serverTickFrequency;
            StartFrame = startFrame;
            FixedDeltaSeconds = fixedDeltaSeconds;
        }

        public bool IsValid => ServerTickFrequency > 0 && FixedDeltaSeconds > 0d;
    }

    public readonly struct RoomGatewayLaunchSpec
    {
        public readonly string Region;
        public readonly string ServerId;
        public readonly string RoomType;
        public readonly string RoomTitle;
        public readonly int MaxPlayers;
        public readonly int GameplayId;
        public readonly int RuleSetId;
        public readonly int ConfigVersion;
        public readonly int ProtocolVersion;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly IReadOnlyDictionary<string, string>? Tags;
        public readonly string SyncTemplateId;
        public readonly int SyncModel;
        public readonly string NetworkEnvironmentId;
        public readonly string CarrierName;
        public readonly bool EnableAuthoritativeWorld;
        public readonly bool InterpolationEnabled;
        public readonly int InputDelayFrames;

        public RoomGatewayLaunchSpec(
            string region,
            string serverId,
            string roomType,
            string roomTitle,
            int maxPlayers,
            int gameplayId,
            int ruleSetId,
            int configVersion,
            int protocolVersion,
            string worldType,
            string clientId,
            IReadOnlyDictionary<string, string>? tags = null,
            string syncTemplateId = "",
            int syncModel = 0,
            string networkEnvironmentId = "",
            string carrierName = "",
            bool enableAuthoritativeWorld = true,
            bool interpolationEnabled = false,
            int inputDelayFrames = 0)
        {
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            RoomType = roomType ?? string.Empty;
            RoomTitle = roomTitle ?? string.Empty;
            MaxPlayers = maxPlayers;
            GameplayId = gameplayId;
            RuleSetId = ruleSetId;
            ConfigVersion = configVersion;
            ProtocolVersion = protocolVersion;
            WorldType = worldType ?? string.Empty;
            ClientId = clientId ?? string.Empty;
            Tags = tags;
            SyncTemplateId = syncTemplateId ?? string.Empty;
            SyncModel = syncModel;
            NetworkEnvironmentId = networkEnvironmentId ?? string.Empty;
            CarrierName = carrierName ?? string.Empty;
            EnableAuthoritativeWorld = enableAuthoritativeWorld;
            InterpolationEnabled = interpolationEnabled;
            InputDelayFrames = inputDelayFrames < 0 ? 0 : inputDelayFrames;
        }
    }

    public readonly struct RoomGatewayCreateRequest
    {
        public readonly string SessionToken;
        public readonly string Region;
        public readonly string ServerId;
        public readonly string RoomType;
        public readonly string Title;
        public readonly bool IsPublic;
        public readonly int MaxPlayers;
        public readonly IReadOnlyDictionary<string, string>? Tags;

        public RoomGatewayCreateRequest(string sessionToken, string region, string serverId, string roomType, string title, bool isPublic, int maxPlayers, IReadOnlyDictionary<string, string>? tags = null)
        {
            SessionToken = sessionToken ?? string.Empty;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            RoomType = roomType ?? string.Empty;
            Title = title ?? string.Empty;
            IsPublic = isPublic;
            MaxPlayers = maxPlayers;
            Tags = tags;
        }
    }

    public readonly struct RoomGatewayJoinRequest
    {
        public readonly string SessionToken;
        public readonly string Region;
        public readonly string ServerId;
        public readonly string RoomId;

        public RoomGatewayJoinRequest(string sessionToken, string region, string serverId, string roomId)
        {
            SessionToken = sessionToken ?? string.Empty;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayReadyRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly bool Ready;

        public RoomGatewayReadyRequest(string sessionToken, string roomId, bool ready)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            Ready = ready;
        }
    }

    public readonly struct RoomGatewayStartBattleRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly int GameplayId;
        public readonly int RuleSetId;
        public readonly int ConfigVersion;
        public readonly int ProtocolVersion;
        public readonly string WorldType;
        public readonly string ClientId;
        public readonly string SyncTemplateId;
        public readonly int SyncModel;
        public readonly string NetworkEnvironmentId;
        public readonly string CarrierName;
        public readonly bool EnableAuthoritativeWorld;
        public readonly bool InterpolationEnabled;
        public readonly int InputDelayFrames;

        public RoomGatewayStartBattleRequest(
            string sessionToken,
            string roomId,
            int gameplayId,
            int ruleSetId,
            int configVersion,
            int protocolVersion,
            string worldType,
            string clientId,
            string syncTemplateId = "",
            int syncModel = 0,
            string networkEnvironmentId = "",
            string carrierName = "",
            bool enableAuthoritativeWorld = true,
            bool interpolationEnabled = false,
            int inputDelayFrames = 0)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            GameplayId = gameplayId;
            RuleSetId = ruleSetId;
            ConfigVersion = configVersion;
            ProtocolVersion = protocolVersion;
            WorldType = worldType ?? string.Empty;
            ClientId = clientId ?? string.Empty;
            SyncTemplateId = syncTemplateId ?? string.Empty;
            SyncModel = syncModel;
            NetworkEnvironmentId = networkEnvironmentId ?? string.Empty;
            CarrierName = carrierName ?? string.Empty;
            EnableAuthoritativeWorld = enableAuthoritativeWorld;
            InterpolationEnabled = interpolationEnabled;
            InputDelayFrames = inputDelayFrames < 0 ? 0 : inputDelayFrames;
        }
    }

    public readonly struct RoomGatewayStateSyncSubscriptionRequest
    {
        public readonly string SessionToken;
        public readonly string BattleId;
        public readonly string RoomId;
        public readonly string EventEpoch;
        public readonly long LastEventAck;

        public RoomGatewayStateSyncSubscriptionRequest(string sessionToken, string battleId, string roomId)
            : this(sessionToken, battleId, roomId, string.Empty, 0L)
        {
        }

        public RoomGatewayStateSyncSubscriptionRequest(
            string sessionToken,
            string battleId,
            string roomId,
            string eventEpoch,
            long lastEventAck)
        {
            SessionToken = sessionToken ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            EventEpoch = eventEpoch ?? string.Empty;
            LastEventAck = Math.Max(0L, lastEventAck);
        }
    }

    public readonly struct RoomGatewayRestoreRoomRequest
    {
        public readonly string SessionToken;
        public readonly string Region;
        public readonly string ServerId;

        public RoomGatewayRestoreRoomRequest(string sessionToken, string region, string serverId)
        {
            SessionToken = sessionToken ?? string.Empty;
            Region = region ?? string.Empty;
            ServerId = serverId ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayCreateResult
    {
        public readonly bool Success;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly string Message;

        public RoomGatewayCreateResult(bool success, string roomId, ulong numericRoomId, string message)
        {
            Success = success;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayJoinResult
    {
        public readonly bool Success;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly RoomGatewayWorldStartAnchor WorldStartAnchor;
        public readonly string Message;
        public readonly string BattleId;
        public readonly bool CanStart;
        public readonly RoomGatewaySessionEntryKind JoinKind;
        public readonly long ServerNowTicks;
        public readonly ulong WorldId;
        public readonly uint CurrentPlayerId;

        public RoomGatewayJoinResult(bool success, string roomId, ulong numericRoomId, RoomGatewayWorldStartAnchor worldStartAnchor, string message, string battleId, bool canStart, RoomGatewaySessionEntryKind joinKind, long serverNowTicks, ulong worldId, uint currentPlayerId = 0u)
        {
            Success = success;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            WorldStartAnchor = worldStartAnchor;
            Message = message ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            CanStart = canStart;
            JoinKind = joinKind;
            ServerNowTicks = serverNowTicks;
            WorldId = worldId;
            CurrentPlayerId = currentPlayerId;
        }
    }

    public readonly struct RoomGatewayReadyResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly string BattleId;
        public readonly bool CanStart;
        public readonly string Message;

        public RoomGatewayReadyResult(bool success, string battleId, bool canStart, string message)
            : this(success, success, 0, string.Empty, 0ul, battleId, canStart, message)
        {
        }

        public RoomGatewayReadyResult(
            bool success,
            string roomId,
            ulong numericRoomId,
            string battleId,
            bool canStart,
            string message)
            : this(success, success, 0, roomId, numericRoomId, battleId, canStart, message)
        {
        }

        public RoomGatewayReadyResult(
            bool success,
            bool applied,
            int errorCode,
            string roomId,
            ulong numericRoomId,
            string battleId,
            bool canStart,
            string message)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            BattleId = battleId ?? string.Empty;
            CanStart = canStart;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayStartBattleResult
    {
        public readonly bool Success;
        public readonly string BattleId;
        public readonly ulong WorldId;
        public readonly bool Started;
        public readonly RoomGatewayWorldStartAnchor WorldStartAnchor;
        public readonly long ServerNowTicks;
        public readonly string Message;
        public readonly RoomGatewayNetworkSyncCapabilities? SyncCapabilities;

        public RoomGatewayStartBattleResult(bool success, string battleId, ulong worldId, bool started, RoomGatewayWorldStartAnchor worldStartAnchor, long serverNowTicks, string message, RoomGatewayNetworkSyncCapabilities? syncCapabilities = null)
        {
            Success = success;
            BattleId = battleId ?? string.Empty;
            WorldId = worldId;
            Started = started;
            WorldStartAnchor = worldStartAnchor;
            ServerNowTicks = serverNowTicks;
            Message = message ?? string.Empty;
            SyncCapabilities = syncCapabilities;
        }
    }

    public readonly struct RoomGatewayStateSyncSubscriptionResult
    {
        public readonly bool Success;
        public readonly string Message;

        public RoomGatewayStateSyncSubscriptionResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayRestoreRoomResult
    {
        public readonly bool Success;
        public readonly bool HasActiveRoom;
        public readonly bool IsInBattle;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly RoomGatewayWorldStartAnchor WorldStartAnchor;
        public readonly string Message;
        public readonly string BattleId;
        public readonly bool CanStart;
        public readonly RoomGatewaySessionEntryKind JoinKind;
        public readonly long ServerNowTicks;
        public readonly ulong WorldId;
        public readonly RoomGatewaySessionRestoreStatus Status;
        public readonly RoomGatewaySessionRestoreErrorCode ErrorCode;
        public readonly uint CurrentPlayerId;
        public readonly RoomGatewaySnapshot? Snapshot;

        public RoomGatewayRestoreRoomResult(bool success, bool hasActiveRoom, bool isInBattle, string roomId, ulong numericRoomId, RoomGatewayWorldStartAnchor worldStartAnchor, string message, string battleId, bool canStart, RoomGatewaySessionEntryKind joinKind, long serverNowTicks, ulong worldId, RoomGatewaySessionRestoreStatus status, RoomGatewaySessionRestoreErrorCode errorCode, uint currentPlayerId = 0u)
            : this(success, hasActiveRoom, isInBattle, roomId, numericRoomId, worldStartAnchor, message, battleId, canStart, joinKind, serverNowTicks, worldId, status, errorCode, currentPlayerId, null)
        {
        }

        public RoomGatewayRestoreRoomResult(bool success, bool hasActiveRoom, bool isInBattle, string roomId, ulong numericRoomId, RoomGatewayWorldStartAnchor worldStartAnchor, string message, string battleId, bool canStart, RoomGatewaySessionEntryKind joinKind, long serverNowTicks, ulong worldId, RoomGatewaySessionRestoreStatus status, RoomGatewaySessionRestoreErrorCode errorCode, uint currentPlayerId, RoomGatewaySnapshot? snapshot)
        {
            Success = success;
            HasActiveRoom = hasActiveRoom;
            IsInBattle = isInBattle;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            WorldStartAnchor = worldStartAnchor;
            Message = message ?? string.Empty;
            BattleId = battleId ?? string.Empty;
            CanStart = canStart;
            JoinKind = joinKind;
            ServerNowTicks = serverNowTicks;
            WorldId = worldId;
            Status = status;
            ErrorCode = errorCode;
            CurrentPlayerId = currentPlayerId;
            Snapshot = snapshot;
        }
    }

    public readonly struct RoomGatewaySessionFlowResult
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly string BattleId;
        public readonly ulong WorldId;
        public readonly uint PlayerId;
        public readonly RoomGatewayWorldStartAnchor WorldStartAnchor;
        public readonly long ServerNowTicks;
        public readonly RoomGatewaySessionEntryKind EntryKind;
        public readonly bool CanStart;
        public readonly bool Started;
        public readonly bool Subscribed;
        public readonly string Message;
        public readonly RoomGatewaySessionRestoreStatus RestoreStatus;
        public readonly RoomGatewaySessionRestoreErrorCode RestoreErrorCode;
        /// <summary>服务端为本次战斗代际声明的同步能力；旧服务端可能不提供。</summary>
        public readonly RoomGatewayNetworkSyncCapabilities? SyncCapabilities;

        public RoomGatewaySessionFlowResult(string sessionToken, string roomId, ulong numericRoomId, string battleId, ulong worldId, uint playerId, RoomGatewayWorldStartAnchor worldStartAnchor, long serverNowTicks, RoomGatewaySessionEntryKind entryKind, bool canStart, bool started, bool subscribed, string message)
            : this(sessionToken, roomId, numericRoomId, battleId, worldId, playerId, worldStartAnchor, serverNowTicks, entryKind, canStart, started, subscribed, message, RoomGatewaySessionRestoreStatus.Restored, RoomGatewaySessionRestoreErrorCode.None)
        {
        }

        public RoomGatewaySessionFlowResult(string sessionToken, string roomId, ulong numericRoomId, string battleId, ulong worldId, uint playerId, RoomGatewayWorldStartAnchor worldStartAnchor, long serverNowTicks, RoomGatewaySessionEntryKind entryKind, bool canStart, bool started, bool subscribed, string message, RoomGatewaySessionRestoreStatus restoreStatus, RoomGatewaySessionRestoreErrorCode restoreErrorCode, RoomGatewayNetworkSyncCapabilities? syncCapabilities = null)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            BattleId = battleId ?? string.Empty;
            WorldId = worldId;
            PlayerId = playerId;
            WorldStartAnchor = worldStartAnchor;
            ServerNowTicks = serverNowTicks;
            EntryKind = entryKind;
            CanStart = canStart;
            Started = started;
            Subscribed = subscribed;
            Message = message ?? string.Empty;
            RestoreStatus = restoreStatus;
            RestoreErrorCode = restoreErrorCode;
            SyncCapabilities = syncCapabilities;
        }
    }

    // ===== 阶段 5：阶段化资源加载流程的请求/结果类型 =====

    /// <summary>
    /// 客户端 Room 阶段枚举（与服务端 RoomPhase 对齐）。
    /// </summary>
    public enum RoomGatewaySessionPhase
    {
        Lobby = 0,
        Loading = 1,
        Starting = 2,
        InBattle = 3,
        Closing = 4,
        Closed = 5,
        Expired = 6
    }

    /// <summary>
    /// 阶段化恢复后建议的下一步动作。
    /// </summary>
    public enum RoomGatewayStagedRestoreNextStep
    {
        None = 0,
        SetReadyAndBeginLoading,
        ReportAssetsLoaded,
        WaitForBattleStart,
        SubscribeStateSync
    }

    public readonly struct RoomGatewayPickHeroRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly int HeroId;
        public readonly int TeamId;
        public readonly int SpawnPointId;
        public readonly int Level;
        public readonly int AttributeTemplateId;
        public readonly int BasicAttackSkillId;
        public readonly IReadOnlyList<int> SkillIds;

        public RoomGatewayPickHeroRequest(
            string sessionToken,
            string roomId,
            int heroId,
            int teamId,
            int spawnPointId,
            int level,
            int attributeTemplateId,
            int basicAttackSkillId,
            IReadOnlyList<int> skillIds)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            HeroId = heroId;
            TeamId = teamId;
            SpawnPointId = spawnPointId;
            Level = level;
            AttributeTemplateId = attributeTemplateId;
            BasicAttackSkillId = basicAttackSkillId;
            SkillIds = skillIds;
        }
    }

    public readonly struct RoomGatewayPickHeroResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly RoomGatewaySnapshot Snapshot;
        public readonly string Message;

        public RoomGatewayPickHeroResult(
            bool success,
            string roomId,
            ulong numericRoomId,
            RoomGatewaySnapshot snapshot,
            string message)
            : this(success, success, 0, roomId, numericRoomId, snapshot, message)
        {
        }

        public RoomGatewayPickHeroResult(
            bool success,
            bool applied,
            int errorCode,
            string roomId,
            ulong numericRoomId,
            RoomGatewaySnapshot snapshot,
            string message)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayBeginLoadingRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long? ExpectedRevision;
        public readonly string CommandId;

        public RoomGatewayBeginLoadingRequest(string sessionToken, string roomId, long? expectedRevision, string commandId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ExpectedRevision = expectedRevision;
            CommandId = commandId ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayBeginLoadingResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string Message;
        public readonly long RoomRevision;
        public readonly RoomGatewaySnapshot? Snapshot;

        public RoomGatewayBeginLoadingResult(bool success, bool applied, int errorCode, string message, long roomRevision, RoomGatewaySnapshot? snapshot)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            RoomRevision = roomRevision;
            Snapshot = snapshot;
        }
    }

    public readonly struct RoomGatewayReportAssetsLoadedRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long LaunchGeneration;
        public readonly int ManifestVersion;
        public readonly string ManifestHash;
        public readonly string CommandId;

        public RoomGatewayReportAssetsLoadedRequest(string sessionToken, string roomId, long launchGeneration, int manifestVersion, string manifestHash, string commandId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            LaunchGeneration = launchGeneration;
            ManifestVersion = manifestVersion;
            ManifestHash = manifestHash ?? string.Empty;
            CommandId = commandId ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayReportAssetsLoadedResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string Message;
        public readonly long RoomRevision;
        public readonly RoomGatewaySnapshot? Snapshot;

        public RoomGatewayReportAssetsLoadedResult(bool success, bool applied, int errorCode, string message, long roomRevision, RoomGatewaySnapshot? snapshot)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            RoomRevision = roomRevision;
            Snapshot = snapshot;
        }
    }

    public readonly struct RoomGatewayLeaveRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long? ExpectedRevision;
        public readonly string CommandId;

        public RoomGatewayLeaveRequest(string sessionToken, string roomId, long? expectedRevision, string commandId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ExpectedRevision = expectedRevision;
            CommandId = commandId ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayLeaveResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string Message;
        public readonly long RoomRevision;
        public readonly RoomGatewaySnapshot? Snapshot;

        public RoomGatewayLeaveResult(bool success, bool applied, int errorCode, string message, long roomRevision, RoomGatewaySnapshot? snapshot)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            RoomRevision = roomRevision;
            Snapshot = snapshot;
        }
    }

    public readonly struct RoomGatewayReportLoadingProgressRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long LaunchGeneration;
        public readonly int ManifestVersion;
        public readonly string ManifestHash;
        public readonly int Progress;

        public RoomGatewayReportLoadingProgressRequest(
            string sessionToken,
            string roomId,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            int progress)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            LaunchGeneration = launchGeneration;
            ManifestVersion = manifestVersion;
            ManifestHash = manifestHash ?? string.Empty;
            Progress = progress;
        }
    }

    public readonly struct RoomGatewayReportLoadingProgressResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string Message;
        public readonly long RoomRevision;
        public readonly RoomGatewaySnapshot? Snapshot;

        public RoomGatewayReportLoadingProgressResult(
            bool success,
            bool applied,
            int errorCode,
            string message,
            long roomRevision,
            RoomGatewaySnapshot? snapshot)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            RoomRevision = roomRevision;
            Snapshot = snapshot;
        }
    }

    public readonly struct RoomGatewayCancelLoadingRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly long? ExpectedRevision;
        public readonly string CommandId;

        public RoomGatewayCancelLoadingRequest(string sessionToken, string roomId, long? expectedRevision, string commandId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            ExpectedRevision = expectedRevision;
            CommandId = commandId ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayCancelLoadingResult
    {
        public readonly bool Success;
        public readonly bool Applied;
        public readonly int ErrorCode;
        public readonly string Message;
        public readonly long RoomRevision;
        public readonly RoomGatewaySnapshot? Snapshot;

        public RoomGatewayCancelLoadingResult(bool success, bool applied, int errorCode, string message, long roomRevision, RoomGatewaySnapshot? snapshot)
        {
            Success = success;
            Applied = applied;
            ErrorCode = errorCode;
            Message = message ?? string.Empty;
            RoomRevision = roomRevision;
            Snapshot = snapshot;
        }
    }

    public readonly struct RoomGatewayGetSnapshotRequest
    {
        public readonly string SessionToken;
        public readonly string RoomId;

        public RoomGatewayGetSnapshotRequest(string sessionToken, string roomId)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
        }
    }

    public readonly struct RoomGatewayGetSnapshotResult
    {
        public readonly bool Success;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly RoomGatewaySnapshot? Snapshot;
        public readonly string Message;
        public readonly long ServerNowTicks;

        public RoomGatewayGetSnapshotResult(bool success, string roomId, ulong numericRoomId, RoomGatewaySnapshot? snapshot, string message)
            : this(success, roomId, numericRoomId, snapshot, message, 0L)
        {
        }

        public RoomGatewayGetSnapshotResult(bool success, string roomId, ulong numericRoomId, RoomGatewaySnapshot? snapshot, string message, long serverNowTicks)
        {
            Success = success;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
            ServerNowTicks = serverNowTicks;
        }
    }

    /// <summary>
    /// 阶段化客户端 Room 快照视图（解耦 wire 类型）。
    /// </summary>
}
