using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Pipeline.Pooling;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 抽象核心管线流程。
    /// </summary>
    public abstract partial class AbilityPipeline<TCtx> : IAbilityPipeline<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 管线事件集合。
        /// </summary>
        public AbilityPipelineEvents<TCtx> Events { get; } = new AbilityPipelineEvents<TCtx>();

        /// <summary>
        /// 当前管线使用的运行时上下文。
        /// </summary>
        public PipelineRuntime Runtime { get; set; } = Pipeline.DefaultRuntime;

        private readonly List<IAbilityPipelinePhase<TCtx>> _phases = new List<IAbilityPipelinePhase<TCtx>>(8);

        /// <summary>
        /// 启动一次管线运行。
        /// </summary>
        public IAbilityPipelineRun<TCtx> Start(IAbilityPipelineConfig config, TCtx context)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (context == null) throw new ArgumentNullException(nameof(context));

            var runPhases = AbilityPipelinePhaseRuntime.CreateRunPhases(_phases);
            return new Run(this, Runtime ?? Pipeline.DefaultRuntime, config, context, runPhases);
        }

        /// <summary>
        /// 重置管线内所有阶段运行态。
        /// </summary>
        public virtual void Reset()
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].Reset();
            }
        }

        /// <summary>
        /// 将阶段追加到管线末尾。
        /// </summary>
        public void AddPhase(IAbilityPipelinePhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Add(phase);
        }

        /// <summary>
        /// 将阶段插入到指定索引位置。
        /// </summary>
        public void InsertPhase(int index, IAbilityPipelinePhase<TCtx> phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _phases.Insert(index, phase);
        }

        /// <summary>
        /// 按阶段 ID 移除第一个匹配阶段。
        /// </summary>
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
        /// 释放管线运行绑定的上下文。
        /// </summary>
        protected abstract void ReleaseContext(TCtx context);

        private sealed class Run : IAbilityPipelineRun<TCtx>, IPipelineLifeOwner
        {
            private readonly AbilityPipeline<TCtx> _owner;
            private readonly PipelineRuntime _runtime;
            private readonly IAbilityPipelineConfig _config;
            private readonly List<IAbilityPipelinePhase<TCtx>> _phases;

            private bool _isCancelled;
            private int _currentPhaseIndex;
            private IAbilityPipelinePhase<TCtx>? _currentPhase;

            public EAbilityPipelineState State { get; private set; }

            public TCtx Context { get; }

            public AbilityPipelinePhaseId CurrentPhaseId => Context.CurrentPhaseId;

            public bool IsPaused { get; private set; }

            private readonly AbilityPipelinePhaseId[] _activePhaseIds = new AbilityPipelinePhaseId[1];

            IReadOnlyList<AbilityPipelinePhaseId> IPipelineLifeOwner.ActivePhases
            {
                get
                {
                    if (_currentPhase == null)
                    {
                        return Array.Empty<AbilityPipelinePhaseId>();
                    }

                    _activePhaseIds[0] = _currentPhase.PhaseId;
                    return _activePhaseIds;
                }
            }

            // 管线生命周期拥有者接口实现
            int IPipelineLifeOwner.OwnerId => GetHashCode();
            string IPipelineLifeOwner.OwnerName => _owner.GetType().Name + "#" + GetHashCode();

            public Run(AbilityPipeline<TCtx> owner, PipelineRuntime runtime, IAbilityPipelineConfig config, TCtx context, List<IAbilityPipelinePhase<TCtx>> phases)
            {
                _owner = owner;
                _runtime = runtime;
                _config = config;
                Context = context;
                _phases = phases;

                State = EAbilityPipelineState.Executing;
                IsPaused = false;
                _currentPhaseIndex = 0;
                _currentPhase = null;

                Context.PipelineState = EAbilityPipelineState.Executing;

                _owner.Events?.OnPipelineStart?.Invoke(Context);

                _runtime.Registry.Register(this);
                PipelineDebugHooks.NotifyRunStarted<TCtx>(this, _owner, _config, this);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.RunStart, default, State, string.Empty);
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
                if (Context.IsAborted)
                {
                    Fail();
                    return;
                }

                try
                {
                    if (_currentPhase != null)
                    {
                        _currentPhase.OnUpdate(Context, deltaTime);
                        if (Context.IsAborted)
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

                    if (Context.IsAborted)
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
                Context.IsPaused = true;
                _owner.Events?.OnPipelinePause?.Invoke(Context);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.Pause, CurrentPhaseId, State, string.Empty);
            }

            public void Resume()
            {
                if (State != EAbilityPipelineState.Executing) return;
                if (!IsPaused) return;
                IsPaused = false;
                Context.IsPaused = false;
                _owner.Events?.OnPipelineResume?.Invoke(Context);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.Resume, CurrentPhaseId, State, string.Empty);
            }

            public void Interrupt()
            {
                if (State != EAbilityPipelineState.Executing) return;

                if (_currentPhase is IInterruptiblePhase<TCtx> interruptible)
                {
                    interruptible.OnInterrupt(Context);
                }

                InterruptSubPhases(_currentPhase);

                Context.IsAborted = true;
                _owner.Events?.OnPipelineInterrupt?.Invoke(Context, true);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.Interrupt, CurrentPhaseId, State, string.Empty);
                Fail();
            }

            public void Cancel()
            {
                _isCancelled = true;
            }

            private void ExecutePipeline()
            {
                while (_currentPhaseIndex < _phases.Count && State == EAbilityPipelineState.Executing)
                {
                    if (Context.IsAborted)
                    {
                        Fail();
                        return;
                    }

                    var phase = _phases[_currentPhaseIndex];

                    if (!phase.ShouldExecute(Context))
                    {
                        _currentPhaseIndex++;
                        continue;
                    }

                    try
                    {
                        ExecutePhase(phase);

                        if (Context.IsAborted)
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

                if (_currentPhaseIndex >= _phases.Count)
                {
                    Complete();
                }
            }

            private void ExecutePhase(IAbilityPipelinePhase<TCtx> phase)
            {
                OnPhaseStart(phase);
                phase.Execute(Context);
            }

            private void OnPhaseStart(IAbilityPipelinePhase<TCtx> phase)
            {
                Context.CurrentPhaseId = phase.PhaseId;
                _owner.ExecuteExtensionPhaseStart(phase.PhaseId, Context, phase);
                _owner.Events?.OnPhaseStart?.Invoke(phase, Context);
                _owner.Events?.RecordTracePhase(_runtime, this, EPipelineTraceEventType.PhaseStart, phase.PhaseId, phase.GetType().Name, State);
            }

            private void OnPhaseComplete(IAbilityPipelinePhase<TCtx> phase)
            {
                _owner.ExecuteExtensionPhaseComplete(phase.PhaseId, Context, phase);
                _owner.Events?.OnPhaseComplete?.Invoke(phase, Context);
                _owner.Events?.RecordTracePhase(_runtime, this, EPipelineTraceEventType.PhaseComplete, phase.PhaseId, phase.GetType().Name, State);
            }

            private void HandlePhaseError(IAbilityPipelinePhase<TCtx>? phase, Exception e)
            {
                if (State != EAbilityPipelineState.Executing) return;
                State = EAbilityPipelineState.Failed;
                Context.PipelineState = EAbilityPipelineState.Failed;
                Exception failure = e;

                if (phase != null)
                {
                    try { phase.HandleError(Context, e); }
                    catch (Exception handlerException)
                    {
                        failure = new AggregateException(
                            "The phase and its error handler both failed.",
                            e,
                            handlerException);
                    }
                    _owner.Events?.OnPhaseError?.Invoke(phase, Context, failure);
                }

                TrySetContextFailReason(phase, failure);
                _owner.Events?.OnPipelineError?.Invoke(Context, failure);
                _owner.Events?.OnPipelineFailed?.Invoke(Context, failure);
                _owner.Events?.RecordTracePhase(_runtime, this, EPipelineTraceEventType.PhaseError, phase?.PhaseId ?? default, phase?.GetType().Name ?? string.Empty, State);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.RunEnd, CurrentPhaseId, State, failure.Message);

                Cleanup();
            }

            private void TrySetContextFailReason(IAbilityPipelinePhase<TCtx>? phase, Exception e)
            {
                if (Context == null || e == null) return;

                try
                {
                    var phaseName = phase?.GetType().Name ?? "<unknown-phase>";
                    var phaseId = phase?.PhaseId.ToString() ?? "<unknown-phase-id>";
                    var message = $"{phaseName} failed (phaseId={phaseId}): {e.GetType().Name}: {e.Message}";
                    Context.SetData("FailReason", message);

                    var failReasonProperty = Context.GetType().GetProperty("FailReason");
                    if (failReasonProperty != null && failReasonProperty.CanWrite && failReasonProperty.PropertyType == typeof(string))
                    {
                        failReasonProperty.SetValue(Context, message);
                    }
                }
                catch (Exception cleanupException)
                {
                    Log.Exception(cleanupException, $"Pipeline context release failed. pipeline={_owner.GetType().Name} state={State}");
                }
            }

            private void Complete()
            {
                if (State != EAbilityPipelineState.Executing) return;
                State = EAbilityPipelineState.Completed;
                Context.PipelineState = EAbilityPipelineState.Completed;
                _owner.Events?.OnPipelineComplete?.Invoke(Context);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.RunEnd, CurrentPhaseId, State, "Completed");

                Cleanup();
            }

            private void Fail()
            {
                if (State != EAbilityPipelineState.Executing) return;
                State = EAbilityPipelineState.Failed;
                Context.PipelineState = EAbilityPipelineState.Failed;

                var failure = CreatePipelineFailureException();
                _owner.Events?.OnPipelineError?.Invoke(Context, null);
                _owner.Events?.OnPipelineFailed?.Invoke(Context, failure);
                _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.RunEnd, CurrentPhaseId, State, failure.Message);

                Cleanup();
            }

            private Exception CreatePipelineFailureException()
            {
                if (Context != null && Context.TryGetData<string>("FailReason", out var failReason) && !string.IsNullOrEmpty(failReason))
                {
                    return new InvalidOperationException(failReason);
                }

                var phaseId = CurrentPhaseId.ToString();
                return new InvalidOperationException($"Pipeline failed without exception (state={State}, phaseId={phaseId}).");
            }

            private void InterruptSubPhases(IAbilityPipelinePhase<TCtx>? phase)
            {
                if (phase == null) return;

                var subPhases = phase.SubPhases;
                for (int i = 0; i < subPhases.Count; i++)
                {
                    if (subPhases[i] is IInterruptiblePhase<TCtx> interruptible)
                    {
                        interruptible.OnInterrupt(Context);
                    }
                }
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
                    _runtime.Registry.Unregister(this);
                    PipelinePools.ReleaseRunPhaseList(_phases);
                }
            }
        }
    }
}
