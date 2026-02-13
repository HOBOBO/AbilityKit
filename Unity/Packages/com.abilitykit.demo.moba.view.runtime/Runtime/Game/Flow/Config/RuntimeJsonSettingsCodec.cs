using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Game.Flow
{
    public static class RuntimeJsonSettingsCodec
    {
        public static Dictionary<string, object> DeserializeFlat(string json)
        {
            var map = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(json);
            if (map == null || map.Count == 0) return new Dictionary<string, object>(StringComparer.Ordinal);

            var dict = new Dictionary<string, object>(map.Count, StringComparer.Ordinal);
            foreach (var kv in map)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                var t = kv.Value;
                if (t == null) continue;

                object v;
                switch (t.Type)
                {
                    case JTokenType.Boolean:
                        v = t.Value<bool>();
                        break;
                    case JTokenType.Integer:
                        v = t.Value<long>();
                        break;
                    case JTokenType.Float:
                        v = t.Value<double>();
                        break;
                    case JTokenType.String:
                        v = t.Value<string>();
                        break;
                    case JTokenType.Null:
                    case JTokenType.Undefined:
                        continue;
                    default:
                        v = t.ToString(Formatting.None);
                        break;
                }

                dict[kv.Key] = v;
            }

            return dict;
        }

        public static string SerializeFlat(IReadOnlyDictionary<string, object> dict)
        {
            return JsonConvert.SerializeObject(dict, Formatting.Indented);
        }
    }
}
