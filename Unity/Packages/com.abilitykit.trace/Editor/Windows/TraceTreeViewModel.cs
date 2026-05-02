using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Trace;

namespace AbilityKit.Trace.Editor.Windows
{
    /// <summary>
    /// 树节点视图数据
    /// </summary>
    public class TraceNodeViewData
    {
        public long ContextId { get; set; }
        public long RootId { get; set; }
        public long ParentId { get; set; }
        public int Kind { get; set; }
        public string KindName { get; set; }
        public int Level { get; set; }
        public int OrderInLevel { get; set; }
        public int ChildCount { get; set; }
        public bool IsEnded { get; set; }
        public bool IsRoot => ContextId == RootId;
        public object Metadata { get; set; }
    }

    /// <summary>
    /// 根节点视图数据
    /// </summary>
    public class TraceRootViewData
    {
        public long RootId { get; set; }
        public int Kind { get; set; }
        public string KindName { get; set; }
        public bool IsActive { get; set; }
        public int ActiveCount { get; set; }
        public int ExternalRefCount { get; set; }
        public int NodeCount { get; set; }
    }

    /// <summary>
    /// 溯源树窗口视图模型
    /// </summary>
    public class TraceTreeViewModel
    {
        private ITraceRegistryProvider _registryProvider;

        /// <summary>
        /// 活跃根节点列表
        /// </summary>
        public List<TraceRootViewData> ActiveRoots { get; private set; } = new List<TraceRootViewData>();

        /// <summary>
        /// 当前选中树的节点列表
        /// </summary>
        public List<TraceNodeViewData> CurrentNodes { get; private set; } = new List<TraceNodeViewData>();

        /// <summary>
        /// 当前选中的根节点ID
        /// </summary>
        public long SelectedRootId { get; private set; }

        /// <summary>
        /// 当前选中的节点
        /// </summary>
        public TraceNodeViewData SelectedNode { get; private set; }

        /// <summary>
        /// 总节点数
        /// </summary>
        public int TotalNodeCount { get; private set; }

        /// <summary>
        /// 设置注册表提供者
        /// </summary>
        public void SetRegistryProvider(ITraceRegistryProvider provider)
        {
            _registryProvider = provider;
        }

        /// <summary>
        /// 刷新数据
        /// </summary>
        public void Refresh()
        {
            ActiveRoots.Clear();
            TotalNodeCount = 0;

            var registries = GetRegistries();

            foreach (var registry in registries)
            {
                RefreshFromRegistry(registry);
            }

            // 如果当前选中的根节点不再活跃，清除选择
            if (SelectedRootId != 0 && !ActiveRoots.Any(r => r.RootId == SelectedRootId))
            {
                SelectedRootId = 0;
                SelectedNode = null;
                CurrentNodes.Clear();
            }

            // 如果选中了根节点但节点列表为空，刷新节点
            if (SelectedRootId != 0 && CurrentNodes.Count == 0)
            {
                RefreshCurrentTree();
            }
        }

        private IEnumerable<TraceTreeRegistryBase> GetRegistries()
        {
            if (_registryProvider != null)
            {
                return _registryProvider.GetRegistries();
            }

            // 默认实现：尝试获取注册的实例
            return GetDefaultRegistries();
        }

