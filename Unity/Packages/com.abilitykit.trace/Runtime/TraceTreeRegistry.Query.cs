using System;
using System.Collections.Generic;

namespace AbilityKit.Trace
{
    public sealed partial class TraceTreeRegistry
    {
        /// <summary>
        /// 获取节点快照
        /// </summary>
        /// <param name="contextId">节点 ID</param>
        /// <returns>快照，如果不存在则返回 null</returns>
        public TraceSnapshot TryGetSnapshot(long contextId)
        {
            if (!_contexts.TryGetValue(contextId, out var record))
                return default;

            return new TraceSnapshot(in record);
        }

        /// <summary>
        /// 检查节点是否存在
        /// </summary>
        public bool Contains(long contextId) => _contexts.ContainsKey(contextId);

        /// <summary>
        /// 获取根节点状态
        /// </summary>
        public bool TryGetRootState(long rootId, out RootState state)
        {
            if (_roots.TryGetValue(rootId, out var record))
            {
                state = new RootState(rootId, record.ActiveCount, record.ExternalRefCount, record.LastTouchedFrame);
                return true;
            }

            state = default;
            return false;
        }

        /// <summary>
        /// 获取根节点的所有子节点 ID
        /// </summary>
        public bool TryGetChildren(long parentId, out IReadOnlyList<long> children)
        {
            if (_childrenByParent.TryGetValue(parentId, out var list))
            {
                children = list;
                return true;
            }

            children = EmptyChildrenList;
            return false;
        }

        private static readonly List<long> EmptyChildrenList = new List<long>();

        /// <summary>
        /// 构建从指定节点到根节点的链路
        /// </summary>
        /// <param name="contextId">起始节点 ID</param>
        /// <param name="chain">输出的链路列表（从起始节点到根节点）</param>
        /// <returns>是否成功构建</returns>
        public bool TryBuildChain(long contextId, List<TraceSnapshot> chain)
        {
            chain.Clear();

            if (!_contexts.ContainsKey(contextId))
                return false;

            var current = contextId;
            while (current != 0)
            {
                if (!_contexts.TryGetValue(current, out var record))
                    break;

                chain.Add(new TraceSnapshot(in record));

                // 如果到达根节点，停止
                if (current == record.RootId)
                    break;

                current = record.ParentId;
            }

            return chain.Count > 0;
        }

        /// <summary>
        /// 获取根节点的统计信息
        /// </summary>
        public bool TryGetRootStats(long rootId, out RootStats stats)
        {
            if (!_roots.ContainsKey(rootId))
            {
                stats = default;
                return false;
            }

            var totalNodes = 0;
            var activeNodes = 0;
            var endedNodes = 0;
            var maxDepth = 0;

            CollectStats(rootId, rootId, ref totalNodes, ref activeNodes, ref endedNodes, ref maxDepth, 0);

            stats = new RootStats(rootId, totalNodes, activeNodes, endedNodes, maxDepth);
            return true;
        }

        private void CollectStats(
            long contextId,
            long rootId,
            ref int totalNodes,
            ref int activeNodes,
            ref int endedNodes,
            ref int maxDepth,
            int currentDepth)
        {
            if (!_contexts.TryGetValue(contextId, out var record))
                return;

            if (record.RootId != rootId)
                return;

            totalNodes++;

            if (record.IsEnded)
                endedNodes++;
            else
                activeNodes++;

            maxDepth = Math.Max(maxDepth, currentDepth);

            if (_childrenByParent.TryGetValue(contextId, out var children))
            {
                foreach (var childId in children)
                {
                    CollectStats(childId, rootId, ref totalNodes, ref activeNodes, ref endedNodes, ref maxDepth, currentDepth + 1);
                }
            }
        }

        /// <summary>
        /// 获取指定种类的所有节点
        /// </summary>
        public IEnumerable<TraceSnapshot> GetNodesByKind(int kind)
        {
            foreach (var kvp in _contexts)
            {
                if (kvp.Value.Kind == kind)
                    yield return new TraceSnapshot(in kvp.Value);
            }
        }

        /// <summary>
        /// 获取指定根节点下的所有节点
        /// </summary>
        public IEnumerable<TraceSnapshot> GetNodesByRoot(long rootId)
        {
            foreach (var kvp in _contexts)
            {
                if (kvp.Value.RootId == rootId)
                    yield return new TraceSnapshot(in kvp.Value);
            }
        }

        /// <summary>
        /// 获取所有活跃根节点
        /// </summary>
        public IEnumerable<RootState> GetActiveRoots()
        {
            foreach (var kvp in _roots)
            {
                if (kvp.Value.ActiveCount > 0)
                    yield return new RootState(kvp.Key, kvp.Value.ActiveCount, kvp.Value.ExternalRefCount, kvp.Value.LastTouchedFrame);
            }
        }

        /// <summary>
        /// 获取所有已结束的根节点
        /// </summary>
        public IEnumerable<RootState> GetEndedRoots()
        {
            foreach (var kvp in _roots)
            {
                if (kvp.Value.ActiveCount == 0)
                    yield return new RootState(kvp.Key, kvp.Value.ActiveCount, kvp.Value.ExternalRefCount, kvp.Value.LastTouchedFrame);
            }
        }
    }
}
