using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.Triggering
{
    public static class TriggerActionArgUtil
    {
        public static int TryGetInt(IReadOnlyDictionary<string, object> args, string key, int fallback = 0)
            => AbilityKit.Examples.Triggering.TriggerActionArgUtil.TryGetInt(args, key, fallback);

        public static float TryGetFloat(IReadOnlyDictionary<string, object> args, string key, float fallback = 0f)
            => AbilityKit.Examples.Triggering.TriggerActionArgUtil.TryGetFloat(args, key, fallback);

        public static TEnum ParseEnum<TEnum>(object obj, TEnum fallback) where TEnum : struct
            => AbilityKit.Examples.Triggering.TriggerActionArgUtil.ParseEnum(obj, fallback);

        public static bool TryResolveActorId(object obj, out int actorId)
            => AbilityKit.Examples.Triggering.TriggerActionArgUtil.TryResolveActorId(obj, out actorId);
    }
}
