using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 复合阶段基类
    /// </summary>
    public abstract class AbilityCompositePhase<TCtx> : IAbilityPipelinePhase<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        public AbilityPipelinePhaseId PhaseId { get; protected set; }
        public bool IsComposite => true;
        public bool IsComplete { get; protected set; }
        
        protected int _currentSubPhaseIndex = 0;
        protected List<IAbilityPipelinePhase<TCtx>> _subPhases = new List<IAbilityPipelinePhase<TCtx>>(4);
        public IReadOnlyList<IAbilityPipelinePhase<TCtx>> SubPhases => _subPhases;

        protected AbilityCompositePhase(AbilityPipelinePhaseId phaseId)
        {
            PhaseId = phaseId;
        }

        public virtual void Execute(TCtx context)
        {
            IsComplete = false;
            _currentSubPhaseIndex = 0;
            ExecuteNextSubPhase(context);
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public virtual void OnUpdate(TCtx context, float deltaTime)
        {
            if (IsComplete || _currentSubPhaseIndex >= _subPhases.Count)
                return;

            var currentPhase = _subPhases[_currentSubPhaseIndex];
            
            // 更新当前子阶段
            currentPhase.OnUpdate(context, deltaTime);
            
            // 检查当前子阶段是否完成
            if (currentPhase.IsComplete)
            {
                _currentSubPhaseIndex++;
                ExecuteNextSubPhase(context);
            }
        }

        protected virtual void ExecuteNextSubPhase(TCtx context)
        {
            while (_currentSubPhaseIndex < _subPhases.Count)
            {
                var subPhase = _subPhases[_currentSubPhaseIndex];
            
                if (!subPhase.ShouldExecute(context))
                {
                    _currentSubPhaseIndex++;
                    continue;
                }
            
                // 执行子阶段
                subPhase.Execute(context);
            
                // 如果子阶段未完成，等待 OnUpdate 驱动
                if (!subPhase.IsComplete)
                {
                    return;
                }
            
                _currentSubPhaseIndex++;
            }
        
            // 所有子阶段执行完成
            OnAllSubPhasesComplete(context);
        }
    
        protected virtual void OnAllSubPhasesComplete(TCtx context)
        {
            IsComplete = true;
        }

        public virtual void HandleError(TCtx context, Exception exception)
        {
            if (_currentSubPhaseIndex < _subPhases.Count)
            {
                _subPhases[_currentSubPhaseIndex].HandleError(context, exception);
            }
        }
    
        public void AddSubPhase(IAbilityPipelinePhase<TCtx> phase)
        {
            _subPhases.Add(phase);
        }

        public virtual bool ShouldExecute(TCtx context)
        {
            return true;
        }

        public virtual void Reset()
        {
            IsComplete = false;
            _currentSubPhaseIndex = 0;
            for (int i = 0; i < _subPhases.Count; i++)
            {
                _subPhases[i].Reset();
            }
        }
    }
}
