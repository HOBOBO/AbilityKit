using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.Battle;
using AbilityKit.Orleans.Contracts.Rooms;
using AbilityKit.Protocol.Room;
using MemoryPack;

namespace AbilityKit.Orleans.Grains.Rooms;

/// <summary>
/// 将 Room 领域快照映射为 wire push payload（byte[]）。
/// RoomGrain 位于 Grains 项目，不能依赖 Gateway 的 RoomGatewayWireMapper，
/// 因此在此处内联一份等价的 append-only 映射，仅用于 RoomStateChanged 推送。
/// </summary>
internal static class RoomStatePushBuilder
{
    /// <summary>
    /// 构建 RoomStateChanged push 的序列化 payload。
    /// </summary>
    public static byte[] BuildRoomStateChangedPayload(RoomSnapshot snapshot, long serverNowTicks)
    {
        var push = new WireRoomStateChangedPush
        {
            RoomId = snapshot.Summary?.RoomId ?? string.Empty,
            Snapshot = ToWireSnapshot(snapshot),
            ServerNowTicks = serverNowTicks
        };
        return MemoryPackSerializer.Serialize(push);
    }

    /// <summary>
    /// 返回当前在线成员的 accountId 列表（用于 push 分发）。
    /// </summary>
    public static List<string> CollectOnlineAccountIds(RoomSnapshot snapshot)
    {
        var result = new List<string>();
        if (snapshot.Players == null || snapshot.Players.Count == 0)
        {
            return result;
        }

        foreach (var player in snapshot.Players)
        {
            if (player.IsOnline && !string.IsNullOrWhiteSpace(player.AccountId))
            {
                result.Add(player.AccountId);
            }
        }

        return result;
    }

    private static WireRoomSnapshot ToWireSnapshot(RoomSnapshot snapshot)
    {
        return new WireRoomSnapshot
        {
            Summary = ToWireSummary(snapshot.Summary),
            Members = snapshot.Members == null ? null : new List<string>(snapshot.Members),
            Players = ToWirePlayers(snapshot.Players),
            CanStart = snapshot.CanStart,
            BattleId = snapshot.BattleId ?? string.Empty,
            WorldId = snapshot.WorldId,
            WorldStartAnchor = ToWireAnchor(snapshot.WorldStartAnchor),
            SchemaVersion = snapshot.SchemaVersion,
            RoomRevision = snapshot.RoomRevision,
            LastEventSequence = snapshot.LastEventSequence,
            Phase = (int)snapshot.Phase,
            PhaseReason = snapshot.PhaseReason ?? string.Empty,
            LaunchGeneration = snapshot.LaunchGeneration,
            LoadingDeadlineUnixMs = snapshot.LoadingDeadlineUnixMs,
            LaunchManifestHash = snapshot.LaunchManifestHash ?? string.Empty,
            LaunchManifestVersion = snapshot.LaunchManifestVersion,
            LastStartFailureCode = snapshot.LastStartFailureCode ?? string.Empty
        };
    }

    private static WireRoomSummary ToWireSummary(RoomSummary? summary)
    {
        if (summary == null)
        {
            return default;
        }

        return new WireRoomSummary
        {
            Region = summary.Region ?? string.Empty,
            ServerId = summary.ServerId ?? string.Empty,
            RoomId = summary.RoomId ?? string.Empty,
            RoomType = summary.RoomType ?? string.Empty,
            Title = summary.Title ?? string.Empty,
            IsPublic = summary.IsPublic,
            MaxPlayers = summary.MaxPlayers,
            PlayerCount = summary.PlayerCount,
            OwnerAccountId = summary.OwnerAccountId ?? string.Empty,
            CreatedAtUnixMs = summary.CreatedAtUnixMs,
            Tags = summary.Tags == null ? null : new Dictionary<string, string>(summary.Tags)
        };
    }

    private static List<WireRoomPlayerSnapshot>? ToWirePlayers(List<RoomPlayerSnapshot>? players)
    {
        if (players == null || players.Count == 0)
        {
            return players == null ? null : new List<WireRoomPlayerSnapshot>();
        }

        var result = new List<WireRoomPlayerSnapshot>(players.Count);
        foreach (var player in players)
        {
            result.Add(new WireRoomPlayerSnapshot
            {
                AccountId = player.AccountId ?? string.Empty,
                TeamId = player.TeamId,
                Ready = player.Ready,
                HeroId = player.HeroId,
                SpawnPointId = player.SpawnPointId,
                Level = player.Level,
                AttributeTemplateId = player.AttributeTemplateId,
                BasicAttackSkillId = player.BasicAttackSkillId,
                SkillIds = player.SkillIds == null ? null : new List<int>(player.SkillIds),
                PlayerId = player.PlayerId,
                LobbyReady = player.LobbyReady,
                AssetsLoaded = player.AssetsLoaded,
                LoadingProgress = player.LoadingProgress,
                IsOnline = player.IsOnline,
                JoinOrdinal = player.JoinOrdinal,
                LoadedManifestVersion = player.LoadedManifestVersion,
                LoadedManifestHash = player.LoadedManifestHash ?? string.Empty,
                LastSeenTicks = player.LastSeenTicks,
                OfflineSinceTicks = player.OfflineSinceTicks
            });
        }

        return result;
    }

    private static WireWorldStartAnchor ToWireAnchor(WorldStartAnchor? anchor)
    {
        if (anchor is null)
        {
            return default;
        }

        return new WireWorldStartAnchor
        {
            StartServerTicks = anchor.StartServerTicks,
            ServerTickFrequency = anchor.ServerTickFrequency,
            StartFrame = anchor.StartFrame,
            FixedDeltaSeconds = anchor.FixedDeltaSeconds
        };
    }
}
