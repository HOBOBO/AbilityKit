using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Demo.Moba;
using AbilityKit.Core.Logging;
using AbilityKit.Effect;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Pipeline;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Observability;
using AbilityKit.Triggering.Collections;
using AbilityKit.Triggering.Blackboard;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public enum MobaEffectExecutionOutcome
    {
        Applied,
        Skipped,
        Failed,
    }

    [WorldService(typeof(MobaEffectExecutionService))]
    public sealed class MobaEffectExecutionService : IService, ITriggerActionExecutionScopeObserver
    {
        [WorldInject] private IWorldResolver _services = null;
        [WorldInject] private TriggerPlanJsonDatabase _planDb = null;
        [WorldInject] private AbilityKit.Triggering.Eventing.IEventBus _planEventBus = null;
        [WorldInject] private FunctionRegistry _planFunctions = null;
        [WorldInject] private ActionRegistry _planActions = null;
        [WorldInject(required: false)] private IPayloadAccessorRegistry _planPayloads = null;
        [WorldInject(required: false)] private IFrameTime _frameTime = null;
        [WorldInject(required: false)] private MobaSkillCastRuntimeService _skillRuntimes = null;
        [WorldInject(required: false)] private MobaTriggerPayloadResolverRegistry _payloadResolvers = null;
        [WorldInject(required: false)] private MobaTriggerConditionRegistry _triggerConditions = null;
        [WorldInject(required: false)] private IMobaBattleDiagnosticsService _diagnostics = null;
        [WorldInject(required: false)] private IMobaTriggerAnalysisHook _triggerAnalysisHook = null;
        [WorldInject(required: false)] private IMobaEffectLifecycleHook _effectLifecycleHook = null;
        [WorldInject(required: false)] private IMobaEffectExecutionEntryHook _effectExecutionHook = null;
        [WorldInject(required: false)] private IMobaActionExecutionHook _actionExecutionHook = null;
        [WorldInject(required: false)] private IBlackboardResolver _globalBlackboards = null;
        [WorldInject(required: false)] private IOwnerBlackboardStore _ownerBlackboards = null;

        private readonly MobaTriggerExecutionBudget _executionBudget = new MobaTriggerExecutionBudget();
        private MobaTriggerPlanExecutor _planExecutor;
 
        /// <summary>
        /// 溯源注册表。正式效果执行必须创建效果溯源作用域，避免动作来源、子对象来源和诊断链路缺少运行时节点。
        /// </summary>
        [WorldInject]
        private MobaExecutionContextRegistry ExecutionContexts { get; set; }

        /// <summary>
        /// 当前正在执行的 Context scope 栈（用于嵌套效果和 Action 父子关系）
        /// </summary>
        private readonly Stack<EffectExecutionScope> _executionScopes = new Stack<EffectExecutionScope>();
        private readonly Stack<CombatExecutionFrame> _executionContexts = new Stack<CombatExecutionFrame>();

        private sealed class CombatExecutionFrame
        {
            public CombatExecutionFrame(
                in MobaCombatExecutionContext context,
                ITriggerCollectionResolver collections,
                bool ownsCollections,
                IBlackboardResolver blackboards)
            {
                Context = context;
                Collections = collections;
                OwnsCollections = ownsCollections;
                Blackboards = blackboards;
            }

            public MobaCombatExecutionContext Context { get; private set; }
            public ITriggerCollectionResolver Collections { get; }
            public bool OwnsCollections { get; }
            public IBlackboardResolver Blackboards { get; }

            public void AdvanceToEffectExecution(
                long effectContextId,
                int effectConfigId,
                bool isRoot)
            {
                Context = Context.WithEffectExecutionNode(
                    effectContextId,
                    effectConfigId,
                    isRoot);
            }
        }

        /// <summary>
        /// 获取当前正在追踪的 Action 链路
        /// </summary>
        public IReadOnlyList<long> CurrentActionChain => _executionScopes.Count > 0 ? _executionScopes.Peek().ActionContextIds : Array.Empty<long>();

        public long CurrentEffectContextId => _executionScopes.Count > 0 ? _executionScopes.Peek().EffectContextId : 0;

        public bool TryGetCurrentExecutionContext(out MobaCombatExecutionContext context)
        {
            context = default;
            if (_executionContexts.Count == 0) return false;

            context = _executionContexts.Peek().Context;
            return context.HasExecutionSource;
        }

        internal bool TryGetCurrentTriggerCollections(out ITriggerCollectionResolver collections)
        {
            collections = _executionContexts.Count > 0
                ? _executionContexts.Peek().Collections
                : null;
            return collections != null;
        }

        internal bool TryGetCurrentExecutionBlackboards(out IBlackboardResolver blackboards)
        {
            blackboards = _executionContexts.Count > 0
                ? _executionContexts.Peek().Blackboards
                : null;
            return blackboards != null;
        }

        public bool TryGetCurrentExecutionScope(out MobaEffectExecutionScopeSnapshot snapshot)
        {
            snapshot = default;
            if (_executionScopes.Count == 0) return false;

            var scope = _executionScopes.Peek();
            if (scope.EffectContextId == 0) return false;

            snapshot = new MobaEffectExecutionScopeSnapshot(
                scope.EffectContextId,
                scope.EffectConfigId,
                scope.TriggerId,
                scope.SourceActorId,
                scope.TargetActorId,
                scope.IsRoot,
                scope.CurrentActionIndex,
                scope.CurrentActionContextId,
                scope.CurrentActionId);
            return true;
        }

        public void EnterActionExecution(int actionIndex, long actionId)
        {
            if (_executionScopes.Count == 0 || actionId == 0L) return;

            var scope = _executionScopes.Peek();
            if (scope.CurrentActionId != 0L)
            {
                throw new InvalidOperationException(
                    $"[MobaEffectExecutionService] Action execution scope is already active. effectContextId={scope.EffectContextId}, currentActionIndex={scope.CurrentActionIndex}, currentActionId={scope.CurrentActionId}, nextActionIndex={actionIndex}, nextActionId={actionId}.");
            }

            var actionConfigId = actionId >= int.MinValue && actionId <= int.MaxValue
                ? (int)actionId
                : 0;
            var actionNode = ExecutionContexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.EffectAction,
                actionConfigId,
                scope.SourceActorId,
                scope.TargetActorId,
                parentContextId: scope.EffectContextId,
                frame: _frameTime != null ? _frameTime.Frame.Value : 0,
                triggerId: scope.TriggerId));

            scope.CurrentActionIndex = actionIndex;
            scope.CurrentActionId = actionId;
            scope.CurrentActionContextId = actionNode.ContextId;
            if (actionNode.ContextId != 0L)
            {
                scope.ActionContextIds.Add(actionNode.ContextId);
            }
            var observation = new MobaActionExecutionObservation(
                MobaActionExecutionObservationStage.Started,
                actionNode.ContextId,
                actionIndex,
                actionId,
                scope.SourceActorId,
                scope.TargetActorId,
                _frameTime != null ? _frameTime.Frame.Value : -1);
            _actionExecutionHook.TryObserve(in observation);
            BeginActionDiagnostics(scope);
        }

        public void ExitActionExecution(int actionIndex, long actionId, bool succeeded)
        {
            if (_executionScopes.Count == 0) return;

            var scope = _executionScopes.Peek();
            if (scope.CurrentActionIndex != actionIndex || scope.CurrentActionId != actionId)
            {
                return;
            }

            var actionContextId = scope.CurrentActionContextId;
            CaptureActionEnd(scope, succeeded, false);
            ResetCurrentAction(scope);
            ExecutionContexts.End(
                actionContextId,
                (int)(succeeded ? MobaExecutionEndReason.Completed : MobaExecutionEndReason.Failed),
                _frameTime != null ? _frameTime.Frame.Value : 0);
            CompleteActionDiagnostics(scope, succeeded);
        }

        private void BeginActionDiagnostics(EffectExecutionScope scope)
        {
            if (MobaPerformanceProfiling.TryBegin(
                    _diagnostics,
                    MobaBattleDiagnosticChannel.TriggerHook,
                    MobaBattleDiagnosticMetric.EffectActionScope,
                    out var actionPerformanceScope))
            {
                if (scope.PerformanceScopes == null)
                {
                    scope.PerformanceScopes = new EffectExecutionPerformanceScopes();
                }

                scope.PerformanceScopes.Action = actionPerformanceScope;
            }

            if (_diagnostics == null) return;

            _diagnostics.Counter(MobaBattleDiagnosticMetric.EffectActionInvoked);
            scope.IsActionDiagnosticsSampled =
                _diagnostics.ShouldSample(MobaBattleDiagnosticChannel.TriggerHook);
            if (!scope.IsActionDiagnosticsSampled) return;

            scope.ActionStartTimestamp = _diagnostics.GetTimestamp();
            scope.ActionAllocatedBytesStart = TryGetAllocatedBytes(out var allocatedBytes)
                ? allocatedBytes
                : -1L;
        }

        private void CaptureActionEnd(EffectExecutionScope scope, bool succeeded, bool aborted)
        {
            var observation = new MobaActionExecutionObservation(
                MobaActionExecutionObservationStage.Ended,
                scope.CurrentActionContextId,
                scope.CurrentActionIndex,
                scope.CurrentActionId,
                scope.SourceActorId,
                scope.TargetActorId,
                _frameTime != null ? _frameTime.Frame.Value : -1,
                succeeded,
                aborted);
            _actionExecutionHook.TryObserve(in observation);
        }

        private void CompleteActionDiagnostics(
            EffectExecutionScope scope,
            bool succeeded)
        {
            if (scope.PerformanceScopes != null)
            {
                scope.PerformanceScopes.Action.Dispose();
                scope.PerformanceScopes.Action = default;
            }

            if (_diagnostics == null) return;

            _diagnostics.Counter(
                succeeded
                    ? MobaBattleDiagnosticMetric.EffectActionSucceeded
                    : MobaBattleDiagnosticMetric.EffectActionFailed);
            if (!scope.IsActionDiagnosticsSampled) return;

            _diagnostics.RecordDuration(
                MobaBattleDiagnosticMetric.EffectActionDuration,
                scope.ActionStartTimestamp);
            if (scope.ActionAllocatedBytesStart >= 0L &&
                TryGetAllocatedBytes(out var allocatedBytes))
            {
                _diagnostics.Sample(
                    MobaBattleDiagnosticMetric.EffectActionAllocatedBytes,
                    Math.Max(0L, allocatedBytes - scope.ActionAllocatedBytesStart));
            }

            scope.IsActionDiagnosticsSampled = false;
            scope.ActionStartTimestamp = 0L;
            scope.ActionAllocatedBytesStart = 0L;
        }

        private static void ResetCurrentAction(EffectExecutionScope scope)
        {
            scope.CurrentActionIndex = -1;
            scope.CurrentActionContextId = 0L;
            scope.CurrentActionId = 0L;
        }

        private static bool TryGetAllocatedBytes(out long allocatedBytes)
        {
            try
            {
                allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
                return true;
            }
            catch (Exception)
            {
                allocatedBytes = 0L;
                return false;
            }
        }

        /// <summary>
        /// 创建正式效果执行 Context。存在父上下文时挂为子节点，否则创建根节点。
        /// </summary>
        private EffectExecutionScope BeginEffectExecutionScope(int effectConfigId, int triggerId, in MobaEffectLineageInput lineageInput)
        {
            if (ExecutionContexts == null)
            {
                MobaRuntimeGuard.ThrowRequired(
                    _services,
                    nameof(MobaEffectExecutionService),
                    "effect.context.begin",
                    nameof(MobaExecutionContextRegistry),
                    MobaBattleExceptionDomain.Service,
                    detail: $"effectConfigId={effectConfigId}, triggerId={triggerId}, sourceActorId={lineageInput.SourceActorId}, parentContextId={lineageInput.ParentContextId}");
            }

            var configId = effectConfigId > 0 ? effectConfigId : triggerId;
            var parentContextId = lineageInput.ParentContextId;
            var scope = new EffectExecutionScope
            {
                EffectConfigId = configId,
                TriggerId = triggerId,
                SourceActorId = lineageInput.SourceActorId,
                TargetActorId = lineageInput.TargetActorId,
            };

            if (MobaPerformanceProfiling.TryBegin(
                    _diagnostics,
                    MobaBattleDiagnosticChannel.TriggerHook,
                    MobaBattleDiagnosticMetric.EffectExecuteScope,
                    out var effectPerformanceScope))
            {
                scope.PerformanceScopes = new EffectExecutionPerformanceScopes
                {
                    Effect = effectPerformanceScope,
                };
            }

            try
            {
                var frame = _frameTime != null ? _frameTime.Frame.Value : 0;
                var node = ExecutionContexts.Create(new MobaExecutionContextCreateRequest(
                    MobaExecutionKind.EffectExecution,
                    configId,
                    lineageInput.SourceActorId,
                    lineageInput.TargetActorId,
                    parentContextId,
                    lineageInput.EffectiveRootContextId,
                    lineageInput.OwnerContextId,
                    frame,
                    triggerId,
                    (MobaExecutionKind)lineageInput.OriginKind,
                    lineageInput.OriginConfigId));
                scope.EffectContextId = node.ContextId;
                scope.IsRoot = node.ParentContextId == 0L;

                if (scope.EffectContextId == 0)
                {
                    throw new InvalidOperationException($"[MobaEffectExecutionService] Failed to create formal effect execution scope. effectConfigId={effectConfigId}, triggerId={triggerId}, sourceActorId={lineageInput.SourceActorId}, targetActorId={lineageInput.TargetActorId}, parentContextId={lineageInput.ParentContextId}, rootContextId={lineageInput.RootContextId}");
                }

                _executionScopes.Push(scope);
                return scope;
            }
            catch
            {
                try
                {
                    if (scope.EffectContextId != 0L)
                    {
                        ExecutionContexts?.End(
                            scope.EffectContextId,
                            (int)MobaExecutionEndReason.Failed,
                            _frameTime != null ? _frameTime.Frame.Value : 0);
                    }
                }
                finally
                {
                    if (scope.PerformanceScopes != null)
                    {
                        scope.PerformanceScopes.Effect.Dispose();
                        scope.PerformanceScopes.Effect = default;
                    }
                }

                throw;
            }
        }

        /// <summary>
        /// 结束当前溯源链路
        /// </summary>
        private void EndCurrentExecutionScope(int reason)
        {
            if (_executionScopes.Count == 0) return;

            var scope = _executionScopes.Pop();
            try
            {
                if (scope.CurrentActionId != 0L)
                {
                    CaptureActionEnd(scope, false, true);
                    ExecutionContexts?.End(
                        scope.CurrentActionContextId,
                        reason,
                        _frameTime != null ? _frameTime.Frame.Value : 0);
                    ResetCurrentAction(scope);
                    CompleteActionDiagnostics(
                        scope,
                        reason == (int)MobaExecutionEndReason.Completed);
                }
                scope.ActionContextIds.Clear();
                ResetCurrentAction(scope);

                ExecutionContexts?.End(
                    scope.EffectContextId,
                    reason,
                    _frameTime != null ? _frameTime.Frame.Value : 0);
            }
            finally
            {
                if (scope.PerformanceScopes != null)
                {
                    scope.PerformanceScopes.Effect.Dispose();
                    scope.PerformanceScopes.Effect = default;
                }
            }
        }

        /// <summary>
        /// 初始化 Plan Actions 注册
        /// 由 InstallPlanTriggering 在 World 启动时统一调用
        /// </summary>
        public void InitializePlanActions()
        {
            if (_planDb == null)
            {
                throw new InvalidOperationException("MobaEffectExecutionService requires TriggerPlanJsonDatabase to initialize plan actions.");
            }

            if (_planActions == null)
            {
                throw new InvalidOperationException("MobaEffectExecutionService requires ActionRegistry to initialize plan actions.");
            }

            RegisterPlanActionModules("InitializePlanActions");
        }

        private void RegisterPlanActionModules(string caller)
        {
            if (_planActions == null)
            {
                throw new InvalidOperationException($"MobaEffectExecutionService.{caller} requires ActionRegistry.");
            }

            if (_services == null)
            {
                throw new InvalidOperationException($"MobaEffectExecutionService.{caller} requires world services.");
            }

            if (!_services.TryResolve<AbilityKit.Demo.Moba.Services.Triggering.PlanActions.PlanActionModuleRegistry>(out var registry) || registry == null)
            {
                throw new InvalidOperationException($"MobaEffectExecutionService.{caller} requires PlanActionModuleRegistry.");
            }

            if (registry.Modules == null || registry.Modules.Length == 0)
            {
                throw new InvalidOperationException($"MobaEffectExecutionService.{caller} requires at least one plan action module.");
            }

            var modules = registry.Modules;
            for (int i = 0; i < modules.Length; i++)
            {
                var m = modules[i];
                if (m == null)
                {
                    throw new InvalidOperationException($"MobaEffectExecutionService.{caller} found null plan action module. index={i}");
                }

                try
                {
                    m.Register(_planActions, _services);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"MobaEffectExecutionService.{caller} failed to register plan action module. module={m.GetType().Name}", ex);
                }
            }
        }

        private int CurrentBudgetFrame
        {
            get
            {
                if (_frameTime != null) return _frameTime.Frame.Value;
                throw new InvalidOperationException("MobaEffectExecutionService requires IFrameTime for trigger execution budget and context frames.");
            }
        }

        public MobaTriggerConditionContext CreateConditionContext(object payload)
        {
            var executionContext = CreateCombatExecutionContext(payload, 0, 0);
            return CreateConditionContext(in executionContext);
        }

        private MobaTriggerConditionContext CreateConditionContext(in MobaCombatExecutionContext executionContext)
        {
            var frame = executionContext.Frame != 0 ? executionContext.Frame : CurrentBudgetFrame;
            var snapshot = executionContext.ExecutionSnapshot.WithFrame(frame);
            var normalizedContext = MobaCombatExecutionContextFactory.WithSnapshot(
                in executionContext,
                in snapshot,
                frame);
            if (_payloadResolvers != null && _payloadResolvers.TryCreateContext(
                    in normalizedContext,
                    _skillRuntimes,
                    frame,
                    out var context))
            {
                return context;
            }

            return MobaTriggerConditionContext.Create(
                in normalizedContext,
                _skillRuntimes,
                frame);
        }

        private MobaTriggerExecutionSnapshot CreateExecutionSnapshot(object payload, in MobaEffectLineageInput lineageInput, int triggerId, int configId)
        {
            return MobaTriggerExecutionSnapshotBuilder.Create()
                .FromLineage(in lineageInput)
                .FromPayload(payload)
                .WithTrigger(triggerId, configId != 0 ? configId : lineageInput.OriginConfigId)
                .WithFrameIfMissing(CurrentBudgetFrame)
                .Build();
        }

        private MobaCombatExecutionContext CreateCombatExecutionContext(object payload, int triggerId, int configId)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload), $"MobaEffectExecutionService requires payload for effect execution context. triggerId={triggerId}, configId={configId}");
            }

            var lineageInput = MobaEffectLineageInputResolver.Resolve(payload);
            var executionSnapshot = CreateExecutionSnapshot(payload, in lineageInput, triggerId, configId);
            var frame = executionSnapshot.Frame != 0 ? executionSnapshot.Frame : CurrentBudgetFrame;
            try
            {
                return MobaCombatExecutionContextFactory.Create(payload, in lineageInput, in executionSnapshot, frame);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidOperationException(
                    $"[MobaEffectExecutionService] Failed to create combat execution context. triggerId={triggerId}, configId={configId}, payloadType={payload.GetType().FullName}, frame={frame}, lineageSourceActorId={lineageInput.SourceActorId}, lineageTargetActorId={lineageInput.TargetActorId}, lineageParentContextId={lineageInput.ParentContextId}, lineageRootContextId={lineageInput.RootContextId}, snapshotSourceActorId={executionSnapshot.SourceActorId}, snapshotTargetActorId={executionSnapshot.TargetActorId}, snapshotSourceContextId={executionSnapshot.SourceContextId}, snapshotRootContextId={executionSnapshot.RootContextId}.",
                    ex);
            }
        }

        private bool TryEnterExecutionBudget(int triggerId, in MobaCombatExecutionContext executionContext, out MobaTriggerExecutionBudgetToken token, out MobaTriggerConditionContext conditionContext)
        {
            conditionContext = CreateConditionContext(in executionContext);
            var request = conditionContext.ToExecutionRequest(triggerId);
            if (_executionBudget.TryEnter(in request, out token, out var block)) return true;

            CollectTriggerAnalysisBudgetBlocked(triggerId, in conditionContext, in block);
            Log.Warning($"[MobaEffectExecutionService] Trigger execution blocked. reason={block.Reason}, triggerId={triggerId}, frame={request.Frame}, depth={block.CurrentDepth}, frameCount={block.CurrentFrameCount}, rootCount={block.CurrentRootCount}, sameTriggerCount={block.CurrentSameTriggerCount}, rootContextId={request.RootContextId}, parentContextId={request.ParentContextId}, sourceActorId={request.SourceActorId}, targetActorId={request.TargetActorId}");
            return false;
        }

        private MobaTriggerConditionCheckResult EvaluateTriggerConditions(int triggerId, in MobaTriggerConditionContext conditionContext)
        {
            var result = _triggerConditions == null || !_triggerConditions.HasConditions(triggerId)
                ? MobaTriggerConditionCheckResult.Pass
                : _triggerConditions.Evaluate(triggerId, in conditionContext);
            CollectTriggerAnalysisCondition(triggerId, in conditionContext, in result);
            if (result.Passed) return result;

            Log.Warning($"[MobaEffectExecutionService] Trigger condition failed. triggerId={triggerId}, reason={result.Reason}, failureKey={result.FailureKey}, rootContextId={conditionContext.RootContextId}, sourceActorId={conditionContext.SourceActorId}, targetActorId={conditionContext.TargetActorId}");
            return result;
        }

        private static int ToExecutionEndReason(bool executed)
        {
            return executed ? (int)MobaExecutionEndReason.Completed : (int)MobaExecutionEndReason.Failed;
        }

        private MobaEffectExecutionSession BeginExecutionSession(
            int effectConfigId,
            int triggerId,
            in MobaCombatExecutionContext executionContext,
            in MobaEffectLineageInput lineageInput,
            in TriggerPlan<object> plan,
            in MobaTriggerExecutionBudgetToken budgetToken,
            IBlackboardResolver blackboards = null)
        {
            EffectExecutionScope executionScope = null;
            var ownsCollections = _executionContexts.Count == 0;
            ITriggerCollectionResolver collections = ownsCollections
                ? new TriggerCollectionStore()
                : _executionContexts.Peek().Collections;
            if (!ownsCollections)
                blackboards = _executionContexts.Peek().Blackboards;
            else if (blackboards == null && executionContext.SourceActorId > 0 && _ownerBlackboards != null)
                blackboards = _ownerBlackboards.GetOrCreate(executionContext.SourceActorId);
            if (blackboards == null)
                blackboards = _globalBlackboards;
            var executionFrame = new CombatExecutionFrame(
                in executionContext,
                collections,
                ownsCollections,
                blackboards);
            _executionContexts.Push(executionFrame);
            try
            {
                executionScope = BeginEffectExecutionScope(effectConfigId, triggerId, in lineageInput);
                executionFrame.AdvanceToEffectExecution(
                    executionScope.EffectContextId,
                    executionScope.EffectConfigId,
                    executionScope.IsRoot);

                if (_effectExecutionHook != null && _effectExecutionHook.IsEnabled)
                {
                    try
                    {
                        var entryContext = executionFrame.Context;
                        var entryObservation = MobaEffectExecutionEntryObservation.Create(
                            executionScope.EffectContextId,
                            effectConfigId,
                            triggerId,
                            in entryContext);
                        _effectExecutionHook.TryObserve(in entryObservation);
                    }
                    catch
                    {
                        // Optional observation extraction must not affect effect execution.
                    }
                }

                CollectEffectStarted(executionScope, in lineageInput);

                return new MobaEffectExecutionSession(this, executionScope, executionFrame, budgetToken, in lineageInput);
            }
            catch
            {
                var ownsSession = false;
                try
                {
                    EnsureCurrentSession(executionFrame, executionScope, "begin-failed");
                    ownsSession = true;
                    if (executionScope != null)
                    {
                        EndCurrentExecutionScope((int)MobaExecutionEndReason.Failed);
                    }
                }
                finally
                {
                    try
                    {
                        if (ownsSession)
                        {
                            PopExecutionFrame(executionFrame, "begin-failed");
                        }
                    }
                    finally
                    {
                        _executionBudget.Exit(in budgetToken);
                    }
                }

                throw;
            }
        }

        private void EnsureCurrentSession(
            CombatExecutionFrame executionFrame,
            EffectExecutionScope executionScope,
            string operation)
        {
            if (_executionContexts.Count == 0 || !ReferenceEquals(_executionContexts.Peek(), executionFrame))
            {
                throw new InvalidOperationException($"[MobaEffectExecutionService] Combat execution session lost LIFO ownership. operation={operation}, executionDepth={_executionContexts.Count}");
            }

            if (executionScope != null &&
                (_executionScopes.Count == 0 || !ReferenceEquals(_executionScopes.Peek(), executionScope)))
            {
                throw new InvalidOperationException($"[MobaEffectExecutionService] Effect execution session lost LIFO ownership. operation={operation}, executionDepth={_executionScopes.Count}, effectContextId={executionScope.EffectContextId}");
            }
        }

        private void PopExecutionFrame(CombatExecutionFrame executionFrame, string operation)
        {
            EnsureCurrentSession(executionFrame, null, operation);
            _executionContexts.Pop();
            if (executionFrame.OwnsCollections && executionFrame.Collections is IDisposable disposable)
                disposable.Dispose();
        }

        private sealed class MobaEffectExecutionSession : IDisposable
        {
            private readonly MobaEffectExecutionService _owner;
            private readonly CombatExecutionFrame _executionFrame;
            private readonly MobaTriggerExecutionBudgetToken _budgetToken;
            private readonly MobaEffectLineageInput _lineageInput;
            private EffectExecutionScope _executionScope;
            private bool _disposed;

            public MobaEffectExecutionSession(
                MobaEffectExecutionService owner,
                EffectExecutionScope executionScope,
                CombatExecutionFrame executionFrame,
                in MobaTriggerExecutionBudgetToken budgetToken,
                in MobaEffectLineageInput lineageInput)
            {
                _owner = owner;
                _executionScope = executionScope;
                _executionFrame = executionFrame;
                _budgetToken = budgetToken;
                _lineageInput = lineageInput;
            }

            public MobaCombatExecutionContext ExecutionContext =>
                _executionFrame.Context;

            public void Complete(bool executed)
            {
                var executionScope = _executionScope;
                if (executionScope == null) return;

                _owner.EnsureCurrentSession(_executionFrame, executionScope, "complete");
                try
                {
                    _owner.EndCurrentExecutionScope(ToExecutionEndReason(executed));
                }
                finally
                {
                    _executionScope = null;
                }

                _owner.CollectEffectEnded(executionScope, in _lineageInput, executed);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                var ownsSession = false;
                try
                {
                    var executionScope = _executionScope;
                    _owner.EnsureCurrentSession(_executionFrame, executionScope, "dispose");
                    ownsSession = true;
                    if (executionScope != null)
                    {
                        try
                        {
                            _owner.EndCurrentExecutionScope((int)MobaExecutionEndReason.Failed);
                        }
                        finally
                        {
                            _executionScope = null;
                        }

                        _owner.CollectEffectEnded(executionScope, in _lineageInput, false);
                    }
                }
                finally
                {
                    try
                    {
                        if (ownsSession)
                        {
                            _owner.PopExecutionFrame(_executionFrame, "dispose");
                        }
                    }
                    finally
                    {
                        _owner._executionBudget.Exit(in _budgetToken);
                    }
                }
            }
        }

        private MobaTriggerPlanExecutor PlanExecutor
        {
            get
            {
                if (_planExecutor == null)
                {
                    _planExecutor = new MobaTriggerPlanExecutor(
                        _services,
                        _planDb,
                        _planEventBus,
                        _planFunctions,
                        _planActions,
                        _planPayloads,
                        this);
                }

                return _planExecutor;
            }
        }

        private bool TryGetPlanByTriggerId(int triggerId, out TriggerPlan<object> plan)
        {
            return PlanExecutor.TryGetPlan(triggerId, out plan);
        }

        private bool TryExecutePlanByTriggerId(int triggerId, object args)
        {
            return PlanExecutor.Execute(triggerId, args);
        }

        public void Execute(int effectId, IAbilityPipelineContext context, EffectExecuteMode mode = EffectExecuteMode.InternalOnly)
        {
            ExecuteWithResult(effectId, context, mode);
        }

        public MobaEffectExecutionOutcome ExecuteWithResult(int effectId, IAbilityPipelineContext context, EffectExecuteMode mode = EffectExecuteMode.InternalOnly)
        {
            if (effectId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(effectId), effectId, "Effect id must be positive.");
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (mode != EffectExecuteMode.InternalOnly)
            {
                throw new InvalidOperationException($"Unsupported effect execute mode. effectId={effectId}, mode={mode}");
            }

            var wrappedContext = EffectContextWrapper.Wrap(context);
            if (wrappedContext == null)
            {
                throw new InvalidOperationException($"Failed to wrap effect pipeline context. effectId={effectId}, context={context.GetType().Name}");
            }

            var executionContext = CreateCombatExecutionContext(wrappedContext, effectId, effectId);
            var lineageInput = executionContext.LineageInput;

            if (!TryGetPlanByTriggerId(effectId, out var plan))
            {
                throw new InvalidOperationException($"Missing trigger plan for effect execution. effectId={effectId}, source={lineageInput.SourceActorId}, target={lineageInput.TargetActorId}, kind={lineageInput.ContextKind}");
            }

            if (!TryEnterExecutionBudget(effectId, in executionContext, out var budgetToken, out var conditionContext))
                return MobaEffectExecutionOutcome.Failed;

            using (var session = BeginExecutionSession(effectId, effectId, in executionContext, in lineageInput, in plan, in budgetToken))
            {
                var activeExecutionContext = session.ExecutionContext;
                conditionContext = CreateConditionContext(in activeExecutionContext);
                var conditionResult = EvaluateTriggerConditions(effectId, in conditionContext);
                var conditionsPassed = conditionResult.Passed;
                var planExecuted = conditionsPassed && TryExecutePlanByTriggerId(effectId, wrappedContext);
                if (conditionsPassed) CollectTriggerAnalysisPlan(effectId, in conditionContext, planExecuted, detailCode: 1);
                if (!planExecuted)
                {
                    var hasFrameTime = _services != null && _services.TryResolve<IFrameTime>(out var frameTime) && frameTime != null;
                    Log.Warning($"[MobaEffectExecutionService] Effect execution returned false. effectId={effectId}, conditionsPassed={conditionsPassed}, payloadType={wrappedContext.GetType().FullName}, source={lineageInput.SourceActorId}, target={lineageInput.TargetActorId}, parentContextId={lineageInput.ParentContextId}, rootContextId={lineageInput.RootContextId}, hasFrameTime={hasFrameTime}");
                }
                session.Complete(planExecuted);
                return !conditionsPassed ? MobaEffectExecutionOutcome.Skipped
                    : planExecuted ? MobaEffectExecutionOutcome.Applied : MobaEffectExecutionOutcome.Failed;
            }
        }

        /// <summary>
        /// 通过强类型触发请求直接执行触发计划。
        /// 用于投射物命中、区域进入/离开、Buff 间隔触发等场景。
        /// </summary>
        public void ExecuteTrigger<TPayload>(in MobaTriggerExecutionRequest<TPayload> request)
        {
            if (request.TriggerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request.TriggerId), request.TriggerId, "Trigger id must be positive.");
            }

            ExecuteTriggerPlan(request.TriggerId, request.Payload, predicateMissIsSuccess: true);
        }

        public bool ExecuteRulePlan(int triggerId, object payload)
        {
            if (triggerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(triggerId), triggerId, "Trigger id must be positive.");
            }

            return ExecuteTriggerPlan(triggerId, payload, predicateMissIsSuccess: false);
        }

        public bool ExecuteOwnerBoundTriggerActions<TArgs>(
            int triggerId,
            TArgs args,
            in ExecCtx<IWorldResolver> ctx,
            in MobaOwnerBoundTriggerExecutionSource source,
            ITrigger<TArgs, IWorldResolver> trigger)
            where TArgs : class
        {
            if (triggerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(triggerId), triggerId, "Trigger id must be positive.");
            }

            if (args == null) throw new ArgumentNullException(nameof(args));
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            if (!source.HasExecutionSource) return false;

            var lineageInput = source.ToLineageInput();
            var executionSnapshot = CreateExecutionSnapshot(args, in lineageInput, triggerId, triggerId);
            var frame = executionSnapshot.Frame != 0 ? executionSnapshot.Frame : CurrentBudgetFrame;
            var origin = new MobaGameplayOrigin(
                lineageInput.SourceActorId,
                lineageInput.TargetActorId,
                lineageInput.OriginKind,
                lineageInput.OriginConfigId,
                lineageInput.ParentContextId,
                lineageInput.ParentContextId,
                lineageInput.EffectiveRootContextId,
                lineageInput.OwnerContextId);
            var executionContext = new MobaCombatExecutionContext(args, lineageInput, origin, executionSnapshot, default, frame);

            if (!TryGetPlanByTriggerId(triggerId, out var plan))
            {
                throw new InvalidOperationException($"Missing trigger plan for owner-bound trigger execution. triggerId={triggerId}, ownerContextId={source.OwnerContextId}, sourceActorId={source.SourceActorId}");
            }

            if (!TryEnterExecutionBudget(triggerId, in executionContext, out var budgetToken, out var conditionContext)) return false;

            using (var session = BeginExecutionSession(
                       triggerId,
                       triggerId,
                       in executionContext,
                       in lineageInput,
                       in plan,
                       in budgetToken,
                       ctx.Blackboards))
            {
                var activeExecutionContext = session.ExecutionContext;
                conditionContext = CreateConditionContext(in activeExecutionContext);
                var conditionResult = EvaluateTriggerConditions(triggerId, in conditionContext);
                var conditionsPassed = conditionResult.Passed;
                if (conditionsPassed)
                {
                    TryGetCurrentTriggerCollections(out var collections);
                    var actionCtx = new ExecCtx<IWorldResolver>(
                        _services ?? ctx.Context,
                        ctx.EventBus,
                        ctx.Functions,
                        ctx.Actions,
                        ctx.Blackboards,
                        ctx.Payloads,
                        ctx.StronglyTypedPayloads,
                        ctx.IdNames,
                        ctx.NumericDomains,
                        ctx.NumericFunctions,
                        ctx.Policy,
                        ctx.Control,
                        ctx.ActionSchedulerManager,
                        collections ?? ctx.Collections);
                    trigger.Execute(in args, in actionCtx);
                    CollectTriggerAnalysisExecution(triggerId, in conditionContext, true, detailCode: 3);
                }

                session.Complete(conditionsPassed);
                return conditionsPassed;
            }
        }

        private bool ExecuteTriggerPlan(int triggerId, object payload, bool predicateMissIsSuccess)
        {
            var executionContext = CreateCombatExecutionContext(payload, triggerId, triggerId);
            var lineageInput = executionContext.LineageInput;

            if (!TryGetPlanByTriggerId(triggerId, out var plan))
            {
                throw new InvalidOperationException($"Missing trigger plan for trigger execution. triggerId={triggerId}, source={lineageInput.SourceActorId}, target={lineageInput.TargetActorId}, kind={lineageInput.ContextKind}");
            }

            if (!TryEnterExecutionBudget(triggerId, in executionContext, out var budgetToken, out var conditionContext)) return false;

            using (var session = BeginExecutionSession(triggerId, triggerId, in executionContext, in lineageInput, in plan, in budgetToken))
            {
                var activeExecutionContext = session.ExecutionContext;
                conditionContext = CreateConditionContext(in activeExecutionContext);
                var conditionResult = EvaluateTriggerConditions(triggerId, in conditionContext);
                var conditionsPassed = conditionResult.Passed;
                var planExecuted = conditionsPassed && (predicateMissIsSuccess
                    ? TryExecutePlanByTriggerId(triggerId, payload)
                    : PlanExecutor.ExecuteRulePlan(triggerId, payload));
                if (conditionsPassed) CollectTriggerAnalysisPlan(triggerId, in conditionContext, planExecuted, predicateMissIsSuccess ? 1 : 2);
                if (!planExecuted)
                {
                    var hasFrameTime = _services != null && _services.TryResolve<IFrameTime>(out var frameTime) && frameTime != null;
                    Log.Warning($"[MobaEffectExecutionService] Trigger execution returned false. triggerId={triggerId}, predicateMissIsSuccess={predicateMissIsSuccess}, conditionsPassed={conditionsPassed}, payloadType={payload?.GetType().FullName ?? "<null>"}, source={lineageInput.SourceActorId}, target={lineageInput.TargetActorId}, parentContextId={lineageInput.ParentContextId}, rootContextId={lineageInput.RootContextId}, hasFrameTime={hasFrameTime}");
                }
                session.Complete(planExecuted);
                return planExecuted;
            }
        }

        // ===== 诊断 Producer：Effect 执行生命周期草稿提交 =====

        private void CollectTriggerAnalysisBudgetBlocked(
            int triggerId,
            in MobaTriggerConditionContext conditionContext,
            in MobaTriggerExecutionBlock block)
        {
            if (_triggerAnalysisHook == null || !_triggerAnalysisHook.IsEnabled) return;

            CollectTriggerAnalysis(
                triggerId,
                in conditionContext,
                MobaTriggerAnalysisStage.Budget,
                MobaTriggerAnalysisResult.Blocked,
                (int)block.Reason,
                block.CurrentDepth,
                block.CurrentFrameCount,
                block.CurrentRootCount,
                block.CurrentSameTriggerCount,
                block.Reason.ToString(),
                "Trigger execution budget rejected the request.");
        }

        private void CollectTriggerAnalysisCondition(
            int triggerId,
            in MobaTriggerConditionContext conditionContext,
            in MobaTriggerConditionCheckResult result)
        {
            if (_triggerAnalysisHook == null || !_triggerAnalysisHook.IsEnabled) return;

            CollectTriggerAnalysis(
                triggerId,
                in conditionContext,
                MobaTriggerAnalysisStage.Conditions,
                result.Passed
                    ? MobaTriggerAnalysisResult.Passed
                    : MobaTriggerAnalysisResult.Failed,
                0,
                failureKey: result.FailureKey,
                reason: result.Reason);
        }

        private void CollectTriggerAnalysisPlan(
            int triggerId,
            in MobaTriggerConditionContext conditionContext,
            bool executed,
            int detailCode)
        {
            if (_triggerAnalysisHook == null || !_triggerAnalysisHook.IsEnabled) return;

            CollectTriggerAnalysis(
                triggerId,
                in conditionContext,
                MobaTriggerAnalysisStage.Plan,
                executed
                    ? MobaTriggerAnalysisResult.Passed
                    : MobaTriggerAnalysisResult.Failed,
                detailCode,
                failureKey: executed ? string.Empty : "planReturnedFalse",
                reason: executed ? string.Empty : "Trigger plan executor returned false.");
        }

        private void CollectTriggerAnalysisExecution(
            int triggerId,
            in MobaTriggerConditionContext conditionContext,
            bool executed,
            int detailCode)
        {
            if (_triggerAnalysisHook == null || !_triggerAnalysisHook.IsEnabled) return;

            CollectTriggerAnalysis(
                triggerId,
                in conditionContext,
                MobaTriggerAnalysisStage.Execution,
                executed
                    ? MobaTriggerAnalysisResult.Passed
                    : MobaTriggerAnalysisResult.Failed,
                detailCode,
                failureKey: executed ? string.Empty : "executionFailed",
                reason: executed ? string.Empty : "Trigger execution failed.");
        }

        private void CollectTriggerAnalysis(
            int triggerId,
            in MobaTriggerConditionContext conditionContext,
            MobaTriggerAnalysisStage stage,
            MobaTriggerAnalysisResult result,
            int detailCode,
            int currentDepth = 0,
            int currentFrameCount = 0,
            int currentRootCount = 0,
            int currentSameTriggerCount = 0,
            string failureKey = "",
            string reason = "")
        {
            if (_triggerAnalysisHook == null || !_triggerAnalysisHook.IsEnabled) return;

            try
            {
                var observation = new MobaTriggerAnalysisObservation(
                    triggerId,
                    (int)conditionContext.ContextKind,
                    (int)conditionContext.OriginKind,
                    stage,
                    result,
                    conditionContext.SourceActorId,
                    conditionContext.TargetActorId,
                    conditionContext.ParentContextId,
                    conditionContext.RootContextId,
                    detailCode,
                    currentDepth,
                    currentFrameCount,
                    currentRootCount,
                    currentSameTriggerCount,
                    failureKey,
                    reason,
                    frame: conditionContext.Frame);
                _triggerAnalysisHook.OnObserved(in observation);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响效果执行流程，静默吞掉异常。
            }
        }

        private void CollectEffectStarted(EffectExecutionScope executionScope, in MobaEffectLineageInput lineageInput)
        {
            if (executionScope == null || _effectLifecycleHook == null || !_effectLifecycleHook.IsEnabled) return;

            try
            {
                var observation = new MobaEffectLifecycleObservation(
                    MobaEffectLifecycleStage.Started,
                    executionScope.EffectConfigId,
                    executionScope.TriggerId,
                    executionScope.SourceActorId,
                    executionScope.TargetActorId,
                    executionScope.EffectContextId,
                    lineageInput.EffectiveRootContextId);
                _effectLifecycleHook.OnObserved(in observation);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响效果执行流程，静默吞掉异常。
            }
        }

        private void CollectEffectEnded(EffectExecutionScope executionScope, in MobaEffectLineageInput lineageInput, bool executed)
        {
            if (executionScope == null || _effectLifecycleHook == null || !_effectLifecycleHook.IsEnabled) return;

            try
            {
                var observation = new MobaEffectLifecycleObservation(
                    MobaEffectLifecycleStage.Ended,
                    executionScope.EffectConfigId,
                    executionScope.TriggerId,
                    executionScope.SourceActorId,
                    executionScope.TargetActorId,
                    executionScope.EffectContextId,
                    lineageInput.EffectiveRootContextId,
                    executed);
                _effectLifecycleHook.OnObserved(in observation);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响效果执行流程，静默吞掉异常。
            }
        }

        public void Dispose()
        {
        }
    }
}
