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
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
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
        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector = null;

        private readonly MobaTriggerExecutionBudget _executionBudget = new MobaTriggerExecutionBudget();
        private MobaTriggerPlanExecutor _planExecutor;
 
        /// <summary>
        /// 溯源注册表。正式效果执行必须创建效果溯源作用域，避免动作来源、子对象来源和诊断链路缺少运行时节点。
        /// </summary>
        [WorldInject]
        public MobaTraceRegistry Trace { get; private set; }

        /// <summary>
        /// 当前正在执行的可选 trace 栈（用于嵌套效果和 Action 父子关系追踪）
        /// </summary>
        private readonly Stack<EffectExecutionTraceScope> _traceScopes = new Stack<EffectExecutionTraceScope>();
        private readonly Stack<MobaCombatExecutionContext> _executionContexts = new Stack<MobaCombatExecutionContext>();

        /// <summary>
        /// 获取当前正在追踪的 Action 链路
        /// </summary>
        public IReadOnlyList<long> CurrentActionChain => _traceScopes.Count > 0 ? _traceScopes.Peek().ActionContextIds : Array.Empty<long>();

        public long CurrentEffectContextId => _traceScopes.Count > 0 ? _traceScopes.Peek().EffectContextId : 0;

        public bool TryGetCurrentExecutionContext(out MobaCombatExecutionContext context)
        {
            context = default;
            if (_executionContexts.Count == 0) return false;

            context = _executionContexts.Peek();
            return context.HasExecutionSource;
        }

        public bool TryGetCurrentTraceScope(out MobaEffectTraceScopeSnapshot snapshot)
        {
            snapshot = default;
            if (_traceScopes.Count == 0) return false;

            var scope = _traceScopes.Peek();
            if (scope.EffectContextId == 0) return false;

            snapshot = new MobaEffectTraceScopeSnapshot(
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
            if (_traceScopes.Count == 0) return;

            var scope = _traceScopes.Peek();
            scope.CurrentActionIndex = actionIndex;
            scope.CurrentActionId = actionId;
            scope.CurrentActionContextId = actionIndex >= 0 && actionIndex < scope.ActionContextIds.Count
                ? scope.ActionContextIds[actionIndex]
                : 0L;
        }

        public void ExitActionExecution(int actionIndex, long actionId)
        {
            if (_traceScopes.Count == 0) return;

            var scope = _traceScopes.Peek();
            if (scope.CurrentActionIndex != actionIndex || scope.CurrentActionId != actionId)
            {
                return;
            }

            scope.CurrentActionIndex = -1;
            scope.CurrentActionContextId = 0L;
            scope.CurrentActionId = 0L;
        }

        /// <summary>
        /// 创建正式效果执行 trace 节点。存在父上下文时挂为子节点，否则创建根节点。
        /// </summary>
        private EffectExecutionTraceScope BeginEffectTraceScope(int effectConfigId, int triggerId, in MobaEffectLineageInput lineageInput)
        {
            if (Trace == null)
            {
                MobaRuntimeGuard.ThrowRequired(
                    _services,
                    nameof(MobaEffectExecutionService),
                    "effect.trace.begin",
                    nameof(MobaTraceRegistry),
                    MobaBattleExceptionDomain.Service,
                    detail: $"effectConfigId={effectConfigId}, triggerId={triggerId}, sourceActorId={lineageInput.SourceActorId}, parentContextId={lineageInput.ParentContextId}");
            }

            var configId = effectConfigId > 0 ? effectConfigId : triggerId;
            var parentContextId = lineageInput.ParentContextId;
            var scope = new EffectExecutionTraceScope
            {
                EffectConfigId = configId,
                TriggerId = triggerId,
                SourceActorId = lineageInput.SourceActorId,
                TargetActorId = lineageInput.TargetActorId,
            };

            if (parentContextId != 0)
            {
                scope.EffectContextId = Trace.CreateChildContext(
                    parentContextId,
                    MobaTraceKind.EffectExecution,
                    configId,
                    lineageInput.SourceActorId,
                    lineageInput.TargetActorId,
                    TraceEndpoint.Config(MobaRuntimeKindNames.Effect, configId),
                    TraceEndpoint.Actor(lineageInput.TargetActorId));
                scope.IsRoot = false;
            }
            else
            {
                var rootScope = Trace.CreateEffectRoot(
                    effectConfigId: configId,
                    triggerPlanId: triggerId,
                    sourceActorId: lineageInput.SourceActorId,
                    targetActorId: lineageInput.TargetActorId,
                    contextKind: lineageInput.ContextKind);

                scope.EffectContextId = rootScope.RootId;
                scope.IsRoot = true;
            }

            if (scope.EffectContextId == 0)
            {
                throw new InvalidOperationException($"[MobaEffectExecutionService] Failed to create formal effect trace scope. effectConfigId={effectConfigId}, triggerId={triggerId}, sourceActorId={lineageInput.SourceActorId}, targetActorId={lineageInput.TargetActorId}, parentContextId={lineageInput.ParentContextId}, rootContextId={lineageInput.RootContextId}");
            }

            _traceScopes.Push(scope);
            return scope;
        }

        /// <summary>
        /// 为 Plan 中的所有 Action 创建子节点（父子关系）
        /// </summary>
        private void CreateActionChildNodes(in TriggerPlan<object> plan, int sourceActorId, int targetActorId)
        {
            if (Trace == null || _traceScopes.Count == 0) return;
            if (plan.Actions == null || plan.Actions.Length == 0) return;

            var currentScope = _traceScopes.Peek();
            currentScope.ActionContextIds.Clear();
            foreach (var actionCall in plan.Actions)
            {
                var actionId = (int)actionCall.Id.Value;
                if (actionId == 0)
                {
                    currentScope.ActionContextIds.Add(0L);
                    continue;
                }

                var childScope = Trace.CreateActionChild(
                    parentRootId: currentScope.EffectContextId,
                    actionId: actionId,
                    sourceActorId: sourceActorId,
                    targetActorId: targetActorId);

                currentScope.ActionContextIds.Add(childScope.ContextId);
            }
        }

        /// <summary>
        /// 结束当前溯源链路
        /// </summary>
        private void EndCurrentTrace(int reason)
        {
            if (Trace == null || _traceScopes.Count == 0) return;

            var scope = _traceScopes.Pop();
            foreach (var childId in scope.ActionContextIds)
            {
                Trace.End(childId, reason);
            }
            scope.ActionContextIds.Clear();
            scope.CurrentActionIndex = -1;
            scope.CurrentActionContextId = 0L;
            scope.CurrentActionId = 0L;

            if (scope.IsRoot)
            {
                Trace.EndRoot(scope.EffectContextId, reason);
            }
            else
            {
                Trace.End(scope.EffectContextId, reason);
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
            var payload = executionContext.HasOriginalPayload ? executionContext.Payload : null;
            var lineageInput = executionContext.LineageInput;
            if (_payloadResolvers != null && _payloadResolvers.TryCreateContext(payload, in lineageInput, in snapshot, _skillRuntimes, frame, out var context))
            {
                return context;
            }

            var normalizedContext = MobaCombatExecutionContextFactory.WithSnapshot(in executionContext, in snapshot, frame);
            return MobaTriggerConditionContext.Create(in normalizedContext, _skillRuntimes, frame);
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

        private static int ToTraceEndReason(bool executed)
        {
            return executed ? (int)TraceLifecycleReason.Completed : (int)TraceLifecycleReason.Failed;
        }

        private MobaEffectExecutionSession BeginExecutionSession(
            int effectConfigId,
            int triggerId,
            in MobaCombatExecutionContext executionContext,
            in MobaEffectLineageInput lineageInput,
            in TriggerPlan<object> plan,
            in MobaTriggerExecutionBudgetToken budgetToken)
        {
            EffectExecutionTraceScope traceScope = null;
            _executionContexts.Push(executionContext);
            try
            {
                traceScope = BeginEffectTraceScope(effectConfigId, triggerId, in lineageInput);
                if (plan.Actions != null && plan.Actions.Length > 0)
                {
                    CreateActionChildNodes(in plan, lineageInput.SourceActorId, lineageInput.TargetActorId);
                }

                CollectEffectStarted(traceScope, in lineageInput);

                return new MobaEffectExecutionSession(this, traceScope, budgetToken, in lineageInput);
            }
            catch
            {
                if (traceScope != null)
                {
                    EndCurrentTrace((int)TraceLifecycleReason.Failed);
                }

                if (_executionContexts.Count > 0)
                {
                    _executionContexts.Pop();
                }

                _executionBudget.Exit(in budgetToken);
                throw;
            }
        }

        private sealed class MobaEffectExecutionSession : IDisposable
        {
            private readonly MobaEffectExecutionService _owner;
            private readonly MobaTriggerExecutionBudgetToken _budgetToken;
            private readonly MobaEffectLineageInput _lineageInput;
            private EffectExecutionTraceScope _traceScope;
            private bool _disposed;

            public MobaEffectExecutionSession(
                MobaEffectExecutionService owner,
                EffectExecutionTraceScope traceScope,
                in MobaTriggerExecutionBudgetToken budgetToken,
                in MobaEffectLineageInput lineageInput)
            {
                _owner = owner;
                _traceScope = traceScope;
                _budgetToken = budgetToken;
                _lineageInput = lineageInput;
            }

            public void Complete(bool executed)
            {
                if (_traceScope == null) return;

                _owner.EndCurrentTrace(ToTraceEndReason(executed));
                _owner.CollectEffectEnded(_traceScope, in _lineageInput, executed);
                _traceScope = null;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                if (_traceScope != null)
                {
                    _owner.CollectEffectEnded(_traceScope, in _lineageInput, false);
                    _owner.EndCurrentTrace((int)TraceLifecycleReason.Failed);
                    _traceScope = null;
                }

                if (_owner._executionContexts.Count > 0)
                {
                    _owner._executionContexts.Pop();
                }

                _owner._executionBudget.Exit(in _budgetToken);
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

            if (!TryEnterExecutionBudget(effectId, in executionContext, out var budgetToken, out var conditionContext)) return;

            using (var session = BeginExecutionSession(effectId, effectId, in executionContext, in lineageInput, in plan, in budgetToken))
            {
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

            using (var session = BeginExecutionSession(triggerId, triggerId, in executionContext, in lineageInput, in plan, in budgetToken))
            {
                var conditionResult = EvaluateTriggerConditions(triggerId, in conditionContext);
                var conditionsPassed = conditionResult.Passed;
                if (conditionsPassed)
                {
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
                        ctx.ActionSchedulerManager);
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
            CollectTriggerAnalysis(
                triggerId,
                in conditionContext,
                BattleDiagnosticTriggerAnalysisStage.Budget,
                BattleDiagnosticTriggerAnalysisResult.Blocked,
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
            CollectTriggerAnalysis(
                triggerId,
                in conditionContext,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                result.Passed
                    ? BattleDiagnosticTriggerAnalysisResult.Passed
                    : BattleDiagnosticTriggerAnalysisResult.Failed,
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
            CollectTriggerAnalysis(
                triggerId,
                in conditionContext,
                BattleDiagnosticTriggerAnalysisStage.Plan,
                executed
                    ? BattleDiagnosticTriggerAnalysisResult.Passed
                    : BattleDiagnosticTriggerAnalysisResult.Failed,
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
            CollectTriggerAnalysis(
                triggerId,
                in conditionContext,
                BattleDiagnosticTriggerAnalysisStage.Execution,
                executed
                    ? BattleDiagnosticTriggerAnalysisResult.Passed
                    : BattleDiagnosticTriggerAnalysisResult.Failed,
                detailCode,
                failureKey: executed ? string.Empty : "executionFailed",
                reason: executed ? string.Empty : "Trigger execution failed.");
        }

        private void CollectTriggerAnalysis(
            int triggerId,
            in MobaTriggerConditionContext conditionContext,
            BattleDiagnosticTriggerAnalysisStage stage,
            BattleDiagnosticTriggerAnalysisResult result,
            int detailCode,
            int currentDepth = 0,
            int currentFrameCount = 0,
            int currentRootCount = 0,
            int currentSameTriggerCount = 0,
            string failureKey = "",
            string reason = "")
        {
            if (_eventCollector == null) return;

            try
            {
                var draft = MobaEffectDiagnosticProducer.CreateTriggerAnalysisDraft(
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
                    reason);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响效果执行流程，静默吞掉异常。
            }
        }

        private void CollectEffectStarted(EffectExecutionTraceScope traceScope, in MobaEffectLineageInput lineageInput)
        {
            if (_eventCollector == null || traceScope == null) return;

            try
            {
                var draft = MobaEffectDiagnosticProducer.CreateEffectStartedDraft(
                    traceScope.EffectConfigId,
                    traceScope.TriggerId,
                    traceScope.SourceActorId,
                    traceScope.TargetActorId,
                    traceScope.EffectContextId,
                    lineageInput.EffectiveRootContextId);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响效果执行流程，静默吞掉异常。
            }
        }

        private void CollectEffectEnded(EffectExecutionTraceScope traceScope, in MobaEffectLineageInput lineageInput, bool executed)
        {
            if (_eventCollector == null || traceScope == null) return;

            try
            {
                var draft = MobaEffectDiagnosticProducer.CreateEffectEndedDraft(
                    traceScope.EffectConfigId,
                    traceScope.TriggerId,
                    traceScope.SourceActorId,
                    traceScope.TargetActorId,
                    traceScope.EffectContextId,
                    lineageInput.EffectiveRootContextId,
                    executed);
                _eventCollector.TryCollect(in draft);
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
