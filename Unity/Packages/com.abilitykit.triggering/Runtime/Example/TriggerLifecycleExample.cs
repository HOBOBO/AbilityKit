using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Registry;
using UnityEngine;

namespace AbilityKit.Triggering.Example
{
    /// <summary>
    /// 触发器生命周期使用示例
    /// </summary>
    public static class TriggerLifecycleExample
    {
        /// <summary>
        /// 战斗上下文（示例）
        /// </summary>
        public readonly struct BattleCtx
        {
            public readonly int ActorId;
            public readonly BattleServices Services;
            public BattleCtx(int actorId, BattleServices services)
            {
                ActorId = actorId;
                Services = services;
            }
        }

        /// <summary>
        /// 战斗服务（示例）
        /// </summary>
        public class BattleServices { }

        /// <summary>
        /// 伤害事件
        /// </summary>
        public readonly struct DamageEvent
        {
            public readonly int TargetId;
            public readonly double Damage;
            public readonly bool IsCritical;

            public DamageEvent(int targetId, double damage, bool isCritical)
            {
                TargetId = targetId;
                Damage = damage;
                IsCritical = isCritical;
            }
        }

        public static void Run()
        {
            // ========== 1. 创建基础组件 ==========
            var eventBus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            // ========== 2. 创建性能监控生命周期 ==========
            var perfLifecycle = new PerformanceTriggerLifecycle<BattleCtx>();

            // ========== 3. 创建调试生命周期 ==========
            var debugLifecycle = new DebugTriggerLifecycle<BattleCtx>("BattleTrigger");

            // ========== 4. 组合生命周期（同时支持性能和调试） ==========
            var compositeLifecycle = new CompositeTriggerLifecycle<BattleCtx>();
            compositeLifecycle.Add(perfLifecycle);
            compositeLifecycle.Add(debugLifecycle);

            // ========== 5. 创建触发器运行器 ==========
            var runner = new HierarchicalTriggerRunner<BattleCtx>(
                eventBus,
                functions,
                actions,
                contextSource: null,
                lifecycle: compositeLifecycle  // 注入生命周期
            );

            // ========== 6. 注册触发器 ==========
            var damageKey = new EventKey<DamageEvent>("damage");
            runner.Register(damageKey, new DelegateTrigger<DamageEvent, BattleCtx>(
                predicate: (args, ctx) => args.Damage > 0,
                actions: (args, ctx) => UnityEngine.Debug.Log($"[Global] Damage dealt: {args.Damage}")
            ), phase: 0, priority: 0);

            // ========== 7. 创建子级触发器 ==========
            var skillRunner = runner.CreateChild(HierarchicalOptions.SkillScope);
            skillRunner.Register(damageKey, new DelegateTrigger<DamageEvent, BattleCtx>(
                predicate: (args, ctx) => args.IsCritical,
                actions: (args, ctx) => UnityEngine.Debug.Log($"[Skill] Critical damage!")
            ), phase: 0, priority: 100);

            // ========== 8. 触发事件 ==========
            var ctx = new BattleCtx(1, new BattleServices());
            var damageEvent = new DamageEvent(1001, 500.0, true);
            eventBus.Publish(damageKey, damageEvent);

            // ========== 9. 打印性能报告 ==========
            perfLifecycle.PrintReport();

            UnityEngine.Debug.Log("=== TriggerLifecycle Example completed! ===");
        }
    }

    /// <summary>
    /// 组合生命周期（支持多个生命周期同时工作）
    /// </summary>
    public sealed class CompositeTriggerLifecycle<TCtx> : ITriggerLifecycle<TCtx>
    {
        private readonly List<ITriggerLifecycle<TCtx>> _lifecycles = new List<ITriggerLifecycle<TCtx>>();

        public void Add(ITriggerLifecycle<TCtx> lifecycle)
        {
            if (lifecycle != null)
                _lifecycles.Add(lifecycle);
        }

