using System;
using AbilityKit.Triggering.Runtime.Behavior;
using AbilityKit.Triggering.Runtime.Config.Actions;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Config.Values;
using AbilityKit.Triggering.Runtime.Factory;

namespace AbilityKit.Triggering.Runtime.Behavior.Actions
{
    /// <summary>
    /// Action 行为接口
    /// </summary>
    public interface IActionBehavior : ITriggerBehavior
    {
    }

    /// <summary>
    /// Action 行为实现
    /// </summary>
    public class ActionBehavior : IActionBehavior
    {
        private readonly IActionCallConfig _actionConfig;
        private readonly IValueResolver _valueResolver;
        private readonly IActionRegistry _actionRegistry;

        public ITriggerPlanConfig Config => null;

        public ActionBehavior(
            IActionCallConfig actionConfig,
            IValueResolver valueResolver,
            IActionRegistry actionRegistry)
        {
            _actionConfig = actionConfig;
            _valueResolver = valueResolver;
            _actionRegistry = actionRegistry;
        }

        public bool Evaluate(IBehaviorContext context) => true;

        public BehaviorExecutionResult Execute(IBehaviorContext context)
        {
            if (_actionConfig == null || _actionRegistry == null)
                return BehaviorExecutionResult.Completed();

            try
            {
                var arity = _actionConfig.Arity;
                var args = _actionConfig.Args;

                switch (arity)
                {
                    case 0:
                        if (_actionRegistry.TryGet<Action<object>>(_actionConfig.ActionId, out var action0, out _))
                            action0(context.Args);
                        break;
                    case 1:
                        var arg0 = args != null && args.Count > 0 ? _valueResolver.Resolve(args[0], context) : 0;
                        if (_actionRegistry.TryGet<Action<object, double>>(_actionConfig.ActionId, out var action1, out _))
                            action1(context.Args, arg0);
                        break;
                    case 2:
                        var a0 = args != null && args.Count > 0 ? _valueResolver.Resolve(args[0], context) : 0;
                        var a1 = args != null && args.Count > 1 ? _valueResolver.Resolve(args[1], context) : 0;
                        if (_actionRegistry.TryGet<Action<object, double, double>>(_actionConfig.ActionId, out var action2, out _))
                            action2(context.Args, a0, a1);
                        break;
                }

                return BehaviorExecutionResult.Success();
            }
            catch (System.Exception ex)
            {
                return BehaviorExecutionResult.Interrupted(ex.Message);
            }
        }
    }
}