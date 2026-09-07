using System;
using System.Collections.Generic;

namespace AbilityKit.BattleFlow
{
    /// <summary>积木调色板注册表（按类别分组）：框架注册内置积木模板，项目注册自己的积木。编辑器调色板枚举这里。</summary>
    public static class BattleBlockPalette
    {
        private static readonly Dictionary<string, List<BattleBlock>> _groups = new Dictionary<string, List<BattleBlock>>();

        /// <summary>类别 → 积木模板列表（编辑器调色板枚举，点击克隆成实例）。</summary>
        public static IReadOnlyDictionary<string, List<BattleBlock>> Groups => _groups;

        /// <summary>按类别注册一个积木模板。</summary>
        public static void Register(string category, BattleBlock template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            if (!_groups.TryGetValue(category, out var list))
                _groups[category] = list = new List<BattleBlock>();
            list.Add(template);
        }

        static BattleBlockPalette()
        {
            Register("环境", new SetEnvironmentBlock { Id = "set-environment", DisplayName = "设置环境" });
            Register("环境", new PlaceObstacleBlock { Id = "place-obstacle", DisplayName = "放置障碍" });
            Register("角色", new SpawnActorBlock { Id = "spawn-actor", DisplayName = "生成角色" });
            Register("驱动", new TimelineStepBlock { Id = "timeline-step", DisplayName = "时间线步骤" });
            Register("驱动", new WaitBlock { Id = "wait", DisplayName = "等待" });
            Register("驱动", new MoveToBlock { Id = "move-to", DisplayName = "移动到" });
        }
    }
}
