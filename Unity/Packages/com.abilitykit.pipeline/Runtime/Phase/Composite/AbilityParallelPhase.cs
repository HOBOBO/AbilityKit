using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 并行阶段：同时执行所有子阶段，全部完成后阶段完成。
    /// </summary>
    /// <typeparam name="TCtx">管线上下文类型。</typeparam>
    public class AbilityParallelPhase<TCtx> : AbilityCompositePhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        private readonly List<int> _activePhases = new List<int>(8);
        private int _debugExecutingPhaseIndex = -1;

        internal bool DebugIsChildActive(int index)
        {
            return index == _debugExecutingPhaseIndex || _activePhases.Contains(index);
        }

        /// <summary>
        /// 使用指定阶段 ID 创建并行阶段。
        /// </summary>
        public AbilityParallelPhase(AbilityPipelinePhaseId phaseId) : base(phaseId)
        {
        }
    
        /// <inheritdoc />
        public override void Execute(TCtx context)
        {
            IsComplete = false;
            _activePhases.Clear();
            
            for (int i = 0; i < _subPhases.Count; i++)
            {
                if (_subPhases[i].ShouldExecute(context))
                {
                    try
                    {
                        _debugExecutingPhaseIndex = i;
                        _subPhases[i].Execute(context);
                    }
                    finally
                    {
                        _debugExecutingPhaseIndex = -1;
                    }
                    
                    if (!_subPhases[i].IsComplete)
                    {
                        _activePhases.Add(i);
                    }
                }
            }
            
            if (_activePhases.Count == 0)
            {
                OnAllSubPhasesComplete(context);
            }
        }

        /// <inheritdoc />
        public override void OnUpdate(TCtx context, float deltaTime)
        {
            if (IsComplete)
                return;

            for (int i = _activePhases.Count - 1; i >= 0; i--)
            {
                int phaseIndex = _activePhases[i];
                var phase = _subPhases[phaseIndex];
                
                phase.OnUpdate(context, deltaTime);
                
                if (phase.IsComplete)
                {
                    _activePhases.RemoveAt(i);
                }
            }
        
            if (_activePhases.Count == 0)
            {
                OnAllSubPhasesComplete(context);
            }
        }

        /// <inheritdoc />
        public override IAbilityPipelinePhase<TCtx> CreateRunPhase()
        {
            var phase = new AbilityParallelPhase<TCtx>(PhaseId);
            CopySubPhasesTo(phase);
            return phase;
        }

        /// <inheritdoc />
        public override void Reset()
        {
            base.Reset();
            _activePhases.Clear();
            _debugExecutingPhaseIndex = -1;
        }
    }
}
