using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Demo.Moba.Systems
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
                    // 鍏峰悕鍙傛暟妯″紡鐨?Action 涓嶆敞鍐?stub
                    // 鍥犱负 PlanActionModule 浼氭敞鍐屾纭被鍨嬬殑 NamedAction<TArgs> 濮旀墭
                    // 娉ㄥ唽 stub 浼氬鑷寸被鍨嬩笉鍖归厤
                    continue;
                }

                // 娉ㄥ唽浼犵粺 Action stub锛堝悜鍚庡吋瀹癸級
                switch (arity)
                {
                    case 0:
                        actions.Register<Action0<object, IWorldResolver>>(
                            actionId,
                            static (args, ctx) => { },
                            isDeterministic: true);
                        break;
                    case 1:
                        actions.Register<Action1<object, IWorldResolver>>(
                            actionId,
                            static (args, a0, ctx) => { },
                            isDeterministic: true);
                        break;
                    case 2:
                        actions.Register<Action2<object, IWorldResolver>>(
                            actionId,
                            static (args, a0, a1, ctx) => { },
                            isDeterministic: true);
                        break;
                }
            }
        }
    }
}
