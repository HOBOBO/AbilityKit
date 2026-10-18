using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    /// <summary>
    /// 战斗副本的只读运行态视图。副本表只描述**客户端装配契约**：
    /// 加载哪个流程 Profile、挂哪些扩展点、入场协议约束是什么。
    /// 玩法常量（时长、地图、队伍规模、胜负条件）由 gameplays / battle_maps 等表负责。
    /// </summary>
    public sealed class DungeonMO
    {
        public int Id { get; }
        public string Name { get; }
        public int GameplayId { get; }
        public string FlowProfileId { get; }
        /// <summary>入场协议约束：最少玩家数。</summary>
        public int MinPlayerCount { get; }
        /// <summary>入场协议约束：最多玩家数；0 表示不限。</summary>
        public int MaxPlayerCount { get; }
        public IReadOnlyList<string> AllowedHostModes { get; }
        public string SceneProviderId { get; }
        public string GameplayHandlerId { get; }
        public string OutcomeRuleId { get; }
        public IReadOnlyList<string> RequiredPortGroups { get; }
        public IReadOnlyList<int> Tags { get; }

        public DungeonMO(DungeonDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Name = dto.Name ?? string.Empty;
            GameplayId = dto.GameplayId;
            FlowProfileId = dto.FlowProfileId ?? string.Empty;
            MinPlayerCount = dto.MinPlayerCount;
            MaxPlayerCount = dto.MaxPlayerCount;
            AllowedHostModes = dto.AllowedHostModes ?? Array.Empty<string>();
            SceneProviderId = dto.SceneProviderId ?? string.Empty;
            GameplayHandlerId = dto.GameplayHandlerId ?? string.Empty;
            OutcomeRuleId = dto.OutcomeRuleId ?? string.Empty;
            RequiredPortGroups = dto.RequiredPortGroups ?? Array.Empty<string>();
            Tags = dto.Tags ?? Array.Empty<int>();
        }

        /// <summary>该副本是否允许指定宿主模式；未声明则不限制。</summary>
        public bool AllowsHostMode(string hostMode)
        {
            if (AllowedHostModes.Count == 0) return true;
            if (string.IsNullOrWhiteSpace(hostMode)) return false;
            for (var i = 0; i < AllowedHostModes.Count; i++)
                if (string.Equals(AllowedHostModes[i], hostMode, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>该副本是否接受给定玩家数量。</summary>
        public bool AcceptsPlayerCount(int playerCount)
        {
            if (playerCount < MinPlayerCount) return false;
            return MaxPlayerCount <= 0 || playerCount <= MaxPlayerCount;
        }
    }
}
