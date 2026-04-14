using AbilityKit.Triggering.Runtime.Config.Plans;

namespace AbilityKit.Triggering.Runtime.Behavior
{
    /// <summary>
    /// 触发器行为接口（运行时执行逻辑）
    /// </summary>
    public interface ITriggerBehavior
    {
        ITriggerPlanConfig Config { get; }
        bool Evaluate(IBehaviorContext context);
        BehaviorExecutionResult Execute(IBehaviorContext context);
    }

    /// <summary>
    /// 简单触发器行为（瞬时完成）
    /// </summary>
    public interface ISimpleTriggerBehavior : ITriggerBehavior
    {
    }

    /// <summary>
    /// 可调度的触发器行为（支持延迟、周期等）
    /// </summary>
    public interface ISchedulableBehavior : ITriggerBehavior
    {
        EBehaviorState State { get; }
        long ElapsedMs { get; }

        void Begin(IBehaviorContext context);
        void Update(float deltaTimeMs, IBehaviorContext context);
        void Pause();
        void Resume();
        void Interrupt(string reason);

        BehaviorSnapshot CreateSnapshot();
        void RestoreFromSnapshot(BehaviorSnapshot snapshot);
    }

    /// <summary>
    /// 组合行为接口
    /// </summary>
    public interface ICompositeBehavior : ITriggerBehavior
    {
        int ChildCount { get; }
        ITriggerBehavior GetChild(int index);
        void AddChild(ITriggerBehavior child);
        void ClearChildren();
    }
}