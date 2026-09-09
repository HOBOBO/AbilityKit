using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace AbilityKit.BehaviorTree.Serialization
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;

    internal static class CanonicalTreeJson
    {
        private static readonly JsonSerializerSettings DefinitionSettings = CreateSettings(true);
        private static readonly JsonSerializerSettings SnapshotSettings = CreateSettings(false);

        public static string Save(TreeDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return JsonConvert.SerializeObject(definition, DefinitionSettings);
        }

        public static TreeDefinition Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("行为树运行时 JSON 不能为空。", nameof(json));

            var root = JObject.Parse(json);
            if (root.Property("schema", StringComparison.OrdinalIgnoreCase) != null
                || root.Property("tree", StringComparison.OrdinalIgnoreCase) != null
                || root.Property("layout", StringComparison.OrdinalIgnoreCase) != null
                || root.Property("groups", StringComparison.OrdinalIgnoreCase) != null
                || root.Property("nodeMetadata", StringComparison.OrdinalIgnoreCase) != null)
            {
                throw new JsonSerializationException(
                    "行为树编辑 JSON 不能直接作为运行时定义加载，请先通过 TreeExporter 导出。");
            }

            var definition = JsonConvert.DeserializeObject<TreeDefinition>(json, DefinitionSettings);
            if (definition == null)
                throw new InvalidOperationException("行为树 JSON 未能生成有效定义。");
            ValidateRuntimeShape(definition);
            return definition;
        }

        public static string SaveSnapshot(TreeRuntimeSnapshot snapshot)
            => JsonConvert.SerializeObject(snapshot, SnapshotSettings);

        public static TreeRuntimeSnapshot LoadSnapshot(string json)
        {
            var snapshot = JsonConvert.DeserializeObject<TreeRuntimeSnapshot>(json, SnapshotSettings);
            if (snapshot == null)
                throw new InvalidOperationException("行为树快照 JSON 未能生成有效快照。");
            return snapshot;
        }

        private static void ValidateRuntimeShape(TreeDefinition definition)
        {
            if (definition.Nodes == null)
                throw new JsonSerializationException("行为树运行时定义缺少有效的 'nodes' 数组。");
            if (definition.Blackboard == null || definition.Blackboard.Keys == null)
                throw new JsonSerializationException("行为树运行时定义缺少有效的黑板 Schema。");

            foreach (var node in definition.Nodes)
            {
                if (node == null)
                    throw new JsonSerializationException("行为树运行时定义包含空节点。");
                if (node.Properties == null)
                    throw new JsonSerializationException($"行为树节点 '{node.Id}' 缺少有效的 'properties' 对象。");
                if (node.ChildIds == null)
                    throw new JsonSerializationException($"行为树节点 '{node.Id}' 缺少有效的 'childIds' 数组。");
            }

            foreach (var key in definition.Blackboard.Keys)
            {
                if (key == null)
                    throw new JsonSerializationException("行为树黑板 Schema 包含空的键定义。");
            }
        }

        private static JsonSerializerSettings CreateSettings(bool indented)
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
                    new PropertyValueConverter(),
                    new PropertyBagConverter(),
                },
            };
        }

        private sealed class PropertyValueConverter : JsonConverter<PropertyValue>
        {
            public override void WriteJson(JsonWriter writer, PropertyValue? value, JsonSerializer serializer)
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
                        ValueType.Bool => JToken.FromObject(value.BoolValue),
                        ValueType.Int64 => JToken.FromObject(value.Int64Value),
                        ValueType.Fixed64 => JToken.FromObject(value.Fixed64Raw),
                        ValueType.String => JToken.FromObject(value.StringValue),
                        _ => JValue.CreateNull(),
                    },
                };
                token.WriteTo(writer);
            }

            public override PropertyValue? ReadJson(
                JsonReader reader,
                Type objectType,
                PropertyValue? existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;

                var token = JToken.Load(reader);
                if (token is not JObject obj)
                    throw new JsonSerializationException("行为树属性值必须是 JSON 对象。");

                var typeName = obj["type"]?.Value<string>()
                    ?? throw new JsonSerializationException("行为树属性值缺少 'type' 字段。");
                if (!Enum.TryParse<ValueType>(typeName, out var type))
                    throw new JsonSerializationException($"未知的行为树属性值类型 '{typeName}'。");

                var valueToken = obj["value"];
                return type switch
                {
                    ValueType.Bool => PropertyValue.Of(valueToken?.Value<bool>() ?? false),
                    ValueType.Int64 => PropertyValue.Of(valueToken?.Value<long>() ?? 0),
                    ValueType.Fixed64 => PropertyValue.Of(Fixed64.FromRaw(valueToken?.Value<long>() ?? 0)),
                    ValueType.String => PropertyValue.Of(valueToken?.Value<string>() ?? ""),
                    _ => throw new JsonSerializationException($"未知的行为树属性值类型 '{typeName}'。"),
                };
            }
        }

        private sealed class PropertyBagConverter : JsonConverter<PropertyBag>
        {
            public override void WriteJson(JsonWriter writer, PropertyBag? value, JsonSerializer serializer)
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

            public override PropertyBag? ReadJson(
                JsonReader reader,
                Type objectType,
                PropertyBag? existingValue,
                bool hasExistingValue,
                JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;

                var bag = existingValue ?? new PropertyBag();
                var token = JToken.Load(reader);
                if (token is not JObject obj)
                    throw new JsonSerializationException("行为树属性集合必须是 JSON 对象。");

                foreach (var property in obj.Properties())
                {
                    var propertyValue = property.Value?.ToObject<PropertyValue>(serializer);
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
