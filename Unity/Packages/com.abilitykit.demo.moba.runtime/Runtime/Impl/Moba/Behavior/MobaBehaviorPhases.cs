using System;
using System.Collections.Generic;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.Behavior;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Share.Impl.Moba.Services;

namespace AbilityKit.Moba.Behavior
{
    using AbilityKit.Ability;
    /// <summary>
    /// 引导行为阶段
    /// 用于持续施法、引导类技能
    /// </summary>
    public class ChannelingBehaviorPhase : AbilityBehaviorPhase<SkillPipelineContext, DelegateDecision>
    {
        public Func<SkillPipelineContext, bool> CanContinueChanneling { get; set; }
        public Action<SkillPipelineContext, float> OnChannelTick { get; set; }
        public Action<SkillPipelineContext, string> OnChannelInterrupted { get; set; }
        public Action<SkillPipelineContext> OnChannelComplete { get; set; }
        
        public ChannelingBehaviorPhase() : base("Channeling")
        {
        }
        
        protected override DelegateDecision CreateDecision(SkillPipelineContext context)
        {
            return new DelegateDecision("Channeling", (ctx, world) =>
            {
                var mobaWorld = world as MobaWorldQuery;
                
                if (!IsAlive(ctx, world))
                {
                    return DecisionResult.Interrupt("OwnerDied");
                }
                
                if (mobaWorld != null)
                {
                    if (!mobaWorld.CanBeControlled(ctx.OwnerId))
                    {
                        return DecisionResult.Interrupt("LostControl");
                    }
                    
                    if (mobaWorld.HasTag(ctx.OwnerId, "Silenced"))
                    {
                        return DecisionResult.Interrupt("Silenced");
                    }
                }
                
                if (ctx.TargetId.HasValue)
                {
                    var targetId = ctx.TargetId.Value;
                    if (!world.EntityExists(targetId))
                    {
                        return DecisionResult.Interrupt("TargetInvalid");
                    }
                    
                    if (mobaWorld != null && !mobaWorld.IsAlive(targetId))
                    {
                        return DecisionResult.Interrupt("TargetDied");
                    }
                    
                    if (context.TryGetData<float>("MaxRange", out var maxRange) && maxRange > 0)
                    {
                        var distance = world.GetDistanceToPosition(ctx.OwnerId, world.GetPosition(targetId));
                        if (distance > maxRange)
                        {
                            return DecisionResult.Interrupt("OutOfRange");
                        }
                    }
                }
                
                if (CanContinueChanneling != null && !CanContinueChanneling(context))
                {
                    return DecisionResult.Interrupt("CustomCondition");
                }
                
                return DecisionResult.Continue("Channeling");
            });
        }
        
        protected override IBehaviorExecutor CreateExecutor(SkillPipelineContext context)
        {
            return new ChannelingExecutor(this);
        }
        
        protected override IWorldQuery CreateWorldQuery(SkillPipelineContext context)
        {
            if (context.TryGetData<MobaWorldQuery>("MobaWorldQuery", out var query))
            {
                return query;
            }
            return new PipelineWorldQueryAdapter<SkillPipelineContext>(context);
        }
        
        protected override void OnBehaviorTick(SkillPipelineContext context, BehaviorRuntime behavior)
        {
            OnChannelTick?.Invoke(context, behavior.ElapsedSeconds);
        }
        
        protected override void OnBehaviorComplete(SkillPipelineContext context, BehaviorRuntime behavior)
        {
            OnChannelComplete?.Invoke(context);
        }
        
        protected override void OnBehaviorInterrupt(SkillPipelineContext context, BehaviorRuntime behavior, string reason)
        {
            OnChannelInterrupted?.Invoke(context, reason);
        }
        
        private bool IsAlive(IBehaviorContext ctx, IWorldQuery world)
        {
            if (world is MobaWorldQuery moba)
                return moba.IsAlive(ctx.OwnerId);
            return world.GetData<bool>(ctx.OwnerId, "alive", true);
        }
        
        private class ChannelingExecutor : ABehaviorExecutor
        {
            private readonly ChannelingBehaviorPhase _phase;
            
            public ChannelingExecutor(ChannelingBehaviorPhase phase)
            {
                _phase = phase;
            }
            
