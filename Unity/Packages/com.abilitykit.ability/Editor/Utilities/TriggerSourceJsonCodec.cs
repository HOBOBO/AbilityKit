#if UNITY_EDITOR
using System;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
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
}
#endif
