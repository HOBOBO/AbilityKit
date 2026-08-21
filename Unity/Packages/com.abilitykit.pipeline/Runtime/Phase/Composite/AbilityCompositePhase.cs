using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 复合阶段基类，负责按子阶段集合推进阶段执行。
    /// </summary>
    public abstract class AbilityCompositePhase<TCtx> : IAbilityPipelinePhase<TCtx>, IAbilityPipelinePhaseInstanceFactory<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 阶段 ID。
        /// </summary>
        public AbilityPipelinePhaseId PhaseId { get; protected set; }

        /// <summary>
        /// 始终表示复合阶段。
        /// </summary>
        public bool IsComposite => true;

        /// <summary>
        /// 阶段是否已完成。
        /// </summary>
        public bool IsComplete { get; protected set; }
        
        /// <summary>
        /// 当前正在执行的子阶段索引。
        /// </summary>
        protected int _currentSubPhaseIndex = 0;

        internal int DebugCurrentSubPhaseIndex => _currentSubPhaseIndex;

        /// <summary>
        /// 子阶段列表。
        /// </summary>
        protected List<IAbilityPipelinePhase<TCtx>> _subPhases = new List<IAbilityPipelinePhase<TCtx>>(4);

        /// <summary>
        /// 子阶段只读集合。
        /// </summary>
        public IReadOnlyList<IAbilityPipelinePhase<TCtx>> SubPhases => _subPhases;

        /// <summary>
        /// 使用指定阶段 ID 创建复合阶段。
        /// </summary>
        protected AbilityCompositePhase(AbilityPipelinePhaseId phaseId)
        {
            PhaseId = phaseId;
        }

        /// <summary>
        /// 执行复合阶段并推进第一个可执行子阶段。
        /// </summary>
        public virtual void Execute(TCtx context)
        {
            IsComplete = false;
            _currentSubPhaseIndex = 0;
            ExecuteNextSubPhase(context);
        }

        /// <summary>
        /// 每帧更新当前子阶段。
        /// </summary>
        public virtual void OnUpdate(TCtx context, float deltaTime)
        {
            if (IsComplete || _currentSubPhaseIndex >= _subPhases.Count)
                return;

            var currentPhase = _subPhases[_currentSubPhaseIndex];
            currentPhase.OnUpdate(context, deltaTime);
            
            if (currentPhase.IsComplete)
            {
                _currentSubPhaseIndex++;
                ExecuteNextSubPhase(context);
            }
        }

        /// <summary>
        /// 执行下一个满足条件的子阶段。
        /// </summary>
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
            
                subPhase.Execute(context);
            
                if (!subPhase.IsComplete)
                {
                    return;
                }
            
                _currentSubPhaseIndex++;
            }
        
            OnAllSubPhasesComplete(context);
        }
    
        /// <summary>
        /// 所有子阶段完成时调用。
        /// </summary>
        protected virtual void OnAllSubPhasesComplete(TCtx context)
        {
            IsComplete = true;
        }

        /// <summary>
        /// 将错误转交给当前子阶段处理。
        /// </summary>
        public virtual void HandleError(TCtx context, Exception exception)
        {
            if (_currentSubPhaseIndex < _subPhases.Count)
            {
                _subPhases[_currentSubPhaseIndex].HandleError(context, exception);
            }
        }
    
        /// <summary>
        /// 添加子阶段。
        /// </summary>
        public void AddSubPhase(IAbilityPipelinePhase<TCtx> phase)
        {
            _subPhases.Add(phase);
        }

        /// <summary>
        /// 将当前子阶段定义复制到新的复合阶段运行实例。
        /// </summary>
        protected void CopySubPhasesTo(AbilityCompositePhase<TCtx> target)
        {
            for (int i = 0; i < _subPhases.Count; i++)
            {
                target.AddSubPhase(AbilityPipelinePhaseRuntime.CreateRunPhase(_subPhases[i]));
            }
        }

        /// <summary>
        /// 创建单次运行使用的复合阶段实例。
        /// </summary>
        public abstract IAbilityPipelinePhase<TCtx> CreateRunPhase();

        /// <summary>
        /// 判断复合阶段是否应执行。
        /// </summary>
        public virtual bool ShouldExecute(TCtx context)
        {
            return true;
        }

        /// <summary>
        /// 重置复合阶段及所有子阶段状态。
        /// </summary>
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
