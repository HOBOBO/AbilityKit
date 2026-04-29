using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule.Behavior
{
    /// <summary>
    /// 调度策略接口
    /// 定义调度项的状态转换和执行逻辑
    /// 框架提供默认实现（DefaultScheduleStrategy）
    /// 项目方也可自行实现以支持特殊的调度行为（如 ECS）
    /// </summary>
    public interface IScheduleStrategy
    {
        /// <summary>
        /// 每帧更新调度项
        /// </summary>
        /// <param name="item">调度项数据（ref 可修改）</param>
        /// <param name="deltaTimeMs">帧间隔（毫秒，未缩放）</param>
        /// <param name="executor">执行器，用于执行效果</param>
        /// <returns>是否需要移除此调度项</returns>
        bool OnUpdate(ref ScheduleItemData item, float deltaTimeMs, IScheduleExecutor executor);

        /// <summary>
        /// 创建调度上下文
        /// </summary>
        ScheduleContext CreateContext(in ScheduleItemData item, float deltaTimeMs, float scaledDeltaMs);
    }

    /// <summary>
    /// 调度执行器接口
    /// 用于执行调度效果
    /// </summary>
    public interface IScheduleExecutor
    {
        /// <summary>
        /// 尝试执行效果
        /// </summary>
        /// <param name="item">调度项数据</param>
        /// <param name="context">调度上下文</param>
        /// <returns>是否执行成功</returns>
        bool TryExecute(in ScheduleItemData item, in ScheduleContext context);
    }
}