        private IEnumerable<TraceTreeRegistryBase> GetDefaultRegistries()
        {
            var result = new List<TraceTreeRegistryBase>();

            // 使用反射查找所有已注册的 TraceTreeRegistry 实例
            var baseType = typeof(TraceTreeRegistryBase);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (baseType.IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                        {
                            // 尝试获取 Instance 属性
                            var instanceProp = type.GetProperty("Instance",
                                BindingFlags.Public | BindingFlags.Static);
                            if (instanceProp != null)
                            {
                                var value = instanceProp.GetValue(null);
                                if (value is TraceTreeRegistryBase registry)
                                {
                                    result.Add(registry);
                                    continue;
                                }
                            }

                            // 尝试获取 Singleton 属性
                            var singletonProp = type.GetProperty("Singleton",
                                BindingFlags.Public | BindingFlags.Static);
                            if (singletonProp != null)
                            {
                                var value = singletonProp.GetValue(null);
                                if (value is TraceTreeRegistryBase registry)
                                {
                                    result.Add(registry);
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // 忽略无法加载的程序集
                }
            }

            return result;
        }

        private void RefreshFromRegistry(TraceTreeRegistryBase registry)
        {
            try
            {
                // 使用反射调用 GetActiveRoots
                var getActiveRootsMethod = registry.GetType().GetMethod("GetActiveRoots");
                if (getActiveRootsMethod == null) return;

                var rootStates = getActiveRootsMethod.Invoke(registry, null) as IEnumerable<object>;
                if (rootStates == null) return;

                foreach (var root in rootStates)
                {
                    long rootId = (long)root.GetType().GetProperty("RootId").GetValue(root);
                    int activeCount = (int)root.GetType().GetProperty("ActiveCount").GetValue(root);
                    int externalRefCount = (int)root.GetType().GetProperty("ExternalRefCount").GetValue(root);

                    // 获取根节点的 kind
                    int kind = 0;
                    try
                    {
                        var tryGetSnapshotMethod = registry.GetType().GetMethod("TryGetSnapshot");
                        var snapshot = tryGetSnapshotMethod.Invoke(registry, new object[] { rootId });

                        // TraceSnapshot<T> 有 IsValid, Kind 属性
                        var isValidProp = snapshot.GetType().GetProperty("IsValid");
                        var kindProp = snapshot.GetType().GetProperty("Kind");

                        if (isValidProp != null && (bool)isValidProp.GetValue(snapshot))
                        {
                            kind = (int)kindProp.GetValue(snapshot);
                        }
                    }
                    catch
                    {
                        // 忽略
                    }

                    // 获取该根节点下的所有节点数量
                    int nodeCount = 0;
                    try
                    {
                        var getNodesByRootMethod = registry.GetType().GetMethod("GetNodesByRoot");
                        var nodes = getNodesByRootMethod.Invoke(registry, new object[] { rootId }) as IEnumerable<object>;
                        if (nodes != null)
                        {
                            nodeCount = nodes.Count();
                        }
                    }
                    catch
                    {
                        // 忽略
                    }

                    var rootData = new TraceRootViewData
                    {
                        RootId = rootId,
                        Kind = kind,
                        KindName = GetKindName(kind, registry),
                        IsActive = activeCount > 0,
                        ActiveCount = activeCount,
                        ExternalRefCount = externalRefCount,
                        NodeCount = nodeCount
                    };

                    ActiveRoots.Add(rootData);
                    TotalNodeCount += nodeCount;
                }
            }
            catch (Exception)
            {
                // 忽略获取数据时的错误
            }
        }

        /// <summary>
        /// 选择根节点
        /// </summary>
        public void SelectRoot(long rootId)
        {
            if (SelectedRootId == rootId) return;

            SelectedRootId = rootId;
            SelectedNode = null;
            RefreshCurrentTree();
        }

        /// <summary>
        /// 选择节点
        /// </summary>
        public void SelectNode(long contextId)
        {
            SelectedNode = CurrentNodes.FirstOrDefault(n => n.ContextId == contextId);
        }

        /// <summary>
        /// 获取节点ID对应的视图数据
        /// </summary>
        public TraceNodeViewData GetNodeById(long contextId)
        {
            return CurrentNodes.FirstOrDefault(n => n.ContextId == contextId);
        }

        /// <summary>
        /// 刷新当前选中树的节点
        /// </summary>
        private void RefreshCurrentTree()
        {
            CurrentNodes.Clear();

            if (SelectedRootId == 0) return;

            var registries = GetRegistries();
            foreach (var registry in registries)
            {
                try
                {
                    var nodes = new List<TraceNodeViewData>();

                    // 使用反射调用 GetNodesByRoot
                    var getNodesByRootMethod = registry.GetType().GetMethod("GetNodesByRoot");
                    if (getNodesByRootMethod == null) continue;

                    var snapshots = getNodesByRootMethod.Invoke(registry, new object[] { SelectedRootId }) as IEnumerable<object>;
                    if (snapshots == null) continue;

                    foreach (var snapshot in snapshots)
                    {
                        var contextId = (long)snapshot.GetType().GetProperty("ContextId").GetValue(snapshot);
                        var rootId = (long)snapshot.GetType().GetProperty("RootId").GetValue(snapshot);
                        var parentId = (long)snapshot.GetType().GetProperty("ParentId").GetValue(snapshot);
                        var kind = (int)snapshot.GetType().GetProperty("Kind").GetValue(snapshot);
                        var isEnded = (bool)snapshot.GetType().GetProperty("IsEnded").GetValue(snapshot);
                        var childCount = (int)snapshot.GetType().GetProperty("ChildCount").GetValue(snapshot);
                        var metadata = snapshot.GetType().GetProperty("Metadata").GetValue(snapshot);

                        var viewData = new TraceNodeViewData
                        {
                            ContextId = contextId,
                            RootId = rootId,
                            ParentId = parentId,
                            Kind = kind,
                            KindName = GetKindName(kind, registry),
                            ChildCount = childCount,
                            IsEnded = isEnded,
                            Metadata = metadata
                        };
                        nodes.Add(viewData);
                    }

                    if (nodes.Count > 0)
                    {
                        // 计算层级和顺序
                        BuildNodeHierarchy(nodes);
                        CurrentNodes = nodes;
                        return;
                    }
                }
                catch (Exception)
                {
                    // 继续尝试其他注册表
                }
            }
        }

        private void BuildNodeHierarchy(List<TraceNodeViewData> nodes)
        {
            // 创建节点映射
            var nodeMap = nodes.ToDictionary(n => n.ContextId);

            // 查找根节点
            var rootNode = nodes.FirstOrDefault(n => n.IsRoot);
            if (rootNode == null) return;

            // 使用 BFS 计算层级和同层顺序
            var queue = new Queue<long>();
            var levelMap = new Dictionary<long, int>();
            var orderMap = new Dictionary<long, int>();
            var levelCounters = new Dictionary<int, int>();

            levelMap[rootNode.ContextId] = 0;
            orderMap[rootNode.ContextId] = 0;
            levelCounters[0] = 1;

            // 获取子节点
            var childrenMap = new Dictionary<long, List<long>>();
            foreach (var node in nodes)
            {
                if (node.ParentId != 0)
                {
                    if (!childrenMap.TryGetValue(node.ParentId, out var children))
                    {
                        children = new List<long>();
                        childrenMap[node.ParentId] = children;
                    }
                    children.Add(node.ContextId);
                }
            }

            queue.Enqueue(rootNode.ContextId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var currentLevel = levelMap[currentId];
                var currentOrder = orderMap[currentId];

                if (nodeMap.TryGetValue(currentId, out var nodeView))
                {
                    nodeView.Level = currentLevel;
                    nodeView.OrderInLevel = currentOrder;
                }

                // 获取子节点
                if (childrenMap.TryGetValue(currentId, out var children))
                {
                    var nextLevel = currentLevel + 1;
                    for (int i = 0; i < children.Count; i++)
                    {
                        var childId = children[i];
                        if (!levelMap.ContainsKey(childId))
                        {
                            levelMap[childId] = nextLevel;

                            if (!levelCounters.TryGetValue(nextLevel, out var counter))
                            {
                                counter = 0;
                            }
                            orderMap[childId] = counter;
                            levelCounters[nextLevel] = counter + 1;

                            queue.Enqueue(childId);
                        }
                    }
                }
            }

            // 更新节点的子节点数量
            foreach (var node in nodes)
            {
                if (childrenMap.TryGetValue(node.ContextId, out var children))
                {
                    node.ChildCount = children.Count;
                }
            }
        }

        /// <summary>
        /// 获取节点类型的名称
        /// </summary>
        private string GetKindName(int kind, TraceTreeRegistryBase registry)
        {
            // 尝试使用注册表提供的类型名称
            try
            {
                var method = registry.GetType().GetMethod("GetKindName");
                if (method != null)
                {
                    var result = method.Invoke(registry, new object[] { kind });
                    if (result is string name && !string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
            }
            catch (Exception)
            {
                // 忽略
            }

            // 默认返回类型编号
            return $"Kind_{kind}";
        }
    }
}
