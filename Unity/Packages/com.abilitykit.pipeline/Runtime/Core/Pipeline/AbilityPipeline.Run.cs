using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Pipeline.Pooling;

namespace AbilityKit.Pipeline
{
    public abstract partial class AbilityPipeline<TCtx>
        where TCtx : IAbilityPipelineContext
    {
        /// <summary>
        /// 保存单次管线执行的位置、阶段与生命周期。
        /// </summary>
        private sealed partial class Run : IAbilityPipelineRun<TCtx>, IPipelineLifeOwner, IPipelineDebugStateProvider
        {
            private readonly AbilityPipeline<TCtx> _owner;
            private readonly PipelineRuntime _runtime;
            private readonly IPipelineRegistry _registry;
            private readonly IAbilityPipelineConfig _config;
            private readonly List<IAbilityPipelinePhase<TCtx>> _phases;
            private readonly int _ownerId;

            private bool _isCancelled;
            private int _currentPhaseIndex;
            private IAbilityPipelinePhase<TCtx>? _currentPhase;
            private IAbilityPipelinePhase<TCtx>? _failedPhase;
            private PipelineDebugRunState? _terminalDebugState;

            public EAbilityPipelineState State { get; private set; }

            public TCtx Context { get; }

            public AbilityPipelinePhaseId CurrentPhaseId => Context.CurrentPhaseId;

            public bool IsPaused { get; private set; }
            public IReadOnlyList<IAbilityPipelinePhase<TCtx>> Phases => _phases;
            public int CurrentPhaseIndex => _currentPhaseIndex;
            public bool HasCurrentPhase => _currentPhase != null;
            public bool IsCancelled => _isCancelled;


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

            int IPipelineLifeOwner.OwnerId => _ownerId;
            string IPipelineLifeOwner.OwnerName => _owner.GetType().Name + "#" + _ownerId;

            public Run(AbilityPipeline<TCtx> owner, PipelineRuntime runtime, IAbilityPipelineConfig config, TCtx context,
                List<IAbilityPipelinePhase<TCtx>> phases, Action<IAbilityPipelineRun<TCtx>>? initialize = null)
            {
                _owner = owner;
                _runtime = runtime;
                _config = config;
                Context = context;
                _phases = phases;
                _ownerId = PipelineRunIdGenerator.Next();

                State = EAbilityPipelineState.Executing;
                IsPaused = false;
                _currentPhaseIndex = 0;
                _currentPhase = null;

                Context.PipelineState = EAbilityPipelineState.Executing;

                if (initialize != null) initialize(this);
                else _owner.Events?.OnPipelineStart?.Invoke(Context);

                /*
                 * 启动回调仍可选择注册表，登记后则固定本次运行的清理归属。
                 * 启动失败只回收框架资源，不代替调用方释放业务上下文。
                 */
                _registry = _runtime.Registry;
                try
                {
                    _registry.Register(this);
                    PipelineDebugHooks.NotifyRunStarted<TCtx>(this, _owner, _config, this);
                    if (initialize == null)
                        _owner.Events?.RecordTrace(_runtime, this, EPipelineTraceEventType.RunStart, default, State, string.Empty);
                }
                catch
                {
                    State = EAbilityPipelineState.Failed;
                    try { _registry.Unregister(this); }
                    catch { }
                    PipelineDebugHooks.NotifyRunEnded(this);
                    throw;
                }
            }

            public void SetExecutionPosition(int phaseIndex, bool hasCurrentPhase, bool isPaused, bool isCancelled)
            {
                if (State != EAbilityPipelineState.Executing)
                    throw new InvalidOperationException("The run is no longer executing.");
                if (phaseIndex < 0 || phaseIndex >= _phases.Count)
                    throw new ArgumentOutOfRangeException(nameof(phaseIndex));
                _currentPhaseIndex = phaseIndex;
                _currentPhase = hasCurrentPhase ? _phases[phaseIndex] : null;
                _isCancelled = isCancelled;
                IsPaused = isPaused;
                Context.PipelineState = EAbilityPipelineState.Executing;
                Context.CurrentPhaseId = _currentPhase?.PhaseId ?? default;
                Context.IsPaused = isPaused;
            }

            public void Release()
            {
                if (State != EAbilityPipelineState.Executing) return;
                State = EAbilityPipelineState.Failed;
                try
                {
                    _registry.Unregister(this);
                }
                finally
                {
                    PipelineDebugHooks.NotifyRunEnded(this);
                    PipelinePools.ReleaseRunPhaseList(_phases);
                }
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

                var phaseHandledInterrupt = _currentPhase is IInterruptiblePhase<TCtx>;
                if (_currentPhase is IInterruptiblePhase<TCtx> interruptible)
                {
                    interruptible.OnInterrupt(Context);
                }

                /*
                 * 由可中断组合阶段停止其活动子阶段。
                 * 不再次遍历子阶段，避免重复清理。
                 */
                if (!phaseHandledInterrupt) InterruptSubPhases(_currentPhase);

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
                _failedPhase = phase;
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
                _owner.Events?.RecordTracePhase(
                    _runtime,
                    this,
                    EPipelineTraceEventType.PhaseError,
                    phase?.PhaseId ?? default,
                    failure.Message,
                    State);
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
                _terminalDebugState = CaptureDebugStateCore();
                PipelineDebugHooks.NotifyRunEnded(this);
                try
                {
                    _owner.ReleaseContext(Context);
                }
                catch
                {
                }
                finally
                {
                    try { _registry.Unregister(this); }
                    finally { PipelinePools.ReleaseRunPhaseList(_phases); }
                }
            }
        }
    }
}
