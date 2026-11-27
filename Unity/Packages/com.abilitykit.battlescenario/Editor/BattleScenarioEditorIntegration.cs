#if UNITY_EDITOR
#nullable enable annotations
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

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

        /// <summary>
        /// 以项目内模型标识创建只读演员投影视图；缺少资源时返回空。
        /// </summary>
        public static Func<int, IActorReplayPreview?>? CreateActorReplayPreview { get; set; }

        /// <summary>
        /// 运行指定的离线施法和解或降级配方。
        /// </summary>
        internal static Func<LocalStateSyncReplay.SkillReplayCase, LocalStateSyncReplay>? CaptureStateSyncSkillReplay { get; set; }

        /// <summary>
        /// 运行离线弹体表现样例并返回逐 Tick 观察；布尔值指定权威确认或拒绝。
        /// </summary>
        internal static Func<bool, Task<LocalStateSyncReplay>>? CaptureProjectilePresentationReplay { get; set; }
    }

    /// <summary>
    /// 只消费已录制姿态的项目预览。视图不执行世界、输入或玩法事件。
    /// </summary>
    public interface IActorReplayPreview : IDisposable
    {
        /// <summary>
        /// 同镜头绘制权威与预测模型。空姿态隐藏对应模型，镜头范围在整份录制内固定。
        /// </summary>
        void Draw(Rect rect, Bounds recordedBounds, Pose? authority, Pose? prediction);
    }

    /// <summary>
    /// 可选的同镜头弹体标记，仅消费已录制位置。
    /// </summary>
    public interface IFlightReplayPreview : IActorReplayPreview
    {
        void SetFlightPositions(Vector3? authority, Vector3? prediction);
    }
}
#endif
