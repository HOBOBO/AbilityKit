using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Trace
{
    public enum TraceExportOrder
    {
        ContextId = 0,
        TreePreOrder = 1
    }

    /// <summary>
    /// 诊断快照的输出选项。节点和深度上限限制输出，不保证限制查询成本。
    /// </summary>
    public readonly struct TraceExportOptions
    {
        public TraceExportOptions(int maxNodes, bool activeOnly, bool includeMetadata)
            : this(maxNodes, activeOnly, includeMetadata, 0, TraceExportOrder.ContextId)
        {
        }

        public TraceExportOptions(int maxNodes, bool activeOnly, bool includeMetadata, int maxDepth, TraceExportOrder order)
        {
            MaxNodes = maxNodes;
            ActiveOnly = activeOnly;
            IncludeMetadata = includeMetadata;
            MaxDepth = maxDepth;
            Order = order;
        }

        public int MaxNodes { get; }
        public bool ActiveOnly { get; }
        public bool IncludeMetadata { get; }
        public int MaxDepth { get; }
        public TraceExportOrder Order { get; }

        public static TraceExportOptions Full => new TraceExportOptions(0, false, true);
        public static TraceExportOptions ActiveOnlyDefault => new TraceExportOptions(0, true, true);
    }

    /// <summary>
    /// 诊断快照的节点投影。元数据可包含业务对象引用，导出不会深拷贝或清洗它。
    /// </summary>
    public readonly struct TraceNodeExportDto
    {
        public TraceNodeExportDto(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            string kindName,
            int endedFrame,
            int endReason,
            int childCount,
            object metadata)
            : this(
                contextId,
                rootId,
                parentId,
                kind,
                kindName,
                0,
                endedFrame,
                endReason,
                childCount,
                metadata,
                endedFrame != 0)
        {
        }

        public TraceNodeExportDto(
            long contextId,
            long rootId,
            long parentId,
            int kind,
            string kindName,
            int createdFrame,
            int endedFrame,
            int endReason,
            int childCount,
            object metadata,
            bool isEnded)
        {
            ContextId = contextId;
            RootId = rootId;
            ParentId = parentId;
            Kind = kind;
            KindName = kindName;
            CreatedFrame = createdFrame;
            EndedFrame = endedFrame;
            EndReason = endReason;
            ChildCount = childCount;
            Metadata = metadata;
            IsEnded = isEnded;
        }

        public long ContextId { get; }
        public long RootId { get; }
        public long ParentId { get; }
        public int Kind { get; }
        public string KindName { get; }
        public int CreatedFrame { get; }
        public int EndedFrame { get; }
        public int EndReason { get; }
        public int ChildCount { get; }
        public object Metadata { get; }
        public bool IsEnded { get; }
        public bool IsRoot => ContextId == RootId;
    }

    /// <summary>
    /// 用于编辑器、调试和验收的 Trace 诊断快照，不是权威状态或稳定的序列化格式。
    /// 不用于网络同步、存档或回滚；长期证据须转换为独立的版本化产物。
    /// </summary>
    public sealed class TraceTreeExportDto
    {
        public TraceTreeExportDto(long rootId, IReadOnlyList<TraceNodeExportDto> nodes, bool truncated)
        {
            RootId = rootId;
            Nodes = nodes ?? Array.Empty<TraceNodeExportDto>();
            Truncated = truncated;
        }

        public long RootId { get; }
        public IReadOnlyList<TraceNodeExportDto> Nodes { get; }
        public bool Truncated { get; }
    }

    /// <summary>
    /// 在注册表所属的逻辑线程上导出诊断快照，不授予额外的根保留引用。
    /// </summary>
    public static class TraceTreeExportExtensions
    {
        /// <summary>
        /// 导出指定根的完整诊断视图，默认包含业务元数据。
        /// </summary>
        public static TraceTreeExportDto ExportRoot(this TraceTreeRegistryBase registry, long rootId)
        {
            return ExportRoot(registry, rootId, TraceExportOptions.Full);
        }

        /// <summary>
        /// 按指定顺序和输出上限导出单根诊断视图。找不到该根时返回空视图。
        /// </summary>
        public static TraceTreeExportDto ExportRoot(this TraceTreeRegistryBase registry, long rootId, TraceExportOptions options)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            if (rootId == 0) return new TraceTreeExportDto(rootId, Array.Empty<TraceNodeExportDto>(), false);

            var snapshots = registry.GetNodeSnapshotsByRoot(rootId);
            return BuildExport(registry, rootId, snapshots, options);
        }

        /// <summary>
        /// 导出符合条件的根。活动筛选仅作用于根，输出上限按每棵树计算。
        /// </summary>
        public static IReadOnlyList<TraceTreeExportDto> ExportRoots(this TraceTreeRegistryBase registry, TraceExportOptions options)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var roots = options.ActiveOnly ? registry.GetActiveRoots() : registry.GetRootStates();
            var result = new List<TraceTreeExportDto>();
            foreach (var root in roots)
            {
                result.Add(registry.ExportRoot(root.RootId, options));
            }

            return result;
        }

        private static TraceTreeExportDto BuildExport(TraceTreeRegistryBase registry, long rootId, IEnumerable<TraceNodeSnapshot> snapshots, TraceExportOptions options)
        {
            var maxNodes = options.MaxNodes;
            var truncated = false;
            var nodes = new List<TraceNodeExportDto>();
            var snapshotList = snapshots.ToList();
            var depthByContextId = options.MaxDepth > 0
                ? BuildDepthMap(rootId, snapshotList)
                : null;

            foreach (var snapshot in OrderSnapshots(rootId, snapshotList, options.Order))
            {
                if (depthByContextId != null && depthByContextId.TryGetValue(snapshot.ContextId, out var depth) && depth > options.MaxDepth)
                {
                    truncated = true;
                    continue;
                }

                if (maxNodes > 0 && nodes.Count >= maxNodes)
                {
                    truncated = true;
                    break;
                }

                nodes.Add(ToDto(registry, in snapshot, options.IncludeMetadata));
            }

            return new TraceTreeExportDto(rootId, nodes, truncated);
        }

        private static IEnumerable<TraceNodeSnapshot> OrderSnapshots(long rootId, IReadOnlyList<TraceNodeSnapshot> snapshots, TraceExportOrder order)
        {
            if (order == TraceExportOrder.TreePreOrder)
            {
                var childrenByParent = snapshots
                    .Where(item => item.ParentId != 0)
                    .GroupBy(item => item.ParentId)
                    .ToDictionary(item => item.Key, item => item.OrderBy(child => child.ContextId).ToList());
                var snapshotByContext = snapshots.ToDictionary(item => item.ContextId);
                var visited = new HashSet<long>();

                if (snapshotByContext.TryGetValue(rootId, out var root))
                {
                    foreach (var item in TraverseTree(root, childrenByParent, visited))
                    {
                        yield return item;
                    }
                }

                foreach (var item in snapshots.OrderBy(item => item.ContextId))
                {
                    if (visited.Add(item.ContextId))
                        yield return item;
                }

                yield break;
            }

            foreach (var item in snapshots.OrderBy(item => item.ContextId))
            {
                yield return item;
            }
        }

        private static IEnumerable<TraceNodeSnapshot> TraverseTree(TraceNodeSnapshot snapshot, IReadOnlyDictionary<long, List<TraceNodeSnapshot>> childrenByParent, ISet<long> visited)
        {
            if (!visited.Add(snapshot.ContextId))
                yield break;

            yield return snapshot;

            if (!childrenByParent.TryGetValue(snapshot.ContextId, out var children))
                yield break;

            foreach (var child in children)
            {
                foreach (var item in TraverseTree(child, childrenByParent, visited))
                {
                    yield return item;
                }
            }
        }

        private static Dictionary<long, int> BuildDepthMap(long rootId, IReadOnlyList<TraceNodeSnapshot> snapshots)
        {
            var result = new Dictionary<long, int>();
            var childrenByParent = snapshots
                .Where(item => item.ParentId != 0)
                .GroupBy(item => item.ParentId)
                .ToDictionary(item => item.Key, item => item.ToList());

            AssignDepth(rootId, 0, childrenByParent, result);
            return result;
        }

        private static void AssignDepth(long contextId, int depth, IReadOnlyDictionary<long, List<TraceNodeSnapshot>> childrenByParent, IDictionary<long, int> result)
        {
            if (result.ContainsKey(contextId))
                return;

            result[contextId] = depth;
            if (!childrenByParent.TryGetValue(contextId, out var children))
                return;

            foreach (var child in children)
            {
                AssignDepth(child.ContextId, depth + 1, childrenByParent, result);
            }
        }

        private static TraceNodeExportDto ToDto(TraceTreeRegistryBase registry, in TraceNodeSnapshot snapshot, bool includeMetadata)
        {
            return new TraceNodeExportDto(
                snapshot.ContextId,
                snapshot.RootId,
                snapshot.ParentId,
                snapshot.Kind,
                registry.GetKindName(snapshot.Kind),
                snapshot.CreatedFrame,
                snapshot.EndedFrame,
                snapshot.EndReason,
                snapshot.ChildCount,
                includeMetadata ? snapshot.Metadata : null,
                snapshot.IsEnded);
        }
    }
}
