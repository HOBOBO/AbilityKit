using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 抽象核心管线流程
    /// </summary>
    public abstract partial class AbilityPipeline<TCtx> : IAbilityPipeline<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 管线事件
        /// </summary>
        public AbilityPipelineEvents<TCtx> Events { get; } = new AbilityPipelineEvents<TCtx>();

        private readonly List<IAbilityPipelinePhase<TCtx>> _phases = new List<IAbilityPipelinePhase<TCtx>>(8);

        public IAbilityPipelineRun<TCtx> Start(IAbilityPipelineConfig config, TCtx context)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));

            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].Reset();
            }

            return new Run(this, config, context);
        }

        /// <summary>
        /// 重置管线
        /// </summary>
        public virtual void Reset()
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].Reset();
            }
        }

        public void AddPhase(IAbilityPipelinePhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Add(phase);
        }

        public void InsertPhase(int index, IAbilityPipelinePhase<TCtx> phase)
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

        protected abstract void ReleaseContext(TCtx context);

        private sealed class Run : IAbilityPipelineRun<TCtx>, IPipelineLifeOwner
        {
            private readonly AbilityPipeline<TCtx> _owner;
            private readonly IAbilityPipelineConfig _config;

            private bool _isCancelled;
            private int _currentPhaseIndex;
            private IAbilityPipelinePhase<TCtx> _currentPhase;

            public EAbilityPipelineState State { get; private set; }

            public TCtx Context { get; }

            public AbilityPipelinePhaseId CurrentPhaseId => Context != null ? Context.CurrentPhaseId : default;

            public bool IsPaused { get; private set; }

            IReadOnlyList<AbilityPipelinePhaseId> IPipelineLifeOwner.ActivePhases
            {
                get
                {
                    if (_currentPhase != null)
                    {
                        return new List<AbilityPipelinePhaseId> { _currentPhase.PhaseId }.AsReadOnly();
                    }
                    return new List<AbilityPipelinePhaseId>().AsReadOnly();
                }
            }

            // IPipelineLifeOwner 实现
            int IPipelineLifeOwner.OwnerId => GetHashCode();
            string IPipelineLifeOwner.OwnerName => _owner.GetType().Name + "#" + GetHashCode();

            public Run(AbilityPipeline<TCtx> owner, IAbilityPipelineConfig config, TCtx context)
            {
                _owner = owner;
                _config = config;
                Context = context;

                State = EAbilityPipelineState.Executing;
                IsPaused = false;
                _currentPhaseIndex = 0;
                _currentPhase = null;
                _owner._currentParallelPhase = null;

                Context.PipelineState = EAbilityPipelineState.Executing;

                _owner.Events?.OnPipelineStart?.Invoke(Context);

                // 注册到生命周期注册表
                Pipeline.Registry.Register(this);
                _owner.Events?.RecordTrace(this, EPipelineTraceEventType.RunStart, default, State, string.Empty);
            }

            public void Tick(float deltaTime)
            {
                if (State != EAbilityPipelineState.Executing) return;
                if (_isCancelled)
                {
                    Fail();
                    return;
                }
                if (IsPaused) return;
                if (Context != null && Context.IsAborted)
                {
                    Fail();
                    return;
                }

                try
                {
                    if (_currentPhase != null)
                    {
                        _currentPhase.OnUpdate(Context, deltaTime);
                        if (_currentPhase.IsComposite)
                        {
                            _owner.OnCompositeUpdate(Context, deltaTime);
                        }

                        if (Context != null && Context.IsAborted)
                        {
                            Fail();
                            return;
                        }

                        if (!_currentPhase.IsComplete)
                        {
                            return;
                        }

                        OnPhaseComplete(_currentPhase);
                        _currentPhase = null;
                        _currentPhaseIndex++;
                    }

                    ExecutePipeline();

                    if (Context != null && Context.IsAborted)
                    {
                        Fail();
                    }
                }
                catch (Exception e)
                {
                    HandlePhaseError(_currentPhase, e);
                }

                _owner.Events?.OnTick?.Invoke(Context, deltaTime, State);
            }

            public void Pause()
            {
                if (State != EAbilityPipelineState.Executing) return;
                if (IsPaused) return;
                IsPaused = true;
                if (Context != null) Context.IsPaused = true;
                _owner.Events?.OnPipelinePause?.Invoke(Context);
                _owner.Events?.RecordTrace(this, EPipelineTraceEventType.Pause, CurrentPhaseId, State, string.Empty);
            }

            public void Resume()
            {
                if (State != EAbilityPipelineState.Executing) return;
                if (!IsPaused) return;
                IsPaused = false;
                if (Context != null) Context.IsPaused = false;
                _owner.Events?.OnPipelineResume?.Invoke(Context);
                _owner.Events?.RecordTrace(this, EPipelineTraceEventType.Resume, CurrentPhaseId, State, string.Empty);
            }

            public void Interrupt()
            {
                if (State != EAbilityPipelineState.Executing) return;

                if (_currentPhase is IInterruptiblePhase<TCtx> interruptible)
                {
                    interruptible.OnInterrupt(Context);
                }

                if (_owner._currentParallelPhase != null)
                {
                    var subPhases = _owner._currentParallelPhase.SubPhases;
                    for (int i = 0; i < subPhases.Count; i++)
                    {
                        if (subPhases[i] is IInterruptiblePhase<TCtx> subInterruptible)
                        {
                            subInterruptible.OnInterrupt(Context);
                        }
                    }
                }

                if (Context != null) Context.IsAborted = true;
                _owner.Events?.OnPipelineInterrupt?.Invoke(Context, true);
                _owner.Events?.RecordTrace(this, EPipelineTraceEventType.Interrupt, CurrentPhaseId, State, string.Empty);
                Fail();
            }

            public void Cancel()
            {
                _isCancelled = true;
            }

            private void ExecutePipeline()
            {
                while (_currentPhaseIndex < _owner._phases.Count && State == EAbilityPipelineState.Executing)
                {
                    if (Context != null && Context.IsAborted)
                    {
                        Fail();
                        return;
                    }

                    var phase = _owner._phases[_currentPhaseIndex];

                    if (!phase.ShouldExecute(Context))
                    {
                        _currentPhaseIndex++;
                        continue;
                    }

                    try
                    {
                        ExecutePhase(phase);

                        if (Context != null && Context.IsAborted)
                        {
                            Fail();
                            return;
                        }

                        if (!phase.IsComplete)
                        {
                            _currentPhase = phase;
                            return;
                        }

                        OnPhaseComplete(phase);
                        _currentPhaseIndex++;
                    }
                    catch (Exception e)
                    {
                        HandlePhaseError(phase, e);
                        return;
                    }
                }

                if (_currentPhaseIndex >= _owner._phases.Count)
                {
                    Complete();
                }
            }

            private void ExecutePhase(IAbilityPipelinePhase<TCtx> phase)
            {
                OnPhaseStart(phase);

                if (phase.IsComposite)
                {
                    _owner.HandleCompositePhase(phase as AbilityCompositePhase<TCtx>, Context);
                }
                else
                {
                    phase.Execute(Context);
                }
            }

            private void OnPhaseStart(IAbilityPipelinePhase<TCtx> phase)
            {
                if (Context != null) Context.CurrentPhaseId = phase.PhaseId;
                _owner.ExecuteExtensionPhaseStart(phase.PhaseId, Context, phase);
                _owner.Events?.OnPhaseStart?.Invoke(phase, Context);
                _owner.Events?.RecordTracePhase(this, EPipelineTraceEventType.PhaseStart, phase.PhaseId, phase.GetType().Name, State);
            }

            private void OnPhaseComplete(IAbilityPipelinePhase<TCtx> phase)
            {
                _owner.ExecuteExtensionPhaseComplete(phase.PhaseId, Context, phase);
                _owner.Events?.OnPhaseComplete?.Invoke(phase, Context);
                _owner.Events?.RecordTracePhase(this, EPipelineTraceEventType.PhaseComplete, phase.PhaseId, phase.GetType().Name, State);
            }

            private void HandlePhaseError(IAbilityPipelinePhase<TCtx> phase, Exception e)
            {
                if (State != EAbilityPipelineState.Executing) return;
                State = EAbilityPipelineState.Failed;
                if (Context != null) Context.PipelineState = EAbilityPipelineState.Failed;

                if (phase != null)
                {
                    try { phase.HandleError(Context, e); }
                    catch { }
                }
                _owner.Events?.OnPhaseError?.Invoke(phase, Context, e);
                _owner.Events?.RecordTracePhase(this, EPipelineTraceEventType.PhaseError, phase?.PhaseId ?? default, phase?.GetType().Name ?? string.Empty, State);

                Cleanup();
            }

            private void Complete()
            {
                if (State != EAbilityPipelineState.Executing) return;
                State = EAbilityPipelineState.Completed;
                if (Context != null) Context.PipelineState = EAbilityPipelineState.Completed;
                _owner.Events?.OnPipelineComplete?.Invoke(Context);
                _owner.Events?.RecordTrace(this, EPipelineTraceEventType.RunEnd, CurrentPhaseId, State, "Completed");

                Cleanup();
            }

            private void Fail()
            {
                if (State != EAbilityPipelineState.Executing) return;
                State = EAbilityPipelineState.Failed;
                if (Context != null) Context.PipelineState = EAbilityPipelineState.Failed;
                _owner.Events?.RecordTrace(this, EPipelineTraceEventType.RunEnd, CurrentPhaseId, State, "Failed");

                Cleanup();
            }

            private void Cleanup()
            {
                try
                {
                    _owner.ReleaseContext(Context);
                }
                catch
                {
                }
                finally
                {
                    Pipeline.Registry.Unregister(this);
                }
            }
        }
    }
}
