using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan.Json;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class TriggerPlanJsonDatabaseExample
    {
        public readonly struct Ping
        {
            public readonly int Amount;
            public Ping(int amount) { Amount = amount; }
        }

        private sealed class InlineTextLoader : TriggerPlanJsonDatabase.ITextLoader
        {
            private readonly string _text;
            public InlineTextLoader(string text) { _text = text; }
            public bool TryLoad(string id, out string text) { text = _text; return true; }
        }

        public static void RunOnce_LoadAndRegister()
        {
            // 最简 JSON：只有一个触发器，没有 predicate、没有 actions。
            // 注意：这里只演示 Load + Register；你可以自行扩展 Actions/PredicateExpr 等字段。
            var json = "{\"Triggers\":[{\"TriggerId\":1,\"EventId\":" + Eventing.StableStringId.Get("event:ping") + ",\"AllowExternal\":true,\"Phase\":0,\"Priority\":0,\"Predicate\":{\"Kind\":\"none\"},\"Actions\":[]}] }";

            var db = new TriggerPlanJsonDatabase();
            db.Load(new InlineTextLoader(json), id: "inline");

            var bus = new EventBus();
            var runner = new TriggerRunner<TriggerContext>(bus, new Registry.FunctionRegistry(), new Registry.ActionRegistry());

            db.RegisterAll<TriggerContext>(runner);

            // Publish 不会做任何事（因为没有 actions），但应该不会抛异常。
            var key = new EventKey<object>(Eventing.StableStringId.Get("event:ping"));
            bus.Publish(key, default(object));
            bus.Flush();
        }
    }
}
