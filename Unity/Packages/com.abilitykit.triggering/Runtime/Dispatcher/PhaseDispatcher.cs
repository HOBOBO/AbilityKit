using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Dispatcher
{
    /// <summary>
    /// 管线阶段触发器上下文
    /// </summary>
    public class PhaseDispatcherContext : ITriggerDispatcherContext
    {
        public object Context { get; }
        public float CurrentTimeMs { get; }

        /// <summary>
        /// 当前执行的 Phase ID
        /// </summary>
        public int CurrentPhaseId { get; }

        /// <summary>
        /// 当前 Phase 的执行索引
        /// </summary>
        public int PhaseExecutionIndex { get; }

        /// <summary>
        /// 管线实例标识
        /// </summary>
        public string PipelineInstanceId { get; }

        public PhaseDispatcherContext(object context, float currentTimeMs, int currentPhaseId, int phaseExecutionIndex = 0, string pipelineInstanceId = null)
        {
            Context = context;
            CurrentTimeMs = currentTimeMs;
            CurrentPhaseId = currentPhaseId;
            PhaseExecutionIndex = phaseExecutionIndex;
            PipelineInstanceId = pipelineInstanceId;
        }

        public T GetService<T>() where T : class
        {
            return Context as T;
        }
    }

    /// <summary>
    /// 管线阶段触发器注册信息
    /// </summary>
    internal class PhaseTriggerRegistration
    {
        public int TriggerId { get; set; }
        public int PhaseId { get; set; }
        public int Priority { get; set; }
        public TriggerPredicate<object> Predicate { get; set; }
        public TriggerExecutor<object> Executor { get; set; }
        public TriggerPlan<object> Plan { get; set; }
    }

    /// <summary>
    /// 管线调度器
    /// 整合 PipelineDispatcher + PipelineDriver
    /// 通过 Pipeline Phase 驱动触发器
    /// </summary>
    public class PhaseDispatcher : TriggerDispatcherBase
    {
        /// <summary>
        /// 按 Phase 分组的触发器注册
        /// </summary>
        private readonly Dictionary<int, List<PhaseTriggerRegistration>> _phaseRegistrations = new Dictionary<int, List<PhaseTriggerRegistration>>();

        /// <summary>
        /// 活跃的持续触发器实例
        /// </summary>
        private readonly List<PhaseTriggerRegistration> _activeTriggers = new List<PhaseTriggerRegistration>();

        public override EDispatcherType DispatcherType => EDispatcherType.Phase;
        public override int RegisteredCount => _registrations.Count;

        public PhaseDispatcher()
        {
            Name = "PhaseDispatcher";
            Priority = 50;
        }

        public override void Initialize()
        {
            _phaseRegistrations.Clear();
            _activeTriggers.Clear();
            _registrations.Clear();
        }

        public override void Dispose()
        {
            _phaseRegistrations.Clear();
            _activeTriggers.Clear();
            _registrations.Clear();
        }

        public override void Register<TArgs>(in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            var registration = new PhaseTriggerRegistration
            {
                TriggerId = plan.TriggerId,
                PhaseId = plan.Phase,
                Priority = plan.Priority,
                Predicate = predicate != null ? (pred, ctx) => predicate((TArgs)pred, ctx) : null,
                Executor = (obj, ctx) => executor((TArgs)obj, ctx),
                Plan = new TriggerPlan<object>(plan.Phase, plan.Priority, plan.TriggerId, plan.InterruptPriority, null)
            };

            // 按 Phase 分组
            if (!_phaseRegistrations.TryGetValue(plan.Phase, out var list))
            {
                list = new List<PhaseTriggerRegistration>();
                _phaseRegistrations[plan.Phase] = list;
            }

            list.Add(registration);
            _registrations[plan.TriggerId] = registration;

            // 排序：按优先级
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }

        public override bool Unregister(int triggerId)
        {
            if (_registrations.TryGetValue(triggerId, out var obj) && obj is PhaseTriggerRegistration reg)
            {
                if (_phaseRegistrations.TryGetValue(reg.PhaseId, out var list))
                {
                    list.Remove(reg);
                }
                _activeTriggers.Remove(reg);
                return _registrations.Remove(triggerId);
            }
            return false;
        }

        /// <summary>
        /// 在指定的 Phase 执行触发器
        /// 由 Pipeline 系统调用
        /// </summary>
        public void ExecutePhase(int phaseId, object args, ITriggerDispatcherContext context)
        {
            if (!IsEnabled) return;

            if (!_phaseRegistrations.TryGetValue(phaseId, out var list)) return;

            var phaseCtx = context as PhaseDispatcherContext;
            int executionIndex = 0;

            foreach (var reg in list)
            {
                // 评估条件
                if (reg.Predicate != null && !reg.Predicate(args, context))
                {
                    continue;
                }

                // 执行
                var wrappedCtx = phaseCtx != null
                    ? new PhaseDispatcherContext(
                        context.Context,
                        phaseCtx.CurrentTimeMs,
                        phaseId,
                        executionIndex++,
                        phaseCtx.PipelineInstanceId)
                    : context;

                reg.Executor(args, wrappedCtx);
            }
        }

        /// <summary>
        /// 执行指定 Phase 范围的所有触发器
        /// </summary>
        public void ExecutePhases(int fromPhaseId, int toPhaseId, object args, ITriggerDispatcherContext context)
        {
            if (!IsEnabled) return;

            foreach (var kvp in _phaseRegistrations)
            {
                if (kvp.Key >= fromPhaseId && kvp.Key <= toPhaseId)
                {
                    ExecutePhase(kvp.Key, args, context);
                }
            }
        }

        public override void Update(float deltaTimeMs, ITriggerDispatcherContext context)
        {
            // PhaseDispatcher 由 Pipeline 系统通过 ExecutePhase 调用
            // 这里可以处理一些需要每帧更新的持续触发器
        }

        /// <summary>
        /// 获取指定 Phase 的触发器数量
        /// </summary>
        public int GetTriggerCountByPhase(int phaseId)
        {
            return _phaseRegistrations.TryGetValue(phaseId, out var list) ? list.Count : 0;
        }

        /// <summary>
        /// 获取所有 Phase ID
        /// </summary>
        public IEnumerable<int> GetAllPhaseIds()
        {
            return _phaseRegistrations.Keys;
        }

        /// <summary>
        /// 注册触发器到指定 Phase
        /// </summary>
        public void RegisterToPhase<TArgs>(int phaseId, in TriggerPlan<TArgs> plan, TriggerPredicate<TArgs> predicate, TriggerExecutor<TArgs> executor)
            where TArgs : class
        {
            var registration = new PhaseTriggerRegistration
            {
                TriggerId = plan.TriggerId,
                PhaseId = phaseId,
                Priority = plan.Priority,
                Predicate = predicate != null ? (pred, ctx) => predicate((TArgs)pred, ctx) : null,
                Executor = (obj, ctx) => executor((TArgs)obj, ctx)
            };

            if (!_phaseRegistrations.TryGetValue(phaseId, out var list))
            {
                list = new List<PhaseTriggerRegistration>();
                _phaseRegistrations[phaseId] = list;
            }

            list.Add(registration);
            _registrations[plan.TriggerId] = registration;
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }
}