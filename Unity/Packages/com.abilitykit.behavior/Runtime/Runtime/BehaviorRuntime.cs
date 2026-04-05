using System;
using System.Collections.Generic;
using System.Threading;
using AbilityKit.Ability;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 行为运行时
    /// 
    /// 核心循环：Tick → Decision.Decide → Executor.Execute
    /// 
    /// 注意：完全独立于 Triggering 模块
    /// </summary>
    public class BehaviorRuntime
    {
        #region Properties
        
        public long InstanceId { get; }
        public string BehaviorKind { get; }
        public long SourceContextId { get; }
        public BehaviorEntityId OwnerId { get; }
        public BehaviorEntityId? TargetId { get; }
        public float? DurationSeconds { get; }
        public int Priority { get; }
        
        public BehaviorPhase Phase { get; private set; }
        public long CurrentFrame { get; private set; }
        public float ElapsedSeconds { get; private set; }
        
        public IBehaviorDecision Decision { get; }
        public IBehaviorExecutor Executor { get; }
        public IWorldQuery World { get; }
        public IBehaviorOutput Output { get; }
        public IBehaviorState State { get; }
        public IReadOnlyDictionary<string, object> Config { get; }
        
        #endregion
        
        #region Events
        
        public event Action<BehaviorRuntime> OnComplete;
        public event Action<BehaviorRuntime, string> OnInterrupt;
        
        #endregion
        
        #region Private Fields
        
        private bool _requestComplete;
        private string _interruptReason;
        
        #endregion
        
        #region Constructor
        
        public BehaviorRuntime(
            long instanceId,
            string behaviorKind,
            long sourceContextId,
            BehaviorEntityId ownerId,
            BehaviorEntityId? targetId,
            float? durationSeconds,
            int priority,
            IBehaviorDecision decision,
            IBehaviorExecutor executor,
            IWorldQuery world,
            IReadOnlyDictionary<string, object> config)
        {
            InstanceId = instanceId;
            BehaviorKind = behaviorKind;
            SourceContextId = sourceContextId;
            OwnerId = ownerId;
            TargetId = targetId;
            DurationSeconds = durationSeconds;
            Priority = priority;
            Decision = decision;
            Executor = executor ?? new DefaultExecutor();
            World = world;
            Config = config ?? EmptyConfig;
            
            State = new BehaviorState();
            Output = new BehaviorOutput();
            Phase = BehaviorPhase.Created;
        }
        
        public BehaviorRuntime(BehaviorCreateConfig config)
        {
            InstanceId = Interlocked.Increment(ref _instanceIdCounter);
            BehaviorKind = config.BehaviorKind;
            SourceContextId = config.SourceContextId;
            OwnerId = config.OwnerId;
            TargetId = config.TargetId;
            DurationSeconds = config.DurationSeconds;
            Priority = config.Priority;
            Decision = config.Decision;
            Executor = config.Executor ?? new DefaultExecutor();
            World = config.World;
            Config = config.Config ?? EmptyConfig;
            
            State = new BehaviorState();
            Output = new BehaviorOutput();
            Phase = BehaviorPhase.Created;
        }
        
        private static int _instanceIdCounter = 0;
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// 启动行为
        /// </summary>
        public void Start()
        {
            if (Phase != BehaviorPhase.Created) return;
            Phase = BehaviorPhase.Running;
        }
        
        /// <summary>
        /// 每帧更新
        /// </summary>
        public void Tick(float deltaTime, long frame)
        {
            if (Phase != BehaviorPhase.Running) return;
            
            CurrentFrame = frame;
            ElapsedSeconds += deltaTime;
            
            // 重置输出
            _requestComplete = false;
            _interruptReason = null;
            Output.Clear();
            
            // 创建上下文
            var context = new RuntimeContext(this);
            
            try
            {
                // 决策
                var result = Decision.Decide(context, World);
                
                // 执行
                Executor.Execute(result, context, Output);
                
                // 处理请求
                if (Output.ShouldComplete || _requestComplete)
                {
                    Complete();
                    return;
                }
                
                if (Output.ShouldInterrupt || !string.IsNullOrEmpty(_interruptReason))
                {
                    Interrupt(_interruptReason ?? Output.InterruptReason);
                    return;
                }
                
                // 检查持续时间
                if (DurationSeconds.HasValue && ElapsedSeconds >= DurationSeconds.Value)
                {
                    Complete();
                }
            }
            catch (Exception ex)
            {
                Interrupt($"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 请求完成
        /// </summary>
        public void RequestComplete() => _requestComplete = true;
        
        /// <summary>
        /// 请求中断
        /// </summary>
        public void RequestInterrupt(string reason) => _interruptReason = reason;
        
        /// <summary>
        /// 暂停
        /// </summary>
        public void Pause()
        {
            if (Phase != BehaviorPhase.Running) return;
            Phase = BehaviorPhase.Paused;
        }
        
        /// <summary>
        /// 恢复
        /// </summary>
        public void Resume()
        {
            if (Phase != BehaviorPhase.Paused) return;
            Phase = BehaviorPhase.Running;
        }
        
        /// <summary>
        /// 完成
        /// </summary>
        public void Complete()
        {
            if (Phase == BehaviorPhase.Completed || Phase == BehaviorPhase.Interrupted) return;
            Phase = BehaviorPhase.Completed;
            OnComplete?.Invoke(this);
        }
        
        /// <summary>
        /// 中断
        /// </summary>
        public void Interrupt(string reason)
        {
            if (Phase == BehaviorPhase.Completed || Phase == BehaviorPhase.Interrupted) return;
            Phase = BehaviorPhase.Interrupted;
            OnInterrupt?.Invoke(this, reason);
        }
        
        /// <summary>
        /// 获取上下文
        /// </summary>
        public IBehaviorContext GetContext() => new RuntimeContext(this);
        
        #endregion
        
        #region Private Classes
        
        private class RuntimeContext : IBehaviorContext
        {
            private readonly BehaviorRuntime _runtime;
            
            public long InstanceId => _runtime.InstanceId;
            public string BehaviorKind => _runtime.BehaviorKind;
            public long SourceContextId => _runtime.SourceContextId;
            public BehaviorEntityId OwnerId => _runtime.OwnerId;
            public BehaviorEntityId? TargetId => _runtime.TargetId;
            public long CurrentFrame => _runtime.CurrentFrame;
            public float ElapsedSeconds => _runtime.ElapsedSeconds;
            public float? DurationSeconds => _runtime.DurationSeconds;
            public BehaviorPhase Phase => _runtime.Phase;
            public IWorldQuery World => _runtime.World;
            public IBehaviorState State => _runtime.State;
            public IReadOnlyDictionary<string, object> Config => _runtime.Config;
            
            public RuntimeContext(BehaviorRuntime runtime) => _runtime = runtime;
            
            public T GetConfig<T>(string key, T defaultValue = default)
            {
                if (Config.TryGetValue(key, out var value) && value is T typed)
                    return typed;
                return defaultValue;
            }

            public bool TryGetConfig<T>(string key, out T value)
            {
                if (Config.TryGetValue(key, out var v) && v is T typed)
                {
                    value = typed;
                    return true;
                }
                value = default;
                return false;
            }

            public Vec3? GetTargetPosition()
            {
                if (Config.TryGetValue("MoveTarget", out var v) && v is Vec3 vec)
                    return vec;
                return null;
            }
        }
        
        #endregion
        
        private static readonly IReadOnlyDictionary<string, object> EmptyConfig = 
            new Dictionary<string, object>();
    }
    
    /// <summary>
    /// 行为状态存储实现
    /// </summary>
    public class BehaviorState : IBehaviorState
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();
        
        public T Get<T>(string key, T defaultValue = default)
        {
            if (_data.TryGetValue(key, out var value) && value is T typed)
                return typed;
            return defaultValue;
        }
        
        public void Set<T>(string key, T value) => _data[key] = value;
        
        public bool Has(string key) => _data.ContainsKey(key);
        
        public void Remove(string key) => _data.Remove(key);
        
        public void Clear() => _data.Clear();
    }
    
    /// <summary>
    /// 行为输出实现
    /// </summary>
    public class BehaviorOutput : IBehaviorOutput
    {
        private readonly List<PendingEvent> _events = new List<PendingEvent>();
        private readonly List<PendingEffect> _effects = new List<PendingEffect>();
        
        public bool ShouldComplete { get; private set; }
        public bool ShouldInterrupt { get; private set; }
        public string InterruptReason { get; private set; }
        public IReadOnlyList<PendingEvent> PendingEvents => _events;
        public IReadOnlyList<PendingEffect> PendingEffects => _effects;
        public MovementSpec? Movement { get; private set; }
        
        public void RequestComplete() => ShouldComplete = true;
        
        public void RequestInterrupt(string reason)
        {
            ShouldInterrupt = true;
            InterruptReason = reason;
        }
        
        public void AddEvent(string eventId, IReadOnlyDictionary<string, object> payload = null)
        {
            _events.Add(new PendingEvent(eventId, payload));
        }
        
        public void AddEffect(string effectId, BehaviorEntityId? targetId = null, IReadOnlyDictionary<string, object> param = null)
        {
            _effects.Add(new PendingEffect(effectId, targetId, param));
        }
        
        public void SetMovement(Vec3? targetPosition, BehaviorEntityId? targetEntity, float speed)
        {
            Movement = new MovementSpec(targetPosition, targetEntity, speed);
        }
        
        public void Clear()
        {
            ShouldComplete = false;
            ShouldInterrupt = false;
            InterruptReason = null;
            _events.Clear();
            _effects.Clear();
            Movement = null;
        }
    }
}
