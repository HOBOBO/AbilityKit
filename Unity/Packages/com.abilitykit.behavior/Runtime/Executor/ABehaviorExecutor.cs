using System;

namespace AbilityKit.Ability.Behavior
{
    /// <summary>
    /// 行为执行器基类
    /// 提供默认实现，方便扩展
    /// </summary>
    public abstract class ABehaviorExecutor : IBehaviorExecutor
    {
        public abstract void Execute(DecisionResult decision, IBehaviorContext context, IBehaviorOutput output);
        
        protected virtual void HandleContinue(DecisionResult decision, IBehaviorContext context, IBehaviorOutput output)
        {
            // 子类可重写
        }
        
        protected virtual void HandleComplete(DecisionResult decision, IBehaviorContext context, IBehaviorOutput output)
        {
            output.RequestComplete();
        }
        
        protected virtual void HandleInterrupt(DecisionResult decision, IBehaviorContext context, IBehaviorOutput output)
        {
            output.RequestInterrupt(decision.InterruptReason ?? "Executor");
        }
    }
}