        public void Remove(ITriggerLifecycle<TCtx> lifecycle)
        {
            _lifecycles.Remove(lifecycle);
        }

        public void Clear()
        {
            _lifecycles.Clear();
        }

        public void OnRegistered<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger, int phase, int priority, long order)
        {
            foreach (var l in _lifecycles) l.OnRegistered(key, trigger, phase, priority, order);
        }

        public void OnUnregistered<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, ITrigger<TArgs, TCtx> trigger)
        {
            foreach (var l in _lifecycles) l.OnUnregistered(key, trigger);
        }

        public void OnEventDispatching<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args)
        {
            foreach (var l in _lifecycles) l.OnEventDispatching(key, in args);
        }

        public void OnEventDispatched<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int executedCount, int shortCircuitedCount)
        {
            foreach (var l in _lifecycles) l.OnEventDispatched(key, in args, executedCount, shortCircuitedCount);
        }

        public void OnBeforeEvaluate<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            foreach (var l in _lifecycles) l.OnBeforeEvaluate(key, in args, phase, priority, order);
        }

        public void OnAfterEvaluate<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool result)
        {
            foreach (var l in _lifecycles) l.OnAfterEvaluate(key, in args, phase, priority, order, result);
        }

        public void OnBeforeExecute<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            foreach (var l in _lifecycles) l.OnBeforeExecute(key, in args, phase, priority, order);
        }

        public void OnAfterExecute<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order)
        {
            foreach (var l in _lifecycles) l.OnAfterExecute(key, in args, phase, priority, order);
        }

        public void OnShortCircuit<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ShortCircuitReason reason)
        {
            foreach (var l in _lifecycles) l.OnShortCircuit(key, in args, phase, priority, order, reason);
        }

        public void OnScopeTransition(string fromScope, string toScope)
        {
            foreach (var l in _lifecycles) l.OnScopeTransition(fromScope, toScope);
        }

        public void OnConditionPassed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
        {
            foreach (var l in _lifecycles) l.OnConditionPassed(key, in args, phase, priority, order, conditionId, conditionName);
        }

        public void OnConditionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName)
        {
            foreach (var l in _lifecycles) l.OnConditionFailed(key, in args, phase, priority, order, conditionId, conditionName);
        }

        public void OnActionExecuting<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions)
        {
            foreach (var l in _lifecycles) l.OnActionExecuting(key, in args, phase, priority, order, actionId, actionName, actionIndex, totalActions);
        }

        public void OnActionExecuted<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted)
        {
            foreach (var l in _lifecycles) l.OnActionExecuted(key, in args, phase, priority, order, actionId, actionName, actionIndex, totalActions, wasInterrupted);
        }

        public void OnActionFailed<TArgs>(AbilityKit.Core.Common.Event.EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage)
        {
            foreach (var l in _lifecycles) l.OnActionFailed(key, in args, phase, priority, order, actionId, actionName, actionIndex, totalActions, errorMessage);
        }
    }

    /// <summary>
    /// 带触发器 ID 的委托触发器
    /// 方便溯源追踪
    /// </summary>
    public sealed class IdentifiedDelegateTrigger<TArgs, TCtx> : ITrigger<TArgs, TCtx>
    {
        private readonly int _triggerId;
        private readonly string _triggerName;
        private readonly Func<TArgs, ExecCtx<TCtx>, bool> _predicate;
        private readonly Action<TArgs, ExecCtx<TCtx>> _actions;

        public int TriggerId => _triggerId;
        public string TriggerName => _triggerName;

        public IdentifiedDelegateTrigger(int triggerId, string triggerName, Func<TArgs, ExecCtx<TCtx>, bool> predicate, Action<TArgs, ExecCtx<TCtx>> actions)
        {
            _triggerId = triggerId;
            _triggerName = triggerName;
            _predicate = predicate;
            _actions = actions;
        }

        public bool Evaluate(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            return _predicate == null || _predicate(args, ctx);
        }

        public void Execute(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            _actions?.Invoke(args, ctx);
        }
    }
}
