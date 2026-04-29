using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Schedule.Behavior;
using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule
{
    /// <summary>
    /// 调度管理器接口
    /// 定义调度器的核心功能，所有调度器都应实现此接口
    /// 
    /// 使用场景：
    /// - Buff 系统：注册周期伤害、持续增益
    /// - 子弹系统：注册飞行轨迹更新
    /// - AOE 系统：注册区域持续伤害
    /// - 通用延迟：注册一次性延迟执行
    /// </summary>
    public interface IScheduleManager
    {
        #region 属性

        /// <summary>
        /// 活跃的调度项数量
        /// </summary>
        int ActiveCount { get; }

        /// <summary>
        /// 总注册数量
        /// </summary>
        int TotalCount { get; }

        #endregion

        #region 注册

        /// <summary>
        /// 注册一个调度项
        /// </summary>
        /// <param name="request">注册请求</param>
        /// <param name="effect">调度效果</param>
        /// <returns>调度句柄，用于后续查询和修改</returns>
        ScheduleHandle Register(ScheduleRegisterRequest request, IScheduleEffect effect);

        /// <summary>
        /// 注册周期性调度
        /// </summary>
        /// <param name="intervalMs">执行间隔（毫秒）</param>
        /// <param name="maxExecutions">最大执行次数，-1 表示无限</param>
        /// <param name="businessId">业务对象ID（关联具体业务对象，如 BuffId、子弹Id）</param>
        /// <param name="effect">调度效果</param>
        ScheduleHandle RegisterPeriodic(float intervalMs, int maxExecutions, int businessId, IScheduleEffect effect);

        /// <summary>
        /// 注册持续调度（需要手动终止）
        /// </summary>
        /// <param name="intervalMs">执行间隔（毫秒）</param>
        /// <param name="businessId">业务对象ID（关联具体业务对象）</param>
        /// <param name="effect">调度效果</param>
        ScheduleHandle RegisterContinuous(float intervalMs, int businessId, IScheduleEffect effect);

        /// <summary>
        /// 注册延迟调度
        /// </summary>
        /// <param name="delayMs">延迟时间（毫秒）</param>
        /// <param name="businessId">业务对象ID（关联具体业务对象）</param>
        /// <param name="effect">调度效果</param>
        ScheduleHandle RegisterDelayed(float delayMs, int businessId, IScheduleEffect effect);

        #endregion

        #region 查询

        /// <summary>
        /// 获取调度项数据
        /// </summary>
        /// <param name="handle">调度句柄</param>
        /// <param name="item">调度项数据</param>
        /// <returns>是否成功获取</returns>
        bool TryGetItem(ScheduleHandle handle, out ScheduleItemData item);

        /// <summary>
        /// 根据业务对象ID查找所有调度项
        /// </summary>
        /// <param name="businessId">业务对象ID（如 BuffId、子弹Id）</param>
        /// <returns>匹配的调度项列表</returns>
        List<ScheduleItemData> FindByBusinessId(int businessId);

        /// <summary>
        /// 根据业务对象ID查找所有调度句柄
        /// </summary>
        /// <param name="businessId">业务对象ID</param>
        /// <returns>匹配的调度句柄列表</returns>
        List<ScheduleHandle> FindHandlesByBusinessId(int businessId);

        #endregion

        #region 修改

        /// <summary>
        /// 修改调度项参数
        /// </summary>
        bool Modify(ScheduleHandle handle, in ScheduleModifyRequest request);

        /// <summary>
        /// 设置速度
        /// </summary>
        bool SetSpeed(ScheduleHandle handle, float speed);

        /// <summary>
        /// 设置间隔
        /// </summary>
        bool SetInterval(ScheduleHandle handle, float intervalMs);

        /// <summary>
        /// 延长执行次数
        /// </summary>
        bool AddExecutions(ScheduleHandle handle, int count);

        #endregion

        #region 控制

        /// <summary>
        /// 暂停调度项
        /// </summary>
        bool Pause(ScheduleHandle handle);

        /// <summary>
        /// 恢复调度项
        /// </summary>
        bool Resume(ScheduleHandle handle);

        /// <summary>
        /// 中断调度项
        /// </summary>
        bool Interrupt(ScheduleHandle handle, string reason = null);

        /// <summary>
        /// 取消调度项（立即移除）
        /// </summary>
        bool Cancel(ScheduleHandle handle);

        /// <summary>
        /// 暂停所有
        /// </summary>
        void PauseAll();

        /// <summary>
        /// 恢复所有
        /// </summary>
        void ResumeAll();

        /// <summary>
        /// 中断所有可中断的
        /// </summary>
        /// <returns>中断的数量</returns>
        int InterruptAll(string reason = null);

        #endregion

        #region 更新

        /// <summary>
        /// 每帧更新
        /// </summary>
        /// <param name="deltaTimeMs">帧间隔（毫秒）</param>
        void Update(float deltaTimeMs);

        #endregion

        #region 清理

        /// <summary>
        /// 清空所有调度项
        /// </summary>
        void Clear();

        #endregion
    }

    /// <summary>
    /// 分组调度管理器接口
    /// 继承自 IScheduleManager，额外提供分组管理能力
    /// 
    /// 使用场景：
    /// - Trigger 系统：按 TriggerId 分组管理调度项
    /// - 场景系统：按场景分组管理调度项
    /// - 阵营系统：按阵营分组管理调度项
    /// 
    /// 注意：分组ID是通用概念，不限于 Trigger
    /// </summary>
    public interface IGroupedScheduleManager : IScheduleManager
    {
        #region 分组属性

        /// <summary>
        /// 获取所有活跃的分组ID
        /// </summary>
        IReadOnlyList<int> GetActiveGroupIds();

        /// <summary>
        /// 获取指定分组的调度项数量
        /// </summary>
        int GetItemCountByGroup(int groupId);

        #endregion

        #region 分组注册

        /// <summary>
        /// 为指定分组注册调度项
        /// </summary>
        /// <param name="groupId">分组ID</param>
        /// <param name="request">注册请求</param>
        /// <param name="effect">调度效果</param>
        ScheduleHandle RegisterForGroup(int groupId, ScheduleRegisterRequest request, IScheduleEffect effect);

        /// <summary>
        /// 为指定分组注册周期性调度
        /// </summary>
        ScheduleHandle RegisterPeriodicForGroup(int groupId, float intervalMs, int maxExecutions, int businessId, IScheduleEffect effect);

        /// <summary>
        /// 为指定分组注册持续调度
        /// </summary>
        ScheduleHandle RegisterContinuousForGroup(int groupId, float intervalMs, int businessId, IScheduleEffect effect);

        #endregion

        #region 分组查询

        /// <summary>
        /// 根据分组ID查找所有调度项
        /// </summary>
        List<ScheduleItemData> FindByGroupId(int groupId);

        /// <summary>
        /// 根据分组ID查找所有调度句柄
        /// </summary>
        List<ScheduleHandle> FindHandlesByGroupId(int groupId);

        #endregion

        #region 分组控制

        /// <summary>
        /// 暂停指定分组的所有调度项
        /// </summary>
        void PauseGroup(int groupId);

        /// <summary>
        /// 恢复指定分组的所有调度项
        /// </summary>
        void ResumeGroup(int groupId);

        /// <summary>
        /// 中断指定分组的所有调度项
        /// </summary>
        /// <returns>中断的数量</returns>
        int InterruptGroup(int groupId, string reason = null);

        /// <summary>
        /// 移除指定分组的所有调度项
        /// </summary>
        /// <returns>移除的数量</returns>
        int RemoveGroup(int groupId);

        #endregion

        #region 分组生命周期

        /// <summary>
        /// 分组激活时调用
        /// </summary>
        void OnGroupActivated(int groupId);

        /// <summary>
        /// 分组停用时调用
        /// </summary>
        void OnGroupDeactivated(int groupId);

        #endregion
    }
}
