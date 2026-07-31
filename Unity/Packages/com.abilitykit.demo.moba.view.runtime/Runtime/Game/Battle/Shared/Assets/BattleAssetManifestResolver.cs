using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Shared.Assets
{
    /// <summary>
    /// 从冻结的 <see cref="IBattleAssetManifestSource"/> 确定性派生 <see cref="BattleAssetManifest"/>。
    /// 相同 source 必须产出完全相同的 entries（按 AssetKey 稳定排序，去重）。
    /// </summary>
    /// <remarks>
    /// 解析器只依赖 <see cref="IBattleAssetManifestSource"/> 抽象视图，
    /// 不直接引用上层 Room 快照类型，以避免 Shared.Assets ↔ View.Runtime 程序集循环依赖。
    /// 上层调用方负责将具体快照适配为 <see cref="IBattleAssetManifestSource"/>。
    /// </remarks>
    public static class BattleAssetManifestResolver
    {
        /// <summary>
        /// 默认地图 key（当 source 未提供 map 信息时使用）。
        /// </summary>
        public const string DefaultMapKey = "classic";

        /// <summary>
        /// 固定必需的配置资源引用（无论玩家构成如何都必须加载）。
        /// </summary>
        private static readonly BattleAssetEntry[] FixedConfigEntries =
        {
            new BattleAssetEntry("moba/skills", "config:skills", BattleAssetKind.Config),
            new BattleAssetEntry("moba/characters", "config:characters", BattleAssetKind.Config),
            new BattleAssetEntry("moba/projectiles", "config:projectiles", BattleAssetKind.Config)
        };

        /// <summary>
        /// 从 source 解析出确定性资源清单。
        /// </summary>
        public static BattleAssetManifest Resolve(IBattleAssetManifestSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var entries = new List<BattleAssetEntry>();

            // 1. 固定 config 引用
            for (var i = 0; i < FixedConfigEntries.Length; i++)
            {
                entries.Add(FixedConfigEntries[i]);
            }

            var players = source.Players;

            // 2. 角色资源引用（从 Players.HeroId 派生）
            if (players != null)
            {
                var seenHero = new HashSet<int>();
                for (var i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player == null)
                    {
                        continue;
                    }

                    var heroId = player.HeroId;
                    if (heroId > 0 && seenHero.Add(heroId))
                    {
                        entries.Add(new BattleAssetEntry(
                            "moba/characters/characters_" + heroId,
                            "character:" + heroId,
                            BattleAssetKind.Character));
                    }
                }
            }

            // 3. 技能配置引用（从 Players.SkillIds + BasicAttackSkillId 派生）
            if (players != null)
            {
                var seenSkill = new HashSet<int>();
                for (var i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player == null)
                    {
                        continue;
                    }

                    TryAddSkillEntry(entries, seenSkill, player.BasicAttackSkillId);
                    var skillIds = player.SkillIds;
                    if (skillIds != null)
                    {
                        for (var j = 0; j < skillIds.Count; j++)
                        {
                            TryAddSkillEntry(entries, seenSkill, skillIds[j]);
                        }
                    }
                }
            }

            // 4. 地图引用
            var mapKey = ResolveMapKey();
            entries.Add(new BattleAssetEntry(
                "moba/battle_maps",
                "map:" + mapKey,
                BattleAssetKind.Map));

            // 5. 确定性排序：按 AssetKey 稳定排序，相同 key 按 AssetPath 二级排序
            entries.Sort(EntryComparer.Instance);

            // 6. 去重（按 AssetKey），保留首个
            var deduped = DedupByKey(entries);

            return new BattleAssetManifest(
                source.LaunchManifestVersion,
                source.LaunchManifestHash,
                source.LaunchGeneration,
                deduped);
        }

        private static void TryAddSkillEntry(List<BattleAssetEntry> entries, HashSet<int> seen, int skillId)
        {
            if (skillId > 0 && seen.Add(skillId))
            {
                entries.Add(new BattleAssetEntry(
                    "moba/skills/skills_" + skillId,
                    "skill:" + skillId,
                    BattleAssetKind.Config));
            }
        }

        private static string ResolveMapKey()
        {
            // 当前阶段 source 没有显式 map 字段；保留扩展点。
            // 未来可从 source 的 Tags 读取 map key。
            return DefaultMapKey;
        }

        private static IReadOnlyList<BattleAssetEntry> DedupByKey(List<BattleAssetEntry> sorted)
        {
            if (sorted.Count == 0)
            {
                return Array.Empty<BattleAssetEntry>();
            }

            var result = new List<BattleAssetEntry>(sorted.Count);
            string lastKey = null;
            for (var i = 0; i < sorted.Count; i++)
            {
                var entry = sorted[i];
                if (lastKey != null && entry.AssetKey == lastKey)
                {
                    continue;
                }

                result.Add(entry);
                lastKey = entry.AssetKey;
            }

            return result;
        }

        private sealed class EntryComparer : IComparer<BattleAssetEntry>
        {
            public static readonly EntryComparer Instance = new EntryComparer();

            public int Compare(BattleAssetEntry x, BattleAssetEntry y)
            {
                var c = string.CompareOrdinal(x.AssetKey, y.AssetKey);
                if (c != 0)
                {
                    return c;
                }

                return string.CompareOrdinal(x.AssetPath, y.AssetPath);
            }
        }
    }
}
