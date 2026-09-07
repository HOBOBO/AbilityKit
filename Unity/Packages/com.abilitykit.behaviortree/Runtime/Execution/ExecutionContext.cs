using System;
using AbilityKit.Deterministic;
using Blackboard = AbilityKit.BehaviorTree.Blackboard.Blackboard;

namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>
    /// 树级执行上下文：黑板 + 服务解析+ tick 刷新的帧号与定点时间
    /// 由引擎持有并在节点生命周期回调中传入；节点可缓存引用（生命周期与树实例一致）
    /// 构造器公开以支持宿主级单测直接驱动节点
    /// </summary>
    public sealed class ExecutionContext
    {
        public AbilityKit.BehaviorTree.Blackboard.Blackboard Blackboard { get; }
        public ServiceResolver Services { get; }
        public int Frame { get; internal set; }
        public Fixed64 Time { get; internal set; }
        public NodeStopReason StopReason { get; private set; }

        public ExecutionContext(AbilityKit.BehaviorTree.Blackboard.Blackboard blackboard, ServiceResolver services)
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            Services = services ?? new DefaultServiceResolver();
        }

        internal void BeginTick(int frame, Fixed64 time)
        {
            Frame = frame;
            Time = time;
        }

        internal void BeginStop(NodeStopReason reason)
        {
            StopReason = reason;
        }

        internal void EndStop()
        {
            StopReason = NodeStopReason.None;
        }
    }
}
