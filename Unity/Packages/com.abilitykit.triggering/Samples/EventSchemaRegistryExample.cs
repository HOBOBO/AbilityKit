using System;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Triggering.Runtime.Example
{
    public static class EventSchemaRegistryExample
    {
        public readonly struct DamageEvent
        {
            public readonly int Amount;
            public DamageEvent(int amount) { Amount = amount; }
        }

        public static void RunOnce()
        {
            // EventSchemaRegistry 用于：
            // 1) 在运行时记录 eventId -> argsType/name
            // 2) 在注册/加载 TriggerPlan 时做基础校验（比如同一个 eventId 被不同 argsType 绑定）
            var schemas = new EventSchemaRegistry();

            var damageEventId = StableStringId.Get("event:damage");

            // 注册：eventId 对应的 payload 类型
            schemas.Register<DamageEvent>(damageEventId, name: "DamageEvent");

            // 查询：argsType
            if (schemas.TryGetArgsType(damageEventId, out var argsType))
            {
                Console.WriteLine("eventId=" + damageEventId + " argsType=" + argsType.FullName);
            }

            // 查询：名字
            if (schemas.TryGetName(damageEventId, out var name))
            {
                Console.WriteLine("eventId=" + damageEventId + " name=" + name);
            }

            // 反例（不要这样做）：同一个 eventId 绑定不同的 argsType 会抛异常。
            // schemas.Register<int>(damageEventId);
        }
    }
}
