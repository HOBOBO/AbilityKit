using System;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 执行中管线的值状态，不持有阶段对象、上下文、配置或服务。
    /// 数组由捕获者单独持有；传入校验及写回时，调用方不得修改其内容。
    /// </summary>
    public readonly struct PipelineRunProgress<TNode> where TNode : PipelinePhaseProgress<TNode>
    {
        /// <summary>
        /// 用已独立拥有的值记录构造状态，不复制数组。
        /// </summary>
        public PipelineRunProgress(int configId, string configName, int currentPhaseIndex,
            bool hasCurrentPhase, bool isPaused, bool isCancelled, TNode[] phases)
        {
            ConfigId = configId;
            ConfigName = configName;
            CurrentPhaseIndex = currentPhaseIndex;
            HasCurrentPhase = hasCurrentPhase;
            IsPaused = isPaused;
            IsCancelled = isCancelled;
            Phases = phases;
        }

        /// <summary>
        /// 已解析配置的标识。
        /// </summary>
        public int ConfigId { get; }

        /// <summary>
        /// 已解析配置的名称，用于匹配原定义。
        /// </summary>
        public string ConfigName { get; }

        /// <summary>
        /// 顶层阶段的执行游标。
        /// </summary>
        public int CurrentPhaseIndex { get; }

        /// <summary>
        /// 游标处是否存在当前执行阶段。
        /// </summary>
        public bool HasCurrentPhase { get; }

        /// <summary>
        /// 运行是否已暂停。
        /// </summary>
        public bool IsPaused { get; }

        /// <summary>
        /// 运行是否已请求取消而尚未结算。
        /// </summary>
        public bool IsCancelled { get; }

        /// <summary>
        /// 按原定义次序排列的顶层状态节点。
        /// </summary>
        public TNode[] Phases { get; }
    }

    /// <summary>
    /// 单个阶段的纯值进度。通用字段的槽位由管线解释，调用方可按原有格式编码。
    /// </summary>
    public abstract class PipelinePhaseProgress<TNode> where TNode : PipelinePhaseProgress<TNode>
    {
        /// <summary>
        /// 阶段的定义标识。
        /// </summary>
        public string PhaseId { get; set; } = string.Empty;

        /// <summary>
        /// 用于匹配同一阶段定义类型的名称。
        /// </summary>
        public string PhaseType { get; set; } = string.Empty;

        /// <summary>
        /// 阶段完成标志。
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// 由阶段类型解释的整型进度，如游标、已选择分支或有序活动索引。
        /// </summary>
        public int[] Integers { get; set; } = Array.Empty<int>();

        /// <summary>
        /// 保留给自定义阶段的长整数状态，内置阶段不使用。
        /// </summary>
        public long[] Longs { get; set; } = Array.Empty<long>();

        /// <summary>
        /// 由阶段类型解释的计时值。内置计时单位与阶段推进一致，不重新取样。
        /// </summary>
        public double[] Numbers { get; set; } = Array.Empty<double>();

        /// <summary>
        /// 按原定义次序排列的子节点状态。
        /// </summary>
        public TNode[] Children { get; set; } = Array.Empty<TNode>();
    }

    /// <summary>
    /// 不需要扩展节点类型时使用的阶段值状态。
    /// </summary>
    public sealed class PipelinePhaseProgress : PipelinePhaseProgress<PipelinePhaseProgress>
    {
    }
}
