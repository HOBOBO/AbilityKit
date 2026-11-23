using System;
using System.Collections.Generic;
using AbilityKit.Network.Room;
using AbilityKit.Protocol.Room;

namespace AbilityKit.Game.Battle.Agent
{
    /// <summary>
    /// 将 wire 层 <see cref="WireRoomSnapshot"/> / <see cref="WireRoomPlayerSnapshot"/>
    /// 映射为纯 C# 客户端模型 <see cref="ClientRoomSnapshot"/> / <see cref="ClientRoomPlayer"/>。
    /// 解耦客户端视图与 wire 类型。
    /// </summary>
    public static class ClientRoomSnapshotMapper
    {
        /// <summary>
        /// 将 <see cref="WireRoomSnapshot"/> 映射为 <see cref="ClientRoomSnapshot"/>。
        /// </summary>
        public static ClientRoomSnapshot ToClientSnapshot(WireRoomSnapshot wire)
        {
            var anchor = wire.WorldStartAnchor;
            var snapshot = new ClientRoomSnapshot
            {
                RoomId = ResolveRoomId(wire),
                GameplayId = wire.Summary.Tags != null &&
                    wire.Summary.Tags.TryGetValue(RoomTagKeys.GameplayId, out var gameplayIdText) &&
                    int.TryParse(gameplayIdText, out var gameplayId) && gameplayId > 0 ? gameplayId : 1,
                OwnerAccountId = wire.Summary.OwnerAccountId ?? string.Empty,
                Phase = ToClientPhase(wire.Phase),
                PhaseReason = wire.PhaseReason ?? string.Empty,
                LaunchGeneration = wire.LaunchGeneration,
                LoadingDeadlineUnixMs = wire.LoadingDeadlineUnixMs,
                LaunchManifestHash = wire.LaunchManifestHash ?? string.Empty,
                TagBundleVersion = wire.Summary.Tags != null && wire.Summary.Tags.TryGetValue(RoomTagKeys.MobaTagBundleVersion, out var tagBundleVersion)
                    ? tagBundleVersion : string.Empty,
                LaunchManifestVersion = wire.LaunchManifestVersion,
                LastStartFailureCode = wire.LastStartFailureCode ?? string.Empty,
                RoomRevision = wire.RoomRevision,
                LastEventSequence = wire.LastEventSequence,
                CanStart = wire.CanStart,
                BattleId = wire.BattleId ?? string.Empty,
                WorldId = wire.WorldId,
                Members = ToStringList(wire.Members),
                Players = ToPlayerList(wire.Players),
                SyncCapabilities = RoomGatewayNetworkSyncCapabilitiesConverter.FromWire(wire.SyncCapabilities),
                WorldStartAnchor = ToAnchor(anchor)
            };
            return snapshot;
        }

        /// <summary>
        /// 将网关领域快照投影到客户端，复制成员、玩家和技能列表。
        /// </summary>
        public static ClientRoomSnapshot ToClientSnapshot(RoomGatewaySnapshot source)
        {
            return ProjectGatewaySnapshot(source, 0UL, fromSession: false);
        }

        internal static ClientRoomSnapshot ToSessionSnapshot(RoomGatewaySnapshot source, ulong numericRoomId)
        {
            return ProjectGatewaySnapshot(source, numericRoomId, fromSession: true);
        }

        private static ClientRoomSnapshot ProjectGatewaySnapshot(
            RoomGatewaySnapshot source, ulong numericRoomId, bool fromSession)
        {
            var anchor = source.WorldStartAnchor;
            return new ClientRoomSnapshot
            {
                RoomId = ProjectGatewayText(source.RoomId, fromSession),
                GameplayId = source.GameplayId,
                NumericRoomId = numericRoomId,
                OwnerAccountId = ProjectGatewayText(source.OwnerAccountId, fromSession),
                Phase = fromSession ? (ClientRoomPhase)source.Phase : ToClientPhase((int)source.Phase),
                PhaseReason = ProjectGatewayText(source.PhaseReason, fromSession),
                LaunchGeneration = source.LaunchGeneration,
                LoadingDeadlineUnixMs = source.LoadingDeadlineUnixMs,
                LaunchManifestHash = ProjectGatewayText(source.LaunchManifestHash, fromSession),
                TagBundleVersion = ProjectGatewayText(source.TagBundleVersion, fromSession),
                LaunchManifestVersion = source.LaunchManifestVersion,
                LastStartFailureCode = ProjectGatewayText(source.LastStartFailureCode, fromSession),
                RoomRevision = source.RoomRevision,
                LastEventSequence = source.LastEventSequence,
                CanStart = source.CanStart,
                BattleId = ProjectGatewayText(source.BattleId, fromSession),
                WorldId = source.WorldId,
                Members = ToStringList(source.Members),
                Players = ToPlayerList(source.Players, fromSession),
                SyncCapabilities = source.SyncCapabilities,
                WorldStartAnchor = ToAnchor(in anchor)
            };
        }

