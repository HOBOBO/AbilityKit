#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Extensions.Client.FrameSync;
using AbilityKit.Network.Room;
using AbilityKit.Game.View.Loading;

namespace AbilityKit.Demo.Shooter.View
{
    public sealed class ShooterRoomGatewayFlow : IDisposable
    {
        private readonly RoomGatewaySessionFlow _flow;
        private readonly IDisposable _sessionClient;
        private readonly ClientLoadingPipelineDefinition _loadingDefinition;
        private readonly IShooterClientLoadingStepProvider _loadingStepProvider;

        public ShooterRoomGatewayFlow(
            IShooterRoomGatewayRoomClient roomClient,
            ClientLoadingPipelineDefinition? loadingDefinition = null,
            IShooterClientLoadingStepProvider? loadingStepProvider = null)
        {
            if (roomClient == null) throw new ArgumentNullException(nameof(roomClient));
            var sessionClient = new ShooterRoomGatewaySessionClient(roomClient);
            _sessionClient = sessionClient;
            _flow = new RoomGatewaySessionFlow(sessionClient);
            _loadingDefinition = loadingDefinition ?? ShooterClientLoadingPipelineDefaults.CreateDefinition();
            _loadingStepProvider = loadingStepProvider ?? new DefaultShooterClientLoadingStepProvider();
        }

        public ShooterRoomGatewayFlow(
            IShooterRoomGatewayRequestTransport transport,
            ClientLoadingPipelineDefinition? loadingDefinition = null,
            IShooterClientLoadingStepProvider? loadingStepProvider = null)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            var sessionClient = new RoomGatewayWireSessionClient(
                transport,
                transport as IRoomGatewayPushSource);
            _sessionClient = sessionClient;
            _flow = new RoomGatewaySessionFlow(sessionClient);
            _loadingDefinition = loadingDefinition ?? ShooterClientLoadingPipelineDefaults.CreateDefinition();
            _loadingStepProvider = loadingStepProvider ?? new DefaultShooterClientLoadingStepProvider();
        }

        internal RoomGatewaySessionFlow StagedFlow => _flow;

