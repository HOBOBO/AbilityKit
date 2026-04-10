using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.ECS;

namespace AbilityKit.Ability.Impl.Triggering
{
    public static class TriggerActionArgUtil
    {
        public static int TryGetInt(IReadOnlyDictionary<string, object> args, string key, int fallback = 0)
        {
            if (args == null || string.IsNullOrEmpty(key)) return fallback;
            if (!args.TryGetValue(key, out var obj) || obj == null) return fallback;

            if (obj is int i) return i;
            if (obj is long l) return (int)l;
            if (obj is float f) return (int)f;
            if (obj is double d) return (int)d;
            if (obj is string s && int.TryParse(s, out var parsed)) return parsed;

            try
            {
                return Convert.ToInt32(obj);
            }
            catch
            {
                return fallback;
            }
        }

        public static float TryGetFloat(IReadOnlyDictionary<string, object> args, string key, float fallback = 0f)
        {
            if (args == null || string.IsNullOrEmpty(key)) return fallback;
            if (!args.TryGetValue(key, out var obj) || obj == null) return fallback;

            if (obj is float f) return f;
            if (obj is int i) return i;
            if (obj is long l) return l;
            if (obj is double d) return (float)d;
            if (obj is string s && float.TryParse(s, out var parsed)) return parsed;

            try
            {
                return Convert.ToSingle(obj);
            }
            catch
            {
                return fallback;
            }
        }

        public static TEnum ParseEnum<TEnum>(object obj, TEnum fallback) where TEnum : struct
        {
            try
            {
                if (obj is int i) return (TEnum)Enum.ToObject(typeof(TEnum), i);
                if (obj is long l) return (TEnum)Enum.ToObject(typeof(TEnum), (int)l);
                if (obj is string s && !string.IsNullOrEmpty(s))
                {
                    if (Enum.TryParse<TEnum>(s, ignoreCase: true, out var parsed)) return parsed;
                    if (int.TryParse(s, out var i2)) return (TEnum)Enum.ToObject(typeof(TEnum), i2);
                }
            }
            catch
            {
            }
            return fallback;
        }

        public static bool TryResolveActorId(object obj, out int actorId)
        {
            actorId = 0;
            if (obj == null) return false;

            if (obj is int i)
            {
                actorId = i;
                return actorId > 0;
            }

            if (obj is long l)
            {
                actorId = (int)l;
                return actorId > 0;
            }

            if (obj is EcsEntityId id)
            {
                actorId = id.ActorId;
                return actorId > 0;
            }

            if (obj is IUnitFacade unit)
            {
                actorId = unit.Id.ActorId;
                return actorId > 0;
            }

            try
            {
                var t = obj.GetType();
                if (t != null && string.Equals(t.Name, "ActorEntity", StringComparison.Ordinal))
                {
                    var hasProp = t.GetProperty("hasActorId");
                    if (hasProp != null && hasProp.PropertyType == typeof(bool))
                    {
                        var has = (bool)hasProp.GetValue(obj);
                        if (!has) return false;
                    }

                    var actorIdProp = t.GetProperty("actorId");
                    if (actorIdProp != null)
                    {
                        var actorIdObj = actorIdProp.GetValue(obj);
                        if (actorIdObj != null)
                        {
                            var valueProp = actorIdObj.GetType().GetProperty("Value");
                            if (valueProp != null)
                            {
                                var v = valueProp.GetValue(actorIdObj);
                                if (v is int i2)
                                {
                                    actorId = i2;
                                    return actorId > 0;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
