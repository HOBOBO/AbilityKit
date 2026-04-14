using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线生命周期注册表接口
    /// 负责管理活跃的管线运行实例
    /// </summary>
    public interface IPipelineRegistry
    {
        /// <summary>
        /// 注册一个管线运行实例
        /// </summary>
        void Register(IPipelineLifeOwner owner);

        /// <summary>
        /// 注销一个管线运行实例
        /// </summary>
        void Unregister(IPipelineLifeOwner owner);

        /// <summary>
        /// 获取所有活跃的管线运行实例
        /// </summary>
        IReadOnlyList<IPipelineLifeOwner> GetActiveOwners();

        /// <summary>
        /// 活跃实例数量
        /// </summary>
        int ActiveCount { get; }

        /// <summary>
        /// 中断所有活跃管线
        /// </summary>
        void InterruptAll();

        /// <summary>
        /// 按阶段ID过滤活跃管线
        /// </summary>
        IReadOnlyList<IPipelineLifeOwner> GetOwnersByPhase(AbilityPipelinePhaseId phaseId);

        /// <summary>
        /// 按状态过滤活跃管线
        /// </summary>
        IReadOnlyList<IPipelineLifeOwner> GetOwnersByState(EAbilityPipelineState state);
    }

    /// <summary>
    /// 管线生命周期注册表事件
    /// </summary>
    public static class PipelineRegistryEvents
    {
        /// <summary>
        /// 当注册表发生变化时触发
        /// </summary>
        public static System.Action OnChanged;

        /// <summary>
        /// 当管线运行开始时触发
        /// </summary>
        public static System.Action<IPipelineLifeOwner> OnRunStarted;

        /// <summary>
        /// 当管线运行结束时触发
        /// </summary>
        public static System.Action<IPipelineLifeOwner, EAbilityPipelineState> OnRunEnded;

        /// <summary>
        /// 当全局中断时触发
        /// </summary>
        public static System.Action OnGlobalInterrupt;
    }
}
