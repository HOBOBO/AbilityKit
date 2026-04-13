using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Triggering
{
    public static class TriggerActionArgUtil
    {
        public static int TryGetInt(IReadOnlyDictionary<string, object> args, string key, int fallback = 0)
        {
            if (args == null || !args.TryGetValue(key, out var obj)) return fallback;
            if (obj is int i) return i;
            if (obj is long l) return (int)l;
            if (obj is double d) return (int)d;
            if (obj is float f) return (int)f;
            if (obj is string s && int.TryParse(s, out var parsed)) return parsed;
            return fallback;
        }

        public static float TryGetFloat(IReadOnlyDictionary<string, object> args, string key, float fallback = 0f)
        {
            if (args == null || !args.TryGetValue(key, out var obj)) return fallback;
            if (obj is float f) return f;
            if (obj is double d) return (float)d;
            if (obj is int i) return i;
            if (obj is long l) return l;
            if (obj is string s && float.TryParse(s, out var parsed)) return parsed;
            return fallback;
        }

        public static TEnum ParseEnum<TEnum>(object obj, TEnum fallback) where TEnum : struct
        {
            if (obj is TEnum e) return e;
            if (obj is int i && Enum.IsDefined(typeof(TEnum), i)) return (TEnum)(object)i;
            if (obj is string s && Enum.TryParse<TEnum>(s, out var parsed)) return parsed;
            return fallback;
        }

        public static bool TryResolveActorId(object obj, out int actorId)
        {
            actorId = 0;
            if (obj == null) return false;
            if (obj is int i) { actorId = i; return true; }
            if (obj is long l) { actorId = (int)l; return true; }
            return false;
        }
    }
}
