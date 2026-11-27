using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 校验执行树调度进度与原地定义的对应关系，不读取任务绑定或修改调度状态。
    /// </summary>
    internal static class TriggerExecutionProgressValidator
    {
        /// <summary>
        /// 校验游标、重复 ID、定义是否存在以及进度字段，返回本次恢复的保留名单。
        /// </summary>
        /// <remarks>
        /// 调用方负责帧边界检查和历史有效性；定义 ID 命中不证明历史分支一致。
        /// 保留名单只供本次恢复使用，不承载顺序，也不缓存预检结果。
        /// </remarks>
        internal static HashSet<long> ValidateSnapshot<TDefinition>(
            in TriggerExecutionProgress progress,
            IReadOnlyDictionary<long, TDefinition> definitions)
        {
            if (progress.NextExecutionId <= 0 || progress.Entries == null)
                throw new InvalidOperationException("Invalid scheduled execution progress or ID cursor.");

            var ids = new HashSet<long>();
            foreach (var item in progress.Entries)
            {
                if (item.Id <= 0 || item.Id >= progress.NextExecutionId || !ids.Add(item.Id) ||
                    !definitions.ContainsKey(item.Id) || !IsValidEntry(in item))
                    throw new InvalidOperationException(
                        $"Scheduled execution {item.Id} cannot be restored without valid progress and its in-process definition.");
            }

            return ids;
        }

        /// <summary>
        /// 检查状态、非负有限时间及非负次数；不查询定义或 ID 游标。
        /// </summary>
        internal static bool IsValidEntry(in TriggerScheduledExecutionInfo progress) =>
            progress.State >= TriggerScheduledExecutionState.Waiting &&
            progress.State <= TriggerScheduledExecutionState.Failed &&
            progress.ElapsedMs >= 0 && !double.IsInfinity(progress.ElapsedMs) &&
            progress.LastExecutionMs >= 0 && !double.IsInfinity(progress.LastExecutionMs) &&
            progress.ExecutionCount >= 0;
    }
}
