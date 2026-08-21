using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 条件分支阶段。
    /// </summary>
    /// <typeparam name="TCtx">管线上下文类型。</typeparam>
    public class AbilityConditionalPhase<TCtx> : AbilityCompositePhase<TCtx>, IDurationalPhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly List<AbilityConditionalBranch<TCtx>> _branches = new List<AbilityConditionalBranch<TCtx>>(4);
        private readonly List<EPipelineDebugConditionResult> _branchResults = new List<EPipelineDebugConditionResult>(4);
        private AbilityConditionalBranch<TCtx>? _currentBranch;
        private int _currentBranchIndex = -1;

        public IReadOnlyList<AbilityConditionalBranch<TCtx>> Branches => _branches;

        internal int DebugCurrentBranchIndex => _currentBranchIndex;
        internal IReadOnlyList<EPipelineDebugConditionResult> DebugBranchResults => _branchResults;
        
        /// <summary>
        /// 阶段持续时间；条件阶段通常由子阶段完成时机决定，因此默认返回 -1。
        /// </summary>
        public float Duration => -1f;
        
        /// <summary>
        /// 无条件命中时的行为。
        /// </summary>
        public ENoConditionBehavior NoConditionBehavior { get; set; } = ENoConditionBehavior.Wait;

        /// <summary>
        /// 创建默认条件阶段。
        /// </summary>
        public AbilityConditionalPhase() : base(new AbilityPipelinePhaseId("Conditional"))
        {
        }

        /// <summary>
        /// 使用指定阶段 ID 创建条件阶段。
        /// </summary>
        public AbilityConditionalPhase(AbilityPipelinePhaseId phaseId) : base(phaseId)
        {
        }

        /// <summary>
        /// 添加一个条件分支。
        /// </summary>
        public void AddBranch(IAbilityConditionNode condition, IAbilityPipelinePhase<TCtx> phase)
        {
            var branch = new AbilityConditionalBranch<TCtx>(condition, phase);
            _branches.Add(branch);
            _subPhases.Add(phase);
            _branchResults.Add(EPipelineDebugConditionResult.Unknown);
        }

        /// <inheritdoc />
        public override void Execute(TCtx context)
        {
            IsComplete = false;
            _currentBranch = null;
            _currentBranchIndex = -1;
            ResetBranchResults();
            
            if (!EvaluateAndSelectBranch(context))
            {
                HandleNoConditionMet(context);
            }
        }

        /// <inheritdoc />
        public override void OnUpdate(TCtx context, float deltaTime)
        {
            if (IsComplete) return;

            if (_currentBranch == null)
            {
                if (EvaluateAndSelectBranch(context))
                {
                    return;
                }
                
                HandleNoConditionMet(context);
                return;
            }

            if (_currentBranch.Condition.CheckStrategy == EConditionCheckStrategy.Continuous)
            {
                if (!EvaluateBranch(_currentBranchIndex, context))
                {
                    if (!TrySwitchToNewBranch(context))
                    {
                        HandleNoConditionMet(context);
                        return;
                    }
                }
            }

            _currentBranch.Phase.OnUpdate(context, deltaTime);
            if (_currentBranch.Phase.IsComplete)
            {
                IsComplete = true;
            }
        }

        private bool EvaluateAndSelectBranch(TCtx context)
        {
            for (int i = 0; i < _branches.Count; i++)
            {
                var branch = _branches[i];
                if (EvaluateBranch(i, context))
                {
                    ExecuteBranch(i, branch, context);
                    return true;
                }
            }
            return false;
        }

        private bool TrySwitchToNewBranch(TCtx context)
        {
            for (int i = 0; i < _branches.Count; i++)
            {
                var branch = _branches[i];
                if (branch != _currentBranch && EvaluateBranch(i, context))
                {
                    InterruptCurrentBranch(context);
                    ExecuteBranch(i, branch, context);
                    return true;
                }
            }
            return false;
        }

        private bool EvaluateBranch(int index, TCtx context)
        {
            bool matched = _branches[index].Condition.Evaluate(context);
            _branchResults[index] = matched
                ? EPipelineDebugConditionResult.Matched
                : EPipelineDebugConditionResult.Rejected;
            return matched;
        }

        private void ExecuteBranch(int index, AbilityConditionalBranch<TCtx> branch, TCtx context)
        {
            _currentBranch = branch;
            _currentBranchIndex = index;
            branch.Phase.Execute(context);
        }

        private void InterruptCurrentBranch(TCtx context)
        {
            if (_currentBranch?.Phase is IInterruptiblePhase<TCtx> interruptible)
            {
                interruptible.OnInterrupt(context);
            }
        }

        private void HandleNoConditionMet(TCtx context)
        {
            switch (NoConditionBehavior)
            {
                case ENoConditionBehavior.Wait:
                    break;
                
                case ENoConditionBehavior.Complete:
                    IsComplete = true;
                    break;
                    
                case ENoConditionBehavior.Fail:
                    InterruptCurrentBranch(context);
                    IsComplete = true;
                    break;
                    
                case ENoConditionBehavior.Skip:
                    IsComplete = true;
                    break;
            }
        }

        /// <inheritdoc />
        public override IAbilityPipelinePhase<TCtx> CreateRunPhase()
        {
            var phase = new AbilityConditionalPhase<TCtx>(PhaseId)
            {
                NoConditionBehavior = NoConditionBehavior
            };

            for (int i = 0; i < _branches.Count; i++)
            {
                var branch = _branches[i];
                phase.AddBranch(branch.Condition, AbilityPipelinePhaseRuntime.CreateRunPhase(branch.Phase));
            }

            return phase;
        }

        /// <inheritdoc />
        public override void Reset()
        {
            base.Reset();
            _currentBranch = null;
            _currentBranchIndex = -1;
            ResetBranchResults();
        }

        private void ResetBranchResults()
        {
            for (int i = 0; i < _branchResults.Count; i++)
            {
                _branchResults[i] = EPipelineDebugConditionResult.Unknown;
            }
        }
    }
}
