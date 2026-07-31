using System;
using System.Collections.Generic;
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
                OwnerAccountId = wire.Summary.OwnerAccountId ?? string.Empty,
                Phase = ToClientPhase(wire.Phase),
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
                Members = ToStringList(wire.Members),
                Players = ToPlayerList(wire.Players),
                WorldStartAnchor = ToAnchor(anchor)
            };
            return snapshot;
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

        private static GatewayWorldStartAnchor ToAnchor(WireWorldStartAnchor anchor)
        {
            return new GatewayWorldStartAnchor(
                anchor.StartServerTicks,
                anchor.ServerTickFrequency,
                anchor.StartFrame,
                anchor.FixedDeltaSeconds);
        }
    }
}
