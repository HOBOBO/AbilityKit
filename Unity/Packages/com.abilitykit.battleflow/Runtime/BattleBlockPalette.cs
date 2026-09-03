using System;
using System.Collections.Generic;

namespace AbilityKit.BattleFlow
{
    /// <summary>积木调色板注册表：框架注册内置积木模板，项目注册自己的积木（断言/复合）。编辑器调色板枚举这里。</summary>
    public static class BattleBlockPalette
    {
        private static readonly List<BattleBlock> _templates = new List<BattleBlock>();

        /// <summary>已注册的积木模板（编辑器调色板枚举，点击克隆成实例）。</summary>
        public static IReadOnlyList<BattleBlock> Templates => _templates;

        /// <summary>注册一个积木模板。</summary>
        public static void Register(BattleBlock template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            _templates.Add(template);
        }

        static BattleBlockPalette()
        {
            Register(new SetEnvironmentBlock { Id = "set-environment", DisplayName = "设置环境" });
            Register(new SpawnActorBlock { Id = "spawn-actor", DisplayName = "生成角色" });
            Register(new TimelineStepBlock { Id = "timeline-step", DisplayName = "时间线步骤" });
            Register(new WaitBlock { Id = "wait", DisplayName = "等待" });
            Register(new MoveToBlock { Id = "move-to", DisplayName = "移动到" });
            Register(new PlaceObstacleBlock { Id = "place-obstacle", DisplayName = "放置障碍" });
        }
    }
}
