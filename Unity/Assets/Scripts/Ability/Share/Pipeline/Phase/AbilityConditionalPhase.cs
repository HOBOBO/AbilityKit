using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 条件分支阶段
    /// </summary>
    public class AbilityConditionalPhase : AbilityCompositePhase, IDurationalPhase
    {
        private List<AbilityConditionalBranch> _branches = new List<AbilityConditionalBranch>(4);
        private AbilityConditionalBranch _currentBranch;
        
        /// <summary>
        /// 阶段持续时间（条件阶段由条件决定，设为-1）
        /// </summary>
        public float Duration => -1f;
        
        /// <summary>
        /// 配置无条件满足时的行为
        /// </summary>
        public ENoConditionBehavior NoConditionBehavior { get; set; } = ENoConditionBehavior.Wait;

        public void AddBranch(IAbilityConditionNode condition, IAbilityPipelinePhase phase)
        {
            _branches.Add(new AbilityConditionalBranch(condition, phase));
        }

        public override void Execute(IAbilityPipelineContext context)
        {
            IsComplete = false;
            _currentBranch = null;
            
            // 初次评估条件并选择分支
            if (!EvaluateAndSelectBranch(context))
            {
                // 处理没有条件满足的情况
                HandleNoConditionMet(context);
            }
        }

        public override void OnUpdate(IAbilityPipelineContext context, float deltaTime)
        {
            if (IsComplete) return;

            // 如果当前没有执行的分支，尝试找到满足条件的分支
            if (_currentBranch == null)
            {
                if (EvaluateAndSelectBranch(context))
                {
                    // 找到了满足条件的分支，开始执行
                    return;
                }
                
                // 仍然没有满足的条件
                HandleNoConditionMet(context);
                return;
            }

            // 检查当前分支的条件
            if (_currentBranch.Condition.CheckStrategy == EConditionCheckStrategy.Continuous)
            {
                if (!_currentBranch.Condition.Evaluate(context))
                {
                    // 当前分支条件不满足，尝试切换分支
                    if (!TrySwitchToNewBranch(context))
                    {
                        // 没有其他满足条件的分支
                        HandleNoConditionMet(context);
                        return;
                    }
                }
            }

            // 更新当前分支的执行
            _currentBranch.Phase.OnUpdate(context, deltaTime);
            if (_currentBranch.Phase.IsComplete)
            {
                IsComplete = true;
            }
        }

        private bool EvaluateAndSelectBranch(IAbilityPipelineContext context)
        {
            for (int i = 0; i < _branches.Count; i++)
            {
                var branch = _branches[i];
                if (branch.Condition.Evaluate(context))
                {
                    ExecuteBranch(branch, context);
                    return true;
                }
            }
            return false;
        }

        private bool TrySwitchToNewBranch(IAbilityPipelineContext context)
        {
            for (int i = 0; i < _branches.Count; i++)
            {
                var branch = _branches[i];
                if (branch != _currentBranch && branch.Condition.Evaluate(context))
                {
                    InterruptCurrentBranch(context);
                    ExecuteBranch(branch, context);
                    return true;
                }
            }
            return false;
        }

        private void ExecuteBranch(AbilityConditionalBranch branch, IAbilityPipelineContext context)
        {
            _currentBranch = branch;
            branch.Phase.Execute(context);
        }

        private void InterruptCurrentBranch(IAbilityPipelineContext context)
        {
            if (_currentBranch?.Phase is IInterruptiblePhase interruptible)
            {
                interruptible.OnInterrupt(context);
            }
        }

        private void HandleNoConditionMet(IAbilityPipelineContext context)
        {
            switch (NoConditionBehavior)
            {
                case ENoConditionBehavior.Wait:
                    // 不做任何处理，继续等待条件满足
                    break;
                    
                case ENoConditionBehavior.Complete:
                    // 标记阶段完成
                    IsComplete = true;
                    break;
                    
                case ENoConditionBehavior.Fail:
                    // 中断当前分支并标记失败
                    InterruptCurrentBranch(context);
                    IsComplete = true;
                    break;
                    
                case ENoConditionBehavior.Skip:
                    // 跳过当前阶段，继续执行后续阶段
                    IsComplete = true;
                    break;
            }
        }

        public override void Reset()
        {
            base.Reset();
            _currentBranch = null;
            for (int i = 0; i < _branches.Count; i++)
            {
                _branches[i].Phase.Reset();
            }
        }
    }
}