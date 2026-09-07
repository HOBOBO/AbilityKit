#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Editor.Platform.Export;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>文档级 Schema 头校验，模块与模板共用规则：schema/version 精确匹配，负载体非空。</summary>
    internal static class TriggerSourceDocumentRules
    {
        public static void ValidateModuleHeader(TriggerAuthoringSourceDocument document)
        {
            if (document == null) throw new InvalidDataException("Trigger authoring Source document is null.");
            if (!string.Equals(document.Schema, TriggerAuthoringSchema.Id, StringComparison.Ordinal))
                throw new InvalidDataException($"Unsupported trigger authoring schema: '{document.Schema ?? string.Empty}'.");
            if (!string.Equals(document.Version, TriggerAuthoringSchema.Version, StringComparison.Ordinal))
                throw new InvalidDataException($"Unsupported trigger authoring version: '{document.Version ?? string.Empty}'.");
            if (document.Module == null) throw new InvalidDataException("Trigger authoring Source document has no module.");
        }

        public static void ValidateTemplateHeader(TriggerAuthoringTemplateSourceDocument document)
        {
            if (document == null) throw new InvalidDataException("Trigger template Source document is null.");
            if (!string.Equals(document.Schema, TriggerAuthoringSchema.Id, StringComparison.Ordinal))
                throw new InvalidDataException($"Unsupported trigger authoring schema: '{document.Schema ?? string.Empty}'.");
            if (!string.Equals(document.Version, TriggerAuthoringSchema.Version, StringComparison.Ordinal))
                throw new InvalidDataException($"Unsupported trigger authoring version: '{document.Version ?? string.Empty}'.");
            if (document.Template == null) throw new InvalidDataException("Trigger template Source document has no template.");
        }
    }

    /// <summary>JSON codec 共享的序列化配置与严格读取管线。</summary>
    internal static class TriggerSourceJson
    {
        public static JsonSerializerSettings CreateSettings(Formatting formatting, bool strict = false)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = formatting,
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = strict ? MissingMemberHandling.Error : MissingMemberHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
            settings.Converters.Add(new StringEnumConverter());
            return settings;
        }

        /// <summary>
        /// 严格读取：先解析为 JObject 检查必需根属性，再以 MissingMemberHandling.Error
        /// 反序列化——未知字段直接报错而不是静默丢弃。
        /// </summary>
        public static TDocument Read<TDocument>(string text, string documentLabel, string rootPropertyName)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException(documentLabel + " Source JSON is empty.");

            TDocument document;
            try
            {
                var root = JObject.Parse(text);
                RequireProperty(root, "schema", documentLabel);
                RequireProperty(root, "version", documentLabel);
                RequireProperty(root, rootPropertyName, documentLabel);
                document = JsonConvert.DeserializeObject<TDocument>(text, CreateSettings(Formatting.None, true));
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(documentLabel + " Source JSON is invalid: " + ex.Message, ex);
            }

            return document;
        }

        private static void RequireProperty(JObject root, string propertyName, string documentLabel)
        {
            if (root.Property(propertyName, StringComparison.Ordinal) == null)
                throw new InvalidDataException($"{documentLabel} Source JSON requires property '{propertyName}'.");
        }
    }

    internal sealed class TriggerSourceModuleJsonCodec : ITriggerSourceCodec<TriggerAuthoringSourceDocument>
    {
        private static readonly JsonSerializerSettings WriteSettings = TriggerSourceJson.CreateSettings(Formatting.Indented);

        public string FormatId => "json";
        public string FileExtension => "json";
        public string DisplayName => "JSON";

        public string Serialize(TriggerAuthoringSourceDocument document)
        {
            TriggerSourceDocumentRules.ValidateModuleHeader(document);
            return JsonConvert.SerializeObject(document, WriteSettings) + Environment.NewLine;
        }

        public TriggerAuthoringSourceDocument Deserialize(string text)
        {
            var document = TriggerSourceJson.Read<TriggerAuthoringSourceDocument>(text, "Trigger authoring", "module");
            TriggerSourceDocumentRules.ValidateModuleHeader(document);
            return document;
        }
    }

    internal sealed class TriggerSourceTemplateJsonCodec : ITriggerSourceCodec<TriggerAuthoringTemplateSourceDocument>
    {
        private static readonly JsonSerializerSettings WriteSettings = TriggerSourceJson.CreateSettings(Formatting.Indented);

        public string FormatId => "json";
        public string FileExtension => "json";
        public string DisplayName => "JSON";

        public string Serialize(TriggerAuthoringTemplateSourceDocument document)
        {
            TriggerSourceDocumentRules.ValidateTemplateHeader(document);
            return JsonConvert.SerializeObject(document, WriteSettings) + Environment.NewLine;
        }

        public TriggerAuthoringTemplateSourceDocument Deserialize(string text)
        {
            var document = TriggerSourceJson.Read<TriggerAuthoringTemplateSourceDocument>(text, "Trigger template", "template");
            TriggerSourceDocumentRules.ValidateTemplateHeader(document);
            return document;
        }
    }

    internal enum TriggerAuthoringSourceSchemaKind
    {
        Module = 0,
        Template = 1
    }

    internal sealed class TriggerAuthoringSourceSchemaExportResult
    {
        public string DirectoryPath;
        public readonly List<string> WrittenPaths = new List<string>();
        public readonly List<string> UnchangedPaths = new List<string>();

        public int TotalCount => WrittenPaths.Count + UnchangedPaths.Count;
    }

    /// <summary>
    /// JSON Schema contract for the AI/editable Trigger Authoring Source documents.
    /// Runtime Plan JSON is intentionally separate from this Source JSON contract.
    /// </summary>
    internal static class TriggerAuthoringSourceSchema
    {
        public const string ModuleSchemaFileName = "trigger-authoring-source.module.schema.json";
        public const string TemplateSchemaFileName = "trigger-authoring-source.template.schema.json";

        private const string Draft07 = "http://json-schema.org/draft-07/schema#";
        private const string ModuleSchemaId = "https://abilitykit.local/schemas/trigger-authoring-source.module.schema.json";
        private const string TemplateSchemaId = "https://abilitykit.local/schemas/trigger-authoring-source.template.schema.json";

        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static string Serialize(TriggerAuthoringSourceSchemaKind kind)
        {
            return Create(kind).ToString(Formatting.Indented) + Environment.NewLine;
        }

        public static JObject Create(TriggerAuthoringSourceSchemaKind kind)
        {
            switch (kind)
            {
                case TriggerAuthoringSourceSchemaKind.Module:
                    return CreateRoot(
                        ModuleSchemaId,
                        "AbilityKit Trigger Authoring Module Source",
                        "module",
                        "triggerAuthoringModule");
                case TriggerAuthoringSourceSchemaKind.Template:
                    return CreateRoot(
                        TemplateSchemaId,
                        "AbilityKit Trigger Authoring Template Source",
                        "template",
                        "triggerAuthoringTemplate");
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Trigger Authoring Source schema kind.");
            }
        }

        public static TriggerAuthoringSourceSchemaExportResult ExportAll(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("Schema export directory is required.", nameof(directoryPath));

            var result = new TriggerAuthoringSourceSchemaExportResult
            {
                DirectoryPath = Path.GetFullPath(directoryPath)
            };
            Write(result, ModuleSchemaFileName, Serialize(TriggerAuthoringSourceSchemaKind.Module));
            Write(result, TemplateSchemaFileName, Serialize(TriggerAuthoringSourceSchemaKind.Template));
            return result;
        }

        private static void Write(TriggerAuthoringSourceSchemaExportResult result, string fileName, string content)
        {
            var path = Path.Combine(result.DirectoryPath, fileName);
            var status = EditorAtomicFileWriter.WriteAllText(path, content, Utf8WithoutBom);
            if (status == EditorAtomicWriteStatus.Written) result.WrittenPaths.Add(path);
            else result.UnchangedPaths.Add(path);
        }

        private static JObject CreateRoot(string schemaId, string title, string payloadProperty, string payloadDefinition)
        {
            return new JObject
            {
                ["$schema"] = Draft07,
                ["$id"] = schemaId,
                ["title"] = title,
                ["description"] = "Source JSON used by Trigger Authoring assets. It can be edited externally and imported back into ScriptableObject assets.",
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JArray("schema", "version", "metadata", payloadProperty),
                ["properties"] = new JObject
                {
                    ["schema"] = Const(TriggerAuthoringSchema.Id),
                    ["version"] = Const(TriggerAuthoringSchema.Version),
                    ["metadata"] = Ref("sourceMetadata"),
                    [payloadProperty] = Ref(payloadDefinition)
                },
                ["definitions"] = CreateDefinitions()
            };
        }

        private static JObject CreateDefinitions()
        {
            return new JObject
            {
                ["sourceMetadata"] = ObjectSchema(
                    ("author", StringSchema()),
                    ("description", StringSchema())),
                ["triggerAuthoringModule"] = ObjectSchema(
                    ("moduleId", StringSchema()),
                    ("displayName", StringSchema()),
                    ("kind", EnumSchema<TriggerModuleKind>()),
                    ("blackboard", ArrayOf("blackboardVariable")),
                    ("conditionGroups", ArrayOf("nodeGroup")),
                    ("actionGroups", ArrayOf("nodeGroup")),
                    ("triggers", ArrayOf("triggerDefinition"))),
                ["nodeGroup"] = ObjectSchema(
                    ("id", StringSchema()),
                    ("displayName", StringSchema()),
                    ("description", StringSchema()),
                    ("root", NullableRef("triggerNode"))),
                ["triggerDefinition"] = ObjectSchema(
                    ("id", IntegerSchema()),
                    ("name", StringSchema()),
                    ("groupPath", StringSchema()),
                    ("tags", new JObject
                    {
                        ["type"] = "array",
                        ["items"] = StringSchema()
                    }),
                    ("enabled", BooleanSchema()),
                    ("event", StringSchema()),
                    ("phase", StringSchema()),
                    ("priority", IntegerSchema()),
                    ("interruptPriority", IntegerSchema()),
                    ("scope", StringSchema()),
                    ("allowExternal", BooleanSchema()),
                    ("schedule", Ref("schedule")),
                    ("cue", Ref("cue")),
                    ("executionControl", Ref("executionControl")),
                    ("template", NullableRef("templateReference")),
                    ("condition", NullableRef("triggerNode")),
                    ("actions", NullableRef("triggerNode")),
                    ("blackboard", ArrayOf("blackboardVariable")),
                    ("note", StringSchema())),
                ["triggerNode"] = ObjectSchema(
                    ("enabled", BooleanSchema()),
                    ("kind", EnumSchema<TriggerNodeKind>()),
                    ("groupReference", StringSchema()),
                    ("type", StringSchema()),
                    ("note", StringSchema()),
                    ("arguments", ArrayOf("triggerArgument")),
                    ("children", ArrayOf("triggerNode"))),
                ["triggerArgument"] = ObjectSchema(
                    ("name", StringSchema()),
                    ("value", Ref("valueRef"))),
                ["valueRef"] = ObjectSchema(
                    ("source", EnumSchema<TriggerValueSource>()),
                    ("type", EnumSchema<TriggerValueType>()),
                    ("integerValue", IntegerSchema()),
                    ("numberValue", NumberSchema()),
                    ("booleanValue", BooleanSchema()),
                    ("stringValue", StringSchema()),
                    ("integerListValue", new JObject
                    {
                        ["type"] = "array",
                        ["items"] = IntegerSchema()
                    }),
                    ("vector3Value", Ref("vector3")),
                    ("fields", ArrayOf("triggerArgument")),
                    ("path", StringSchema()),
                    ("expression", StringSchema())),
                ["vector3"] = ObjectSchema(
                    ("x", NumberSchema()),
                    ("y", NumberSchema()),
                    ("z", NumberSchema())),
                ["blackboardVariable"] = ObjectSchema(
                    ("key", StringSchema()),
                    ("type", EnumSchema<TriggerValueType>()),
                    ("readOnly", BooleanSchema()),
                    ("description", StringSchema()),
                    ("defaultValue", Ref("valueRef"))),
                ["schedule"] = ObjectSchema(
                    ("mode", StringSchema()),
                    ("delayMilliseconds", IntegerSchema()),
                    ("intervalMilliseconds", IntegerSchema()),
                    ("repeatCount", IntegerSchema())),
                ["cue"] = ObjectSchema(
                    ("cueId", StringSchema())),
                ["executionControl"] = ObjectSchema(
                    ("interruptPolicy", StringSchema()),
                    ("stopPropagationOnSuccess", BooleanSchema()),
                    ("stopPropagationOnFailure", BooleanSchema())),
                ["templateReference"] = ObjectSchema(
                    ("templateId", StringSchema()),
                    ("version", StringSchema()),
                    ("bindings", ArrayOf("triggerArgument"))),
                ["triggerAuthoringTemplate"] = ObjectSchema(
                    ("templateId", StringSchema()),
                    ("templateVersion", StringSchema()),
                    ("displayName", StringSchema()),
                    ("description", StringSchema()),
                    ("event", StringSchema()),
                    ("parameters", ArrayOf("templateParameter")),
                    ("condition", NullableRef("triggerNode")),
                    ("actions", NullableRef("triggerNode"))),
                ["templateParameter"] = ObjectSchema(
                    ("name", StringSchema()),
                    ("type", EnumSchema<TriggerValueType>()),
                    ("required", BooleanSchema()),
                    ("allowedSources", TemplateValueSourceMaskSchema()),
                    ("hasDefault", BooleanSchema()),
                    ("defaultValue", Ref("valueRef")),
                    ("description", StringSchema()))
            };
        }

        private static JObject ObjectSchema(params (string Name, JToken Schema)[] properties)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false
            };
            var jsonProperties = new JObject();
            for (var i = 0; i < properties.Length; i++)
            {
                jsonProperties[properties[i].Name] = properties[i].Schema;
            }
            schema["properties"] = jsonProperties;
            return schema;
        }

        private static JObject StringSchema()
        {
            return new JObject { ["type"] = "string" };
        }

        private static JObject IntegerSchema()
        {
            return new JObject { ["type"] = "integer" };
        }

        private static JObject NumberSchema()
        {
            return new JObject { ["type"] = "number" };
        }

        private static JObject BooleanSchema()
        {
            return new JObject { ["type"] = "boolean" };
        }

        private static JObject Const(string value)
        {
            return new JObject
            {
                ["type"] = "string",
                ["const"] = value
            };
        }

        private static JObject Ref(string definitionName)
        {
            return new JObject { ["$ref"] = "#/definitions/" + definitionName };
        }

        private static JObject NullableRef(string definitionName)
        {
            return new JObject
            {
                ["anyOf"] = new JArray(
                    Ref(definitionName),
                    new JObject { ["type"] = "null" })
            };
        }

        private static JObject ArrayOf(string definitionName)
        {
            return new JObject
            {
                ["type"] = "array",
                ["items"] = Ref(definitionName)
            };
        }

        private static JObject EnumSchema<TEnum>() where TEnum : struct
        {
            return new JObject
            {
                ["type"] = "string",
                ["enum"] = new JArray(Enum.GetNames(typeof(TEnum)))
            };
        }

        private static JObject TemplateValueSourceMaskSchema()
        {
            return new JObject
            {
                ["type"] = "string",
                ["description"] = "A TriggerTemplateValueSourceMask enum name or comma-separated flag combination."
            };
        }
    }

}
#endif
