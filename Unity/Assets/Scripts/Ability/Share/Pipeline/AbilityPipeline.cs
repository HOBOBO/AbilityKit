using System;
using System.Collections.Generic;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 抽象核心管线流程
    /// </summary>
    public abstract partial class AbilityPipeline : IAbilityPipeline
    {
        /// <summary>
        /// 管线事件
        /// </summary>
        public AbilityPipelineEvents Events { get; } = new AbilityPipelineEvents();
        
        /// <summary>
        /// 当前状态
        /// </summary>
        public EAbilityPipelineState State => _state;
        
        /// <summary>
        /// 当前上下文
        /// </summary>
        public IAbilityPipelineContext Context => _context;
        
        /// <summary>
        /// 是否暂停
        /// </summary>
        public bool IsPaused => _isPaused;
        
        protected EAbilityPipelineState _state = EAbilityPipelineState.Ready;
        protected bool _isPaused = false;
        protected int _currentPhaseIndex = 0;
        protected IAbilityPipelinePhase _currentPhase;
        
        /// <summary>
        /// 管线配置
        /// </summary>
        protected IAbilityPipelineConfig _config;
        
        /// <summary>
        /// 管线上下文
        /// </summary>
        protected IAbilityPipelineContext _context;
        
        private readonly List<IAbilityPipelinePhase> _phases = new List<IAbilityPipelinePhase>(8);

        /// <summary>
        /// 统一处理管线执行流程
        /// </summary>
        public virtual EAbilityPipelineState Execute(IAbilityPipelineConfig config, object abilityInstance, params object[] args)
        {
            try
            {
                // 初始化管线
                _config = config;
                _context = CreateContext(abilityInstance, args);
                _state = EAbilityPipelineState.Executing;
                _isPaused = false;
                _currentPhaseIndex = 0;
                _currentPhase = null;
                _currentParallelPhase = null;
                
                // 重置所有阶段状态
                for (int i = 0; i < _phases.Count; i++)
                {
                    _phases[i].Reset();
                }
            
                // 执行管线初始化
                OnPipelineStart();
            
                // 开始执行管线
                ExecutePipeline(_context);
            
                return _state;
            }
            catch (Exception e)
            {
                _state = EAbilityPipelineState.Failed;
                OnPipelineError(e);
                return _state;
            }
            finally
            {
                if (_state != EAbilityPipelineState.Executing)
                {
                    ReleaseContext(_context);
                }
            }
        }

        /// <summary>
        /// 暂停管线
        /// </summary>
        public virtual void Pause()
        {
            if (_state != EAbilityPipelineState.Executing || _isPaused)
                return;
                
            _isPaused = true;
            if (_context != null)
            {
                _context.IsPaused = true;
            }
            Events?.OnPipelinePause?.Invoke(_context);
        }

        /// <summary>
        /// 恢复管线
        /// </summary>
        public virtual void Resume()
        {
            if (_state != EAbilityPipelineState.Executing || !_isPaused)
                return;
                
            _isPaused = false;
            if (_context != null)
            {
                _context.IsPaused = false;
            }
            Events?.OnPipelineResume?.Invoke(_context);
        }

        /// <summary>
        /// 中断管线
        /// </summary>
        public virtual void Interrupt()
        {
            if (_state != EAbilityPipelineState.Executing)
                return;

            // 中断当前阶段
            if (_currentPhase is IInterruptiblePhase interruptible)
            {
                interruptible.OnInterrupt(_context);
            }

            // 中断并行阶段的子阶段
            if (_currentParallelPhase != null)
            {
                var subPhases = _currentParallelPhase.SubPhases;
                for (int i = 0; i < subPhases.Count; i++)
                {
                    var phase = subPhases[i];
                    if (phase is IInterruptiblePhase subInterruptible)
                    {
                        subInterruptible.OnInterrupt(_context);
                    }
                }
            }

            _context.IsAborted = true;
            _state = EAbilityPipelineState.Failed;
            OnPipelineInterrupt(true);
            ReleaseContext(_context);
        }

        /// <summary>
        /// 重置管线
        /// </summary>
        public virtual void Reset()
        {
            _state = EAbilityPipelineState.Ready;
            _isPaused = false;
            _currentPhaseIndex = 0;
            _currentPhase = null;
            _currentParallelPhase = null;
            _config = null;
            
            // 重置所有阶段
            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].Reset();
            }
            
            if (_context != null)
            {
                ReleaseContext(_context);
                _context = null;
            }
        }
        
        protected abstract IAbilityPipelineContext CreateContext(object abilityInstance, params object[] args);

        /// <summary>
        /// 提供默认实现，子类可覆盖执行管线
        /// 统一模型：所有阶段都通过 IsComplete 判断是否完成
        /// </summary>
        protected virtual void ExecutePipeline(IAbilityPipelineContext context)
        {
            // 按顺序执行所有阶段
            while (_currentPhaseIndex < _phases.Count && _state == EAbilityPipelineState.Executing)
            {
                var phase = _phases[_currentPhaseIndex];
            
                // 检查阶段是否应该执行
                if (!phase.ShouldExecute(context))
                {
                    _currentPhaseIndex++;
                    continue;
                }

                try
                {
                    // 执行阶段
                    ExecutePhase(phase, context);
                    
                    // 如果阶段未完成，保存当前阶段并退出，等待 Update 驱动
                    if (!phase.IsComplete)
                    {
                        _currentPhase = phase;
                        return;
                    }
                    
                    // 阶段已完成，继续下一个
                    OnPhaseComplete(phase);
                    _currentPhaseIndex++;
                }
                catch (Exception e)
                {
                    HandlePhaseError(phase, e);
                    return;
                }
            }

            // 所有阶段执行完成
            if (_currentPhaseIndex >= _phases.Count)
            {
                OnPipelineComplete();
            }
        }
        
        /// <summary>
        /// 释放上下文
        /// </summary>
        /// <param name="context"></param>
        protected abstract void ReleaseContext(IAbilityPipelineContext context);


        /// <summary>
        /// 更新管线（每帧调用）
        /// 统一模型：所有阶段都通过 OnUpdate 更新，检查 IsComplete 完成
        /// </summary>
        public virtual void OnUpdate(IAbilityPipelineContext context, float deltaTime)
        {
            // 检查状态
            if (_state != EAbilityPipelineState.Executing)
                return;
            
            // 暂停时不更新
            if (_isPaused)
                return;
            
            // 没有当前阶段时不需要更新
            if (_currentPhase == null)
                return;

            try
            {
                // 更新当前阶段
                _currentPhase.OnUpdate(context, deltaTime);
                
                // 复合阶段更新（并行阶段等）
                if (_currentPhase.IsComposite)
                {
                    OnCompositeUpdate(context, deltaTime);
                }
                
                // 检查是否完成
                if (_currentPhase.IsComplete)
                {
                    OnPhaseComplete(_currentPhase);
                    _currentPhase = null;
                    _currentPhaseIndex++;
                
                    // 继续执行后续阶段
                    ExecutePipeline(context);
                }
            }
            catch (Exception e)
            {
                HandlePhaseError(_currentPhase, e);
            }
        }

        public void AddPhase(IAbilityPipelinePhase phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Add(phase);
        }

        public void InsertPhase(int index, IAbilityPipelinePhase phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Insert(index, phase);
        }

        public void RemovePhase(AbilityPipelinePhaseId phaseId)
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                if (_phases[i].PhaseId == phaseId)
                {
                    _phases.RemoveAt(i);
                    return;
                }
            }
        }
        
        /// <summary>
        /// 执行阶段（统一处理瞬时和持续阶段）
        /// </summary>
        protected virtual void ExecutePhase(IAbilityPipelinePhase phase, IAbilityPipelineContext context)
        {
            OnPhaseStart(phase);
        
            if (phase.IsComposite)
            {
                HandleCompositePhase(phase as AbilityCompositePhase, context);
            }
            else
            {
                phase.Execute(context);
                // 注意：不再在这里调用 OnPhaseComplete
                // 完成检查统一在 ExecutePipeline 和 OnUpdate 中进行
            }
        }

        #region 生命周期事件
        protected virtual void OnPipelineStart()
        {
            Events?.OnPipelineStart?.Invoke(_context);
        }

        protected virtual void OnPipelineComplete()
        {
            _state = EAbilityPipelineState.Completed;
            Events?.OnPipelineComplete?.Invoke(_context);
        }

        protected virtual void OnPipelineError(Exception e)
        {
            Events?.OnPipelineError?.Invoke(_context, e);
        }

        protected virtual void OnPipelineInterrupt(bool isInterrupt)
        {
            Events?.OnPipelineInterrupt?.Invoke(_context, isInterrupt);
        }

        protected virtual void OnPhaseStart(IAbilityPipelinePhase phase)
        {
            this.ExecuteExtensionPhaseStart(phase.PhaseId, _context, phase);
            Events?.OnPhaseStart?.Invoke(phase, _context);
        }

        protected virtual void OnPhaseComplete(IAbilityPipelinePhase phase)
        {
            this.ExecuteExtensionPhaseComplete(phase.PhaseId, _context, phase);
            Events?.OnPhaseComplete?.Invoke(phase, _context);
        }

        protected virtual void HandlePhaseError(IAbilityPipelinePhase phase, Exception e)
        {
            _state = EAbilityPipelineState.Failed;
            phase.HandleError(_context, e);
            Events?.OnPhaseError?.Invoke(phase, _context, e);
        }
        #endregion
    }
}