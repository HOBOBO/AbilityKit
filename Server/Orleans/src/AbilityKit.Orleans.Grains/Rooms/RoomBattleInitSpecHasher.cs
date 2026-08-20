using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Orleans.Contracts.Battle;

namespace AbilityKit.Orleans.Grains.Rooms;

/// <summary>
/// 对 <see cref="BattleInitParams"/> 的关键字段计算稳定 hash（SHA256）。
/// 用于检测"同一 battleId 被用不同参数重复初始化"的冲突。
/// 仅纳入决定战斗世界拓扑/玩法的字段，排除运行时锚点（WorldStartAnchor）与同步传输细节。
/// </summary>
internal static class RoomBattleInitSpecHasher
{
    public static string Compute(BattleInitParams initParams)
    {
        if (initParams is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        // 标量字段（按固定顺序）。
        AppendField(builder, "WorldId", initParams.WorldId);
        AppendField(builder, "TickRate", initParams.TickRate);
        AppendField(builder, "MapId", initParams.MapId);
        AppendField(builder, "GameplayId", initParams.GameplayId);
        AppendField(builder, "RuleSetId", initParams.RuleSetId);
        AppendField(builder, "ConfigVersion", initParams.ConfigVersion);
        AppendField(builder, "ProtocolVersion", initParams.ProtocolVersion);
        AppendField(builder, "RandomSeed", initParams.RandomSeed);
        AppendField(builder, "InputDelayFrames", initParams.InputDelayFrames);
        AppendField(builder, "DurationFrames", initParams.DurationFrames);
        AppendField(builder, "EnemyBudget", initParams.EnemyBudget);
        AppendField(builder, "VictoryTargetDefeats", initParams.VictoryTargetDefeats);
        AppendField(builder, "ContinueAfterAllPlayersDefeated", initParams.ContinueAfterAllPlayersDefeated ? 1 : 0);
        AppendField(builder, "WorldType", initParams.WorldType ?? string.Empty);
        AppendField(builder, "ClientId", initParams.ClientId ?? string.Empty);
        AppendField(builder, "RoomType", initParams.RoomType ?? string.Empty);

        // 玩家列表：按 PlayerId 排序后逐字段序列化，保证顺序无关。
        var players = initParams.Players ?? new List<PlayerInitInfo>();
        var orderedPlayers = players
            .OrderBy(p => p.PlayerId)
            .ThenBy(p => p.AccountId ?? string.Empty, System.StringComparer.Ordinal)
            .ToList();
        AppendField(builder, "Players.Count", orderedPlayers.Count);
        for (var i = 0; i < orderedPlayers.Count; i++)
        {
            var player = orderedPlayers[i];
            var prefix = $"Players[{i}]";
            AppendField(builder, prefix + ".PlayerId", player.PlayerId);
            AppendField(builder, prefix + ".ActorId", player.ActorId);
            AppendField(builder, prefix + ".HeroId", player.HeroId);
            AppendField(builder, prefix + ".TeamId", player.TeamId);
            AppendField(builder, prefix + ".Level", player.Level);
            AppendField(builder, prefix + ".AttributeTemplateId", player.AttributeTemplateId);
            AppendField(builder, prefix + ".BasicAttackSkillId", player.BasicAttackSkillId);
            AppendField(builder, prefix + ".AccountId", player.AccountId ?? string.Empty);
            AppendField(builder, prefix + ".PosX", player.PosX.ToString("R", CultureInfo.InvariantCulture));
            AppendField(builder, prefix + ".PosY", player.PosY.ToString("R", CultureInfo.InvariantCulture));
            AppendField(builder, prefix + ".PosZ", player.PosZ.ToString("R", CultureInfo.InvariantCulture));
            AppendField(builder, prefix + ".HasSpawnPosition", player.HasSpawnPosition);
            AppendField(builder, prefix + ".SpawnIndex", player.SpawnIndex);
            AppendField(builder, prefix + ".SkillIds", player.SkillIds is null
                ? string.Empty
                : string.Join(",", player.SkillIds.OrderBy(id => id).Select(id => id.ToString(CultureInfo.InvariantCulture))));
        }

        var payload = Encoding.UTF8.GetBytes(builder.ToString());
#if NET5_0_OR_GREATER
        var hashBytes = SHA256.HashData(payload);
#else
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(payload);
#endif
        return ToHex(hashBytes);
    }

    private static void AppendField(StringBuilder builder, string name, long value)
    {
        builder.Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
    }

    private static void AppendField(StringBuilder builder, string name, ulong value)
    {
        builder.Append(name).Append('=').Append(value.ToString(CultureInfo.InvariantCulture)).Append(';');
    }

    private static void AppendField(StringBuilder builder, string name, string value)
    {
        builder.Append(name).Append('=').Append(value).Append(';');
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        for (var i = 0; i < bytes.Length; i++)
        {
            sb.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}
