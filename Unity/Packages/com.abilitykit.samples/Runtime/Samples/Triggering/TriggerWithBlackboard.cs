#nullable enable

using System;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.Triggering
{
    /// <summary>
    /// TriggerWithBlackboard - 带黑板的触发器
    /// 演示如何使用 Blackboard 在触发器间共享状态
    /// </summary>
    [Sample]
    public sealed class TriggerWithBlackboard : SampleBase
    {
        public override string Title => "Trigger with Blackboard";
        public override string Description => "使用 Blackboard 在触发器间共享状态";
        public override SampleCategory Category => SampleCategory.Triggering;

        protected override void OnRun()
        {
            Log("=== 触发器与黑板(Blackboard) ===");
            Output.Divider();

            Log("Blackboard 用于在触发器间共享和存储运行时状态");
            Log("");

            Log("核心接口: IBlackboard");
            Output.Bullet("SetInt(key, value) / GetInt(key)");
            Output.Bullet("SetFloat(key, value) / GetFloat(key)");
            Output.Bullet("SetObject<T>(key, value) / GetObject<T>(key)");

            Output.Divider();

            Log("典型应用:");
            Output.Bullet("连击计数: combo = GetInt(\"combo\") + 1");
            Output.Bullet("Buff 状态: SetBool(\"hasShield\", true)");
            Output.Bullet("目标追踪: SetObject(\"lastTarget\", target)");

            Output.Divider();

            Log("触发器协同示例:");
            Log("  [Trigger1] -> 设置 combo=1");
            Log("  [Trigger2] -> 检查 combo>=3 -> 触发 bonus");
            Log("  [Trigger3] -> 造成 damage (享受 bonus 加成)");

            Output.Divider();

            Log("API 参考:");
            Log("  AbilityKit.Triggering.Blackboard");
        }
    }
}
