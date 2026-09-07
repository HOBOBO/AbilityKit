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
                throw new ArgumentException("BT runtime JSON must not be empty.", nameof(json));

            var root = JObject.Parse(json);
            if (root.Property("schema", StringComparison.OrdinalIgnoreCase) != null
                || root.Property("tree", StringComparison.OrdinalIgnoreCase) != null
                || root.Property("layout", StringComparison.OrdinalIgnoreCase) != null
                || root.Property("groups", StringComparison.OrdinalIgnoreCase) != null
                || root.Property("nodeMetadata", StringComparison.OrdinalIgnoreCase) != null)
            {
                throw new JsonSerializationException(
                    "BT authoring JSON cannot be loaded as a runtime definition. Export it with BtTreeExporter first.");
            }

            var definition = JsonConvert.DeserializeObject<TreeDefinition>(json, DefinitionSettings);
            if (definition == null)
                throw new InvalidOperationException("BT tree JSON produced a null definition.");
            ValidateRuntimeShape(definition);
            return definition;
        }

        public static string SaveSnapshot(TreeRuntimeSnapshot snapshot)
            => JsonConvert.SerializeObject(snapshot, SnapshotSettings);

        public static TreeRuntimeSnapshot LoadSnapshot(string json)
        {
            var snapshot = JsonConvert.DeserializeObject<TreeRuntimeSnapshot>(json, SnapshotSettings);
            if (snapshot == null)
                throw new InvalidOperationException("BT snapshot JSON produced a null snapshot.");
            return snapshot;
        }

        private static void ValidateRuntimeShape(TreeDefinition definition)
        {
            if (definition.Nodes == null)
                throw new JsonSerializationException("BT runtime definition requires a non-null 'nodes' array.");
            if (definition.Blackboard == null || definition.Blackboard.Keys == null)
                throw new JsonSerializationException("BT runtime definition requires a non-null blackboard schema.");

            foreach (var node in definition.Nodes)
            {
                if (node == null)
                    throw new JsonSerializationException("BT runtime definition contains a null node.");
                if (node.Properties == null)
                    throw new JsonSerializationException($"BT node '{node.Id}' requires a non-null 'properties' object.");
                if (node.ChildIds == null)
                    throw new JsonSerializationException($"BT node '{node.Id}' requires a non-null 'childIds' array.");
            }

            foreach (var key in definition.Blackboard.Keys)
            {
                if (key == null)
                    throw new JsonSerializationException("BT blackboard schema contains a null key definition.");
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
                    throw new JsonSerializationException("BT property value must be an object.");

                var typeName = obj["type"]?.Value<string>()
                    ?? throw new JsonSerializationException("BT property value requires 'type'.");
                if (!Enum.TryParse<ValueType>(typeName, out var type))
                    throw new JsonSerializationException($"Unknown BT property value type '{typeName}'.");

                var valueToken = obj["value"];
                return type switch
                {
                    ValueType.Bool => PropertyValue.Of(valueToken?.Value<bool>() ?? false),
                    ValueType.Int64 => PropertyValue.Of(valueToken?.Value<long>() ?? 0),
                    ValueType.Fixed64 => PropertyValue.Of(Fixed64.FromRaw(valueToken?.Value<long>() ?? 0)),
                    ValueType.String => PropertyValue.Of(valueToken?.Value<string>() ?? ""),
                    _ => throw new JsonSerializationException($"Unknown BT property value type '{typeName}'."),
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
                    throw new JsonSerializationException("BT property bag must be an object.");

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