        public async Task<ShooterRoomGatewayFlowResult> CreateReadyStartAndSubscribeAsync(
            string sessionToken,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidatePlayerId(playerId);
            ShooterMultiplayerLoadingStatus.Reset();
            var roomLaunchSpec = ToLaunchSpec(in launchSpec);
            var roomId = await _flow.CreateRoomAsync(
                sessionToken,
                roomLaunchSpec,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var result = await JoinAndLaunchAsync(
                sessionToken,
                roomId,
                roomLaunchSpec,
                playerId,
                createdRoomOwner: true,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToShooterResult(in result);
        }

        public async Task<ShooterRoomGatewayFlowResult> JoinReadyStartAndSubscribeAsync(
            string sessionToken,
            string roomId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidatePlayerId(playerId);
            ShooterMultiplayerLoadingStatus.Reset();
            var result = await JoinAndLaunchAsync(
                sessionToken,
                roomId,
                ToLaunchSpec(in launchSpec),
                playerId,
                createdRoomOwner: false,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return ToShooterResult(in result);
        }

        private async Task<RoomGatewaySessionFlowResult> JoinAndLaunchAsync(
            string sessionToken,
            string roomId,
            RoomGatewayLaunchSpec launchSpec,
            uint playerId,
            bool createdRoomOwner,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            var join = await _flow.JoinRoomAsync(
                sessionToken,
                launchSpec.Region,
                launchSpec.ServerId,
                roomId,
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(join.Success, join.Message, "join room");

            var metadata = new LaunchMetadata(
                sessionToken,
                roomId,
                join.NumericRoomId,
                SelectPlayerId(join.CurrentPlayerId, playerId),
                join.WorldStartAnchor,
                join.ServerNowTicks,
                join.JoinKind,
                join.CanStart,
                RoomGatewaySessionRestoreStatus.Restored,
                RoomGatewaySessionRestoreErrorCode.None);
            if (join.JoinKind != RoomGatewaySessionEntryKind.TeamLobby &&
                !string.IsNullOrWhiteSpace(join.BattleId))
            {
                var runningSnapshot = new RoomGatewaySnapshot
                {
                    RoomId = roomId,
                    Phase = RoomGatewaySessionPhase.InBattle,
                    BattleId = join.BattleId,
                    WorldId = join.WorldId,
                    WorldStartAnchor = join.WorldStartAnchor,
                    CanStart = join.CanStart
                };
                return await SubscribeRunningBattleAsync(
                    metadata,
                    runningSnapshot,
                    join.ServerNowTicks,
                    string.Empty,
                    0L,
                    timeout,
                    cancellationToken).ConfigureAwait(false);
            }

            var ready = await _flow.SetReadyAsync(
                sessionToken,
                roomId,
                ready: true,
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(ready.Success, ready.Message, "set ready");

            metadata = metadata.WithCanStart(ready.CanStart);
            return await CoordinateLoadingAsync(
                metadata,
                createdRoomOwner,
                null,
                string.Empty,
                0L,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RoomGatewaySessionFlowResult> CoordinateLoadingAsync(
            LaunchMetadata metadata,
            bool ownerFallback,
            RoomGatewaySnapshot? initialSnapshot,
            string eventEpoch,
            long lastEventAck,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            if (ownerFallback && metadata.CanStart)
            {
                return await BeginLoadingAndCompleteAsync(
                    metadata,
                    eventEpoch,
                    lastEventAck,
                    timeout,
                    cancellationToken).ConfigureAwait(false);
            }

            ShooterMultiplayerLoadingStatus.Update(0, "Waiting for room loading", initialSnapshot);
            RoomGatewayGetSnapshotResult coordinated;
            if (initialSnapshot != null &&
                (initialSnapshot.Phase != RoomGatewaySessionPhase.Lobby ||
                 (initialSnapshot.CanStart && IsLocalOwner(initialSnapshot, metadata.PlayerId, ownerFallback))))
            {
                coordinated = new RoomGatewayGetSnapshotResult(
                    success: true,
                    metadata.RoomId,
                    metadata.NumericRoomId,
                    initialSnapshot,
                    string.Empty,
                    metadata.ServerNowTicks);
            }
            else
            {
                coordinated = await _flow.WaitForSnapshotAsync(
                    metadata.SessionToken,
                    metadata.RoomId,
                    snapshot => snapshot.Phase != RoomGatewaySessionPhase.Lobby ||
                                (snapshot.CanStart && IsLocalOwner(snapshot, metadata.PlayerId, ownerFallback)),
                    TimeSpan.FromMilliseconds(500),
                    timeout ?? TimeSpan.FromSeconds(135),
                    new ImmediateProgress<RoomGatewaySnapshot>(ShooterMultiplayerLoadingStatus.UpdateSnapshot),
                    cancellationToken).ConfigureAwait(false);
            }
            EnsureSuccess(coordinated.Success, coordinated.Message, "coordinate room loading");
            var snapshot = coordinated.Snapshot
                ?? throw new InvalidOperationException("Room gateway coordination returned no snapshot.");
            metadata = metadata.WithCanStart(snapshot.CanStart);

            switch (snapshot.Phase)
            {
                case RoomGatewaySessionPhase.Lobby:
                    if (!snapshot.CanStart || !IsLocalOwner(snapshot, metadata.PlayerId, ownerFallback))
                    {
                        throw new InvalidOperationException("Only the ready room owner can begin loading.");
                    }

                    return await BeginLoadingAndCompleteAsync(
                        metadata,
                        eventEpoch,
                        lastEventAck,
                        timeout,
                        cancellationToken).ConfigureAwait(false);
                case RoomGatewaySessionPhase.Loading:
                    return await ReportAssetsLoadedAndCompleteAsync(
                        metadata,
                        snapshot,
                        eventEpoch,
                        lastEventAck,
                        timeout,
                        cancellationToken).ConfigureAwait(false);
                case RoomGatewaySessionPhase.Starting:
                    return await WaitAndSubscribeAsync(
                        metadata,
                        eventEpoch,
                        lastEventAck,
                        timeout,
                        cancellationToken).ConfigureAwait(false);
                case RoomGatewaySessionPhase.InBattle:
                    return await SubscribeRunningBattleAsync(
                        metadata,
                        snapshot,
                        coordinated.ServerNowTicks,
                        eventEpoch,
                        lastEventAck,
                        timeout,
                        cancellationToken).ConfigureAwait(false);
                default:
                    throw new InvalidOperationException(
                        $"Room gateway cannot continue loading from phase {snapshot.Phase}.");
            }
        }

        private static bool IsLocalOwner(
            RoomGatewaySnapshot snapshot,
            uint playerId,
            bool ownerFallback)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.OwnerAccountId) && snapshot.Players != null)
            {
                for (var i = 0; i < snapshot.Players.Count; i++)
                {
                    var player = snapshot.Players[i];
                    if (player.PlayerId == playerId)
                    {
                        return string.Equals(
                            player.AccountId,
                            snapshot.OwnerAccountId,
                            StringComparison.Ordinal);
                    }
                }
            }

            return ownerFallback;
        }

        private async Task<RoomGatewaySessionFlowResult> BeginLoadingAndCompleteAsync(
            LaunchMetadata metadata,
            string eventEpoch,
            long lastEventAck,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            var begin = await _flow.BeginLoadingAsync(
                new RoomGatewayBeginLoadingRequest(
                    metadata.SessionToken,
                    metadata.RoomId,
                    expectedRevision: null,
                    commandId: Guid.NewGuid().ToString("N")),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(begin.Success, begin.Message, "begin loading");
            if (begin.Snapshot == null)
            {
                throw new InvalidOperationException("Room gateway begin loading did not return a snapshot.");
            }

            ShooterMultiplayerLoadingStatus.Begin(begin.Snapshot, "Room loading generation accepted");

            return await ReportAssetsLoadedAndCompleteAsync(
                metadata,
                begin.Snapshot,
                eventEpoch,
                lastEventAck,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RoomGatewaySessionFlowResult> ReportAssetsLoadedAndCompleteAsync(
            LaunchMetadata metadata,
            RoomGatewaySnapshot loadingSnapshot,
            string eventEpoch,
            long lastEventAck,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            await PrepareAndReportAssetsLoadedAsync(
                metadata.SessionToken,
                metadata.RoomId,
                metadata.PlayerId,
                loadingSnapshot,
                eventEpoch,
                lastEventAck,
                timeout,
                cancellationToken).ConfigureAwait(false);

            return await WaitAndSubscribeAsync(
                metadata,
                eventEpoch,
                lastEventAck,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        internal Task<RoomGatewayReportAssetsLoadedResult> PrepareAndReportAssetsLoadedAsync(
            string sessionToken,
            string roomId,
            uint playerId,
            ShooterRoomSessionSnapshot loadingSnapshot,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            if (loadingSnapshot == null) throw new ArgumentNullException(nameof(loadingSnapshot));
            var anchor = loadingSnapshot.WorldStartAnchor;
            var roomSnapshot = new RoomGatewaySnapshot
            {
                RoomId = loadingSnapshot.RoomId,
                OwnerAccountId = loadingSnapshot.OwnerAccountId,
                Phase = (RoomGatewaySessionPhase)loadingSnapshot.Phase,
                PhaseReason = loadingSnapshot.PhaseReason,
                LaunchGeneration = loadingSnapshot.LaunchGeneration,
                LaunchManifestHash = loadingSnapshot.LaunchManifestHash,
                LaunchManifestVersion = loadingSnapshot.LaunchManifestVersion,
                RoomRevision = loadingSnapshot.RoomRevision,
                CanStart = loadingSnapshot.CanStart,
                BattleId = loadingSnapshot.BattleId,
                WorldId = loadingSnapshot.WorldId,
                WorldStartAnchor = ToRoomAnchor(in anchor)
            };
            return PrepareAndReportAssetsLoadedAsync(
                sessionToken,
                roomId,
                playerId,
                roomSnapshot,
                string.Empty,
                0L,
                timeout,
                cancellationToken);
        }

        private async Task<RoomGatewayReportAssetsLoadedResult> PrepareAndReportAssetsLoadedAsync(
            string sessionToken,
            string roomId,
            uint playerId,
            RoomGatewaySnapshot loadingSnapshot,
            string eventEpoch,
            long lastEventAck,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            if (loadingSnapshot.LaunchGeneration <= 0)
            {
                throw new InvalidOperationException("Shooter loading snapshot has no launch generation.");
            }

            ShooterMultiplayerLoadingStatus.Begin(loadingSnapshot, "Preparing client loading pipeline");
            var loadingContext = new ShooterClientLoadingContext(
                sessionToken,
                roomId,
                playerId,
                loadingSnapshot,
                eventEpoch,
                lastEventAck);
            var resolver = _loadingStepProvider.CreateResolver(in loadingContext);
            var pipeline = new ClientLoadingPipeline(_loadingDefinition, resolver);
            var relay = new ClientLoadingProgressRelay();
            using var uploadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var uploadTask = relay.UploadUntilCompletedAsync(
                async (progress, ct) =>
                {
                    var result = await _flow.ReportLoadingProgressAsync(
                        new RoomGatewayReportLoadingProgressRequest(
                            sessionToken,
                            roomId,
                            loadingSnapshot.LaunchGeneration,
                            loadingSnapshot.LaunchManifestVersion,
                            loadingSnapshot.LaunchManifestHash,
                            progress),
                        timeout,
                        ct).ConfigureAwait(false);
                    EnsureSuccess(result.Success, result.Message, "report loading progress");
                    ShooterMultiplayerLoadingStatus.Update(
                        progress,
                        relay.LatestStageId,
                        result.Snapshot ?? loadingSnapshot);
                },
                cancellationToken: uploadCancellation.Token);

            try
            {
                await pipeline.ExecuteAsync(
                    new ImmediateProgress<ClientLoadingProgress>(progress =>
                    {
                        relay.Report(progress);
                        ShooterMultiplayerLoadingStatus.Update(
                            progress.OverallProgress,
                            progress.StageId,
                            loadingSnapshot);
                    }),
                    cancellationToken).ConfigureAwait(false);
                relay.Complete();
                await uploadTask.ConfigureAwait(false);
            }
            catch
            {
                uploadCancellation.Cancel();
                try
                {
                    await uploadTask.ConfigureAwait(false);
                }
                catch (Exception)
                {
                }

                ShooterMultiplayerLoadingStatus.Fail(roomId, loadingSnapshot.LaunchGeneration, "Client loading failed");
                throw;
            }

            var report = await _flow.ReportAssetsLoadedAsync(
                new RoomGatewayReportAssetsLoadedRequest(
                    sessionToken,
                    roomId,
                    loadingSnapshot.LaunchGeneration,
                    loadingSnapshot.LaunchManifestVersion,
                    loadingSnapshot.LaunchManifestHash,
                    Guid.NewGuid().ToString("N")),
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(report.Success, report.Message, "report assets loaded");
            ShooterMultiplayerLoadingStatus.Update(100, "Waiting for all players", report.Snapshot);
            return report;
        }

        private async Task<RoomGatewaySessionFlowResult> WaitAndSubscribeAsync(
            LaunchMetadata metadata,
            string eventEpoch,
            long lastEventAck,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            var running = await _flow.WaitForBattleStartAsync(
                metadata.SessionToken,
                metadata.RoomId,
                TimeSpan.FromSeconds(2),
                timeout ?? TimeSpan.FromSeconds(135),
                new ImmediateProgress<RoomGatewaySnapshot>(ShooterMultiplayerLoadingStatus.UpdateSnapshot),
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(running.Success, running.Message, "wait for battle start");
            if (running.Snapshot == null)
            {
                throw new InvalidOperationException("Room gateway battle start polling did not return a snapshot.");
            }

            return await SubscribeRunningBattleAsync(
                metadata,
                running.Snapshot,
                running.ServerNowTicks,
                eventEpoch,
                lastEventAck,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<RoomGatewaySessionFlowResult> SubscribeRunningBattleAsync(
            LaunchMetadata metadata,
            RoomGatewaySnapshot runningSnapshot,
            long serverNowTicks,
            string eventEpoch,
            long lastEventAck,
            TimeSpan? timeout,
            CancellationToken cancellationToken)
        {
            if (runningSnapshot == null || string.IsNullOrWhiteSpace(runningSnapshot.BattleId))
            {
                throw new InvalidOperationException("Room gateway battle start did not return a battle id.");
            }
            var subscribe = await _flow.SubscribeStateSyncAsync(
                metadata.SessionToken,
                runningSnapshot.BattleId,
                metadata.RoomId,
                eventEpoch,
                lastEventAck,
                timeout,
                cancellationToken).ConfigureAwait(false);
            EnsureSuccess(subscribe.Success, subscribe.Message, "subscribe state sync");
            ShooterMultiplayerLoadingStatus.MarkStarted(runningSnapshot);

            return new RoomGatewaySessionFlowResult(
                metadata.SessionToken,
                metadata.RoomId,
                metadata.NumericRoomId,
                runningSnapshot.BattleId,
                runningSnapshot.WorldId,
                metadata.PlayerId,
                runningSnapshot.WorldStartAnchor.IsValid
                    ? runningSnapshot.WorldStartAnchor
                    : metadata.FallbackAnchor,
                serverNowTicks != 0L ? serverNowTicks : metadata.ServerNowTicks,
                metadata.EntryKind,
                metadata.CanStart,
                started: true,
                subscribed: true,
                message: subscribe.Message,
                metadata.RestoreStatus,
                metadata.RestoreErrorCode);
        }

        private static uint SelectPlayerId(uint serverPlayerId, uint fallbackPlayerId)
        {
            return serverPlayerId == 0u ? fallbackPlayerId : serverPlayerId;
        }

        private static void ValidatePlayerId(uint playerId)
        {
            if (playerId == 0u) throw new ArgumentOutOfRangeException(nameof(playerId));
        }

        private static void EnsureSuccess(bool success, string message, string operation)
        {
            if (!success) throw new InvalidOperationException($"Room gateway {operation} failed: {message}");
        }

        public Task<ShooterRoomGatewayFlowResult> RestoreRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            ShooterRoomLaunchSpec launchSpec,
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

        public async Task<ShooterRoomGatewayFlowResult> RestoreRoomAsync(
            string sessionToken,
            string region,
            string serverId,
            ShooterRoomLaunchSpec launchSpec,
            uint playerId,
            string eventEpoch,
            long lastEventAck,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            ValidatePlayerId(playerId);
            ShooterMultiplayerLoadingStatus.Reset();
            var restored = await _flow.RestoreAsync(
                sessionToken,
                region,
                serverId,
                playerId,
                timeout,
                cancellationToken).ConfigureAwait(false);
            if (restored.NextStep == RoomGatewayStagedRestoreNextStep.None ||
                string.IsNullOrWhiteSpace(restored.RoomId) ||
                restored.Snapshot == null)
            {
                var failureAnchor = restored.Snapshot?.WorldStartAnchor ?? default;
                var shooterAnchor = ToShooterAnchor(failureAnchor);
                return new ShooterRoomGatewayFlowResult(
                    sessionToken,
                    restored.RoomId,
                    restored.NumericRoomId,
                    restored.Snapshot?.BattleId ?? string.Empty,
                    restored.Snapshot?.WorldId ?? 0UL,
                    restored.PlayerId,
                    in shooterAnchor,
                    restored.ServerNowTicks,
                    ToShooterEntryKind(restored.EntryKind),
                    restored.CanStart,
                    started: false,
                    subscribed: false,
                    restored.Message,
                    ToShooterRestoreStatus(restored.RestoreStatus),
                    ToShooterRestoreErrorCode(restored.RestoreErrorCode));
            }

            var metadata = new LaunchMetadata(
                sessionToken,
                restored.RoomId,
                restored.NumericRoomId,
                restored.PlayerId,
                restored.Snapshot.WorldStartAnchor,
                restored.ServerNowTicks,
                restored.EntryKind,
                restored.CanStart,
                restored.RestoreStatus,
                restored.RestoreErrorCode);

            RoomGatewaySessionFlowResult result;
            switch (restored.NextStep)
            {
                case RoomGatewayStagedRestoreNextStep.SetReadyAndBeginLoading:
                {
                    var ready = await _flow.SetReadyAsync(
                        sessionToken,
                        restored.RoomId,
                        ready: true,
                        timeout,
                        cancellationToken).ConfigureAwait(false);
                    EnsureSuccess(ready.Success, ready.Message, "set ready");
                    metadata = metadata.WithCanStart(ready.CanStart);
                    result = await CoordinateLoadingAsync(
                        metadata,
                        false,
                        restored.Snapshot,
                        eventEpoch,
                        lastEventAck,
                        timeout,
                        cancellationToken).ConfigureAwait(false);
                    break;
                }
                case RoomGatewayStagedRestoreNextStep.ReportAssetsLoaded:
                    result = await ReportAssetsLoadedAndCompleteAsync(
                        metadata,
                        restored.Snapshot,
                        eventEpoch,
                        lastEventAck,
                        timeout,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case RoomGatewayStagedRestoreNextStep.WaitForBattleStart:
                    result = await WaitAndSubscribeAsync(
                        metadata,
                        eventEpoch,
                        lastEventAck,
                        timeout,
                        cancellationToken).ConfigureAwait(false);
                    break;
                case RoomGatewayStagedRestoreNextStep.SubscribeStateSync:
                    result = await SubscribeRunningBattleAsync(
                        metadata,
                        restored.Snapshot,
                        restored.ServerNowTicks,
                        eventEpoch,
                        lastEventAck,
                        timeout,
                        cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"restore room cannot continue from phase {restored.Phase}.");
            }

            return ToShooterResult(in result);
        }

        private readonly struct LaunchMetadata
        {
            public LaunchMetadata(
                string sessionToken,
                string roomId,
                ulong numericRoomId,
                uint playerId,
                RoomGatewayWorldStartAnchor fallbackAnchor,
                long serverNowTicks,
                RoomGatewaySessionEntryKind entryKind,
                bool canStart,
                RoomGatewaySessionRestoreStatus restoreStatus,
                RoomGatewaySessionRestoreErrorCode restoreErrorCode)
            {
                SessionToken = sessionToken;
                RoomId = roomId;
                NumericRoomId = numericRoomId;
                PlayerId = playerId;
                FallbackAnchor = fallbackAnchor;
                ServerNowTicks = serverNowTicks;
                EntryKind = entryKind;
                CanStart = canStart;
                RestoreStatus = restoreStatus;
                RestoreErrorCode = restoreErrorCode;
            }

            public string SessionToken { get; }
            public string RoomId { get; }
            public ulong NumericRoomId { get; }
            public uint PlayerId { get; }
            public RoomGatewayWorldStartAnchor FallbackAnchor { get; }
            public long ServerNowTicks { get; }
            public RoomGatewaySessionEntryKind EntryKind { get; }
            public bool CanStart { get; }
            public RoomGatewaySessionRestoreStatus RestoreStatus { get; }
            public RoomGatewaySessionRestoreErrorCode RestoreErrorCode { get; }

            public LaunchMetadata WithCanStart(bool canStart)
            {
                return new LaunchMetadata(
                    SessionToken,
                    RoomId,
                    NumericRoomId,
                    PlayerId,
                    FallbackAnchor,
                    ServerNowTicks,
                    EntryKind,
                    canStart,
                    RestoreStatus,
                    RestoreErrorCode);
            }
        }

        internal static RoomGatewayLaunchSpec ToLaunchSpec(in ShooterRoomLaunchSpec launchSpec)
        {
            return new RoomGatewayLaunchSpec(
                launchSpec.Region,
                launchSpec.ServerId,
                ShooterGameplay.RoomType,
                launchSpec.RoomTitle,
                launchSpec.MaxPlayers,
                launchSpec.GameplayId,
                launchSpec.RuleSetId,
                launchSpec.ConfigVersion,
                launchSpec.ProtocolVersion,
                launchSpec.WorldType,
                launchSpec.ClientId,
                launchSpec.Tags,
                launchSpec.SyncTemplateId,
                launchSpec.SyncModel,
                launchSpec.NetworkEnvironmentId,
                launchSpec.CarrierName,
                launchSpec.EnableAuthoritativeWorld,
                launchSpec.InterpolationEnabled,
                launchSpec.InputDelayFrames);
        }

        private static ShooterRoomGatewayFlowResult ToShooterResult(in RoomGatewaySessionFlowResult result)
        {
            var anchor = ToShooterAnchor(result.WorldStartAnchor);
            return new ShooterRoomGatewayFlowResult(
                result.SessionToken,
                result.RoomId,
                result.NumericRoomId,
                result.BattleId,
                result.WorldId,
                result.PlayerId,
                in anchor,
                result.ServerNowTicks,
                ToShooterEntryKind(result.EntryKind),
                result.CanStart,
                result.Started,
                result.Subscribed,
                result.Message,
                ToShooterRestoreStatus(result.RestoreStatus),
                ToShooterRestoreErrorCode(result.RestoreErrorCode));
        }

        private sealed class ImmediateProgress<T> : IProgress<T>
        {
            private readonly Action<T> _report;

            public ImmediateProgress(Action<T> report)
            {
                _report = report ?? throw new ArgumentNullException(nameof(report));
            }

            public void Report(T value) => _report(value);
        }

        private static RoomGatewayWorldStartAnchor ToRoomAnchor(in ShooterGatewayWorldStartAnchor anchor)
        {
            return new RoomGatewayWorldStartAnchor(anchor.StartServerTicks, anchor.ServerTickFrequency, anchor.StartFrame, anchor.FixedDeltaSeconds);
        }

        private static ShooterGatewayWorldStartAnchor ToShooterAnchor(RoomGatewayWorldStartAnchor anchor)
        {
            return new ShooterGatewayWorldStartAnchor(anchor.StartServerTicks, anchor.ServerTickFrequency, anchor.StartFrame, anchor.FixedDeltaSeconds);
        }

        private static ShooterRoomGatewayEntryKind ToShooterEntryKind(RoomGatewaySessionEntryKind entryKind)
        {
            return entryKind switch
            {
                RoomGatewaySessionEntryKind.Reconnect => ShooterRoomGatewayEntryKind.Reconnect,
                RoomGatewaySessionEntryKind.LateJoin => ShooterRoomGatewayEntryKind.LateJoin,
                _ => ShooterRoomGatewayEntryKind.TeamLobby
            };
        }

        private static RoomGatewaySessionEntryKind ToRoomEntryKind(ShooterGatewayRoomJoinKind joinKind)
        {
            return joinKind switch
            {
                ShooterGatewayRoomJoinKind.Reconnect => RoomGatewaySessionEntryKind.Reconnect,
                ShooterGatewayRoomJoinKind.LateJoin => RoomGatewaySessionEntryKind.LateJoin,
                _ => RoomGatewaySessionEntryKind.TeamLobby
            };
        }

        private static ShooterGatewayRoomRestoreStatus ToShooterRestoreStatus(RoomGatewaySessionRestoreStatus status)
        {
            return status switch
            {
                RoomGatewaySessionRestoreStatus.NoActiveRoom => ShooterGatewayRoomRestoreStatus.NoActiveRoom,
                RoomGatewaySessionRestoreStatus.NotMember => ShooterGatewayRoomRestoreStatus.NotMember,
                RoomGatewaySessionRestoreStatus.RoomClosed => ShooterGatewayRoomRestoreStatus.RoomClosed,
                RoomGatewaySessionRestoreStatus.RoomExpired => ShooterGatewayRoomRestoreStatus.RoomExpired,
                RoomGatewaySessionRestoreStatus.InvalidSession => ShooterGatewayRoomRestoreStatus.InvalidSession,
                RoomGatewaySessionRestoreStatus.Timeout => ShooterGatewayRoomRestoreStatus.Timeout,
                RoomGatewaySessionRestoreStatus.Failed => ShooterGatewayRoomRestoreStatus.Failed,
                _ => ShooterGatewayRoomRestoreStatus.Restored
            };
        }

        private static RoomGatewaySessionRestoreStatus ToRoomRestoreStatus(ShooterGatewayRoomRestoreStatus status)
        {
            return status switch
            {
                ShooterGatewayRoomRestoreStatus.NoActiveRoom => RoomGatewaySessionRestoreStatus.NoActiveRoom,
                ShooterGatewayRoomRestoreStatus.NotMember => RoomGatewaySessionRestoreStatus.NotMember,
                ShooterGatewayRoomRestoreStatus.RoomClosed => RoomGatewaySessionRestoreStatus.RoomClosed,
                ShooterGatewayRoomRestoreStatus.RoomExpired => RoomGatewaySessionRestoreStatus.RoomExpired,
                ShooterGatewayRoomRestoreStatus.InvalidSession => RoomGatewaySessionRestoreStatus.InvalidSession,
                ShooterGatewayRoomRestoreStatus.Timeout => RoomGatewaySessionRestoreStatus.Timeout,
                ShooterGatewayRoomRestoreStatus.Failed => RoomGatewaySessionRestoreStatus.Failed,
                _ => RoomGatewaySessionRestoreStatus.Restored
            };
        }

        private static ShooterGatewayRoomRestoreErrorCode ToShooterRestoreErrorCode(RoomGatewaySessionRestoreErrorCode errorCode)
        {
            return errorCode switch
            {
                RoomGatewaySessionRestoreErrorCode.NoAccountRoomMapping => ShooterGatewayRoomRestoreErrorCode.NoAccountRoomMapping,
                RoomGatewaySessionRestoreErrorCode.AccountNotInRoom => ShooterGatewayRoomRestoreErrorCode.AccountNotInRoom,
                RoomGatewaySessionRestoreErrorCode.RoomClosed => ShooterGatewayRoomRestoreErrorCode.RoomClosed,
                RoomGatewaySessionRestoreErrorCode.RoomExpired => ShooterGatewayRoomRestoreErrorCode.RoomExpired,
                RoomGatewaySessionRestoreErrorCode.InvalidSession => ShooterGatewayRoomRestoreErrorCode.InvalidSession,
                RoomGatewaySessionRestoreErrorCode.Timeout => ShooterGatewayRoomRestoreErrorCode.Timeout,
                RoomGatewaySessionRestoreErrorCode.InternalError => ShooterGatewayRoomRestoreErrorCode.InternalError,
                _ => ShooterGatewayRoomRestoreErrorCode.None
            };
        }

        private static RoomGatewaySessionRestoreErrorCode ToRoomRestoreErrorCode(ShooterGatewayRoomRestoreErrorCode errorCode)
        {
            return errorCode switch
            {
                ShooterGatewayRoomRestoreErrorCode.NoAccountRoomMapping => RoomGatewaySessionRestoreErrorCode.NoAccountRoomMapping,
                ShooterGatewayRoomRestoreErrorCode.AccountNotInRoom => RoomGatewaySessionRestoreErrorCode.AccountNotInRoom,
                ShooterGatewayRoomRestoreErrorCode.RoomClosed => RoomGatewaySessionRestoreErrorCode.RoomClosed,
                ShooterGatewayRoomRestoreErrorCode.RoomExpired => RoomGatewaySessionRestoreErrorCode.RoomExpired,
                ShooterGatewayRoomRestoreErrorCode.InvalidSession => RoomGatewaySessionRestoreErrorCode.InvalidSession,
                ShooterGatewayRoomRestoreErrorCode.Timeout => RoomGatewaySessionRestoreErrorCode.Timeout,
                ShooterGatewayRoomRestoreErrorCode.InternalError => RoomGatewaySessionRestoreErrorCode.InternalError,
                _ => RoomGatewaySessionRestoreErrorCode.None
            };
        }

        private sealed class ShooterRoomGatewaySessionClient :
            IRoomGatewaySessionClientBase,
            IRoomGatewayStagedLoadingCapability,
            IRoomGatewayDirectBattleStartCapability,
            IRoomGatewayStateSyncSubscriptionCapability,
            IRoomGatewaySnapshotFeed,
            IDisposable
        {
            private readonly IShooterRoomGatewayRoomClient _roomClient;
            private readonly IShooterRoomGatewaySnapshotFeed? _snapshotFeed;

            public ShooterRoomGatewaySessionClient(IShooterRoomGatewayRoomClient roomClient)
            {
                _roomClient = roomClient ?? throw new ArgumentNullException(nameof(roomClient));
                _snapshotFeed = roomClient as IShooterRoomGatewaySnapshotFeed;
                if (_snapshotFeed != null)
                {
                    _snapshotFeed.SnapshotChanged += HandleSnapshotChanged;
                }
            }

            public RoomGatewaySnapshot? Current => ToRoomSnapshot(_snapshotFeed?.Current);

            public event Action<RoomGatewaySnapshot>? SnapshotChanged;

            private void HandleSnapshotChanged(ShooterGatewayStagedRoomSnapshot snapshot)
            {
                var projected = ToRoomSnapshot(snapshot);
                if (projected != null) SnapshotChanged?.Invoke(projected);
            }

            public async Task<RoomGatewayCreateResult> CreateRoomAsync(RoomGatewayCreateRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.CreateRoomAsync(
                    new ShooterGatewayCreateRoomRequest(request.SessionToken, request.Region, request.ServerId, request.RoomType, request.Title, request.IsPublic, request.MaxPlayers, request.Tags),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayCreateResult(result.Success, result.RoomId, result.NumericRoomId, result.Message);
            }

            public async Task<RoomGatewayJoinResult> JoinRoomAsync(RoomGatewayJoinRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.JoinRoomAsync(
                    new ShooterGatewayJoinRoomRequest(request.SessionToken, request.Region, request.ServerId, request.RoomId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayJoinResult(
                    result.Success,
                    result.RoomId,
                    result.NumericRoomId,
                    ToRoomAnchor(in result.WorldStartAnchor),
                    result.Message,
                    result.BattleId,
                    result.CanStart,
                    ToRoomEntryKind(result.JoinKind),
                    result.ServerNowTicks,
                    result.WorldId,
                    result.CurrentPlayerId);
            }

            public async Task<RoomGatewayReadyResult> SetReadyAsync(RoomGatewayReadyRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.SetReadyAsync(
                    new ShooterGatewayReadyRequest(request.SessionToken, request.RoomId, request.Ready),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayReadyResult(result.Success, result.BattleId, result.CanStart, result.Message);
            }

            public async Task<RoomGatewayStartBattleResult> StartBattleAsync(RoomGatewayStartBattleRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.StartBattleAsync(
                    new ShooterGatewayStartBattleRequest(
                        request.SessionToken,
                        request.RoomId,
                        request.GameplayId,
                        request.RuleSetId,
                        request.ConfigVersion,
                        request.ProtocolVersion,
                        request.WorldType,
                        request.ClientId,
                        request.SyncTemplateId,
                        request.SyncModel,
                        request.NetworkEnvironmentId,
                        request.CarrierName,
                        request.EnableAuthoritativeWorld,
                        request.InterpolationEnabled,
                        request.InputDelayFrames),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayStartBattleResult(result.Success, result.BattleId, result.WorldId, result.Started, ToRoomAnchor(in result.WorldStartAnchor), result.ServerNowTicks, result.Message);
            }

            public async Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(RoomGatewayStateSyncSubscriptionRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.SubscribeStateSyncAsync(
                    new ShooterGatewayStateSyncSubscriptionRequest(
                        request.SessionToken,
                        request.BattleId,
                        request.RoomId,
                        request.EventEpoch,
                        request.LastEventAck),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayStateSyncSubscriptionResult(result.Success, result.Message);
            }

            public async Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(RoomGatewayRestoreRoomRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.RestoreRoomAsync(
                    new ShooterGatewayRestoreRoomRequest(request.SessionToken, request.Region, request.ServerId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayRestoreRoomResult(
                    result.Success,
                    result.HasActiveRoom,
                    result.IsInBattle,
                    result.RoomId,
                    result.NumericRoomId,
                    ToRoomAnchor(in result.WorldStartAnchor),
                    result.Message,
                    result.BattleId,
                    result.CanStart,
                    ToRoomEntryKind(result.JoinKind),
                    result.ServerNowTicks,
                    result.WorldId,
                    ToRoomRestoreStatus(result.Status),
                    ToRoomRestoreErrorCode(result.ErrorCode),
                    result.CurrentPlayerId);
            }

            public async Task<RoomGatewayBeginLoadingResult> BeginLoadingAsync(RoomGatewayBeginLoadingRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.BeginLoadingAsync(
                    new ShooterGatewayBeginLoadingRequest(request.SessionToken, request.RoomId, request.ExpectedRevision, request.CommandId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayBeginLoadingResult(
                    result.Success,
                    result.Applied,
                    result.ErrorCode,
                    result.Message,
                    result.RoomRevision,
                    ToRoomSnapshot(result.Snapshot));
            }

            public async Task<RoomGatewayReportAssetsLoadedResult> ReportAssetsLoadedAsync(RoomGatewayReportAssetsLoadedRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.ReportAssetsLoadedAsync(
                    new ShooterGatewayReportAssetsLoadedRequest(
                        request.SessionToken,
                        request.RoomId,
                        request.LaunchGeneration,
                        request.ManifestVersion,
                        request.ManifestHash,
                        request.CommandId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayReportAssetsLoadedResult(
                    result.Success,
                    result.Applied,
                    result.ErrorCode,
                    result.Message,
                    result.RoomRevision,
                    ToRoomSnapshot(result.Snapshot));
            }

            public async Task<RoomGatewayReportLoadingProgressResult> ReportLoadingProgressAsync(RoomGatewayReportLoadingProgressRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.ReportLoadingProgressAsync(
                    new ShooterGatewayReportLoadingProgressRequest(
                        request.SessionToken,
                        request.RoomId,
                        request.LaunchGeneration,
                        request.ManifestVersion,
                        request.ManifestHash,
                        request.Progress),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayReportLoadingProgressResult(
                    result.Success,
                    result.Applied,
                    result.ErrorCode,
                    result.Message,
                    result.RoomRevision,
                    ToRoomSnapshot(result.Snapshot));
            }

            public async Task<RoomGatewayLeaveResult> LeaveRoomAsync(RoomGatewayLeaveRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.LeaveRoomAsync(
                    new ShooterGatewayLeaveRoomRequest(
                        request.SessionToken,
                        request.RoomId,
                        request.ExpectedRevision,
                        request.CommandId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayLeaveResult(
                    result.Success,
                    result.Applied,
                    result.ErrorCode,
                    result.Message,
                    result.RoomRevision,
                    ToRoomSnapshot(result.Snapshot));
            }

            public async Task<RoomGatewayCancelLoadingResult> CancelLoadingAsync(RoomGatewayCancelLoadingRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.CancelLoadingAsync(
                    new ShooterGatewayCancelLoadingRequest(
                        request.SessionToken,
                        request.RoomId,
                        request.ExpectedRevision,
                        request.CommandId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayCancelLoadingResult(
                    result.Success,
                    result.Applied,
                    result.ErrorCode,
                    result.Message,
                    result.RoomRevision,
                    ToRoomSnapshot(result.Snapshot));
            }

            public async Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(RoomGatewayGetSnapshotRequest request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
            {
                var result = await _roomClient.GetSnapshotAsync(
                    new ShooterGatewayGetRoomSnapshotRequest(request.SessionToken, request.RoomId),
                    timeout,
                    cancellationToken).ConfigureAwait(false);
                return new RoomGatewayGetSnapshotResult(
                    result.Success,
                    result.RoomId,
                    result.NumericRoomId,
                    ToRoomSnapshot(result.Snapshot),
                    result.Message,
                    result.ServerNowTicks);
            }

            private static RoomGatewaySnapshot? ToRoomSnapshot(ShooterGatewayStagedRoomSnapshot? snapshot)
            {
                if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.RoomId))
                {
                    return null;
                }

                var worldStartAnchor = snapshot.WorldStartAnchor;
                return new RoomGatewaySnapshot
                {
                    RoomId = snapshot.RoomId,
                    OwnerAccountId = snapshot.OwnerAccountId,
                    Phase = (RoomGatewaySessionPhase)snapshot.Phase,
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
                    Players = ToRoomPlayers(snapshot.Players),
                    WorldStartAnchor = ToRoomAnchor(in worldStartAnchor)
                };
            }

            private static IReadOnlyList<RoomGatewayPlayerSnapshot> ToRoomPlayers(
                IReadOnlyList<ShooterGatewayStagedRoomPlayerSnapshot> players)
            {
                if (players == null || players.Count == 0)
                {
                    return Array.Empty<RoomGatewayPlayerSnapshot>();
                }

                var result = new RoomGatewayPlayerSnapshot[players.Count];
                for (var i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    result[i] = new RoomGatewayPlayerSnapshot
                    {
                        AccountId = player.AccountId,
                        PlayerId = player.PlayerId,
                        IsOnline = player.IsOnline,
                        LobbyReady = player.LobbyReady,
                        AssetsLoaded = player.AssetsLoaded,
                        LoadingProgress = player.LoadingProgress
                    };
                }

                return result;
            }

            public void Dispose()
            {
                if (_snapshotFeed != null)
                {
                    _snapshotFeed.SnapshotChanged -= HandleSnapshotChanged;
                }

                SnapshotChanged = null;
            }
        }

        public void Dispose()
        {
            _sessionClient.Dispose();
        }
    }

    public enum ShooterRoomGatewayEntryKind
    {
        TeamLobby = 0,
        Reconnect = 1,
        LateJoin = 2
    }

    public readonly struct ShooterRoomGatewayFlowResult
    {
        public readonly string SessionToken;
        public readonly string RoomId;
        public readonly ulong NumericRoomId;
        public readonly string BattleId;
        public readonly ulong WorldId;
        public readonly uint PlayerId;
        public readonly ShooterGatewayWorldStartAnchor WorldStartAnchor;
        public readonly long ServerNowTicks;
        public readonly int TargetFrame;
        public readonly int CatchUpFrames;
        public readonly ShooterRemoteTimeAnchorProjection RemoteTimeAnchorProjection;
        public readonly ShooterRoomGatewayEntryKind EntryKind;
        public readonly bool CanStart;
        public readonly bool Started;
        public readonly bool Subscribed;
        public readonly string Message;
        public readonly ShooterGatewayRoomRestoreStatus RestoreStatus;
        public readonly ShooterGatewayRoomRestoreErrorCode RestoreErrorCode;

        public bool CanRetryRestore =>
            RestoreStatus == ShooterGatewayRoomRestoreStatus.Timeout ||
            (RestoreStatus == ShooterGatewayRoomRestoreStatus.Failed &&
             RestoreErrorCode == ShooterGatewayRoomRestoreErrorCode.InternalError);

        public ShooterRoomGatewayFlowResult(
            string sessionToken,
            string roomId,
            ulong numericRoomId,
            string battleId,
            ulong worldId,
            uint playerId,
            in ShooterGatewayWorldStartAnchor worldStartAnchor,
            long serverNowTicks,
            ShooterRoomGatewayEntryKind entryKind,
            bool canStart,
            bool started,
            bool subscribed,
            string message)
            : this(sessionToken, roomId, numericRoomId, battleId, worldId, playerId, in worldStartAnchor, serverNowTicks, entryKind, canStart, started, subscribed, message, ShooterGatewayRoomRestoreStatus.Restored, ShooterGatewayRoomRestoreErrorCode.None)
        {
        }

        public ShooterRoomGatewayFlowResult(
            string sessionToken,
            string roomId,
            ulong numericRoomId,
            string battleId,
            ulong worldId,
            uint playerId,
            in ShooterGatewayWorldStartAnchor worldStartAnchor,
            long serverNowTicks,
            ShooterRoomGatewayEntryKind entryKind,
            bool canStart,
            bool started,
            bool subscribed,
            string message,
            ShooterGatewayRoomRestoreStatus restoreStatus,
            ShooterGatewayRoomRestoreErrorCode restoreErrorCode)
        {
            SessionToken = sessionToken ?? string.Empty;
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            BattleId = battleId ?? string.Empty;
            WorldId = worldId;
            PlayerId = playerId;
            WorldStartAnchor = worldStartAnchor;
            ServerNowTicks = serverNowTicks;
            RemoteTimeAnchorProjection = ShooterTimeAnchorCoordinator.ProjectRemote(in worldStartAnchor, serverNowTicks);
            TargetFrame = RemoteTimeAnchorProjection.TargetFrame;
            CatchUpFrames = RemoteTimeAnchorProjection.CatchUpFrames;
            EntryKind = entryKind;
            CanStart = canStart;
            Started = started;
            Subscribed = subscribed;
            Message = message ?? string.Empty;
            RestoreStatus = restoreStatus;
            RestoreErrorCode = restoreErrorCode;
        }

        public ShooterRoomGatewayLaunchSummary ToSummary()
        {
            return new ShooterRoomGatewayLaunchSummary(
                RoomId,
                NumericRoomId,
                BattleId,
                WorldId,
                PlayerId,
                TargetFrame,
                CatchUpFrames,
                EntryKind,
                CanStart,
                Started,
                Subscribed,
                Message,
                RestoreStatus,
                RestoreErrorCode);
        }

        public ShooterGatewayBattleInputContext CreateBattleInputContext(int frame)
        {
            return new ShooterGatewayBattleInputContext(SessionToken, BattleId, WorldId, frame, PlayerId);
        }
    }

    public readonly struct ShooterRoomGatewayLaunchSummary
    {
        public ShooterRoomGatewayLaunchSummary(
            string roomId,
            ulong numericRoomId,
            string battleId,
            ulong worldId,
            uint playerId,
            int targetFrame,
            int catchUpFrames,
            ShooterRoomGatewayEntryKind entryKind,
            bool canStart,
            bool started,
            bool subscribed,
            string message)
            : this(roomId, numericRoomId, battleId, worldId, playerId, targetFrame, catchUpFrames, entryKind, canStart, started, subscribed, message, ShooterGatewayRoomRestoreStatus.Restored, ShooterGatewayRoomRestoreErrorCode.None)
        {
        }

        public ShooterRoomGatewayLaunchSummary(
            string roomId,
            ulong numericRoomId,
            string battleId,
            ulong worldId,
            uint playerId,
            int targetFrame,
            int catchUpFrames,
            ShooterRoomGatewayEntryKind entryKind,
            bool canStart,
            bool started,
            bool subscribed,
            string message,
            ShooterGatewayRoomRestoreStatus restoreStatus,
            ShooterGatewayRoomRestoreErrorCode restoreErrorCode)
        {
            RoomId = roomId ?? string.Empty;
            NumericRoomId = numericRoomId;
            BattleId = battleId ?? string.Empty;
            WorldId = worldId;
            PlayerId = playerId;
            TargetFrame = targetFrame;
            CatchUpFrames = catchUpFrames;
            EntryKind = entryKind;
            CanStart = canStart;
            Started = started;
            Subscribed = subscribed;
            Message = message ?? string.Empty;
            RestoreStatus = restoreStatus;
            RestoreErrorCode = restoreErrorCode;
        }

        public string RoomId { get; }

        public ulong NumericRoomId { get; }

        public string BattleId { get; }

        public ulong WorldId { get; }

        public uint PlayerId { get; }

        public int TargetFrame { get; }

        public int CatchUpFrames { get; }

        public ShooterRoomGatewayEntryKind EntryKind { get; }

        public bool CanStart { get; }

        public bool Started { get; }

        public bool Subscribed { get; }

        public string Message { get; }

        public ShooterGatewayRoomRestoreStatus RestoreStatus { get; }

        public ShooterGatewayRoomRestoreErrorCode RestoreErrorCode { get; }

        public bool CanRetryRestore =>
            RestoreStatus == ShooterGatewayRoomRestoreStatus.Timeout ||
            (RestoreStatus == ShooterGatewayRoomRestoreStatus.Failed &&
             RestoreErrorCode == ShooterGatewayRoomRestoreErrorCode.InternalError);

        public bool IsRunningEntry => EntryKind == ShooterRoomGatewayEntryKind.Reconnect || EntryKind == ShooterRoomGatewayEntryKind.LateJoin;

        public bool IsClosed => !string.IsNullOrWhiteSpace(RoomId)
            && !string.IsNullOrWhiteSpace(BattleId)
            && WorldId != 0UL
            && PlayerId != 0U
            && Started
            && Subscribed;
    }
}
