using System;
using System.Collections.Generic;
using AbilityKit.Ability;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 行为阶段基类
    /// 用于在 Pipeline 中嵌入行为执行
    /// </summary>
    public abstract class AbilityBehaviorPhase<TCtx, TDecision> : AbilityInterruptiblePhaseBase<TCtx>
        where TCtx : IAbilityPipelineContext
        where TDecision : IBehaviorDecision
    {
        private BehaviorRuntime _behavior;
        private IBehaviorOutput _output;
        
        public event Action<TCtx, BehaviorRuntime> OnBehaviorStarted;
        public event Action<TCtx, BehaviorRuntime, BehaviorEndReason> OnBehaviorEnded;
        
        protected AbilityBehaviorPhase(string behaviorKind, int priority = 0) : base(behaviorKind)
        {
            BehaviorKind = behaviorKind;
            Priority = priority;
        }
        
        protected string BehaviorKind { get; }
        protected int Priority { get; }
        
        protected override void OnEnter(TCtx context)
        {
            base.OnEnter(context);
            
            var decision = CreateDecision(context);
            var executor = CreateExecutor(context);
            var world = CreateWorldQuery(context);
            var config = CreateConfig(context);
            
            var sourceContextId = context.GetData<long>("SourceContextId", 0);
            var ownerId = context.GetData<long>("OwnerId", 0);
            var targetIdOpt = context.GetData<long?>("TargetId", null);
            
            var behaviorConfig = new BehaviorCreateConfig
            {
                BehaviorKind = BehaviorKind,
                SourceContextId = sourceContextId,
                OwnerId = ownerId,
                TargetId = targetIdOpt.HasValue ? new BehaviorEntityId(targetIdOpt.Value) : null,
                DurationSeconds = Duration > 0 ? Duration : null,
                Priority = Priority,
                Decision = decision,
                Executor = executor,
                World = world,
                Config = config
            };
            
            _behavior = new BehaviorRuntime(behaviorConfig);
            
            _output = _behavior.Output;
            _behavior.OnComplete += OnRuntimeComplete;
            _behavior.OnInterrupt += OnRuntimeInterrupt;
            
            _behavior.Start();
            OnBehaviorStarted?.Invoke(context, _behavior);
        }
        
        protected override sealed void OnExecute(TCtx context)
        {
            // 行为阶段在 OnTick 中处理逻辑
        }
        
        protected override void OnTick(TCtx context, float deltaTime)
        {
            if (_behavior == null || _behavior.Phase != BehaviorPhase.Running)
            {
                Complete(context);
                return;
            }
            
            var currentFrame = context.GetData<long>("CurrentFrame", 0);
            _behavior.Tick(deltaTime, currentFrame);
            ProcessOutput(context);
            OnBehaviorTick(context, _behavior);
        }
        
        protected virtual void ProcessOutput(TCtx context)
        {
            if (_output == null) return;
            
            // 处理移动指令
            if (_output.Movement.HasValue)
            {
                var movement = _output.Movement.Value;
                ProcessMovement(context, movement);
            }
            
            // 处理待触发效果
            foreach (var effect in _output.PendingEffects)
            {
                ProcessEffect(context, effect);
            }
            
            // 处理待触发事件
            foreach (var evt in _output.PendingEvents)
            {
                ProcessEvent(context, evt);
            }
            
            // 检查完成/中断请求
            if (_output.ShouldComplete)
            {
                Complete(context);
            }
            else if (_output.ShouldInterrupt)
            {
                OnInterrupt(context);
            }
        }
        
        protected virtual void ProcessMovement(TCtx context, MovementSpec movement)
        {
            // 子类可重写
        }
        
        protected virtual void ProcessEffect(TCtx context, PendingEffect effect)
        {
            // 子类可重写
        }
        
        protected virtual void ProcessEvent(TCtx context, PendingEvent evt)
        {
            // 子类可重写
        }
        
        protected virtual void OnBehaviorTick(TCtx context, BehaviorRuntime behavior)
        {
            // 子类可重写
        }
        
        protected virtual void OnBehaviorComplete(TCtx context, BehaviorRuntime behavior)
        {
            // 子类可重写
        }
        
        protected virtual void OnBehaviorInterrupt(TCtx context, BehaviorRuntime behavior, string reason)
        {
            // 子类可重写
        }
        
        protected abstract TDecision CreateDecision(TCtx context);
        protected abstract IBehaviorExecutor CreateExecutor(TCtx context);
        protected abstract IWorldQuery CreateWorldQuery(TCtx context);
        protected virtual IReadOnlyDictionary<string, object> CreateConfig(TCtx context) => EmptyConfig;
        
        private void OnRuntimeComplete(BehaviorRuntime behavior)
        {
        }
        
        private void OnRuntimeInterrupt(BehaviorRuntime behavior, string reason)
        {
        }
        
        public override void OnInterrupt(TCtx context)
        {
            if (_behavior != null && _behavior.Phase == BehaviorPhase.Running)
            {
                _behavior.Interrupt("PipelineInterrupted");
            }
            Cleanup();
            base.OnInterrupt(context);
        }
        
        protected override void OnExit(TCtx context)
        {
            Cleanup();
            base.OnExit(context);
        }
        
        private void Cleanup()
        {
            if (_behavior != null)
            {
                _behavior.OnComplete -= OnRuntimeComplete;
                _behavior.OnInterrupt -= OnRuntimeInterrupt;
            }
            _behavior = null;
            _output = null;
        }
        
        protected override sealed void Complete(TCtx context)
        {
            if (IsComplete) return;
            
            var behavior = _behavior;
            var endReason = behavior?.Phase == BehaviorPhase.Completed 
                ? BehaviorEndReason.Completed 
                : BehaviorEndReason.Interrupted;
            
            IsComplete = true;
            OnExit(context);
            
            OnBehaviorEnded?.Invoke(context, behavior, endReason);
            OnBehaviorComplete(context, behavior);
        }
        
        public override void Reset()
        {
            Cleanup();
            base.Reset();
        }
        
        private static readonly IReadOnlyDictionary<string, object> EmptyConfig = 
            new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Pipeline 上下文到 IWorldQuery 的适配器
    /// </summary>
    public class PipelineWorldQueryAdapter<TCtx> : IWorldQuery
        where TCtx : IAbilityPipelineContext
    {
        protected readonly TCtx _context;
        
        public PipelineWorldQueryAdapter(TCtx context)
        {
            _context = context;
        }
        
        public virtual Vec3 GetPosition(BehaviorEntityId id)
        {
            return _context.GetData<Vec3>(GetKey(id.Value, "Position"), Vec3.Zero);
        }
        
        public virtual void SetPosition(BehaviorEntityId id, Vec3 position)
        {
            _context.SetData(GetKey(id.Value, "Position"), position);
        }
        
        public virtual Vec3 GetForward(BehaviorEntityId id)
        {
            return _context.GetData<Vec3>(GetKey(id.Value, "Forward"), Vec3.Forward);
        }
        
        public virtual void SetForward(BehaviorEntityId id, Vec3 forward)
        {
            _context.SetData(GetKey(id.Value, "Forward"), forward);
        }
        
        public virtual float GetDistance(BehaviorEntityId a, BehaviorEntityId b)
        {
            var posA = GetPosition(a);
            var posB = GetPosition(b);
            return (posA - posB).Magnitude;
        }
        
        public virtual float GetDistanceToPosition(BehaviorEntityId entityId, Vec3 position)
        {
            var entityPos = GetPosition(entityId);
            return (entityPos - position).Magnitude;
        }
        
        public virtual bool EntityExists(BehaviorEntityId id)
        {
            return _context.TryGetData<bool>(GetKey(id.Value, "Exists"), out var exists) && exists;
        }
        
        public virtual T GetData<T>(BehaviorEntityId id, string key, T defaultValue = default)
        {
            return _context.GetData<T>(GetKey(id.Value, key), defaultValue);
        }
        
        public virtual void SetData<T>(BehaviorEntityId id, string key, T value)
        {
            _context.SetData(GetKey(id.Value, key), value);
        }
        
        public virtual bool HasData(BehaviorEntityId id, string key)
        {
            return _context.TryGetData<object>(GetKey(id.Value, key), out var val) && val != null;
        }
        
        private static string GetKey(long entityId, string key) => $"{entityId}:{key}";
    }
    
    /// <summary>
    /// 行为阶段工厂
    /// </summary>
    public static class BehaviorPhaseFactory
    {
        /// <summary>
        /// 创建引导阶段
        /// </summary>
        public static AbilityBehaviorPhase<TCtx, DelegateDecision> CreateChanneling<TCtx>(
            Func<TCtx, bool> canContinue,
            Action<TCtx> onComplete = null,
            Action<TCtx, string> onInterrupt = null)
            where TCtx : IAbilityPipelineContext
        {
            return new ChannelingBehaviorPhase<TCtx>(canContinue, onComplete, onInterrupt);
        }
        
        private class ChannelingBehaviorPhase<TCtx> : AbilityBehaviorPhase<TCtx, DelegateDecision>
            where TCtx : IAbilityPipelineContext
        {
            private readonly Func<TCtx, bool> _canContinue;
            private readonly Action<TCtx> _onComplete;
            private readonly Action<TCtx, string> _onInterrupt;
            
            public ChannelingBehaviorPhase(
                Func<TCtx, bool> canContinue,
                Action<TCtx> onComplete,
                Action<TCtx, string> onInterrupt) : base("Channeling")
            {
                _canContinue = canContinue;
                _onComplete = onComplete;
                _onInterrupt = onInterrupt;
            }
            
            protected override DelegateDecision CreateDecision(TCtx context)
            {
                return new DelegateDecision("Channeling", (bctx, world) =>
                {
                    if (!_canContinue(context))
                        return DecisionResult.Interrupt("ConditionFailed");
                    return DecisionResult.Continue("Channeling");
                });
            }
            
            protected override IBehaviorExecutor CreateExecutor(TCtx context) => new DefaultExecutor();
            protected override IWorldQuery CreateWorldQuery(TCtx context) => new PipelineWorldQueryAdapter<TCtx>(context);
            
            protected override void OnBehaviorComplete(TCtx context, BehaviorRuntime behavior)
            {
                _onComplete?.Invoke(context);
            }
            
            protected override void OnBehaviorInterrupt(TCtx context, BehaviorRuntime behavior, string reason)
            {
                _onInterrupt?.Invoke(context, reason);
            }
        }
        
        /// <summary>
        /// 创建跟随阶段
        /// </summary>
        public static AbilityBehaviorPhase<TCtx, DelegateDecision> CreateFollow<TCtx>(
            float stopDistance = 1f,
            float? moveSpeed = null)
            where TCtx : IAbilityPipelineContext
        {
            return new FollowBehaviorPhase<TCtx>(stopDistance, moveSpeed);
        }
        
        private class FollowBehaviorPhase<TCtx> : AbilityBehaviorPhase<TCtx, DelegateDecision>
            where TCtx : IAbilityPipelineContext
        {
            private readonly float _stopDistance;
            private readonly float _moveSpeed;
            
            public FollowBehaviorPhase(float stopDistance, float? moveSpeed) : base("Follow")
            {
                _stopDistance = stopDistance;
                _moveSpeed = moveSpeed ?? 5f;
            }
            
            protected override DelegateDecision CreateDecision(TCtx context)
            {
                return new DelegateDecision("Follow", (bctx, world) =>
                {
                    if (!bctx.TargetId.HasValue || !world.EntityExists(bctx.TargetId.Value))
                        return DecisionResult.Interrupt("TargetInvalid");
                    
                    var targetPos = world.GetPosition(bctx.TargetId.Value);
                    var ownerPos = world.GetPosition(bctx.OwnerId);
                    var distance = world.GetDistanceToPosition(bctx.OwnerId, targetPos);
                    
                    if (distance <= _stopDistance)
                        return DecisionResult.Complete();
                    
                    return DecisionResult.Continue("Following")
                        .WithMovement(targetPos, bctx.TargetId, _moveSpeed);
                });
            }
            
            protected override IBehaviorExecutor CreateExecutor(TCtx context) => new DefaultExecutor();
            protected override IWorldQuery CreateWorldQuery(TCtx context) => new PipelineWorldQueryAdapter<TCtx>(context);
        }
    }
}
