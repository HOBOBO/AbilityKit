#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Ability.Host.Extensions.Session
{
    public interface IRoomGatewaySessionClient
    {
        Task<RoomGatewayCreateResult> CreateRoomAsync(RoomGatewayCreateRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<RoomGatewayJoinResult> JoinRoomAsync(RoomGatewayJoinRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<RoomGatewayReadyResult> SetReadyAsync(RoomGatewayReadyRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<RoomGatewayStartBattleResult> StartBattleAsync(RoomGatewayStartBattleRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(RoomGatewayStateSyncSubscriptionRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(RoomGatewayRestoreRoomRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        // ===== 阶段 5：阶段化资源加载流程 =====

        Task<RoomGatewayPickHeroResult> PickHeroAsync(RoomGatewayPickHeroRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<RoomGatewayBeginLoadingResult> BeginLoadingAsync(RoomGatewayBeginLoadingRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<RoomGatewayReportAssetsLoadedResult> ReportAssetsLoadedAsync(RoomGatewayReportAssetsLoadedRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(RoomGatewayGetSnapshotRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }

    public sealed class RoomGatewaySessionFlow
    {
        private readonly IRoomGatewaySessionClient _client;

        public RoomGatewaySessionFlow(IRoomGatewaySessionClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        [Obsolete("Use staged flow (CreateRoomAsync -> JoinRoomAsync -> SetReadyAsync -> BeginLoadingAsync -> ReportAssetsLoadedAsync -> WaitForBattleStartAsync -> SubscribeStateSyncAsync).")]
        public async Task<RoomGatewaySessionFlowResult> CreateReadyStartAndSubscribeAsync(
            string sessionToken,
            RoomGatewayLaunchSpec launchSpec,
            uint playerId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            ValidatePlayerId(playerId);

            var create = await _client.CreateRoomAsync(
                new RoomGatewayCreateRequest(sessionToken, launchSpec.Region, launchSpec.ServerId, launchSpec.RoomType, launchSpec.RoomTitle, true, launchSpec.MaxPlayers, launchSpec.Tags),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(create.Success, create.Message, "create room");

            var join = await _client.JoinRoomAsync(
                new RoomGatewayJoinRequest(sessionToken, launchSpec.Region, launchSpec.ServerId, create.RoomId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(join.Success, join.Message, "join room");

            var ready = await _client.SetReadyAsync(
                new RoomGatewayReadyRequest(sessionToken, create.RoomId, true),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(ready.Success, ready.Message, "set ready");

            return await CompleteStagedLaunchAsync(
                sessionToken,
                create.RoomId,
                create.NumericRoomId,
                SelectPlayerId(join.CurrentPlayerId, playerId),
                join.WorldStartAnchor,
                ready.CanStart,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        [Obsolete("Use staged flow (JoinRoomAsync -> SetReadyAsync -> BeginLoadingAsync -> ReportAssetsLoadedAsync -> WaitForBattleStartAsync -> SubscribeStateSyncAsync).")]
        public async Task<RoomGatewaySessionFlowResult> JoinReadyStartAndSubscribeAsync(
            string sessionToken,
            string roomId,
            RoomGatewayLaunchSpec launchSpec,
            uint playerId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            ValidatePlayerId(playerId);

            var join = await _client.JoinRoomAsync(
                new RoomGatewayJoinRequest(sessionToken, launchSpec.Region, launchSpec.ServerId, roomId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(join.Success, join.Message, "join room");

            if (join.JoinKind != RoomGatewaySessionEntryKind.TeamLobby && !string.IsNullOrWhiteSpace(join.BattleId))
            {
                var runningSubscribe = await _client.SubscribeStateSyncAsync(
                    new RoomGatewayStateSyncSubscriptionRequest(sessionToken, join.BattleId, roomId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                EnsureSuccess(runningSubscribe.Success, runningSubscribe.Message, "subscribe state sync");

                return new RoomGatewaySessionFlowResult(
                    sessionToken,
                    roomId,
                    join.NumericRoomId,
                    join.BattleId,
                    join.WorldId,
                    SelectPlayerId(join.CurrentPlayerId, playerId),
                    join.WorldStartAnchor,
                    join.ServerNowTicks,
                    join.JoinKind,
                    join.CanStart,
                    started: true,
                    runningSubscribe.Success,
                    runningSubscribe.Message);
            }

            var ready = await _client.SetReadyAsync(
                new RoomGatewayReadyRequest(sessionToken, roomId, true),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(ready.Success, ready.Message, "set ready");

            return await CompleteStagedLaunchAsync(
                sessionToken,
                roomId,
                join.NumericRoomId,
                SelectPlayerId(join.CurrentPlayerId, playerId),
                join.WorldStartAnchor,
                ready.CanStart,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        public Task<RoomGatewaySessionFlowResult> RestoreRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            RoomGatewayLaunchSpec launchSpec,
            uint playerId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            return RestoreRoomAsync(
                sessionToken,
                region,
                serverId,
                launchSpec,
                playerId,
                string.Empty,
                0L,
                timeout,
                cancellationToken);
        }

        public async Task<RoomGatewaySessionFlowResult> RestoreRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            RoomGatewayLaunchSpec launchSpec,
            uint playerId,
            string eventEpoch,
            long lastEventAck,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(region)) throw new ArgumentException("region is required.", nameof(region));
            if (string.IsNullOrWhiteSpace(serverId)) throw new ArgumentException("serverId is required.", nameof(serverId));
            ValidatePlayerId(playerId);

            var restored = await _client.RestoreRoomAsync(
                new RoomGatewayRestoreRoomRequest(sessionToken, region, serverId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(restored.Success, restored.Message, "restore room");

            if (!restored.HasActiveRoom || string.IsNullOrWhiteSpace(restored.RoomId))
            {
                throw new InvalidOperationException("restore room did not find an active room.");
            }

            if (!restored.IsInBattle || string.IsNullOrWhiteSpace(restored.BattleId))
            {
                throw new InvalidOperationException("restore room did not find a running battle.");
            }

            var subscribe = await _client.SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(
                    sessionToken,
                    restored.BattleId,
                    restored.RoomId,
                    eventEpoch,
                    lastEventAck),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(subscribe.Success, subscribe.Message, "subscribe state sync");

            return new RoomGatewaySessionFlowResult(
                sessionToken,
                restored.RoomId,
                restored.NumericRoomId,
                restored.BattleId,
                restored.WorldId,
                SelectPlayerId(restored.CurrentPlayerId, playerId),
                restored.WorldStartAnchor,
                restored.ServerNowTicks,
                restored.JoinKind,
                restored.CanStart,
                started: true,
                subscribe.Success,
                subscribe.Message,
                restored.Status,
                restored.ErrorCode);
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

        /// <summary>
        /// 阶段 3：配置出战（PickHero）。
        /// </summary>
        public Task<RoomGatewayPickHeroResult> ConfigureLoadoutAsync(
            RoomGatewayPickHeroRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(request.SessionToken);
            return _client.PickHeroAsync(request, timeout, cancellationToken);
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
            return _client.BeginLoadingAsync(request, timeout, cancellationToken);
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
            return _client.ReportAssetsLoadedAsync(request, timeout, cancellationToken);
        }

        /// <summary>
        /// 阶段 7：等待战斗提交完成。
        /// 通过 GetSnapshot 轮询，直到 Phase=InBattle 且 BattleId 已回填或超时。
        /// Starting 仅表示服务端正在初始化战斗运行时，尚不可订阅状态同步。
        /// </summary>
        public async Task<RoomGatewayGetSnapshotResult> WaitForBattleStartAsync(
            string sessionToken,
            string roomId,
            TimeSpan pollInterval,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(roomId)) throw new ArgumentException("roomId is required.", nameof(roomId));
            if (pollInterval <= TimeSpan.Zero) pollInterval = TimeSpan.FromMilliseconds(500);

            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var snapshot = await _client.GetSnapshotAsync(
                    new RoomGatewayGetSnapshotRequest(sessionToken, roomId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);

                if (snapshot.Success &&
                    snapshot.Snapshot != null &&
                    snapshot.Snapshot.Phase == RoomGatewaySessionPhase.InBattle &&
                    !string.IsNullOrWhiteSpace(snapshot.Snapshot.BattleId))
                {
                    return snapshot;
                }

                try
                {
                    await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }

            throw new TimeoutException($"WaitForBattleStart timed out after {timeout} for room {roomId}.");
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
            ValidateSessionToken(sessionToken);
            if (string.IsNullOrWhiteSpace(battleId)) throw new ArgumentException("battleId is required.", nameof(battleId));
            return _client.SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(sessionToken, battleId, roomId),
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

            var restored = await _client.RestoreRoomAsync(
                new RoomGatewayRestoreRoomRequest(sessionToken, region, serverId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(restored.Success, restored.Message, "restore room");

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
                    restored.Message);
            }

            var phase = restored.IsInBattle
                ? RoomGatewaySessionPhase.InBattle
                : RoomGatewaySessionPhase.Lobby;
            var nextStep = ResolveNextStep(phase, restored.IsInBattle);

            return new RoomGatewayStagedRestoreResult(
                restored.RoomId,
                restored.NumericRoomId,
                null,
                phase,
                nextStep,
                SelectPlayerId(restored.CurrentPlayerId, playerId),
                restored.ServerNowTicks,
                restored.Message);
        }

        private static RoomGatewaySessionPhase ResolvePhase(RoomGatewaySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return RoomGatewaySessionPhase.Lobby;
            }

            return snapshot.Phase;
        }

        private static RoomGatewayStagedRestoreNextStep ResolveNextStep(RoomGatewaySessionPhase phase, bool isInBattle)
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
                    return isInBattle
                        ? RoomGatewayStagedRestoreNextStep.SubscribeStateSync
                        : RoomGatewayStagedRestoreNextStep.WaitForBattleStart;
                default:
                    return RoomGatewayStagedRestoreNextStep.None;
            }
        }

        private async Task<RoomGatewaySessionFlowResult> CompleteStagedLaunchAsync(
            string sessionToken,
            string roomId,
            ulong numericRoomId,
            uint playerId,
            RoomGatewayWorldStartAnchor fallbackAnchor,
            bool canStart,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            var begin = await _client.BeginLoadingAsync(
                new RoomGatewayBeginLoadingRequest(sessionToken, roomId, null, Guid.NewGuid().ToString("N")),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(begin.Success, begin.Message, "begin loading");
            if (begin.Snapshot == null)
            {
                throw new InvalidOperationException("Room gateway begin loading did not return a snapshot.");
            }

            var report = await _client.ReportAssetsLoadedAsync(
                new RoomGatewayReportAssetsLoadedRequest(
                    sessionToken,
                    roomId,
                    begin.Snapshot.LaunchGeneration,
                    begin.Snapshot.LaunchManifestVersion,
                    begin.Snapshot.LaunchManifestHash,
                    Guid.NewGuid().ToString("N")),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(report.Success, report.Message, "report assets loaded");

            var waitTimeout = timeout ?? TimeSpan.FromSeconds(30);
            var running = await WaitForBattleStartAsync(
                sessionToken,
                roomId,
                TimeSpan.FromMilliseconds(50),
                waitTimeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(running.Success, running.Message, "wait for battle start");
            if (running.Snapshot == null)
            {
                throw new InvalidOperationException("Room gateway battle start polling did not return a snapshot.");
            }

            var battleId = SelectBattleId(running.Snapshot.BattleId, string.Empty, string.Empty);
            var subscribe = await _client.SubscribeStateSyncAsync(
                new RoomGatewayStateSyncSubscriptionRequest(sessionToken, battleId, roomId),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(subscribe.Success, subscribe.Message, "subscribe state sync");

            return new RoomGatewaySessionFlowResult(
                sessionToken,
                roomId,
                numericRoomId,
                battleId,
                running.Snapshot.WorldId,
                playerId,
                SelectWorldStartAnchor(running.Snapshot.WorldStartAnchor, fallbackAnchor),
                running.ServerNowTicks,
                RoomGatewaySessionEntryKind.TeamLobby,
                canStart,
                started: true,
                subscribe.Success,
                subscribe.Message);
        }

        private static uint SelectPlayerId(uint serverPlayerId, uint fallbackPlayerId)
        {
            return serverPlayerId == 0u ? fallbackPlayerId : serverPlayerId;
        }

        private static RoomGatewayWorldStartAnchor SelectWorldStartAnchor(RoomGatewayWorldStartAnchor startAnchor, RoomGatewayWorldStartAnchor joinAnchor)
        {
            return startAnchor.IsValid ? startAnchor : joinAnchor;
        }

        private static string SelectBattleId(string startBattleId, string readyBattleId, string joinBattleId)
        {
            var battleId = string.IsNullOrWhiteSpace(startBattleId) ? readyBattleId : startBattleId;
            if (string.IsNullOrWhiteSpace(battleId)) battleId = joinBattleId;
            if (string.IsNullOrWhiteSpace(battleId)) throw new InvalidOperationException("start battle did not return a battle id.");
            return battleId;
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
        Failed = 6
    }

    public enum RoomGatewaySessionRestoreErrorCode
    {
        None = 0,
        NoAccountRoomMapping = 1,
        AccountNotInRoom = 2,
        RoomClosed = 3,
        RoomExpired = 4,
        InvalidSession = 5,
        InternalError = 6
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
        public readonly string BattleId;
        public readonly bool CanStart;
        public readonly string Message;

        public RoomGatewayReadyResult(bool success, string battleId, bool canStart, string message)
        {
            Success = success;
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

        public RoomGatewayStartBattleResult(bool success, string battleId, ulong worldId, bool started, RoomGatewayWorldStartAnchor worldStartAnchor, long serverNowTicks, string message)
        {
            Success = success;
            BattleId = battleId ?? string.Empty;
            WorldId = worldId;
            Started = started;
            WorldStartAnchor = worldStartAnchor;
            ServerNowTicks = serverNowTicks;
            Message = message ?? string.Empty;
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

        public RoomGatewayRestoreRoomResult(bool success, bool hasActiveRoom, bool isInBattle, string roomId, ulong numericRoomId, RoomGatewayWorldStartAnchor worldStartAnchor, string message, string battleId, bool canStart, RoomGatewaySessionEntryKind joinKind, long serverNowTicks, ulong worldId, RoomGatewaySessionRestoreStatus status, RoomGatewaySessionRestoreErrorCode errorCode, uint currentPlayerId = 0u)
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

        public RoomGatewaySessionFlowResult(string sessionToken, string roomId, ulong numericRoomId, string battleId, ulong worldId, uint playerId, RoomGatewayWorldStartAnchor worldStartAnchor, long serverNowTicks, RoomGatewaySessionEntryKind entryKind, bool canStart, bool started, bool subscribed, string message)
            : this(sessionToken, roomId, numericRoomId, battleId, worldId, playerId, worldStartAnchor, serverNowTicks, entryKind, canStart, started, subscribed, message, RoomGatewaySessionRestoreStatus.Restored, RoomGatewaySessionRestoreErrorCode.None)
        {
        }

        public RoomGatewaySessionFlowResult(string sessionToken, string roomId, ulong numericRoomId, string battleId, ulong worldId, uint playerId, RoomGatewayWorldStartAnchor worldStartAnchor, long serverNowTicks, RoomGatewaySessionEntryKind entryKind, bool canStart, bool started, bool subscribed, string message, RoomGatewaySessionRestoreStatus restoreStatus, RoomGatewaySessionRestoreErrorCode restoreErrorCode)
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
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly RoomGatewaySnapshot Snapshot;
        public readonly string Message;

        public RoomGatewayPickHeroResult(bool success, string roomId, ulong numericRoomId, RoomGatewaySnapshot snapshot, string message)
        {
            Success = success;
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
        public readonly RoomGatewaySnapshot Snapshot;

        public RoomGatewayBeginLoadingResult(bool success, bool applied, int errorCode, string message, long roomRevision, RoomGatewaySnapshot snapshot)
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
        public readonly RoomGatewaySnapshot Snapshot;

        public RoomGatewayReportAssetsLoadedResult(bool success, bool applied, int errorCode, string message, long roomRevision, RoomGatewaySnapshot snapshot)
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
        public readonly RoomGatewaySnapshot Snapshot;
        public readonly string Message;
        public readonly long ServerNowTicks;

        public RoomGatewayGetSnapshotResult(bool success, string roomId, ulong numericRoomId, RoomGatewaySnapshot snapshot, string message)
            : this(success, roomId, numericRoomId, snapshot, message, 0L)
        {
        }

        public RoomGatewayGetSnapshotResult(bool success, string roomId, ulong numericRoomId, RoomGatewaySnapshot snapshot, string message, long serverNowTicks)
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
    public sealed class RoomGatewaySnapshot
    {
        public string RoomId { get; set; } = string.Empty;
        public RoomGatewaySessionPhase Phase { get; set; }
        public string PhaseReason { get; set; } = string.Empty;
        public long LaunchGeneration { get; set; }
        public long LoadingDeadlineUnixMs { get; set; }
        public string LaunchManifestHash { get; set; } = string.Empty;
        public int LaunchManifestVersion { get; set; }
        public string LastStartFailureCode { get; set; } = string.Empty;
        public long RoomRevision { get; set; }
        public long LastEventSequence { get; set; }
        public bool CanStart { get; set; }
        public string BattleId { get; set; } = string.Empty;
        public ulong WorldId { get; set; }
        public RoomGatewayWorldStartAnchor WorldStartAnchor { get; set; }
    }

    /// <summary>
    /// 阶段化恢复结果。
    /// </summary>
    public readonly struct RoomGatewayStagedRestoreResult
    {
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly RoomGatewaySnapshot Snapshot;
        public readonly RoomGatewaySessionPhase Phase;
        public readonly RoomGatewayStagedRestoreNextStep NextStep;
        public readonly uint PlayerId;
        public readonly long ServerNowTicks;
        public readonly string Message;

        public RoomGatewayStagedRestoreResult(
            string roomId,
            ulong numericRoomId,
            RoomGatewaySnapshot snapshot,
            RoomGatewaySessionPhase phase,
            RoomGatewayStagedRestoreNextStep nextStep,
            uint playerId,
            long serverNowTicks,
            string message)
        {
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            Snapshot = snapshot;
            Phase = phase;
            NextStep = nextStep;
            PlayerId = playerId;
            ServerNowTicks = serverNowTicks;
            Message = message ?? string.Empty;
        }
    }
}
