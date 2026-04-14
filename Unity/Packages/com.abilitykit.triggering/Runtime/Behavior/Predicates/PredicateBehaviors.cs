using System;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Predicates;
using AbilityKit.Triggering.Runtime.Config.Values;
using AbilityKit.Triggering.Runtime.Factory;

namespace AbilityKit.Triggering.Runtime.Behavior.Predicates
{
    /// <summary>
    /// 条件行为接口
    /// </summary>
    public interface IConditionalBehavior
    {
        bool Evaluate(IBehaviorContext context);
    }

    /// <summary>
    /// 空条件行为
    /// </summary>
    public class NullConditionalBehavior : IConditionalBehavior
    {
        public static readonly NullConditionalBehavior Instance = new NullConditionalBehavior();
        private NullConditionalBehavior() { }
        public bool Evaluate(IBehaviorContext context) => true;
    }

    /// <summary>
    /// 函数条件行为
    /// </summary>
    public class FunctionPredicateBehavior : IConditionalBehavior
    {
        private readonly FunctionPredicateConfig _config;
        private readonly IValueResolver _valueResolver;
        private readonly IActionRegistry _actionRegistry;

        public FunctionPredicateBehavior(
            FunctionPredicateConfig config,
            IValueResolver valueResolver,
            IActionRegistry actionRegistry)
        {
            _config = config;
            _valueResolver = valueResolver;
            _actionRegistry = actionRegistry;
        }

        public bool Evaluate(IBehaviorContext context)
        {
            if (_config == null || _actionRegistry == null)
                return true;

            try
            {
                var arity = _config.Arity;
                switch (arity)
                {
                    case 0:
                        if (_actionRegistry.TryGet<Func<object, bool>>(_config.FunctionId, out var func0, out _))
                            return func0(context.Args);
                        break;
                    case 1:
                        var arg0 = _valueResolver.Resolve(_config.Arg0, context);
                        if (_actionRegistry.TryGet<Func<object, double, bool>>(_config.FunctionId, out var func1, out _))
                            return func1(context.Args, arg0);
                        break;
                    case 2:
                        var a0 = _valueResolver.Resolve(_config.Arg0, context);
                        var a1 = _valueResolver.Resolve(_config.Arg1, context);
                        if (_actionRegistry.TryGet<Func<object, double, double, bool>>(_config.FunctionId, out var func2, out _))
                            return func2(context.Args, a0, a1);
                        break;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }

    /// <summary>
    /// 表达式条件行为
    /// </summary>
    public class ExpressionPredicateBehavior : IConditionalBehavior
    {
        private readonly ExpressionPredicateConfig _config;
        private readonly IValueResolver _valueResolver;

        public ExpressionPredicateBehavior(
            ExpressionPredicateConfig config,
            IValueResolver valueResolver)
        {
            _config = config;
            _valueResolver = valueResolver;
        }

        public bool Evaluate(IBehaviorContext context)
        {
            if (_config?.Nodes == null || _config.Nodes.Count == 0)
                return true;

            return EvaluateNodes(0, _config.Nodes, context);
        }

        private bool EvaluateNodes(int startIndex, System.Collections.Generic.List<BoolExprNodeConfig> nodes, IBehaviorContext context)
        {
            return true;
        }
    }

    /// <summary>
    /// Blackboard 条件行为
    /// </summary>
    public class BlackboardPredicateBehavior : IConditionalBehavior
    {
        private readonly FunctionPredicateConfig _config;
        private readonly IValueResolver _valueResolver;

        public BlackboardPredicateBehavior(
            FunctionPredicateConfig config,
            IValueResolver valueResolver)
        {
            _config = config;
            _valueResolver = valueResolver;
        }

        public bool Evaluate(IBehaviorContext context)
        {
            if (_config == null)
                return true;

            var value = _valueResolver.Resolve(_config.Arg0, context);
            return value > 0;
        }
    }
}