            public override void Execute(DecisionResult decision, IBehaviorContext context, IBehaviorOutput output)
            {
                if (decision.Kind == DecisionKind.Continue)
                {
                    if (context.TargetId.HasValue && output.Movement == null)
                    {
                        var targetPos = _phase.CreateWorldQuery(null).GetPosition(context.TargetId.Value);
                        output.SetMovement(targetPos, context.TargetId, 0f);
                    }
                }
                
                if (decision.Kind == DecisionKind.Interrupt)
                {
                    output.RequestInterrupt(decision.InterruptReason);
                }
                
                if (decision.Kind == DecisionKind.Complete)
                {
                    output.RequestComplete();
                }
            }
        }
    }
    
    /// <summary>
    /// 跟随行为阶段
    /// 用于跟随目标移动
    /// </summary>
    public class FollowBehaviorPhase : AbilityBehaviorPhase<SkillPipelineContext, DelegateDecision>
    {
        public float FollowDistance { get; set; } = 1f;
        public float MoveSpeed { get; set; } = 5f;
        
        public FollowBehaviorPhase() : base("Follow")
        {
        }
        
        protected override DelegateDecision CreateDecision(SkillPipelineContext context)
        {
            return new DelegateDecision("Follow", (ctx, world) =>
            {
                if (!ctx.TargetId.HasValue)
                {
                    return DecisionResult.Complete();
                }
                
                var targetId = ctx.TargetId.Value;
                if (!world.EntityExists(targetId))
                {
                    return DecisionResult.Interrupt("TargetDied");
                }
                
                if (world is MobaWorldQuery moba && !moba.IsAlive(targetId))
                {
                    return DecisionResult.Interrupt("TargetDied");
                }
                
                var ownerPos = world.GetPosition(ctx.OwnerId);
                var targetPos = world.GetPosition(targetId);
                var distance = world.GetDistanceToPosition(ctx.OwnerId, targetPos);
                
                if (distance <= FollowDistance)
                {
                    return DecisionResult.Complete();
                }
                
                var speed = MoveSpeed > 0 ? MoveSpeed : (world as MobaWorldQuery)?.GetMoveSpeed(ctx.OwnerId, 5f) ?? 5f;
                
                return DecisionResult.Continue("Following")
                    .WithMovement(targetPos, targetId, speed);
            });
        }
        
        protected override IBehaviorExecutor CreateExecutor(SkillPipelineContext context)
        {
            return new DefaultExecutor();
        }
        
        protected override IWorldQuery CreateWorldQuery(SkillPipelineContext context)
        {
            return new PipelineWorldQueryAdapter<SkillPipelineContext>(context);
        }
    }
    
    /// <summary>
    /// 状态机行为阶段
    /// 用于复杂的多状态行为
    /// </summary>
    public class StateMachineBehaviorPhase : AbilityBehaviorPhase<SkillPipelineContext, DelegateDecision>
    {
        private readonly Dictionary<string, Func<SkillPipelineContext, IBehaviorContext, IWorldQuery, DecisionResult>> _states = 
            new Dictionary<string, Func<SkillPipelineContext, IBehaviorContext, IWorldQuery, DecisionResult>>();
        
        private string _initialState;
        
        public StateMachineBehaviorPhase(string initialState) : base("StateMachine")
        {
            _initialState = initialState;
        }
        
        public StateMachineBehaviorPhase AddState(string stateName, 
            Func<SkillPipelineContext, IBehaviorContext, IWorldQuery, DecisionResult> onTick)
        {
            _states[stateName] = onTick;
            return this;
        }
        
        protected override DelegateDecision CreateDecision(SkillPipelineContext context)
        {
            return new DelegateDecision("StateMachine", (ctx, world) =>
            {
                if (!context.TryGetData<string>("currentState", out var currentState))
                {
                    currentState = _initialState;
                }
                
                if (!_states.TryGetValue(currentState, out var handler))
                {
                    return DecisionResult.Complete();
                }
                
                var result = handler(context, ctx, world);
                
                if (result.Kind == DecisionKind.ChangeState && !string.IsNullOrEmpty(result.StateName))
                {
                    context.SetData("currentState", result.StateName);
                }
                
                return result;
            });
        }
        
        protected override IBehaviorExecutor CreateExecutor(SkillPipelineContext context)
        {
            return new DefaultExecutor();
        }
        
        protected override IWorldQuery CreateWorldQuery(SkillPipelineContext context)
        {
            return new PipelineWorldQueryAdapter<SkillPipelineContext>(context);
        }
    }
}
