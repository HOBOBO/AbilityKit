using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 行为创建配置
    /// </summary>
    public class BehaviorCreateConfig
    {
        public string BehaviorKind { get; set; }
        public long SourceContextId { get; set; }
        public BehaviorEntityId OwnerId { get; set; }
        public BehaviorEntityId? TargetId { get; set; }
        public float? DurationSeconds { get; set; }
        public int Priority { get; set; }
        public IBehaviorDecision Decision { get; set; }
        public IBehaviorExecutor Executor { get; set; }
        public IWorldQuery World { get; set; }
        public IReadOnlyDictionary<string, object> Config { get; set; }
    }
    
    /// <summary>
    /// 行为绑定
    /// 用于将行为绑定到某个来源（如 Effect、Buff、Skill）
    /// 当来源结束时，行为也会结束
    /// </summary>
    public class BehaviorBinding
    {
        public string SourceType { get; }
        public long SourceId { get; }
        public BehaviorEntityId OwnerId { get; }
        public bool AutoEndWithSource { get; set; } = true;
        
        internal event Action<long> OnRemoveBehavior;
        
        private readonly List<long> _behaviorIds = new List<long>();
        public IReadOnlyList<long> BehaviorIds => _behaviorIds.AsReadOnly();
        
        public BehaviorBinding(string sourceType, long sourceId, BehaviorEntityId ownerId)
        {
            SourceType = sourceType;
            SourceId = sourceId;
            OwnerId = ownerId;
        }
        
        internal void AddBehavior(long behaviorId) => _behaviorIds.Add(behaviorId);
        internal void RemoveBehavior(long behaviorId) => _behaviorIds.Remove(behaviorId);
        
        /// <summary>
        /// 结束所有绑定的行为
        /// </summary>
        public void EndAllBehaviors(BehaviorManager manager, string reason)
        {
            foreach (var id in _behaviorIds.ToArray())
            {
                manager.Interrupt(id, reason);
            }
        }
    }
    
    /// <summary>
    /// 行为管理器
    /// 
    /// 统一管理所有持续行为
    /// 
    /// 特点：
    /// - 完全独立于 Triggering 模块
    /// - 不依赖任何业务层接口
    /// - 由业务层决定何时创建/中断行为
    /// </summary>
    public class BehaviorManager
    {
        #region Private Fields
        
        private readonly Dictionary<long, BehaviorRuntime> _behaviors = new Dictionary<long, BehaviorRuntime>();
        private readonly Dictionary<BehaviorEntityId, List<BehaviorRuntime>> _entityBehaviors = new Dictionary<BehaviorEntityId, List<BehaviorRuntime>>();
        private readonly Dictionary<long, BehaviorBinding> _bindings = new Dictionary<long, BehaviorBinding>();
        
        private long _nextInstanceId = 1;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// 行为创建事件
        /// </summary>
        public event Action<BehaviorRuntime> OnBehaviorCreated;
        
        /// <summary>
        /// 行为结束事件
        /// </summary>
        public event Action<BehaviorRuntime, BehaviorEndReason> OnBehaviorEnded;
        
        #endregion
        
        #region Create
        
        /// <summary>
        /// 创建并启动一个行为
        /// </summary>
        public BehaviorRuntime CreateBehavior(BehaviorCreateConfig config)
        {
            if (config.Decision == null)
                throw new ArgumentNullException(nameof(config.Decision), "Decision cannot be null");
            
            var instanceId = _nextInstanceId++;
            
            var behavior = new BehaviorRuntime(
                instanceId,
                config.BehaviorKind,
                config.SourceContextId,
                config.OwnerId,
                config.TargetId,
                config.DurationSeconds,
                config.Priority,
                config.Decision,
                config.Executor ?? new DefaultExecutor(),
                config.World ?? new DefaultWorldQuery(),
                config.Config);
            
            _behaviors[instanceId] = behavior;
            
            // 添加到实体列表
            if (!_entityBehaviors.TryGetValue(config.OwnerId, out var list))
            {
                list = new List<BehaviorRuntime>();
                _entityBehaviors[config.OwnerId] = list;
            }
            list.Add(behavior);
            
            // 绑定来源
            if (config is BehaviorCreateConfigWithBinding binding && binding.Binding != null)
            {
                _bindings[instanceId] = binding.Binding;
                binding.Binding.AddBehavior(instanceId);
            }
            
            behavior.OnComplete += OnRuntimeComplete;
            behavior.OnInterrupt += OnRuntimeInterrupt;
            
            behavior.Start();
            
            OnBehaviorCreated?.Invoke(behavior);
            
            return behavior;
        }
        
        /// <summary>
        /// 创建带绑定的行为
        /// </summary>
        public BehaviorRuntime CreateBehavior(BehaviorCreateConfigWithBinding config)
        {
            return CreateBehavior(config as BehaviorCreateConfig);
        }
        
        #endregion
        
        #region Query
        
        /// <summary>
        /// 获取行为
        /// </summary>
        public BehaviorRuntime GetBehavior(long instanceId)
        {
            return _behaviors.TryGetValue(instanceId, out var behavior) ? behavior : null;
        }
        
        /// <summary>
        /// 获取实体的所有行为
        /// </summary>
        public IEnumerable<BehaviorRuntime> GetEntityBehaviors(BehaviorEntityId ownerId)
        {
            if (_entityBehaviors.TryGetValue(ownerId, out var list))
            {
                return list.ToList();
            }
            return Array.Empty<BehaviorRuntime>();
        }
        
        /// <summary>
        /// 获取实体的指定类型行为
        /// </summary>
        public BehaviorRuntime GetEntityBehavior(BehaviorEntityId ownerId, string behaviorKind)
        {
            if (_entityBehaviors.TryGetValue(ownerId, out var list))
            {
                foreach (var behavior in list)
                {
                    if (behavior.BehaviorKind == behaviorKind && behavior.Phase == BehaviorPhase.Running)
                        return behavior;
                }
            }
            return null;
        }
        
        /// <summary>
        /// 获取实体的最高优先级行为
        /// </summary>
        public BehaviorRuntime GetHighestPriorityBehavior(BehaviorEntityId ownerId)
        {
            if (!_entityBehaviors.TryGetValue(ownerId, out var list))
                return null;
            
            BehaviorRuntime highest = null;
            foreach (var behavior in list)
            {
                if (behavior.Phase != BehaviorPhase.Running)
                    continue;
                
                if (highest == null || behavior.Priority > highest.Priority)
                    highest = behavior;
            }
            return highest;
        }
        
        #endregion
        
        #region Control
        
        /// <summary>
        /// 中断指定行为
        /// </summary>
        public void Interrupt(long instanceId, string reason)
        {
            if (_behaviors.TryGetValue(instanceId, out var behavior))
            {
                behavior.Interrupt(reason);
            }
        }
        
        /// <summary>
        /// 中断实体的所有行为
        /// </summary>
        public void InterruptAll(BehaviorEntityId ownerId, string reason)
        {
            if (_entityBehaviors.TryGetValue(ownerId, out var list))
            {
                foreach (var behavior in list.ToArray())
                {
                    if (behavior.Phase == BehaviorPhase.Running)
                        behavior.Interrupt(reason);
                }
            }
        }
        
        /// <summary>
        /// 中断实体的指定类型行为
        /// </summary>
        public void InterruptBehavior(BehaviorEntityId ownerId, string behaviorKind, string reason)
        {
            var behavior = GetEntityBehavior(ownerId, behaviorKind);
            behavior?.Interrupt(reason);
        }
        
        /// <summary>
        /// 暂停实体的所有行为
        /// </summary>
        public void PauseAll(BehaviorEntityId ownerId)
        {
            if (_entityBehaviors.TryGetValue(ownerId, out var list))
            {
                foreach (var behavior in list)
                {
                    if (behavior.Phase == BehaviorPhase.Running)
                        behavior.Pause();
                }
            }
        }
        
        /// <summary>
        /// 恢复实体的所有行为
        /// </summary>
        public void ResumeAll(BehaviorEntityId ownerId)
        {
            if (_entityBehaviors.TryGetValue(ownerId, out var list))
            {
                foreach (var behavior in list)
                {
                    if (behavior.Phase == BehaviorPhase.Paused)
                        behavior.Resume();
                }
            }
        }
        
        #endregion
        
        #region Tick
        
        /// <summary>
        /// Tick 所有运行中的行为
        /// </summary>
        public void Tick(float deltaTime, long frame)
        {
            foreach (var behavior in _behaviors.Values)
            {
                if (behavior.Phase == BehaviorPhase.Running)
                {
                    behavior.Tick(deltaTime, frame);
                }
            }
            
            CleanupTerminatedBehaviors();
        }
        
        #endregion
        
        #region Statistics
        
        /// <summary>
        /// 运行中的行为数量
        /// </summary>
        public int RunningCount
        {
            get
            {
                int count = 0;
                foreach (var b in _behaviors.Values)
                {
                    if (b.Phase == BehaviorPhase.Running)
                        count++;
                }
                return count;
            }
        }
        
        /// <summary>
        /// 总行为数量
        /// </summary>
        public int TotalCount => _behaviors.Count;
        
        #endregion
        
        #region Private Methods
        
        private void OnRuntimeComplete(BehaviorRuntime behavior)
        {
            Cleanup(behavior, BehaviorEndReason.Completed);
        }
        
        private void OnRuntimeInterrupt(BehaviorRuntime behavior, string reason)
        {
            Cleanup(behavior, BehaviorEndReason.Interrupted);
        }
        
        private void Cleanup(BehaviorRuntime behavior, BehaviorEndReason reason)
        {
            var instanceId = behavior.InstanceId;
            
            behavior.OnComplete -= OnRuntimeComplete;
            behavior.OnInterrupt -= OnRuntimeInterrupt;
            
            // 清理绑定
            if (_bindings.TryGetValue(instanceId, out var binding))
            {
                binding.RemoveBehavior(instanceId);
                _bindings.Remove(instanceId);
            }
            
            // 从实体列表移除
            if (_entityBehaviors.TryGetValue(behavior.OwnerId, out var list))
            {
                list.Remove(behavior);
            }
            
            // 从总表移除
            _behaviors.Remove(instanceId);
            
            OnBehaviorEnded?.Invoke(behavior, reason);
        }
        
        private void CleanupTerminatedBehaviors()
        {
            // 已经在 Cleanup 中处理
        }
        
        #endregion
    }
    
    /// <summary>
    /// 带绑定的行为创建配置
    /// </summary>
    public class BehaviorCreateConfigWithBinding : BehaviorCreateConfig
    {
        public BehaviorBinding Binding { get; set; }
    }
    
    /// <summary>
    /// 默认世界查询
    /// 空实现，用于在没有自定义实现时避免空引用
    /// </summary>
    public class DefaultWorldQuery : IWorldQuery
    {
        public AbilityKit.Ability.Share.Math.Vec3 GetPosition(BehaviorEntityId id) => AbilityKit.Ability.Share.Math.Vec3.Zero;
        public void SetPosition(BehaviorEntityId id, AbilityKit.Ability.Share.Math.Vec3 position) { }
        public AbilityKit.Ability.Share.Math.Vec3 GetForward(BehaviorEntityId id) => AbilityKit.Ability.Share.Math.Vec3.Forward;
        public void SetForward(BehaviorEntityId id, AbilityKit.Ability.Share.Math.Vec3 forward) { }
        public float GetDistance(BehaviorEntityId a, BehaviorEntityId b) => 0f;
        public float GetDistanceToPosition(BehaviorEntityId entityId, AbilityKit.Ability.Share.Math.Vec3 position) => 0f;
        public bool EntityExists(BehaviorEntityId id) => false;
        public T GetData<T>(BehaviorEntityId id, string key, T defaultValue = default) => defaultValue;
        public void SetData<T>(BehaviorEntityId id, string key, T value) { }
        public bool HasData(BehaviorEntityId id, string key) => false;
    }
}