        /// <summary>
        /// 将 <see cref="WireRoomPlayerSnapshot"/> 映射为 <see cref="ClientRoomPlayer"/>。
        /// </summary>
        public static ClientRoomPlayer ToClientPlayer(WireRoomPlayerSnapshot wire)
        {
            return new ClientRoomPlayer
            {
                AccountId = wire.AccountId ?? string.Empty,
                TeamId = wire.TeamId,
                Ready = wire.Ready,
                HeroId = wire.HeroId,
                SpawnPointId = wire.SpawnPointId,
                Level = wire.Level,
                AttributeTemplateId = wire.AttributeTemplateId,
                BasicAttackSkillId = wire.BasicAttackSkillId,
                SkillIds = ToIntList(wire.SkillIds),
                PlayerId = wire.PlayerId,
                LobbyReady = wire.LobbyReady,
                AssetsLoaded = wire.AssetsLoaded,
                LoadingProgress = wire.LoadingProgress,
                IsOnline = wire.IsOnline,
                JoinOrdinal = wire.JoinOrdinal,
                LoadedManifestVersion = wire.LoadedManifestVersion,
                LoadedManifestHash = wire.LoadedManifestHash ?? string.Empty,
                LastSeenTicks = wire.LastSeenTicks,
                OfflineSinceTicks = wire.OfflineSinceTicks
            };
        }

        /// <summary>
        /// 将网关领域玩家投影到客户端，复制技能列表。
        /// </summary>
        public static ClientRoomPlayer ToClientPlayer(RoomGatewayPlayerSnapshot source)
        {
            return ToClientPlayer(source, fromSession: false);
        }

        private static ClientRoomPlayer ToClientPlayer(RoomGatewayPlayerSnapshot source, bool fromSession)
        {
            return new ClientRoomPlayer
            {
                AccountId = ProjectGatewayText(source.AccountId, fromSession),
                TeamId = source.TeamId,
                Ready = fromSession ? source.LobbyReady : source.Ready,
                HeroId = source.HeroId,
                SpawnPointId = source.SpawnPointId,
                Level = source.Level,
                AttributeTemplateId = source.AttributeTemplateId,
                BasicAttackSkillId = source.BasicAttackSkillId,
                SkillIds = ToIntList(source.SkillIds),
                PlayerId = source.PlayerId,
                LobbyReady = source.LobbyReady,
                AssetsLoaded = source.AssetsLoaded,
                LoadingProgress = source.LoadingProgress,
                IsOnline = source.IsOnline,
                JoinOrdinal = source.JoinOrdinal,
                LoadedManifestVersion = source.LoadedManifestVersion,
                LoadedManifestHash = ProjectGatewayText(source.LoadedManifestHash, fromSession),
                LastSeenTicks = source.LastSeenTicks,
                OfflineSinceTicks = source.OfflineSinceTicks
            };
        }

        private static string ProjectGatewayText(string value, bool fromSession)
        {
            return fromSession ? value : value ?? string.Empty;
        }

        private static ClientRoomPhase ToClientPhase(int phase)
        {
            // 与服务端 RoomPhase 对齐；未知值回退为 Lobby。
            if (phase < 0 || phase > (int)ClientRoomPhase.Expired)
            {
                return ClientRoomPhase.Lobby;
            }

            return (ClientRoomPhase)phase;
        }

        private static string ResolveRoomId(WireRoomSnapshot wire)
        {
            var summary = wire.Summary;
            return summary.RoomId ?? string.Empty;
        }

        private static IReadOnlyList<string> ToStringList(List<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = source[i] ?? string.Empty;
            }

            return result;
        }

        private static IReadOnlyList<string> ToStringList(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = source[i] ?? string.Empty;
            }

            return result;
        }

        private static IReadOnlyList<ClientRoomPlayer> ToPlayerList(List<WireRoomPlayerSnapshot> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<ClientRoomPlayer>();
            }

            var result = new ClientRoomPlayer[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                var player = source[i];
                result[i] = ToClientPlayer(player);
            }

            return result;
        }

        private static IReadOnlyList<ClientRoomPlayer> ToPlayerList(IReadOnlyList<RoomGatewayPlayerSnapshot> source, bool fromSession)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<ClientRoomPlayer>();
            }

            var result = new ClientRoomPlayer[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = ToClientPlayer(source[i], fromSession);
            }

            return result;
        }

        private static IReadOnlyList<int> ToIntList(List<int> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<int>();
            }

            var result = new int[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = source[i];
            }

            return result;
        }

        private static IReadOnlyList<int> ToIntList(IReadOnlyList<int> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<int>();
            }

            var result = new int[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                result[i] = source[i];
            }

            return result;
        }

        private static GatewayWorldStartAnchor ToAnchor(WireWorldStartAnchor anchor)
        {
            return new GatewayWorldStartAnchor(
                anchor.StartServerTicks,
                anchor.ServerTickFrequency,
                anchor.StartFrame,
                anchor.FixedDeltaSeconds);
        }

        private static GatewayWorldStartAnchor ToAnchor(in RoomGatewayWorldStartAnchor anchor)
        {
            return new GatewayWorldStartAnchor(
                anchor.StartServerTicks,
                anchor.ServerTickFrequency,
                anchor.StartFrame,
                anchor.FixedDeltaSeconds);
        }
    }
}
