using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>调试视图的单节点只读快照（编辑器拉取时构建，不占运行时开销）。</summary>
    public sealed class BtNodeDebugInfo
    {
        public string NodeId { get; }
        public string Name { get; }
        public string TypeId { get; }
        public BtNodeKind Kind { get; }
        public BtNodeState State { get; }
        public int Depth { get; }
        /// <summary>当前位于几个运行栈上（0=不在执行路径，≥1=运行中）。</summary>
        public int OnStackCount { get; }
        public int RunningChildIndex { get; }
        /// <summary>子树展开来源树（未用子树引用时为 null）。</summary>
        public string? SourceTreeId { get; }

        public BtNodeDebugInfo(
            string nodeId, string name, string typeId, BtNodeKind kind,
            BtNodeState state, int depth, int onStackCount, int runningChildIndex,
            string? sourceTreeId = null)
        {
            NodeId = nodeId;
            Name = name;
            TypeId = typeId;
            Kind = kind;
            State = state;
            Depth = depth;
            OnStackCount = onStackCount;
            RunningChildIndex = runningChildIndex;
            SourceTreeId = sourceTreeId;
        }
    }

    /// <summary>
    /// 树实例的调试视图契约。运行时实现此接口向注册中心登记；
    /// 编辑器（或 console dump、服务端诊断）**主动拉取**，运行时不感知任何观察者。
    /// </summary>
    public interface IBtTreeDebugView
    {
        string TreeId { get; }
        string DisplayName { get; }
        string OwnerLabel { get; }
        int NodeCount { get; }
        /// <summary>最近一次 Update 的帧号（观察端显示"当前进度"用）。</summary>
        int LastFrame { get; }
        /// <summary>树定义（只读观察用途；观察端据此渲染图/跳转子树，不得修改）。</summary>
        BtTreeDefinition TreeDefinition { get; }
        /// <summary>子树展开后的节点来源树（nodeId -> treeId）；未用子树引用时为 null。</summary>
        IReadOnlyDictionary<string, string>? NodeSourceTree { get; }
        /// <summary>子树实例（内联根 -> 被引用 treeId）；观察端标记子树边界/跨树跳转。</summary>
        IReadOnlyList<BtSubtreeInstance> SubtreeInstances { get; }
        List<BtNodeDebugInfo> GetNodeStates();
        BtBlackboardValueSnapshot GetBlackboard();
        BtTreeRuntimeSnapshot CaptureState();
    }

    /// <summary>注册中心返回的实例句柄。</summary>
    public sealed class BtTreeDebugHandle
    {
        internal long Id { get; }
        internal BtTreeDebugHandle(long id) { Id = id; }
    }

    /// <summary>登记表条目：实例序号（注册顺序，观察端按 ID 区分用）+ 视图。</summary>
    public sealed class BtDebugRegistryEntry
    {
        public long Id { get; }
        public IBtTreeDebugView View { get; }

        internal BtDebugRegistryEntry(long id, IBtTreeDebugView view)
        {
            Id = id;
            View = view;
        }
    }

    /// <summary>
    /// 进程内行为树调试注册中心：运行时实例启动时单向登记，观察端按需拉取列表与视图。
    /// 纯 C#、无 Unity 依赖；Unity 编辑器窗口、console 宿主与服务端诊断共用同一契约。
    /// </summary>
    public static class BtDebugRegistry
    {
        private static readonly object Gate = new();
        private static readonly Dictionary<long, IBtTreeDebugView> Views = new();
        private static long _nextId = 1;

        public static BtTreeDebugHandle Register(IBtTreeDebugView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            lock (Gate)
            {
                var handle = new BtTreeDebugHandle(_nextId++);
                Views.Add(handle.Id, view);
                return handle;
            }
        }

        public static void Unregister(BtTreeDebugHandle handle)
        {
            if (handle == null) return;
            lock (Gate)
            {
                Views.Remove(handle.Id);
            }
        }

        /// <summary>当前已登记实例的快照列表（编辑器轮询入口）。</summary>
        public static List<IBtTreeDebugView> GetViews()
        {
            lock (Gate)
            {
                return new List<IBtTreeDebugView>(Views.Values);
            }
        }

        /// <summary>按注册序号排序的登记条目（观察端按实例 ID 区分与选择）。</summary>
        public static List<BtDebugRegistryEntry> GetEntries()
        {
            lock (Gate)
            {
                var entries = new List<BtDebugRegistryEntry>(Views.Count);
                foreach (var pair in Views)
                {
                    entries.Add(new BtDebugRegistryEntry(pair.Key, pair.Value));
                }
                entries.Sort(static (a, b) => a.Id.CompareTo(b.Id));
                return entries;
            }
        }

        public static int Count
        {
            get { lock (Gate) { return Views.Count; } }
        }

        /// <summary>测试辅助：清空登记表。</summary>
        public static void ClearForTests()
        {
            lock (Gate)
            {
                Views.Clear();
            }
        }
    }
}
