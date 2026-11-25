using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    using ExecutionNodeDto = TriggerPlanJsonDatabase.ExecutionNodeDto;
    using TriggerPlanDatabaseDto = TriggerPlanJsonDatabase.TriggerPlanDatabaseDto;

    /// <summary>
    /// 保存单份文档的行为与节点查找索引；不修改原始描述，也不缓存运行时节点。
    /// </summary>
    internal sealed class TriggerPlanExecutionNodeReferenceCatalog
    {
        private static readonly IReadOnlyDictionary<string, ExecutionNodeDto> Empty = new Dictionary<string, ExecutionNodeDto>();
        private readonly IReadOnlyDictionary<string, ExecutionNodeDto> _behaviors;
        private readonly IReadOnlyDictionary<string, ExecutionNodeDto> _nodes;

        internal TriggerPlanExecutionNodeReferenceCatalog(TriggerPlanDatabaseDto document)
        {
            _behaviors = BuildIndex(document.Behaviors);
            _nodes = BuildIndex(document.Nodes);
        }

        /// <summary>
        /// 按引用类型查找描述；通用引用优先查行为，未找到才查节点。
        /// </summary>
        internal ExecutionNodeDto ResolveTarget(string id, string kind)
        {
            if (kind == "behavior")
            {
                if (_behaviors.TryGetValue(id, out var behavior) && behavior != null)
                    return behavior;

                throw new InvalidOperationException($"Behavior reference not found: {id}");
            }

            if (kind == "node")
            {
                if (_nodes.TryGetValue(id, out var node) && node != null)
                    return node;

                throw new InvalidOperationException($"Node reference not found: {id}");
            }

            if (_behaviors.TryGetValue(id, out var behaviorRef) && behaviorRef != null)
                return behaviorRef;

            if (_nodes.TryGetValue(id, out var nodeRef) && nodeRef != null)
                return nodeRef;

            throw new InvalidOperationException($"Execution node reference not found: {id}");
        }

        private static IReadOnlyDictionary<string, ExecutionNodeDto> BuildIndex(Dictionary<string, ExecutionNodeDto> catalog)
        {
            return catalog == null || catalog.Count == 0
                ? Empty
                : new Dictionary<string, ExecutionNodeDto>(catalog, StringComparer.OrdinalIgnoreCase);
        }
    }
}
