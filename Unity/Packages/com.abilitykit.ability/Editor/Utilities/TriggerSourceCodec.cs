#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// Authoring Source 文档的编解码器抽象。JSON 是默认实现，其它格式通过
    /// <see cref="TriggerSourceCodecs"/> 注册扩展。契约：
    /// - Deserialize 必须对未知字段报错或显式保留，不得静默丢弃；
    /// Serialize 的输出必须能被同一 codec 的 Deserialize 完整还原；
    /// - FileExtension 决定读写哪个 codec：资产上的 Source 路径扩展名即格式选择。
    /// </summary>
    internal interface ITriggerSourceCodec<TDocument>
    {
        string FormatId { get; }

        /// <summary>不带点的小写扩展名，如 "json"。</summary>
        string FileExtension { get; }

        string DisplayName { get; }

        string Serialize(TDocument document);

        TDocument Deserialize(string text);
    }

    /// <summary>
    /// codec 注册表，按文件扩展名（忽略大小写）解析。默认格式固定为 JSON；
    /// 注册新 codec 不会抢占默认值，只增加可解析的扩展名。
    /// </summary>
    internal static class TriggerSourceCodecs
    {
        private static readonly Dictionary<string, ITriggerSourceCodec<TriggerAuthoringSourceDocument>> ModuleCodecs =
            new Dictionary<string, ITriggerSourceCodec<TriggerAuthoringSourceDocument>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, ITriggerSourceCodec<TriggerAuthoringTemplateSourceDocument>> TemplateCodecs =
            new Dictionary<string, ITriggerSourceCodec<TriggerAuthoringTemplateSourceDocument>>(StringComparer.OrdinalIgnoreCase);

        private static ITriggerSourceCodec<TriggerAuthoringSourceDocument> _moduleDefault;
        private static ITriggerSourceCodec<TriggerAuthoringTemplateSourceDocument> _templateDefault;

        static TriggerSourceCodecs()
        {
            ResetToDefaults();
        }

        public static ITriggerSourceCodec<TriggerAuthoringSourceDocument> ModuleDefault => _moduleDefault;

        public static ITriggerSourceCodec<TriggerAuthoringTemplateSourceDocument> TemplateDefault => _templateDefault;

        public static void RegisterModule(ITriggerSourceCodec<TriggerAuthoringSourceDocument> codec)
        {
            Validate(codec);
            ModuleCodecs[codec.FileExtension] = codec;
        }

        public static void RegisterTemplate(ITriggerSourceCodec<TriggerAuthoringTemplateSourceDocument> codec)
        {
            Validate(codec);
            TemplateCodecs[codec.FileExtension] = codec;
        }

        public static bool TryResolveModule(string path, out ITriggerSourceCodec<TriggerAuthoringSourceDocument> codec)
        {
            return ModuleCodecs.TryGetValue(GetExtension(path), out codec);
        }

        public static bool TryResolveTemplate(string path, out ITriggerSourceCodec<TriggerAuthoringTemplateSourceDocument> codec)
        {
            return TemplateCodecs.TryGetValue(GetExtension(path), out codec);
        }

        public static string DescribeModuleExtensions()
        {
            return DescribeExtensions(ModuleCodecs.Keys);
        }

        public static string DescribeTemplateExtensions()
        {
            return DescribeExtensions(TemplateCodecs.Keys);
        }

        /// <summary>恢复为仅 JSON 的默认注册；测试清理自定义 codec 时使用。</summary>
        public static void ResetToDefaults()
        {
            ModuleCodecs.Clear();
            TemplateCodecs.Clear();
            _moduleDefault = new TriggerSourceModuleJsonCodec();
            _templateDefault = new TriggerSourceTemplateJsonCodec();
            ModuleCodecs[_moduleDefault.FileExtension] = _moduleDefault;
            TemplateCodecs[_templateDefault.FileExtension] = _templateDefault;
        }

        private static void Validate<TDocument>(ITriggerSourceCodec<TDocument> codec)
        {
            if (codec == null) throw new ArgumentNullException(nameof(codec));
            if (string.IsNullOrWhiteSpace(codec.FormatId))
                throw new ArgumentException("Codec FormatId is required.", nameof(codec));
            if (string.IsNullOrWhiteSpace(codec.FileExtension) || codec.FileExtension.Contains("."))
                throw new ArgumentException("Codec FileExtension must not be empty or contain dots.", nameof(codec));
        }

        private static string GetExtension(string path)
        {
            var extension = Path.GetExtension(path ?? string.Empty);
            return string.IsNullOrEmpty(extension) ? string.Empty : extension.TrimStart('.').ToLowerInvariant();
        }

        private static string DescribeExtensions(IEnumerable<string> extensions)
        {
            var list = new List<string>(extensions);
            list.Sort(StringComparer.Ordinal);
            return string.Join(", ", list);
        }
    }

    /// <summary>
    /// 与格式无关的共享管线：
    /// - 内容哈希基于固定的 DOM 规范投影（camelCase、忽略 null、无缩进、字符串枚举的 JSON 形态），
    ///   不随 codec 变化。同一内容换格式导出/导入，基线哈希不变，不产生假冲突；
    /// - 原子写入只关心"临时文件 + 原子替换"，与序列化格式无关。
    /// </summary>
    internal static class TriggerSourceCanonical
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static string ComputeContentHash(object document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var canonical = Newtonsoft.Json.JsonConvert.SerializeObject(
                document,
                TriggerSourceJson.CreateSettings(Newtonsoft.Json.Formatting.None));
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Utf8WithoutBom.GetBytes(canonical));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public static void WriteTextAtomic(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Source path is required.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Source directory could not be resolved.");
            Directory.CreateDirectory(directory);

            var temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporaryPath, content, Utf8WithoutBom);
                if (File.Exists(fullPath)) File.Replace(temporaryPath, fullPath, null);
                else File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
#endif
