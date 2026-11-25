using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Plan.Json
{
    /// <summary>
    /// 跟踪单棵执行树的当前引用路径，兄弟分支之间不共享活动引用。
    /// </summary>
    internal sealed class TriggerPlanExecutionNodeReferenceResolver
    {
        private readonly TriggerPlanExecutionNodeReferenceCatalog _catalog;
        private HashSet<string> _activeKeys;
        private List<string> _path;

        internal TriggerPlanExecutionNodeReferenceResolver(TriggerPlanExecutionNodeReferenceCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// 返回进入当前节点前的引用路径深度，供退出时恢复父路径。
        /// </summary>
        internal int PathDepth => _path?.Count ?? 0;

        /// <summary>
        /// 展开别名链；引用在所有子节点转换结束前保持活动。
        /// </summary>
        internal TriggerPlanJsonDatabase.ExecutionNodeDto Resolve(TriggerPlanJsonDatabase.ExecutionNodeDto dto)
        {
            while (TryGetReference(dto, out var id, out var kind))
            {
                var key = kind + ":" + id;
                if (_activeKeys == null)
                {
                    _activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _path = new List<string>();
                }

                if (!_activeKeys.Add(key))
                {
                    throw new InvalidOperationException($"Cyclic execution node reference detected: {key}");
                }

                _path.Add(key);
                dto = _catalog.ResolveTarget(id, kind);
            }

            return dto;
        }

        /// <summary>
        /// 从当前路径移除本次节点访问添加的引用，包括异常退出的情况。
        /// </summary>
        internal void RestorePath(int depth)
        {
            if (_path == null)
            {
                return;
            }

            for (int index = _path.Count - 1; index >= depth; index--)
            {
                _activeKeys.Remove(_path[index]);
                _path.RemoveAt(index);
            }
        }

        private static bool TryGetReference(TriggerPlanJsonDatabase.ExecutionNodeDto dto, out string id, out string kind)
        {
            id = null;
            kind = null;
            if (dto == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(dto.BehaviorRef))
            {
                id = dto.BehaviorRef;
                kind = "behavior";
                return true;
            }

            if (!string.IsNullOrEmpty(dto.BehaviorId))
            {
                id = dto.BehaviorId;
                kind = "behavior";
                return true;
            }

            if (!string.IsNullOrEmpty(dto.NodeRef))
            {
                id = dto.NodeRef;
                kind = "node";
                return true;
            }

            if (!string.IsNullOrEmpty(dto.NodeId))
            {
                id = dto.NodeId;
                kind = "node";
                return true;
            }

            if (!string.IsNullOrEmpty(dto.Ref))
            {
                id = dto.Ref;
                kind = "any";
                return true;
            }

            return false;
        }
    }
}
