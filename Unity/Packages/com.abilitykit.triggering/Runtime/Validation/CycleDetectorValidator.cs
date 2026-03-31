using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// 事件映射器接口
    /// 用于检测 Action 触发了哪些事件
    /// </summary>
    public interface IActionEventMapper
    {
        /// <summary>
        /// 获取 Action 触发的事件 ID 列表
        /// </summary>
        IEnumerable<string> GetTriggeredEvents(string actionId);
    }

    /// <summary>
    /// 循环检测校验器
    /// 检测触发器之间的循环依赖（死循环）
    /// </summary>
    public sealed class CycleDetectorValidator<TCtx> : ITriggerValidator<TCtx>
    {
        public string Name => "循环依赖检测";
        public int Priority => 0;
        public bool IsCritical => true;

        private readonly int _maxRecursionDepth;
        private readonly IActionEventMapper _eventMapper;

        public CycleDetectorValidator(int maxRecursionDepth = 5, IActionEventMapper eventMapper = null)
        {
            _maxRecursionDepth = maxRecursionDepth;
            _eventMapper = eventMapper;
        }

        public ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context)
        {
            var result = new ValidationResult();

            // 构建事件到计划的映射
            var eventToPlans = new Dictionary<string, List<TriggerPlanEntry<TCtx>>>();
            foreach (var entry in database.Plans)
            {
                var eventKey = entry.EventKey.StringId ?? entry.EventKey.IntId.ToString();
                if (!eventToPlans.TryGetValue(eventKey, out var list))
                {
                    list = new List<TriggerPlanEntry<TCtx>>();
                    eventToPlans[eventKey] = list;
                }
                list.Add(entry);
            }

            // 使用 DFS 检测循环
            var visited = new HashSet<string>();
            var recursionStack = new Stack<string>();
            var recursionPath = new List<string>();

            foreach (var entry in database.Plans)
            {
                visited.Clear();
                recursionStack.Clear();
                recursionPath.Clear();

                var startEvent = entry.EventKey.StringId ?? entry.EventKey.IntId.ToString();
                DetectCycle(entry, eventToPlans, visited, recursionStack, recursionPath, _maxRecursionDepth, ref result);
            }

            return result;
        }

        private void DetectCycle(
            TriggerPlanEntry<TCtx> entry,
            Dictionary<string, List<TriggerPlanEntry<TCtx>>> eventToPlans,
            HashSet<string> visited,
            Stack<string> recursionStack,
            List<string> recursionPath,
            int remainingDepth,
            ref ValidationResult result)
        {
            var eventKey = entry.EventKey.StringId ?? entry.EventKey.IntId.ToString();
            var path = entry.GetPath();

            if (recursionStack.Contains(eventKey))
            {
                var cyclePath = string.Join(" -> ", recursionPath) + " -> " + eventKey;
                result.AddError(
                    ValidationErrorCodes.CYCLE_DETECTED,
                    $"检测到触发器循环依赖: {cyclePath}",
                    path);
                return;
            }

            if (visited.Contains(eventKey))
                return;

            if (remainingDepth <= 0)
            {
                result.AddError(
                    ValidationErrorCodes.EXCEEDS_RECURSION_DEPTH,
                    $"触发器调用链超过最大深度 {_maxRecursionDepth}，可能存在未检测到的循环",
                    path);
                return;
            }

            recursionStack.Push(eventKey);
            recursionPath.Add(eventKey);
            visited.Add(eventKey);

            if (entry.Plan.Actions != null)
            {
                foreach (var action in entry.Plan.Actions)
                {
                    var actionIdStr = action.Id.Value.ToString();

                    if (_eventMapper != null)
                    {
                        var triggeredEvents = _eventMapper.GetTriggeredEvents(actionIdStr);
                        foreach (var triggeredEvent in triggeredEvents)
                        {
                            if (eventToPlans.TryGetValue(triggeredEvent, out var nextEntries))
                            {
                                foreach (var nextEntry in nextEntries)
                                {
                                    DetectCycle(nextEntry, eventToPlans, visited, recursionStack,
                                        recursionPath, remainingDepth - 1, ref result);
                                }
                            }
                        }
                    }
                }
            }

            recursionStack.Pop();
        }
    }

    /// <summary>
    /// 自触发检测校验器
    /// 检测事件是否会触发自己（特殊的循环情况）
    /// </summary>
    public sealed class SelfTriggerValidator<TCtx> : ITriggerValidator<TCtx>
    {
        public string Name => "自触发检测";
        public int Priority => 0;
        public bool IsCritical => true;

        private readonly IActionEventMapper _eventMapper;

        public SelfTriggerValidator(IActionEventMapper eventMapper = null)
        {
            _eventMapper = eventMapper;
        }

        public ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context)
        {
            var result = new ValidationResult();

            foreach (var entry in database.Plans)
            {
                var eventKey = entry.EventKey.StringId ?? entry.EventKey.IntId.ToString();
                var path = entry.GetPath();

                if (entry.Plan.Actions != null)
                {
                    foreach (var action in entry.Plan.Actions)
                    {
                        var actionIdStr = action.Id.Value.ToString();

                        if (_eventMapper != null)
                        {
                            var triggeredEvents = _eventMapper.GetTriggeredEvents(actionIdStr);
                            foreach (var triggeredEvent in triggeredEvents)
                            {
                                if (triggeredEvent == eventKey)
                                {
                                    result.AddError(
                                        ValidationErrorCodes.SELF_TRIGGER,
                                        $"事件 '{eventKey}' 的触发器包含会触发自身的动作 '{actionIdStr}'，将导致无限循环",
                                        $"{path}.actions");
                                }
                            }
                        }

                        if (actionIdStr.IndexOf(eventKey, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            result.AddWarning(
                                ValidationErrorCodes.SELF_TRIGGER,
                                $"动作 ID '{actionIdStr}' 可能与事件 ID '{eventKey}' 相同，可能导致自触发",
                                $"{path}.actions");
                        }
                    }
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 跨作用域循环检测校验器
    /// 用于层级触发器中的跨 Scope 循环检测
    /// </summary>
    public sealed class CrossScopeCycleValidator<TCtx> : ITriggerValidator<TCtx>
    {
        public string Name => "跨作用域循环检测";
        public int Priority => 0;
        public bool IsCritical => true;

        public ValidationResult Validate(in TriggerPlanDatabase<TCtx> database, in ValidationContext<TCtx> context)
        {
            var result = new ValidationResult();

            // 按作用域分组
            var scopeToPlans = new Dictionary<string, List<TriggerPlanEntry<TCtx>>>();
            foreach (var entry in database.Plans)
            {
                var scope = entry.ScopePath ?? "/";
                if (!scopeToPlans.TryGetValue(scope, out var list))
                {
                    list = new List<TriggerPlanEntry<TCtx>>();
                    scopeToPlans[scope] = list;
                }
                list.Add(entry);
            }

            // 检测作用域间的循环
            var visited = new HashSet<string>();

            foreach (var scope in scopeToPlans.Keys)
            {
                visited.Clear();
                DetectCrossScopeCycle(scope, scopeToPlans, visited, ref result);
            }

            return result;
        }

        private void DetectCrossScopeCycle(
            string currentScope,
            Dictionary<string, List<TriggerPlanEntry<TCtx>>> scopeToPlans,
            HashSet<string> visited,
            ref ValidationResult result)
        {
            if (visited.Contains(currentScope))
                return;

            visited.Add(currentScope);

            // 检查当前作用域内的触发器是否会触发其他作用域的事件
            if (scopeToPlans.TryGetValue(currentScope, out var plans))
            {
                foreach (var plan in plans)
                {
                    var eventKey = plan.EventKey.StringId ?? plan.EventKey.IntId.ToString();

                    // 检查是否有其他作用域的触发器响应这个事件
                    foreach (var otherScope in scopeToPlans.Keys)
                    {
                        if (otherScope == currentScope)
                            continue;

                        if (scopeToPlans.TryGetValue(otherScope, out var otherPlans))
                        {
                            foreach (var otherPlan in otherPlans)
                            {
                                if (otherPlan.ScopePath == currentScope)
                                {
                                    var otherEventKey = otherPlan.EventKey.StringId ?? otherPlan.EventKey.IntId.ToString();
                                    if (otherEventKey == eventKey)
                                    {
                                        result.AddWarning(
                                            ValidationErrorCodes.CROSS_SCOPE_CYCLE,
                                            $"检测到跨作用域循环: {currentScope} -> {otherScope}",
                                            plan.GetPath());
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
