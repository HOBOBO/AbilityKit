using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Factory;

namespace AbilityKit.Triggering.Runtime.Behavior.Composite
{
    /// <summary>
    /// 组合行为基类
    /// </summary>
    public abstract class CompositeTriggerBehavior : ICompositeBehavior
    {
        protected readonly List<ITriggerBehavior> _children = new List<ITriggerBehavior>();
        protected readonly ITriggerPlanConfig _planConfig;
        protected readonly BehaviorFactory _factory;
        protected readonly IValueResolver _valueResolver;
        protected readonly IActionRegistry _actionRegistry;
        protected readonly ITriggerCueFactory _cueFactory;

        public ITriggerPlanConfig Config => _planConfig;
        public int ChildCount => _children.Count;

        protected CompositeTriggerBehavior(
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

        public ITriggerBehavior GetChild(int index)
        {
            if (index < 0 || index >= _children.Count)
                throw new IndexOutOfRangeException();
            return _children[index];
        }

        public void AddChild(ITriggerBehavior child)
        {
            if (child != null)
                _children.Add(child);
        }

        public void ClearChildren()
        {
            _children.Clear();
        }

        public virtual bool Evaluate(IBehaviorContext context)
        {
            var predicate = _planConfig.Predicate;
            if (predicate == null || predicate.IsEmpty)
                return true;

            var predicateBehavior = _factory.CreatePredicate(predicate);
            return predicateBehavior.Evaluate(context);
        }

        public abstract BehaviorExecutionResult Execute(IBehaviorContext context);
    }

    /// <summary>
    /// 顺序执行组合行为（Sequence）
    /// 遇到失败停止
    /// </summary>
    public class SequenceBehavior : CompositeTriggerBehavior
    {
        public SequenceBehavior(
            ITriggerPlanConfig planConfig,
            BehaviorFactory factory,
            IValueResolver valueResolver,
            IActionRegistry actionRegistry,
            ITriggerCueFactory cueFactory)
            : base(planConfig, factory, valueResolver, actionRegistry, cueFactory)
        {
        }

        public override BehaviorExecutionResult Execute(IBehaviorContext context)
        {
            int executedCount = 0;
            foreach (var child in _children)
            {
                if (!child.Evaluate(context))
                    return BehaviorExecutionResult.Interrupted("Condition failed");

                var result = child.Execute(context);
                if (!result.IsSuccess)
                    return result;

                executedCount += result.ExecutedCount;
            }

            return BehaviorExecutionResult.Success(executedCount);
        }
    }

    /// <summary>
    /// 选择执行组合行为（Selector）
    /// 选择第一个成功的执行
    /// </summary>
    public class SelectorBehavior : CompositeTriggerBehavior
    {
        public SelectorBehavior(
            ITriggerPlanConfig planConfig,
            BehaviorFactory factory,
            IValueResolver valueResolver,
            IActionRegistry actionRegistry,
            ITriggerCueFactory cueFactory)
            : base(planConfig, factory, valueResolver, actionRegistry, cueFactory)
        {
        }

        public override BehaviorExecutionResult Execute(IBehaviorContext context)
        {
            foreach (var child in _children)
            {
                if (!child.Evaluate(context))
                    continue;

                var result = child.Execute(context);
                if (result.IsSuccess)
                    return result;
            }

            return BehaviorExecutionResult.Interrupted("All children failed");
        }
    }

    /// <summary>
    /// 并行执行组合行为（Parallel）
    /// 所有子行为并行执行
    /// </summary>
    public class ParallelBehavior : CompositeTriggerBehavior, ISchedulableBehavior
    {
        private long _elapsedMs;
        private EBehaviorState _state;
        private int _executedCount;

        public EBehaviorState State => _state;
        public long ElapsedMs => _elapsedMs;

        public ParallelBehavior(
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
            _executedCount = 0;

            foreach (var child in _children)
            {
                if (child is ISchedulableBehavior schedulable)
                    schedulable.Begin(context);
            }
        }

        public void Update(float deltaTimeMs, IBehaviorContext context)
        {
            if (_state != EBehaviorState.Running)
                return;

            _elapsedMs += (long)deltaTimeMs;

            bool allCompleted = true;
            foreach (var child in _children)
            {
                if (child is ISchedulableBehavior schedulable)
                {
                    if (schedulable.State == EBehaviorState.Running)
                    {
                        schedulable.Update(deltaTimeMs, context);
                        allCompleted = false;
                    }
                }
            }

            if (allCompleted)
                _state = EBehaviorState.Completed;
        }

        public void Pause() => _state = EBehaviorState.Paused;
        public void Resume() => _state = EBehaviorState.Running;
        public void Interrupt(string reason) => _state = EBehaviorState.Interrupted;

        public new BehaviorSnapshot CreateSnapshot()
        {
            return new BehaviorSnapshot
            {
                TriggerId = _planConfig.TriggerId,
                BehaviorTypeId = GetType().GetHashCode(),
                ElapsedMs = _elapsedMs,
                ExecutionCount = _executedCount,
                State = _state
            };
        }

        public new void RestoreFromSnapshot(BehaviorSnapshot snapshot)
        {
            _elapsedMs = snapshot.ElapsedMs;
            _executedCount = snapshot.ExecutionCount;
            _state = snapshot.State;
        }

        public override BehaviorExecutionResult Execute(IBehaviorContext context)
        {
            Begin(context);
            return BehaviorExecutionResult.Success(0);
        }
    }
}