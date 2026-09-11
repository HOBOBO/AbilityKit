#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Ability.Core.Pipeline
{
    /// <summary>
    /// 管线接口，定义管线的结构和执行方式。
    /// </summary>
    public interface IPipeline
    {
        /// <summary>
        /// 管线的唯一标识符。
        /// </summary>
        string PipelineId { get; }

        /// <summary>
        /// 管线的显示名称。
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 获取管线的所有阶段。
        /// </summary>
        IReadOnlyList<IPipelinePhase> Phases { get; }

        /// <summary>
        /// 执行管线。
        /// </summary>
        PipelineResult Execute(IPipelineContext context);
    }

    /// <summary>
    /// 时间线管线接口，支持基于时间的阶段执行。
    /// </summary>
    public interface ITimelinePipeline : IPipeline
    {
        /// <summary>
        /// 获取管线的总时长（毫秒）。
        /// </summary>
        int TotalDurationMs { get; }

        /// <summary>
        /// 获取当前时间点的事件。
        /// </summary>
        IEnumerable<TimelineEvent> GetEventsAt(int elapsedMs);
    }

    /// <summary>
    /// 时间线事件，用于在特定时间点触发动作。
    /// </summary>
    public readonly struct TimelineEvent
    {
        public int AtMs { get; }
        public string EventId { get; }
        public object Data { get; }

        public TimelineEvent(int atMs, string eventId, object data = null)
        {
            AtMs = atMs;
            EventId = eventId;
            Data = data;
        }
    }

    /// <summary>
    /// 管线执行结果。
    /// </summary>
    public enum PipelineResult
    {
        /// <summary>
        /// 管线成功执行完成。
        /// </summary>
        Success,

        /// <summary>
        /// 管线执行失败。
        /// </summary>
        Failure,

        /// <summary>
        /// 管线被取消。
        /// </summary>
        Cancelled,

        /// <summary>
        /// 管线正在等待外部信号。
        /// </summary>
        Pending
    }
}
