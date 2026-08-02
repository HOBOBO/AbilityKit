#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Extensions.Session;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Maps the MOBA room protocol port onto the framework's staged room-session contract.
    /// Protocol serialization stays in GatewayRoomClient; lifecycle orchestration stays in RoomGatewaySessionFlow.
    /// </summary>
    internal sealed class MobaRoomGatewaySessionClient :
        IRoomGatewaySessionClient,
        IRoomGatewaySnapshotFeed
    {
        private readonly IGatewayRoomClient _client;
        private readonly ClientRoomStore _store;

        public MobaRoomGatewaySessionClient(IGatewayRoomClient client, ClientRoomStore store)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _store.OnSnapshotChanged += HandleSnapshotChanged;
        }

        public RoomGatewaySnapshot? Current => ToRoomSnapshot(_store.Current);

        public event Action<RoomGatewaySnapshot>? SnapshotChanged;

        private void HandleSnapshotChanged(ClientRoomSnapshot snapshot)
        {
            var projected = ToRoomSnapshot(snapshot);
            if (projected != null) SnapshotChanged?.Invoke(projected);
        }

        public async Task<RoomGatewayCreateResult> CreateRoomAsync(
            RoomGatewayCreateRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.CreateRoomAsync(
                request.SessionToken,
                request.Region,
                request.ServerId,
                request.RoomType,
                request.Title,
                request.IsPublic,
                request.MaxPlayers,
                request.Tags,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var success = !string.IsNullOrWhiteSpace(result.RoomId);
            return new RoomGatewayCreateResult(success, result.RoomId, result.NumericRoomId, success ? string.Empty : "Gateway returned an empty room id.");
        }

        public async Task<RoomGatewayJoinResult> JoinRoomAsync(
            RoomGatewayJoinRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.JoinRoomAsync(
                request.SessionToken,
                request.Region,
                request.ServerId,
                request.RoomId,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new RoomGatewayJoinResult(
                result.Success,
                string.IsNullOrWhiteSpace(result.RoomId) ? request.RoomId : result.RoomId,
                result.NumericRoomId,
                ToRoomAnchor(in result.WorldStartAnchor),
                result.Message,
                result.BattleId,
                result.CanStart,
                RoomGatewaySessionEntryKind.TeamLobby,
                result.ServerNowTicks,
                result.WorldId,
                result.CurrentPlayerId);
        }

        public async Task<RoomGatewayReadyResult> SetReadyAsync(
            RoomGatewayReadyRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            await _client.SetReadyAsync(request.SessionToken, request.RoomId, request.Ready, timeout, cancellationToken).ConfigureAwait(false);
            return new RoomGatewayReadyResult(true, string.Empty, false, string.Empty);
        }

        public async Task<RoomGatewayLeaveResult> LeaveRoomAsync(
            RoomGatewayLeaveRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.LeaveRoomAsync(
                request.SessionToken,
                request.RoomId,
                request.ExpectedRevision,
                request.CommandId,
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

        public Task<RoomGatewayStartBattleResult> StartBattleAsync(
            RoomGatewayStartBattleRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("MOBA uses the staged loading flow instead of direct StartBattle.");
        }

        public Task<RoomGatewayStateSyncSubscriptionResult> SubscribeStateSyncAsync(
            RoomGatewayStateSyncSubscriptionRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("State-sync subscription is owned by BattleSessionFeature.");
        }

        public async Task<RoomGatewayRestoreRoomResult> RestoreRoomAsync(
            RoomGatewayRestoreRoomRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.RestoreRoomAsync(
                request.SessionToken,
                request.Region,
                request.ServerId,
                timeout,
                cancellationToken).ConfigureAwait(false);
            var snapshot = ToRoomSnapshot(result.Snapshot);
            return new RoomGatewayRestoreRoomResult(
                result.Success,
                result.HasActiveRoom,
                result.IsInBattle,
                result.RoomId,
                result.NumericRoomId,
                ToRoomAnchor(in result.WorldStartAnchor),
                result.Message,
                snapshot?.BattleId ?? string.Empty,
                snapshot?.CanStart ?? false,
                ToRoomEntryKind(result.JoinKind),
                result.ServerNowTicks,
                snapshot?.WorldId ?? 0UL,
                ResolveRestoreStatus(in result),
                result.Success ? RoomGatewaySessionRestoreErrorCode.None : RoomGatewaySessionRestoreErrorCode.InternalError,
                result.CurrentPlayerId);
        }

        public async Task<RoomGatewayPickHeroResult> PickHeroAsync(
            RoomGatewayPickHeroRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.PickHeroAsync(
                request.SessionToken,
                request.RoomId,
                request.HeroId,
                request.TeamId,
                request.SpawnPointId,
                request.Level,
                request.AttributeTemplateId,
                request.BasicAttackSkillId,
                request.SkillIds,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new RoomGatewayPickHeroResult(
                result.Success,
                result.Applied,
                result.ErrorCode,
                result.RoomId,
                result.NumericRoomId,
                new RoomGatewaySnapshot { RoomId = result.RoomId },
                result.Message);
        }

        public async Task<RoomGatewayBeginLoadingResult> BeginLoadingAsync(
            RoomGatewayBeginLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.BeginLoadingAsync(
                request.SessionToken,
                request.RoomId,
                request.ExpectedRevision,
                request.CommandId,
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

        public async Task<RoomGatewayReportAssetsLoadedResult> ReportAssetsLoadedAsync(
            RoomGatewayReportAssetsLoadedRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.ReportAssetsLoadedAsync(
                request.SessionToken,
                request.RoomId,
                request.LaunchGeneration,
                request.ManifestVersion,
                request.ManifestHash,
                request.CommandId,
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

        public async Task<RoomGatewayReportLoadingProgressResult> ReportLoadingProgressAsync(
            RoomGatewayReportLoadingProgressRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.ReportLoadingProgressAsync(
                request.SessionToken,
                request.RoomId,
                request.LaunchGeneration,
                request.ManifestVersion,
                request.ManifestHash,
                request.Progress,
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

        public async Task<RoomGatewayCancelLoadingResult> CancelLoadingAsync(
            RoomGatewayCancelLoadingRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.CancelLoadingAsync(
                request.SessionToken,
                request.RoomId,
                request.ExpectedRevision,
                request.CommandId,
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

        public async Task<RoomGatewayGetSnapshotResult> GetSnapshotAsync(
            RoomGatewayGetSnapshotRequest request,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            var result = await _client.GetSnapshotAsync(
                request.SessionToken,
                request.RoomId,
                timeout,
                cancellationToken).ConfigureAwait(false);
            return new RoomGatewayGetSnapshotResult(
                result.Success,
                result.RoomId,
                result.NumericRoomId,
                ToRoomSnapshot(result.Snapshot),
                result.Message);
        }

        private static RoomGatewaySnapshot? ToRoomSnapshot(ClientRoomSnapshot? snapshot)
        {
            if (snapshot == null) return null;
            var anchor = snapshot.WorldStartAnchor;
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
                Members = CopyStrings(snapshot.Members),
                Players = ToRoomPlayers(snapshot.Players),
                WorldStartAnchor = ToRoomAnchor(in anchor)
            };
        }

        private static IReadOnlyList<RoomGatewayPlayerSnapshot> ToRoomPlayers(
            IReadOnlyList<ClientRoomPlayer> players)
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

        private static RoomGatewayWorldStartAnchor ToRoomAnchor(in GatewayWorldStartAnchor anchor)
        {
            return new RoomGatewayWorldStartAnchor(
                anchor.StartServerTicks,
                anchor.ServerTickFrequency,
                anchor.StartFrame,
                anchor.FixedDeltaSeconds);
        }

        private static RoomGatewaySessionEntryKind ToRoomEntryKind(RoomGatewayJoinKind joinKind)
        {
            return joinKind switch
            {
                RoomGatewayJoinKind.Reconnect => RoomGatewaySessionEntryKind.Reconnect,
                RoomGatewayJoinKind.LateJoin => RoomGatewaySessionEntryKind.LateJoin,
                _ => RoomGatewaySessionEntryKind.TeamLobby
            };
        }

        private static RoomGatewaySessionRestoreStatus ResolveRestoreStatus(in GatewayRestoreRoomResult result)
        {
            if (!result.Success) return RoomGatewaySessionRestoreStatus.Failed;
            return result.HasActiveRoom
                ? RoomGatewaySessionRestoreStatus.Restored
                : RoomGatewaySessionRestoreStatus.NoActiveRoom;
        }
    }
}
