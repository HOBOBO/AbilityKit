#if UNITY_EDITOR
#nullable enable annotations
using System;
using System.Collections.Generic;

namespace AbilityKit.BattleScenario.Editor
{
    /// <summary>
    /// 项目为用例编辑器提供仓库目录、项目搜索词和画面播放入口；不改变场景编译或断言运行契约。
    /// </summary>
    public static class BattleScenarioEditorIntegration
    {
        /// <summary>
        /// 需要递归索引的项目用例目录，允许位于 Unity Assets 之外。
        /// </summary>
        public static IReadOnlyList<string> CaseDirectories { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 从已读取的用例提取项目级搜索词，例如英雄显示名与技能槽位。
        /// </summary>
        public static Func<BattleScenarioDocument, IEnumerable<string>>? SearchTerms { get; set; }

        /// <summary>
        /// 播放已保存用例；第二个参数为 true 表示无交互预览。返回 false 表示未启动。
        /// </summary>
        public static Func<string, bool, bool>? PlaySavedCase { get; set; }
    }
}
#endif
