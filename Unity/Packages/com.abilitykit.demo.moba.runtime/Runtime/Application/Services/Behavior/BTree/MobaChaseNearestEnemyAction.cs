using BTCore.Runtime;
using BTCore.Runtime.Externals;

namespace AbilityKit.Demo.Moba.Services.Behavior.BTree
{
    /// <summary>
    /// BT 外部动作节点：追击最近的敌人。
    ///
    /// 读取黑板输入（由 <see cref="MobaBTreeDecision"/> 每 tick 同步）：
    /// - enemy.valid / enemy.x / enemy.z / enemy.dist — 最近敌人的位置与距离
    /// - owner.speed — 自身移动速度
    /// - chase.range — 攻击范围（树属性 Properties["range"]，默认 1.5）
    ///
    /// 写出黑板输出：
    /// - out.hasMove / out.moveX / out.moveZ — 是否需要移动及目标位置
    ///
    /// 节点状态：超出范围移动时 Running；进入范围 Success；无有效敌人 Failure。
    /// </summary>
    public sealed class MobaChaseNearestEnemyAction : ExternalAction
    {
        private const string RangeProperty = "range";

        protected override NodeState OnUpdate()
        {
            var bb = Blackboard;
            if (bb == null) return NodeState.Failure;
            if (!bb.GetValue<bool>("enemy.valid"))
            {
                bb.SetValue("out.hasMove", false);
                return NodeState.Failure;
            }

            var range = 1.5f;
            if (Properties != null
                && Properties.TryGetValue(RangeProperty, out var rangeText)
                && float.TryParse(rangeText, out var parsed))
            {
                range = parsed;
            }

            var dist = bb.GetValue<float>("enemy.dist");
            if (dist <= range)
            {
                bb.SetValue("out.hasMove", false);
                return NodeState.Success;
            }

            bb.SetValue("out.hasMove", true);
            bb.SetValue("out.moveX", bb.GetValue<float>("enemy.x"));
            bb.SetValue("out.moveZ", bb.GetValue<float>("enemy.z"));
            return NodeState.Running;
        }
    }
}
