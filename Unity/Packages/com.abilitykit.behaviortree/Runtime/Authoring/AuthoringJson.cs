using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.Deterministic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.BehaviorTree.Authoring
{
    public static class AuthoringJson
    {
        private static readonly JsonSerializerSettings Settings = CreateSettings();

        public static string Save(AuthoringSourceDocument document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            document.Schema = AuthoringSchema.Id;
            document.Version = AuthoringSchema.Version;
            return JsonConvert.SerializeObject(document, Settings);
        }

        public static AuthoringSourceDocument Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("BT authoring JSON must not be empty.", nameof(json));

            var root = JObject.Parse(json);
            var document = JsonConvert.DeserializeObject<AuthoringSourceDocument>(json, Settings);
            if (document == null)
                throw new InvalidOperationException("BT authoring JSON produced a null document.");

            if (!string.Equals(document.Schema, AuthoringSchema.Id, StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported BT authoring schema '{document.Schema}'.");
            if (!string.Equals(document.Version, AuthoringSchema.Version, StringComparison.Ordinal)
                && !string.Equals(document.Version, AuthoringSchema.LegacyVersion, StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsupported BT authoring version '{document.Version}'.");

            document.Notes ??= new List<AuthoringNoteData>();

            if (root["tree"]?["nodes"] is JArray nodes)
            {
                foreach (var token in nodes)
                {
                    var nodeId = token?["id"]?.Value<string>() ?? "";
                    if (nodeId.Length == 0 || document.TryGetNodeMetadata(nodeId, out _)) continue;
                    var displayName = token?["name"]?.Value<string>() ?? "";
                    var comment = token?["comment"]?.Value<string>() ?? "";
                    if (displayName.Length == 0 && comment.Length == 0) continue;
                    document.NodeMetadata.Add(new AuthoringNodeMetadata
                    {
                        NodeId = nodeId,
                        DisplayName = displayName,
                        Comment = comment,
                    });
                }
            }

            document.Schema = AuthoringSchema.Id;
            document.Version = AuthoringSchema.Version;
            return document;
        }

        public static string SaveProjectManifest(ProjectManifest manifest)
            => JsonConvert.SerializeObject(manifest, Settings);

        public static ProjectManifest LoadProjectManifest(string json)
        {
            var manifest = JsonConvert.DeserializeObject<ProjectManifest>(json, Settings);
            if (manifest == null)
                throw new InvalidOperationException("BT project manifest JSON produced a null manifest.");
            return manifest;
        }

        private static JsonSerializerSettings CreateSettings()
        {
            var resolver = new CamelCasePropertyNamesContractResolver
            {
                NamingStrategy = { ProcessDictionaryKeys = false },
            };
            return new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
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
