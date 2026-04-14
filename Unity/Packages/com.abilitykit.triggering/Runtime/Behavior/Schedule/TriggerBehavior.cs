using System;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Actions;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Factory;
using AbilityKit.Triggering.Runtime.Sync;

namespace AbilityKit.Triggering.Runtime.Behavior.Schedule
{
    /// <summary>
    /// 简单触发器行为（瞬时完成）
    /// </summary>
    public class SimpleTriggerBehavior : ISimpleTriggerBehavior
    {
        protected readonly ITriggerPlanConfig _planConfig;
        protected readonly BehaviorFactory _factory;
        protected readonly IValueResolver _valueResolver;
        protected readonly IActionRegistry _actionRegistry;
        protected readonly ITriggerCueFactory _cueFactory;

        public ITriggerPlanConfig Config => _planConfig;

        public SimpleTriggerBehavior(
            ITriggerPlanConfig planConfig,
            BehaviorFactory factory,
            IValueResolver valueResolver,
            IActionRegistry actionRegistry,
            ITriggerCueFactory cueFactory)
        {
            _planConfig = planConfig;
            _factory = factory;
            _valueResolver = valueResolver;
            _actionRegistry = actionRegistry;
            _cueFactory = cueFactory;
        }

        public virtual bool Evaluate(IBehaviorContext context)
        {
            var predicate = _planConfig.Predicate;
            if (predicate == null || predicate.IsEmpty)
                return true;

            var predicateBehavior = _factory.CreatePredicate(predicate);
            return predicateBehavior.Evaluate(context);
        }

        public virtual BehaviorExecutionResult Execute(IBehaviorContext context)
        {
            var actions = _planConfig.Actions;
            if (actions == null || actions.Count == 0)
                return BehaviorExecutionResult.Completed();

            int executedCount = 0;
            foreach (var actionConfig in actions)
            {
                var actionBehavior = new Actions.ActionBehavior(actionConfig, _valueResolver, _actionRegistry);
                var result = actionBehavior.Execute(context);
                if (result.IsSuccess)
                    executedCount++;
                else if (result.IsInterrupted)
                    return result;
            }

            return BehaviorExecutionResult.Success(executedCount);
        }
    }

    /// <summary>
    /// 延迟触发器行为
    /// </summary>
    public class TimedTriggerBehavior : SimpleTriggerBehavior, ISchedulableBehavior
    {
        protected long _elapsedMs;
        protected EBehaviorState _state;
        protected int _executionCount;
        protected bool _delayPassed;

        public EBehaviorState State => _state;
        public long ElapsedMs => _elapsedMs;

        public TimedTriggerBehavior(
            ITriggerPlanConfig planConfig,
            BehaviorFactory factory,
            IValueResolver valueResolver,
            IActionRegistry actionRegistry,
            ITriggerCueFactory cueFactory)
            : base(planConfig, factory, valueResolver, actionRegistry, cueFactory)
        {
            _state = EBehaviorState.Idle;
        }

        public void Begin(IBehaviorContext context)
        {
            _state = EBehaviorState.Running;
            _elapsedMs = 0;
            _executionCount = 0;
            _delayPassed = false;
        }

        public virtual void Update(float deltaTimeMs, IBehaviorContext context)
        {
            if (_state != EBehaviorState.Running)
                return;

            _elapsedMs += (long)deltaTimeMs;

            var delayMs = _planConfig.Schedule.DurationMs;
            if (!_delayPassed && _elapsedMs >= delayMs)
            {
                _delayPassed = true;
                Execute(context);
            }

            if (_elapsedMs >= delayMs)
            {
                _state = EBehaviorState.Completed;
            }
        }

        public void Pause()
        {
            _state = EBehaviorState.Paused;
        }

        public void Resume()
        {
            _state = EBehaviorState.Running;
        }

        public void Interrupt(string reason)
        {
            _state = EBehaviorState.Interrupted;
        }

        public virtual BehaviorSnapshot CreateSnapshot()
        {
            return new BehaviorSnapshot
            {
                TriggerId = _planConfig.TriggerId,
                BehaviorTypeId = GetType().GetHashCode(),
                ElapsedMs = _elapsedMs,
                ExecutionCount = _executionCount,
                State = _state,
                CustomData = null
            };
        }

        public virtual void RestoreFromSnapshot(BehaviorSnapshot snapshot)
        {
            _elapsedMs = snapshot.ElapsedMs;
            _executionCount = snapshot.ExecutionCount;
            _state = snapshot.State;
            _delayPassed = _elapsedMs >= _planConfig.Schedule.DurationMs;
        }
    }

    /// <summary>
    /// 周期触发器行为
    /// </summary>
    public class PeriodicTriggerBehavior : TimedTriggerBehavior
    {
        private int _maxExecutions;
        private long _lastExecutionMs;

        public PeriodicTriggerBehavior(
            ITriggerPlanConfig planConfig,
            BehaviorFactory factory,
            IValueResolver valueResolver,
            IActionRegistry actionRegistry,
            ITriggerCueFactory cueFactory)
            : base(planConfig, factory, valueResolver, actionRegistry, cueFactory)
        {
            _maxExecutions = planConfig.Schedule.MaxExecutions;
        }

        public override void Update(float deltaTimeMs, IBehaviorContext context)
        {
            if (_state != EBehaviorState.Running)
                return;

            _elapsedMs += (long)deltaTimeMs;

            var delayMs = _planConfig.Schedule.DurationMs;
            var periodMs = _planConfig.Schedule.PeriodMs;

            if (!_delayPassed && _elapsedMs >= delayMs)
            {
                _delayPassed = true;
                _lastExecutionMs = _elapsedMs;
                ExecuteOnce(context);
            }
            else if (_delayPassed)
            {
                var timeSinceLastExecution = _elapsedMs - _lastExecutionMs;
                if (timeSinceLastExecution >= periodMs)
                {
                    if (_maxExecutions < 0 || _executionCount < _maxExecutions)
                    {
                        _lastExecutionMs = _elapsedMs;
                        ExecuteOnce(context);
                    }
                    else
                    {
                        _state = EBehaviorState.Completed;
                    }
                }
            }

            if (_elapsedMs >= _planConfig.Schedule.DurationMs + _planConfig.Schedule.PeriodMs * _maxExecutions)
            {
                _state = EBehaviorState.Completed;
            }
        }

        private BehaviorExecutionResult ExecuteOnce(IBehaviorContext context)
        {
            var result = Execute(context);
            if (result.IsSuccess)
                _executionCount++;
            return result;
        }
    }
}