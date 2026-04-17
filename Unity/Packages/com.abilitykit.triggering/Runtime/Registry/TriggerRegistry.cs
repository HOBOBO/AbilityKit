using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Instance;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Plans;

namespace AbilityKit.Triggering.Runtime.Registry
{
    /// <summary>
    /// 触发器注册表接口
    /// </summary>
    public interface ITriggerRegistry
    {
        int Count { get; }
        ITriggerHandle Register<TArgs>(object config) where TArgs : class;
        bool Unregister(int triggerId);
        bool TryGet(int triggerId, out ITriggerInstance trigger);
        IEnumerable<ITriggerInstance> GetAllTriggers();
        void Clear();
    }

    /// <summary>
    /// 触发器注册表
    /// 管理所有触发器的注册、注销、查询和生命周期
    /// </summary>
    public class TriggerRegistry : ITriggerRegistry
    {
        private readonly Dictionary<int, ITriggerInstance> _byId = new Dictionary<int, ITriggerInstance>();
        private int _nextId;
        private bool _disposed;

        /// <summary>已注册的触发器数量</summary>
        public int Count => _byId.Count;

        /// <summary>
        /// 注册触发器
        /// </summary>
        public ITriggerHandle Register<TArgs>(object config)
            where TArgs : class
        {
            var triggerId = _nextId++;
            // 创建占位实例 - 实际 Spec 后续通过其他方式设置
            var instance = new TriggerInstanceWrapper(triggerId);
            _byId[triggerId] = instance;
            return new TriggerHandle(triggerId, this);
        }

        /// <summary>注销触发器</summary>
        public bool Unregister(int triggerId)
        {
            if (_byId.TryGetValue(triggerId, out var instance))
            {
                instance.Dispose();
                _byId.Remove(triggerId);
                return true;
            }
            return false;
        }

        /// <summary>尝试获取触发器</summary>
        public bool TryGet(int triggerId, out ITriggerInstance trigger)
        {
            return _byId.TryGetValue(triggerId, out trigger);
        }

        /// <summary>获取所有触发器</summary>
        public IEnumerable<ITriggerInstance> GetAllTriggers()
        {
            return _byId.Values;
        }

        /// <summary>清空所有触发器</summary>
        public void Clear()
        {
            foreach (var instance in _byId.Values)
            {
                instance.Dispose();
            }
            _byId.Clear();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// 触发器句柄
        /// </summary>
        private sealed class TriggerHandle : ITriggerHandle
        {
            public int TriggerId { get; }
            private readonly TriggerRegistry _registry;
            private bool _disposed;

            public TriggerHandle(int triggerId, TriggerRegistry registry)
            {
                TriggerId = triggerId;
                _registry = registry;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _registry.Unregister(TriggerId);
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// 简化的触发器实例包装（用于注册表内部）
        /// </summary>
        private class TriggerInstanceWrapper : ITriggerInstance
        {
            public int Id { get; }

            public TriggerInstanceWrapper(int id)
            {
                Id = id;
            }

            public ITriggerPlanConfig Spec => null;
            public ETriggerState CurrentState { get; set; }
            public long ElapsedMs { get; set; }
            public int ExecutionCount { get; set; }
            public long StartServerTime => 0;
            public IReadOnlyDictionary<string, object> InstanceData => null;
            public ITriggerBehavior Behavior { get; set; }
            public bool IsTerminated => CurrentState == ETriggerState.Completed || CurrentState == ETriggerState.Interrupted;
            public bool IsRunning => CurrentState == ETriggerState.Running;
            public bool IsCompleted => CurrentState == ETriggerState.Completed;

            public bool TryGetInstanceData<T>(string key, out T value) { value = default; return false; }
            public void SetInstanceData<T>(string key, T value) { }
            public bool RemoveInstanceData(string key) => false;
            public TriggerSnapshot CreateSnapshot() => null;
            public void RestoreFromSnapshot(TriggerSnapshot snapshot) { }
            public void Dispose() { }
        }
    }
}