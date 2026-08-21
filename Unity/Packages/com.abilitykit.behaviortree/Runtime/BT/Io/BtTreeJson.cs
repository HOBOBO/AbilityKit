using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 运行时 IR 与快照的 JSON 读写权威（Newtonsoft，无 TypeNameHandling——
    /// 类型以字符串 id 显式出现，不存在 CLR 类型注入面）。
    /// 字段名 camelCase、字典键保持原样；同定义两次 Save 输出字节一致（golden 基础）。
    /// </summary>
    public static class BtTreeJson
    {
        private static readonly JsonSerializerSettings DefinitionSettings = CreateSettings(true);
        private static readonly JsonSerializerSettings SnapshotSettings = CreateSettings(false);

        public static string Save(BtTreeDefinition definition)
            => JsonConvert.SerializeObject(definition, DefinitionSettings);

        public static BtTreeDefinition Load(string json)
        {
            var definition = JsonConvert.DeserializeObject<BtTreeDefinition>(json, DefinitionSettings);
            if (definition == null)
                throw new InvalidOperationException("BT tree JSON produced a null definition.");
            return definition;
        }

        public static string SaveSnapshot(BtTreeRuntimeSnapshot snapshot)
            => JsonConvert.SerializeObject(snapshot, SnapshotSettings);

        public static BtTreeRuntimeSnapshot LoadSnapshot(string json)
        {
            var snapshot = JsonConvert.DeserializeObject<BtTreeRuntimeSnapshot>(json, SnapshotSettings);
            if (snapshot == null)
                throw new InvalidOperationException("BT snapshot JSON produced a null snapshot.");
            return snapshot;
        }

        internal static JsonSerializerSettings CreateSettings(bool indented)
        {
            var resolver = new CamelCasePropertyNamesContractResolver
            {
                NamingStrategy = { ProcessDictionaryKeys = false },
            };
            return new JsonSerializerSettings
            {
                Formatting = indented ? Formatting.Indented : Formatting.None,
                ContractResolver = resolver,
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Include,
                Converters = new List<JsonConverter>
                {
                    new StringEnumConverter(),
                    new BtPropertyValueConverter(),
                    new BtPropertyBagConverter(),
                },
            };
        }

        private sealed class BtPropertyValueConverter : JsonConverter<BtPropertyValue>
        {
            public override void WriteJson(JsonWriter writer, BtPropertyValue? value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                var token = new JObject
                {
                    ["type"] = value.Type.ToString(),
                    ["value"] = value.Type switch
                    {
                        BtValueType.Bool => JToken.FromObject(value.BoolValue),
                        BtValueType.Int64 => JToken.FromObject(value.Int64Value),
                        BtValueType.Fixed64 => JToken.FromObject(value.Fixed64Raw),
                        BtValueType.String => JToken.FromObject(value.StringValue),
                        _ => JValue.CreateNull(),
                    },
                };
                token.WriteTo(writer);
            }

            public override BtPropertyValue? ReadJson(
                JsonReader reader, Type objectType, BtPropertyValue? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;

                var token = JToken.Load(reader);
                if (token is not JObject obj)
                    throw new JsonSerializationException("BT property value must be an object.");

                var typeName = obj["type"]?.Value<string>()
                    ?? throw new JsonSerializationException("BT property value requires 'type'.");
                if (!Enum.TryParse<BtValueType>(typeName, out var type))
                    throw new JsonSerializationException($"Unknown BT property value type '{typeName}'.");

                var valueToken = obj["value"];
                return type switch
                {
                    BtValueType.Bool => BtPropertyValue.Of(valueToken?.Value<bool>() ?? false),
                    BtValueType.Int64 => BtPropertyValue.Of(valueToken?.Value<long>() ?? 0),
                    BtValueType.Fixed64 => BtPropertyValue.Of(Fixed64.FromRaw(valueToken?.Value<long>() ?? 0)),
                    BtValueType.String => BtPropertyValue.Of(valueToken?.Value<string>() ?? ""),
                    _ => throw new JsonSerializationException($"Unknown BT property value type '{typeName}'."),
                };
            }
        }

        private sealed class BtPropertyBagConverter : JsonConverter<BtPropertyBag>
        {
            public override void WriteJson(JsonWriter writer, BtPropertyBag? value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                writer.WriteStartObject();
                foreach (var pair in value.Values)
                {
                    writer.WritePropertyName(pair.Key);
                    serializer.Serialize(writer, pair.Value);
                }
                writer.WriteEndObject();
            }

            public override BtPropertyBag? ReadJson(
                JsonReader reader, Type objectType, BtPropertyBag? existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;

                var bag = existingValue ?? new BtPropertyBag();
                var token = JToken.Load(reader);
                if (token is not JObject obj)
                    throw new JsonSerializationException("BT property bag must be an object.");

                foreach (var property in obj.Properties())
                {
                    var propertyValue = property.Value?.ToObject<BtPropertyValue>(serializer);
                    if (propertyValue != null)
                    {
                        bag.Set(property.Name, propertyValue);
                    }
                }
                return bag;
            }
        }
    }
}
