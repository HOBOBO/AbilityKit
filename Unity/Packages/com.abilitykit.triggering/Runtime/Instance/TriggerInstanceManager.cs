using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Factory;

namespace AbilityKit.Triggering.Runtime.Instance
{
    /// <summary>
    /// 触发器实例管理器
    /// 负责管理所有活跃的触发器实例
    /// </summary>
    public class TriggerInstanceManager
    {
        private readonly Dictionary<(int, int), ITriggerInstance> _instances = new Dictionary<(int, int), ITriggerInstance>();
        private readonly IBehaviorFactory _behaviorFactory;
        private int _nextExecutorId;

        public TriggerInstanceManager(IBehaviorFactory behaviorFactory)
        {
            _behaviorFactory = behaviorFactory ?? throw new ArgumentNullException(nameof(behaviorFactory));
            _nextExecutorId = 0;
        }

        /// <summary>
        /// 创建并注册一个新的触发器实例
        /// </summary>
        public ITriggerInstance CreateInstance(ITriggerPlanConfig config, long serverTime = 0)
        {
            var executorId = _nextExecutorId++;
            var instance = new TriggerInstance(config, executorId, serverTime);
            
            var behavior = _behaviorFactory.Create(config);
            instance.Behavior = behavior;
            
            _instances[(config.TriggerId, executorId)] = instance;
            
            return instance;
        }

        /// <summary>
        /// 获取触发器实例
        /// </summary>
        public bool TryGetInstance(int triggerId, int executorId, out ITriggerInstance instance)
        {
            return _instances.TryGetValue((triggerId, executorId), out instance);
        }

        /// <summary>
        /// 获取所有活跃实例
        /// </summary>
        public IEnumerable<ITriggerInstance> GetActiveInstances()
        {
            foreach (var kvp in _instances)
            {
                if (!kvp.Value.IsTerminated)
                    yield return kvp.Value;
            }
        }

        /// <summary>
        /// 获取指定执行器下的所有实例
        /// </summary>
        public IEnumerable<ITriggerInstance> GetInstancesByExecutor(int executorId)
        {
            foreach (var kvp in _instances)
            {
                if (kvp.Key.Item2 == executorId && !kvp.Value.IsTerminated)
                    yield return kvp.Value;
            }
        }

        /// <summary>
        /// 移除实例
        /// </summary>
        public bool RemoveInstance(int triggerId, int executorId)
        {
            return _instances.Remove((triggerId, executorId));
        }

        /// <summary>
        /// 清理所有已终止的实例
        /// </summary>
        public void CleanupTerminated()
        {
            var keysToRemove = new List<(int, int)>();
            foreach (var kvp in _instances)
            {
                if (kvp.Value.IsTerminated)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
            {
                _instances.Remove(key);
            }
        }

        /// <summary>
        /// 获取实例数量
        /// </summary>
        public int Count => _instances.Count;

        /// <summary>
        /// 获取活跃实例数量
        /// </summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;
                foreach (var kvp in _instances)
                {
                    if (!kvp.Value.IsTerminated)
                        count++;
                }
                return count;
            }
        }
    }
}