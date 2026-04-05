using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Math;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 行为执行器接口
    /// 
    /// 职责：执行决策的结果
    /// </summary>
    public interface IBehaviorExecutor
    {
        /// <summary>
        /// 执行决策
        /// </summary>
        void Execute(DecisionResult decision, IBehaviorContext context, IBehaviorOutput output);
    }
    
    /// <summary>
    /// 行为输出接口
    /// 用于收集执行结果，传递给外部处理
    /// </summary>
    public interface IBehaviorOutput
    {
        /// <summary>
        /// 请求完成行为
        /// </summary>
        void RequestComplete();
        
        /// <summary>
        /// 请求中断行为
        /// </summary>
        void RequestInterrupt(string reason);
        
        /// <summary>
        /// 是否请求完成
        /// </summary>
        bool ShouldComplete { get; }
        
        /// <summary>
        /// 是否请求中断
        /// </summary>
        bool ShouldInterrupt { get; }
        
        /// <summary>
        /// 中断原因
        /// </summary>
        string InterruptReason { get; }
        
        /// <summary>
        /// 待触发的事件列表
        /// </summary>
        IReadOnlyList<PendingEvent> PendingEvents { get; }
        
        /// <summary>
        /// 待处理的效果列表
        /// </summary>
        IReadOnlyList<PendingEffect> PendingEffects { get; }
        
        /// <summary>
        /// 移动指令
        /// </summary>
        MovementSpec? Movement { get; }
        
        /// <summary>
        /// 添加待触发事件
        /// </summary>
        void AddEvent(string eventId, IReadOnlyDictionary<string, object> payload = null);
        
        /// <summary>
        /// 添加待触发效果
        /// </summary>
        void AddEffect(string effectId, BehaviorEntityId? targetId = null, IReadOnlyDictionary<string, object> param = null);
        
        /// <summary>
        /// 设置移动指令
        /// </summary>
        void SetMovement(Vec3? targetPosition, BehaviorEntityId? targetEntity, float speed);
        
        /// <summary>
        /// 清除所有输出
        /// </summary>
        void Clear();
    }
    
    /// <summary>
    /// 待触发事件
    /// </summary>
    public readonly struct PendingEvent
    {
        public string EventId { get; }
        public IReadOnlyDictionary<string, object> Payload { get; }
        
        public PendingEvent(string eventId, IReadOnlyDictionary<string, object> payload = null)
        {
            EventId = eventId;
            Payload = payload;
        }
    }
    
    /// <summary>
    /// 待触发效果
    /// </summary>
    public readonly struct PendingEffect
    {
        public string EffectId { get; }
        public BehaviorEntityId? TargetId { get; }
        public IReadOnlyDictionary<string, object> Params { get; }
        
        public PendingEffect(string effectId, BehaviorEntityId? targetId = null, IReadOnlyDictionary<string, object> param = null)
        {
            EffectId = effectId;
            TargetId = targetId;
            Params = param;
        }
    }
    
    /// <summary>
    /// 移动指令
    /// </summary>
    public readonly struct MovementSpec
    {
        public Vec3? TargetPosition { get; }
        public BehaviorEntityId? TargetEntity { get; }
        public float Speed { get; }
        
        public MovementSpec(Vec3? targetPosition, BehaviorEntityId? targetEntity, float speed)
        {
            TargetPosition = targetPosition;
            TargetEntity = targetEntity;
            Speed = speed;
        }
        
        public static MovementSpec Stop(float speed = 0f) => new MovementSpec(null, null, speed);
        public static MovementSpec MoveTo(Vec3 position, float speed) => new MovementSpec(position, null, speed);
        public static MovementSpec Follow(BehaviorEntityId target, float speed) => new MovementSpec(null, target, speed);
    }
    
    /// <summary>
    /// 默认执行器
    /// 处理移动指令和请求
    /// </summary>
    public class DefaultExecutor : IBehaviorExecutor
    {
        public virtual void Execute(DecisionResult decision, IBehaviorContext context, IBehaviorOutput output)
        {
            switch (decision.Kind)
            {
                case DecisionKind.Continue:
                    var targetPos = decision.GetMoveTarget();
                    var targetEntity = decision.GetMoveTargetEntity();
                    var speed = decision.GetMoveSpeed(0f);
                    
                    if (targetPos.HasValue || targetEntity.HasValue)
                    {
                        output.SetMovement(targetPos, targetEntity, speed);
                    }
                    break;
                    
                case DecisionKind.Complete:
                    output.RequestComplete();
                    break;
                    
                case DecisionKind.Interrupt:
                    output.RequestInterrupt(decision.InterruptReason ?? "Decision");
                    break;
            }
        }
    }
    
    /// <summary>
    /// 决策结果扩展方法
    /// </summary>
    public static class DecisionResultExtensions
    {
        /// <summary>
        /// 添加移动参数
        /// </summary>
        public static DecisionResult WithMovement(
            this DecisionResult result, 
            Vec3? targetPosition, 
            BehaviorEntityId? targetEntity, 
            float speed)
        {
            var dict = new Dictionary<string, object>();
            
            if (targetPosition.HasValue)
                dict["MoveTarget"] = targetPosition.Value;
            
            if (targetEntity.HasValue)
                dict["MoveTargetEntity"] = targetEntity.Value;
            
            dict["MoveSpeed"] = speed;
            
            return DecisionResult.WithParams(result, dict);
        }
        
        /// <summary>
        /// 添加参数
        /// </summary>
        public static DecisionResult WithParams(
            this DecisionResult result,
            Dictionary<string, object> @params)
        {
            if (@params == null || @params.Count == 0)
                return result;
            
            return DecisionResult.WithParams(result, @params);
        }
    }
}
