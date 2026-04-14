using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Behavior.Actions;
using AbilityKit.Triggering.Runtime.Behavior.Predicates;
using AbilityKit.Triggering.Runtime.Behavior.Schedule;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Config.Actions;
using AbilityKit.Triggering.Runtime.Config.Cue;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Config.Predicates;
using AbilityKit.Triggering.Runtime.Config.Schedule;
using AbilityKit.Triggering.Runtime.Config.Values;

namespace AbilityKit.Triggering.Runtime.Factory
{
    /// <summary>
    /// 行为工厂接口
    /// </summary>
    public interface IBehaviorFactory
    {
        ITriggerBehavior Create(ITriggerPlanConfig planConfig);
        ISchedulableBehavior CreateScheduled(ITriggerPlanConfig planConfig);
        ISimpleTriggerBehavior CreateSimple(ITriggerPlanConfig planConfig);
        IConditionalBehavior CreatePredicate(IPredicateConfig predicateConfig);
        IActionBehavior CreateAction(IActionCallConfig actionConfig);
        List<ITriggerBehavior> CreateActions(IReadOnlyList<IActionCallConfig> actions);
    }

    /// <summary>
    /// 行为工厂实现
    /// </summary>
    public class BehaviorFactory : IBehaviorFactory
    {
        private readonly IValueResolver _valueResolver;
        private readonly IActionRegistry _actionRegistry;
        private readonly IConditionalBehaviorResolver _predicateResolver;
        private readonly ITriggerCueFactory _cueFactory;

        public BehaviorFactory(
            IValueResolver valueResolver,
            IActionRegistry actionRegistry,
            IConditionalBehaviorResolver predicateResolver,
            ITriggerCueFactory cueFactory)
        {
            _valueResolver = valueResolver ?? throw new ArgumentNullException(nameof(valueResolver));
            _actionRegistry = actionRegistry ?? throw new ArgumentNullException(nameof(actionRegistry));
            _predicateResolver = predicateResolver;
            _cueFactory = cueFactory;
        }

        public ITriggerBehavior Create(ITriggerPlanConfig planConfig)
        {
            if (planConfig == null)
                throw new ArgumentNullException(nameof(planConfig));

            var scheduleConfig = planConfig.Schedule;
            if (scheduleConfig != null && scheduleConfig.IsEmpty)
            {
                return CreateSimple(planConfig);
            }

            return CreateScheduled(planConfig);
        }

        public ISchedulableBehavior CreateScheduled(ITriggerPlanConfig planConfig)
        {
            var schedule = planConfig.Schedule;
            return schedule.Mode switch
            {
                EScheduleMode.Timed => new TimedTriggerBehavior(planConfig, this, _valueResolver, _actionRegistry, _cueFactory),
                EScheduleMode.Periodic => new PeriodicTriggerBehavior(planConfig, this, _valueResolver, _actionRegistry, _cueFactory),
                _ => throw new ArgumentException($"Unsupported schedule mode: {schedule.Mode}")
            };
        }

        public ISimpleTriggerBehavior CreateSimple(ITriggerPlanConfig planConfig)
        {
            return new SimpleTriggerBehavior(planConfig, this, _valueResolver, _actionRegistry, _cueFactory);
        }

        public IConditionalBehavior CreatePredicate(IPredicateConfig predicateConfig)
        {
            if (predicateConfig == null || predicateConfig.IsEmpty)
                return NullConditionalBehavior.Instance;

            return predicateConfig.Kind switch
            {
                EPredicateKind.Function => CreateFunctionPredicate((FunctionPredicateConfig)predicateConfig),
                EPredicateKind.Expression => CreateExpressionPredicate((ExpressionPredicateConfig)predicateConfig),
                EPredicateKind.Blackboard => CreateBlackboardPredicate((FunctionPredicateConfig)predicateConfig),
                _ => NullConditionalBehavior.Instance
            };
        }

        public IActionBehavior CreateAction(IActionCallConfig actionConfig)
        {
            return new ActionBehavior(actionConfig, _valueResolver, _actionRegistry);
        }

        private IConditionalBehavior CreateFunctionPredicate(FunctionPredicateConfig config)
        {
            return new FunctionPredicateBehavior(config, _valueResolver, _actionRegistry);
        }

        private IConditionalBehavior CreateExpressionPredicate(ExpressionPredicateConfig config)
        {
            return new ExpressionPredicateBehavior(config, _valueResolver);
        }

        private IConditionalBehavior CreateBlackboardPredicate(FunctionPredicateConfig config)
        {
            return new BlackboardPredicateBehavior(config, _valueResolver);
        }

        public List<ITriggerBehavior> CreateActions(IReadOnlyList<IActionCallConfig> actions)
        {
            var behaviors = new List<ITriggerBehavior>(actions.Count);
            foreach (var action in actions)
            {
                behaviors.Add(new ActionBehavior(action, _valueResolver, _actionRegistry));
            }
            return behaviors;
        }
    }

    /// <summary>
    /// 值解析器实现
    /// </summary>
    public class ValueResolver : IValueResolver
    {
        public double Resolve(IValueRefConfig valueRef, IBehaviorContext context)
        {
            if (valueRef == null) return 0;

            return valueRef.Kind switch
            {
                EValueRefKind.Const => valueRef.ConstValue,
                EValueRefKind.Blackboard => ResolveBlackboard(valueRef, context),
                EValueRefKind.PayloadField => ResolvePayloadField(valueRef, context),
                EValueRefKind.Var => ResolveVar(valueRef, context),
                EValueRefKind.Expr => ResolveExpr(valueRef, context),
                _ => 0
            };
        }

        private double ResolveBlackboard(IValueRefConfig valueRef, IBehaviorContext context)
        {
            if (context.Blackboards == null) return 0;
            if (context.Blackboards.TryGetValue<double>(valueRef.BlackboardId, valueRef.BlackboardKey, out var value))
                return value;
            return 0;
        }

        private double ResolvePayloadField(IValueRefConfig valueRef, IBehaviorContext context)
        {
            return 0;
        }

        private double ResolveVar(IValueRefConfig valueRef, IBehaviorContext context)
        {
            if (context.Values != null)
                return context.Values.Resolve(valueRef, context);
            return 0;
        }

        private double ResolveExpr(IValueRefConfig valueRef, IBehaviorContext context)
        {
            return 0;
        }
    }

    /// <summary>
    /// 条件行为解析器接口
    /// </summary>
    public interface IConditionalBehaviorResolver
    {
        IConditionalBehavior Resolve(IPredicateConfig config);
    }

    /// <summary>
    /// 触发器 Cue 工厂接口
    /// </summary>
    public interface ITriggerCueFactory
    {
        ITriggerCue Create(ICueConfig cueConfig);
    }
}