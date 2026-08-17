#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Flow
{
    internal static class GatewayRoomProtocolMapper
    {
        private const string SyncTemplateIdTag = "syncTemplateId";
        private const string SyncModelTag = "syncModel";

        internal static RoomGatewayLaunchSpec ToLaunchSpec(MultiplayerRoomLaunchSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

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
                tags: BuildLaunchTags(spec),
                syncTemplateId: spec.SyncTemplateId,
                syncModel: spec.SyncModel);
        }

        internal static IReadOnlyDictionary<string, string> BuildLaunchTags(
            MultiplayerRoomLaunchSpec spec)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));

            var tags = new Dictionary<string, string>(StringComparer.Ordinal)
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
            if (!string.IsNullOrWhiteSpace(spec.SyncTemplateId))
            {
                tags[SyncTemplateIdTag] = spec.SyncTemplateId.Trim();
            }

            if (spec.SyncModel > 0)
            {
                tags[SyncModelTag] = spec.SyncModel.ToString(CultureInfo.InvariantCulture);
            }

            return tags;
        }

        internal static MultiplayerRoomRestoreResult ToRestoreResult(
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

        internal static MultiplayerRoomRestoreResult CreateRestoreFailure(
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

        internal static ClientRoomSnapshot ToClientSnapshot(
            RoomGatewaySnapshot snapshot,
            ulong numericRoomId)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

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
                SyncCapabilities = snapshot.SyncCapabilities,
                WorldStartAnchor = new GatewayWorldStartAnchor(
                    anchor.StartServerTicks,
                    anchor.ServerTickFrequency,
                    anchor.StartFrame,
                    anchor.FixedDeltaSeconds)
            };
        }

        internal static MultiplayerRoomRestoreNextStep ToNextStep(
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

        internal static MultiplayerRoomEntryKind ToEntryKind(RoomGatewaySessionEntryKind entryKind)
        {
            return entryKind switch
            {
                RoomGatewaySessionEntryKind.Reconnect => MultiplayerRoomEntryKind.Reconnect,
                RoomGatewaySessionEntryKind.LateJoin => MultiplayerRoomEntryKind.LateJoin,
                _ => MultiplayerRoomEntryKind.TeamLobby
            };
        }

        internal static MultiplayerRoomRestoreStatus ToRestoreStatus(
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

        internal static MultiplayerRoomRestoreErrorCode ToRestoreErrorCode(
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
    }
}
