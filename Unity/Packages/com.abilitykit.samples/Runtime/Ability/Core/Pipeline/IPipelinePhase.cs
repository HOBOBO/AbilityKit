#nullable enable

using System;
using System.Threading.Tasks;

namespace AbilityKit.Samples.Logic.Ability.Core.Pipeline
{
    /// <summary>
    /// 管线阶段的接口定义。
    /// 每个阶段负责处理特定的逻辑，并决定是否继续执行后续阶段。
    /// </summary>
    public interface IPipelinePhase
    {
        /// <summary>
        /// 阶段的唯一标识符。
        /// </summary>
        string PhaseId { get; }

        /// <summary>
        /// 阶段的执行优先级，数值越小越先执行。
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 执行阶段逻辑。
        /// </summary>
        /// <param name="context">管线上下文</param>
        /// <returns>执行结果</returns>
        PhaseResult Execute(IPipelineContext context);

        /// <summary>
        /// 异步执行阶段逻辑。
        /// </summary>
        /// <param name="context">管线上下文</param>
        /// <returns>异步执行结果</returns>
        Task<PhaseResult> ExecuteAsync(IPipelineContext context);
    }

    /// <summary>
    /// 阶段的执行结果。
    /// </summary>
    public enum PhaseResult
    {
        /// <summary>
        /// 阶段成功，继续执行下一个阶段。
        /// </summary>
        Success,

        /// <summary>
        /// 阶段失败，中断管线执行。
        /// </summary>
        Failure,

        /// <summary>
        /// 阶段被跳过，跳到下一个阶段。
        /// </summary>
        Skip,

        /// <summary>
        /// 阶段请求暂停，等待外部信号继续。
        /// </summary>
        Pending
    }
}
