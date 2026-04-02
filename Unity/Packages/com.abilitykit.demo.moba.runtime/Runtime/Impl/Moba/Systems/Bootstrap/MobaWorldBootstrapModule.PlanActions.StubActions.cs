using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Ability.Impl.Moba.Systems
{
    public sealed partial class MobaWorldBootstrapModule
    {
        private static void RegisterStubActionsFromPlans(TriggerPlanJsonDatabase db, ActionRegistry actions)
        {
            if (db == null || actions == null) return;

            var arityById = new Dictionary<int, byte>();
            var hasNamedArgsById = new Dictionary<int, bool>();
            var records = db.Records;
            if (records == null) return;

            for (int i = 0; i < records.Count; i++)
            {
                var plan = records[i].Plan;
                var calls = plan.Actions;
                if (calls == null) continue;

                for (int j = 0; j < calls.Length; j++)
                {
                    var call = calls[j];
                    var id = call.Id.Value;
                    if (id == 0) continue;

                    if (call.HasNamedArgs)
                    {
                        hasNamedArgsById[id] = true;
                    }

                    if (arityById.TryGetValue(id, out var existing))
                    {
                        if (existing != call.Arity)
                        {
                            arityById[id] = byte.MaxValue;
                        }
                    }
                    else
                    {
                        arityById[id] = call.Arity;
                    }
                }
            }

            foreach (var kv in arityById)
            {
                var actionId = new ActionId(kv.Key);
                var arity = kv.Value;
                if (arity == byte.MaxValue) continue;

                var hasNamedArgs = hasNamedArgsById.TryGetValue(kv.Key, out var hna) && hna;
                if (hasNamedArgs)
                {
                    // 具名参数模式的 Action 不注册 stub
                    // 因为 PlanActionModule 会注册正确类型的 NamedAction<TArgs> 委托
                    // 注册 stub 会导致类型不匹配
                    continue;
                }

                // 注册传统 Action stub（向后兼容）
                switch (arity)
                {
                    case 0:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action0>(
                            actionId,
                            static (args, ctx) => { },
                            isDeterministic: true);
                        break;
                    case 1:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action1>(
                            actionId,
                            static (args, a0, ctx) => { },
                            isDeterministic: true);
                        break;
                    case 2:
                        actions.Register<PlannedTrigger<object, IWorldResolver>.Action2>(
                            actionId,
                            static (args, a0, a1, ctx) => { },
                            isDeterministic: true);
                        break;
                }
            }
        }
    }
}
