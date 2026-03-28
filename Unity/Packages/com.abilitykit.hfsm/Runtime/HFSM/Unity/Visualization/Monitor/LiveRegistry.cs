// ============================================================================
// LiveRegistry - 运行时 FSM 实例注册与管理
// 提供自动注册、手动注册功能，收集运行时状态信息
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHFSM.Visualization
{
    /// <summary>
    /// 运行时 FSM 注册表
    /// 负责收集和管理所有运行中的 FSM 实例
    /// </summary>
    public static class LiveRegistry
    {
        /// <summary>
        /// 是否启用自动注册
        /// </summary>
        public static bool AutoRegisterEnabled = true;

        /// <summary>
        /// 自动注册过滤器，返回 true 表示允许注册
        /// </summary>
        public static Predicate<object> AutoRegisterFilter;

        /// <summary>
        /// 名称提供者
        /// </summary>
        public static Func<object, string> AutoRegisterNameProvider;

        /// <summary>
        /// 注册项
        /// </summary>
        public sealed class Entry
        {
            public readonly string Name;
            public readonly WeakReference Fsm;
            public readonly Type FsmType;
            public readonly IVisualizationProvider Provider;
            public FsmSnapshot Snapshot;

            public Entry(string name, object fsm, IVisualizationProvider provider)
            {
                Name = name ?? string.Empty;
                Fsm = new WeakReference(fsm);
                FsmType = fsm?.GetType();
                Provider = provider;
                Snapshot = new FsmSnapshot();
                Snapshot.name = name;
                Snapshot.typeName = FsmType?.Name ?? "<null>";
            }

            public object Target => Fsm.Target;
            public bool IsAlive => Fsm.IsAlive;
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        private static readonly Dictionary<object, Entry> _fsmToEntry = new Dictionary<object, Entry>();

        /// <summary>
        /// 当注册表变化时触发
        /// </summary>
        public static event Action Changed;

        /// <summary>
        /// 当某个 FSM 快照更新时触发
        /// </summary>
        public static event Action<object /* fsm */> SnapshotUpdated;

        /// <summary>
        /// 自动注册 FSM 实例
        /// </summary>
        public static void AutoRegister(object fsm)
        {
            if (!AutoRegisterEnabled) return;
            if (fsm == null) return;

            if (AutoRegisterFilter != null && !AutoRegisterFilter(fsm))
                return;

            var name = AutoRegisterNameProvider != null
                ? AutoRegisterNameProvider(fsm)
                : fsm.GetType().Name;

            Register(name, fsm);
        }

        /// <summary>
        /// 注册 FSM 实例
        /// </summary>
        public static void Register(string name, object fsm, IVisualizationProvider provider = null)
        {
            if (fsm == null) return;

            CleanupDeadEntries();

            // 检查是否已存在
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (ReferenceEquals(e.Fsm.Target, fsm))
                {
                    _entries[i] = new Entry(name, fsm, provider ?? e.Provider);
                    Changed?.Invoke();
                    return;
                }
            }

            var providerImpl = provider ?? TryCreateProvider(fsm);
            var entry = new Entry(name, fsm, providerImpl);
            _entries.Add(entry);
            _fsmToEntry[fsm] = entry;
            Changed?.Invoke();
        }

        /// <summary>
        /// 注册 FSM 实例并指定可视化提供者
        /// </summary>
        public static void Register(string name, object fsm, Action<FsmSnapshot> onSnapshot)
        {
            if (fsm == null) return;

            var provider = new CallbackVisualizationProvider(onSnapshot);
            Register(name, fsm, provider);
        }

        /// <summary>
        /// 注销 FSM 实例
        /// </summary>
        public static void Unregister(object fsm)
        {
            if (fsm == null) return;

            var removed = false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var target = _entries[i].Fsm.Target;
                if (target == null || ReferenceEquals(target, fsm))
                {
                    _entries.RemoveAt(i);
                    removed = true;
                }
            }

            _fsmToEntry.Remove(fsm);

            if (removed)
                Changed?.Invoke();
        }

        /// <summary>
        /// 获取所有注册项
        /// </summary>
        public static IReadOnlyList<Entry> GetEntries()
        {
            CleanupDeadEntries();
            return _entries;
        }

        /// <summary>
        /// 根据索引获取条目
        /// </summary>
        public static Entry GetEntry(int index)
        {
            if (index < 0 || index >= _entries.Count)
                return null;

            CleanupDeadEntries();
            return _entries[index];
        }

        /// <summary>
        /// 根据名称查找条目
        /// </summary>
        public static Entry FindEntry(string name)
        {
            CleanupDeadEntries();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Name == name)
                    return _entries[i];
            }
            return null;
        }

        /// <summary>
        /// 更新所有快照
        /// </summary>
        public static void UpdateAllSnapshots()
        {
            CleanupDeadEntries();

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Provider != null)
                {
                    entry.Snapshot = entry.Provider.GetSnapshot();
                    entry.Snapshot.name = entry.Name;
                    entry.Snapshot.typeName = entry.FsmType?.Name ?? "<unknown>";
                    SnapshotUpdated?.Invoke(entry.Target);
                }
            }
        }

        /// <summary>
        /// 更新单个 FSM 的快照
        /// </summary>
        public static void UpdateSnapshot(object fsm)
        {
            if (fsm == null) return;

            if (_fsmToEntry.TryGetValue(fsm, out var entry))
            {
                if (entry.Provider != null)
                {
                    entry.Snapshot = entry.Provider.GetSnapshot();
                    entry.Snapshot.name = entry.Name;
                    entry.Snapshot.typeName = entry.FsmType?.Name ?? "<unknown>";
                    SnapshotUpdated?.Invoke(fsm);
                }
            }
        }

        /// <summary>
        /// 清理已销毁的条目
        /// </summary>
        private static void CleanupDeadEntries()
        {
            var removed = false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (!_entries[i].Fsm.IsAlive)
                {
                    var target = _entries[i].Fsm.Target;
                    if (target != null)
                        _fsmToEntry.Remove(target);
                    _entries.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
                Changed?.Invoke();
        }

        /// <summary>
        /// 尝试为 FSM 创建可视化提供者
        /// </summary>
        private static IVisualizationProvider TryCreateProvider(object fsm)
        {
            // 检查是否实现了 IVisualizationProvider 接口
            if (fsm is IVisualizationProvider provider)
                return provider;

            // 检查类型是否实现了相关接口
            var type = fsm.GetType();
            var providerInterface = typeof(IVisualizationProvider);

            foreach (var iface in type.GetInterfaces())
            {
                if (iface == providerInterface)
                {
                    // 创建装饰器包装
                    return new WrapperVisualizationProvider(fsm);
                }
            }

            // 返回默认的反射式提供者
            return new ReflectionVisualizationProvider(fsm);
        }

        /// <summary>
        /// 手动触发更新（编辑器中用于刷新显示）
        /// </summary>
        public static void MarkDirty()
        {
            Changed?.Invoke();
        }

        /// <summary>
        /// 获取注册数量
        /// </summary>
        public static int Count
        {
            get
            {
                CleanupDeadEntries();
                return _entries.Count;
            }
        }
    }

    /// <summary>
    /// 基于回调的可视化提供者
    /// </summary>
    internal class CallbackVisualizationProvider : IVisualizationProvider
    {
        private readonly Action<FsmSnapshot> _callback;
        private readonly FsmSnapshot _snapshot;

        public CallbackVisualizationProvider(Action<FsmSnapshot> callback)
        {
            _callback = callback;
            _snapshot = new FsmSnapshot();
        }

        public FsmSnapshot GetSnapshot() => _snapshot;

        public IEnumerable<string> GetActiveStatePaths()
        {
            _callback(_snapshot);
            return _snapshot.activeStatePaths;
        }

        public IEnumerable<ParameterInfo> GetParameters() => _snapshot.parameters;

        public IEnumerable<(string name, string parentPath, bool isStateMachine)> GetStateStructure()
        {
            foreach (var state in _snapshot.states)
            {
                yield return (state.name, state.parentPath, state.isStateMachine);
            }
        }

        public IEnumerable<TransitionInfo> GetTransitions() => _snapshot.transitions;

        public void RecordTransition(string fromPath, string toPath, string trigger)
        {
            _snapshot.history.Add(new StateTransitionRecord
            {
                fromPath = fromPath,
                toPath = toPath,
                trigger = trigger,
                timestamp = Time.time,
                timeAgo = 0
            });
        }

        public IEnumerable<StateTransitionRecord> GetHistory(int maxCount = 50)
        {
            var currentTime = Time.time;
            var count = Math.Min(_snapshot.history.Count, maxCount);
            for (int i = _snapshot.history.Count - count; i < _snapshot.history.Count; i++)
            {
                var record = _snapshot.history[i];
                record.timeAgo = currentTime - record.timestamp;
                yield return record;
            }
        }
    }

    /// <summary>
    /// 基于反射的可视化提供者（通用实现）
    /// </summary>
    internal class ReflectionVisualizationProvider : IVisualizationProvider
    {
        private readonly object _fsm;
        private readonly FsmSnapshot _snapshot;
        private readonly List<StateTransitionRecord> _history = new List<StateTransitionRecord>();

        public ReflectionVisualizationProvider(object fsm)
        {
            _fsm = fsm;
            _snapshot = new FsmSnapshot();
            ProbeFsmStructure();
        }

        public FsmSnapshot GetSnapshot()
        {
            _snapshot.snapshotTime = Time.time;
            UpdateActiveStates();
            UpdateHistory();
            return _snapshot;
        }

        public IEnumerable<string> GetActiveStatePaths()
        {
            UpdateActiveStates();
            return _snapshot.activeStatePaths;
        }

        public IEnumerable<ParameterInfo> GetParameters() => _snapshot.parameters;

        public IEnumerable<(string name, string parentPath, bool isStateMachine)> GetStateStructure()
        {
            foreach (var state in _snapshot.states)
            {
                yield return (state.name, state.parentPath, state.isStateMachine);
            }
        }

        public IEnumerable<TransitionInfo> GetTransitions() => _snapshot.transitions;

        public void RecordTransition(string fromPath, string toPath, string trigger)
        {
            _history.Add(new StateTransitionRecord
            {
                fromPath = fromPath,
                toPath = toPath,
                trigger = trigger,
                timestamp = Time.time,
                timeAgo = 0
            });
        }

        public IEnumerable<StateTransitionRecord> GetHistory(int maxCount = 50)
        {
            var currentTime = Time.time;
            var count = Math.Min(_history.Count, maxCount);
            for (int i = _history.Count - count; i < _history.Count; i++)
            {
                var record = _history[i];
                record.timeAgo = currentTime - record.timestamp;
                yield return record;
            }
        }

        private void ProbeFsmStructure()
        {
            var type = _fsm.GetType();

            // 探测 activeStateName 或类似属性
            var activeStateProperty = type.GetProperty("activeStateName");
            var statesProperty = type.GetProperty("states");
            var stateMachinesProperty = type.GetProperty("stateMachines");

            // 如果有子状态机，递归探测
            if (stateMachinesProperty != null)
            {
                var stateMachines = stateMachinesProperty.GetValue(_fsm) as System.Collections.IEnumerable;
                if (stateMachines != null)
                {
                    foreach (var sm in stateMachines)
                    {
                        ProbeSubStateMachine(sm, "");
                    }
                }
            }

            // 探测顶层状态
            if (statesProperty != null)
            {
                var states = statesProperty.GetValue(_fsm) as System.Collections.IEnumerable;
                if (states != null)
                {
                    foreach (var state in states)
                    {
                        AddStateFromObject(state, "");
                    }
                }
            }
        }

        private void ProbeSubStateMachine(object sm, string parentPath)
        {
            if (sm == null) return;

            var type = sm.GetType();
            var nameProperty = type.GetProperty("name") ?? type.GetProperty("Name");
            var name = nameProperty?.GetValue(sm)?.ToString() ?? "Unknown";
            var path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";

            _snapshot.states.Add(new StateNodeInfo
            {
                name = name,
                path = path,
                parentPath = parentPath,
                isStateMachine = true,
                nestingLevel = string.IsNullOrEmpty(parentPath) ? 0 : parentPath.Split('/').Length
            });

            // 探测子状态
            var statesProperty = type.GetProperty("states");
            var stateMachinesProperty = type.GetProperty("stateMachines");

            if (stateMachinesProperty != null)
            {
                var subStateMachines = stateMachinesProperty.GetValue(sm) as System.Collections.IEnumerable;
                if (subStateMachines != null)
                {
                    foreach (var subSm in subStateMachines)
                    {
                        ProbeSubStateMachine(subSm, path);
                    }
                }
            }

            if (statesProperty != null)
            {
                var states = statesProperty.GetValue(sm) as System.Collections.IEnumerable;
                if (states != null)
                {
                    foreach (var state in states)
                    {
                        AddStateFromObject(state, path);
                    }
                }
            }
        }

        private void AddStateFromObject(object state, string parentPath)
        {
            if (state == null) return;

            var type = state.GetType();
            var nameProperty = type.GetProperty("name") ?? type.GetProperty("Name");
            var name = nameProperty?.GetValue(state)?.ToString() ?? "Unknown";
            var path = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}/{name}";

            // 检查是否是状态机
            var hasChildStates = type.GetProperty("states") != null || type.GetProperty("stateMachines") != null;

            _snapshot.states.Add(new StateNodeInfo
            {
                name = name,
                path = path,
                parentPath = parentPath,
                isStateMachine = hasChildStates,
                nestingLevel = string.IsNullOrEmpty(parentPath) ? 0 : parentPath.Split('/').Length
            });
        }

        private void UpdateActiveStates()
        {
            _snapshot.activeStatePaths.Clear();
            var type = _fsm.GetType();

            // 尝试获取当前激活的状态名称
            var activeStateProperty = type.GetProperty("activeStateName");
            var activeStateField = type.GetField("activeStateName");

            if (activeStateProperty != null || activeStateField != null)
            {
                var activeName = (activeStateProperty?.GetValue(_fsm) ?? activeStateField?.GetValue(_fsm))?.ToString();
                if (!string.IsNullOrEmpty(activeName))
                {
                    _snapshot.activeStatePaths.Add(activeName);
                }
            }

            // 更新状态的激活状态
            for (int i = 0; i < _snapshot.states.Count; i++)
            {
                var state = _snapshot.states[i];
                state.isActive = _snapshot.activeStatePaths.Contains(state.path);
                _snapshot.states[i] = state;
            }
        }

        private void UpdateHistory()
        {
            var currentTime = Time.time;
            for (int i = 0; i < _history.Count; i++)
            {
                var record = _history[i];
                record.timeAgo = currentTime - record.timestamp;
                _history[i] = record;
            }
        }
    }

    /// <summary>
    /// 包装已有的可视化提供者
    /// </summary>
    internal class WrapperVisualizationProvider : IVisualizationProvider
    {
        private readonly object _fsm;

        public WrapperVisualizationProvider(object fsm)
        {
            _fsm = fsm;
        }

        public FsmSnapshot GetSnapshot() => (_fsm as IVisualizationProvider)?.GetSnapshot() ?? new FsmSnapshot();
        public IEnumerable<string> GetActiveStatePaths() => (_fsm as IVisualizationProvider)?.GetActiveStatePaths() ?? Array.Empty<string>();
        public IEnumerable<ParameterInfo> GetParameters() => (_fsm as IVisualizationProvider)?.GetParameters() ?? Array.Empty<ParameterInfo>();
        public IEnumerable<(string name, string parentPath, bool isStateMachine)> GetStateStructure() => (_fsm as IVisualizationProvider)?.GetStateStructure() ?? Array.Empty<(string, string, bool)>();
        public IEnumerable<TransitionInfo> GetTransitions() => (_fsm as IVisualizationProvider)?.GetTransitions() ?? Array.Empty<TransitionInfo>();
        public void RecordTransition(string fromPath, string toPath, string trigger) => (_fsm as IVisualizationProvider)?.RecordTransition(fromPath, toPath, trigger);
        public IEnumerable<StateTransitionRecord> GetHistory(int maxCount = 50) => (_fsm as IVisualizationProvider)?.GetHistory(maxCount) ?? Array.Empty<StateTransitionRecord>();
    }
}

#endif
