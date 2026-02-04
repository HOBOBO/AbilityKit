using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Triggering.Variables.Numeric;
using AbilityKit.Triggering.Variables.Numeric.Domains;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class TriggerPlanJsonDatabaseExample
    {
        public readonly struct Ping
        {
            public readonly int Amount;
            public Ping(int amount) { Amount = amount; }
        }

        private sealed class PingPayloadAccessor : IPayloadIntAccessor<object>
        {
            public bool TryGet(in object args, int fieldId, out int value)
            {
                if (fieldId == Eventing.StableStringId.Get("payload:amount") && args is Ping p)
                {
                    value = p.Amount;
                    return true;
                }

                value = default;
                return false;
            }
        }

        private sealed class InlineTextLoader : TriggerPlanJsonDatabase.ITextLoader
        {
            private readonly string _text;
            public InlineTextLoader(string text) { _text = text; }
            public bool TryLoad(string id, out string text) { text = _text; return true; }
        }

        public static void RunOnce_LoadAndRegister()
        {
            var eventId = Eventing.StableStringId.Get("event:ping");
            var payloadAmountFieldId = Eventing.StableStringId.Get("payload:amount");

            var boardId = Eventing.StableStringId.Get("bb:combat");
            var atkKeyId = Eventing.StableStringId.Get("bb:combat:atk");

            // 条件：payload.amount >= 3 AND var(actor.hp) >= 7
            // 动作：打印两个参数（arg0=payload.amount, arg1=var(actor.hp)）
            var json =
                "{\"Triggers\":[{" +
                "\"TriggerId\":1," +
                "\"EventId\":" + eventId + "," +
                "\"AllowExternal\":true," +
                "\"Phase\":0," +
                "\"Priority\":0," +
                "\"Predicate\":{\"Kind\":\"expr\",\"Nodes\":[" +
                // payload.amount >= 3
                "{\"Kind\":\"CompareNumeric\",\"CompareOp\":\"Ge\",\"Left\":{\"Kind\":\"PayloadField\",\"FieldId\":" + payloadAmountFieldId + "},\"Right\":{\"Kind\":\"Const\",\"ConstValue\":3}}," +
                // var(actor.hp) >= 7
                "{\"Kind\":\"CompareNumeric\",\"CompareOp\":\"Ge\",\"Left\":{\"Kind\":\"Var\",\"DomainId\":\"actor\",\"Key\":\"hp\"},\"Right\":{\"Kind\":\"Const\",\"ConstValue\":7}}," +
                // AND
                "{\"Kind\":\"And\"}" +
                "]}," +
                "\"Actions\":[{" +
                "\"ActionId\":" + Eventing.StableStringId.Get("action:print_2") + "," +
                "\"Arity\":2," +
                "\"Arg0\":{\"Kind\":\"PayloadField\",\"FieldId\":" + payloadAmountFieldId + "}," +
                "\"Arg1\":{\"Kind\":\"Var\",\"DomainId\":\"actor\",\"Key\":\"hp\"}" +
                "}]}]}";

            var db = new TriggerPlanJsonDatabase();
            db.LoadFromJson(json, sourceName: "inline");

            var bus = new EventBus();
            var functions = new FunctionRegistry();
            var actions = new ActionRegistry();

            var blackboards = new DictionaryBlackboardResolver();
            var bb = new DictionaryBlackboard();
            blackboards.Register(boardId, bb);
            bb.SetDouble(atkKeyId, 7d);

            var domainResolver = new DictionaryBlackboardDomainResolver();
            domainResolver.Register("actor", boardId);

            var numericDomains = new NumericVarDomainRegistry();
            numericDomains.Register(new BlackboardNumericVarDomain("actor", domainResolver));

            bb.SetDouble(BlackboardIdMapper.KeyId("actor.hp"), 7d);

            var payloads = new PayloadAccessorRegistry();
            payloads.RegisterIntAccessor(new PingPayloadAccessor());

            actions.Register<PlannedTrigger<object, TriggerContext>.Action2>(
                new ActionId(Eventing.StableStringId.Get("action:print_2")),
                (evt, arg0, arg1, ctx) =>
                {
                    Console.WriteLine("json action: arg0(amount)=" + arg0 + " arg1(atk)=" + arg1);
                },
                isDeterministic: true);

            var runner = new TriggerRunner<TriggerContext>(bus, functions, actions, blackboards: blackboards, payloads: payloads, numericDomains: numericDomains, numericFunctions: null);
            db.RegisterAll<TriggerContext>(runner);

            var key = new EventKey<object>(eventId);
            bus.Publish(key, new Ping(amount: 5));
            bus.Flush();
        }
    }
}
