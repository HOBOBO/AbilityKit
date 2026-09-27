#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Shared.Assets
{
    /// <summary>
    /// 资源类别。用于在 manifest 中区分不同来源的必需资源。
    /// </summary>
    public enum BattleAssetKind
    {
        /// <summary>配置表（skills.json, characters.json 等）。</summary>
        Config = 0,

        /// <summary>角色相关资源。</summary>
        Character = 1,

        /// <summary>表现层资源。</summary>
        Presentation = 2,

        /// <summary>地图 / 场景资源。</summary>
        Map = 3,

        /// <summary>通用兜底类别。</summary>
        Generic = 99
    }

    /// <summary>
    /// 单个必需资源条目。描述一次战斗必须加载的一个资源引用。
    /// </summary>
    public readonly struct BattleAssetEntry : IEquatable<BattleAssetEntry>
    {
        /// <summary>Resources 路径，如 "moba/skills" 或 "moba/characters/1001"。</summary>
        public readonly string AssetPath;

        /// <summary>逻辑键，如 "character:1001" 或 "config:skills"。</summary>
        public readonly string AssetKey;

        /// <summary>资源类别。</summary>
        public readonly BattleAssetKind Kind;

        /// <summary>可选预期哈希，用于校验（当前阶段可空）。</summary>
        public readonly string? ExpectedHash;

        public BattleAssetEntry(string? assetPath, string? assetKey, BattleAssetKind kind, string? expectedHash = null)
        {
            AssetPath = assetPath ?? string.Empty;
            AssetKey = assetKey ?? string.Empty;
            Kind = kind;
            ExpectedHash = expectedHash;
        }

        public bool Equals(BattleAssetEntry other)
        {
            return AssetPath == other.AssetPath
                && AssetKey == other.AssetKey
                && Kind == other.Kind
                && ExpectedHash == other.ExpectedHash;
        }

        public override bool Equals(object? obj) => obj is BattleAssetEntry other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (AssetPath == null ? 0 : AssetPath.GetHashCode());
                hash = hash * 31 + (AssetKey == null ? 0 : AssetKey.GetHashCode());
                hash = hash * 31 + Kind.GetHashCode();
                hash = hash * 31 + (ExpectedHash == null ? 0 : ExpectedHash.GetHashCode());
                return hash;
            }
        }

        public override string ToString()
        {
            return "BattleAssetEntry{Key=" + AssetKey + ", Path=" + AssetPath + ", Kind=" + Kind + "}";
        }
    }

    /// <summary>
    /// 从冻结的 RoomLaunchManifest + Room snapshot 派生的确定性资源清单。
    /// 列出本次战斗必须加载的全部资源引用。
    /// 相同 snapshot 必须产出完全相同的 entries（按 AssetKey 稳定排序）。
    /// </summary>
    public sealed class BattleAssetManifest
    {
        public int ManifestVersion { get; }
        public string ManifestHash { get; }
        public long LaunchGeneration { get; }
        public IReadOnlyList<BattleAssetEntry> Entries { get; }

        public BattleAssetManifest(
            int manifestVersion,
            string? manifestHash,
            long launchGeneration,
            IReadOnlyList<BattleAssetEntry>? entries)
        {
            ManifestVersion = manifestVersion;
            ManifestHash = manifestHash ?? string.Empty;
            LaunchGeneration = launchGeneration;
            Entries = entries ?? Array.Empty<BattleAssetEntry>();
        }
    }
}
