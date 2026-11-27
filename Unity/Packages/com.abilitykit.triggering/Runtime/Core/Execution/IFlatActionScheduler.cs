using System;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 扁平计划向宿主登记动作的可选契约，不提供时钟、推进或快照能力。
    /// </summary>
    public interface IFlatActionScheduler
    {
        /// <summary>
        /// 在本次计划执行任何动作前检查支持的动作及分组归属，并返回可登记的分组。
        /// 按引用比较计划所有者；不同计划不能共用同一个仍被占用的触发器 ID。
        /// 动作列表仅供本次装配检查，不改变计划，也不在此调用动作。
        /// </summary>
        IFlatActionScheduleGroup BindPlan(int triggerId, object owner, ReadOnlySpan<ActionCallPlan> actions);
    }

    /// <summary>
    /// 本次扁平计划执行使用的分组登记入口；生命周期由宿主实现管理。
    /// </summary>
    public interface IFlatActionScheduleGroup
    {
        /// <summary>
        /// 按计划索引登记或替换同一所有者的动作。登记不执行回调；
        /// 推进时使用本次成功登记的载荷和回调，条件未满足时不调用动作。
        /// </summary>
        void RegisterOrReplace(int planIndex, in ActionCallPlan plan,
            Action<object, ITriggerDispatcherContext> action, TriggerPredicate<object> condition, object boundArgs);
    }
}
