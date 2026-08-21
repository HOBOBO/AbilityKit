#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// 模板 Source 读写的外观层：保持既有调用面不变，内部委托给
    /// <see cref="TriggerSourceCodecs"/> 注册的 codec——按路径扩展名解析格式，默认 JSON。
    /// </summary>
    internal static class TriggerAuthoringTemplateSourceCodec
    {
        public static TriggerAuthoringTemplateSourceDocument CreateDocument(TriggerAuthoringTemplateAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return new TriggerAuthoringTemplateSourceDocument
            {
                Schema = TriggerAuthoringSchema.Id,
                Version = TriggerAuthoringSchema.Version,
                Metadata = asset.Metadata ?? new TriggerAuthoringSourceMetadata(),
                Template = asset.Template ?? new TriggerAuthoringTemplateData()
            };
        }

        public static string Serialize(TriggerAuthoringTemplateSourceDocument document)
        {
            return TriggerSourceCodecs.TemplateDefault.Serialize(document);
        }

        public static TriggerAuthoringTemplateSourceDocument Deserialize(string json)
        {
            return TriggerSourceCodecs.TemplateDefault.Deserialize(json);
        }

        public static TriggerAuthoringTemplateSourceDocument ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Source path is required.", nameof(path));
            return ResolveCodec(path).Deserialize(File.ReadAllText(path, Encoding.UTF8));
        }

        public static string ComputeContentHash(TriggerAuthoringTemplateSourceDocument document)
        {
            TriggerSourceDocumentRules.ValidateTemplateHeader(document);
            return TriggerSourceCanonical.ComputeContentHash(document);
        }

        public static void WriteFileAtomic(string path, TriggerAuthoringTemplateSourceDocument document)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Source path is required.", nameof(path));
            TriggerSourceCanonical.WriteTextAtomic(path, ResolveCodec(path).Serialize(document));
        }

        private static ITriggerSourceCodec<TriggerAuthoringTemplateSourceDocument> ResolveCodec(string path)
        {
            if (!TriggerSourceCodecs.TryResolveTemplate(path, out var codec))
                throw new InvalidDataException(
                    "No Trigger Source codec is registered for extension '" +
                    (Path.GetExtension(path) ?? string.Empty) +
                    "'. Supported: " + TriggerSourceCodecs.DescribeTemplateExtensions() + ".");
            return codec;
        }
    }

    internal static class TriggerAuthoringTemplateSourceSync
    {
        public static TriggerAuthoringSyncInspection Inspect(TriggerAuthoringTemplateAsset asset, string sourcePath = null)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            var assetHash = TriggerAuthoringTemplateSourceCodec.ComputeContentHash(
                TriggerAuthoringTemplateSourceCodec.CreateDocument(asset));
            var inspection = new TriggerAuthoringSyncInspection
            {
                SourcePath = sourcePath,
                AssetHash = assetHash,
                State = TriggerAuthoringSyncState.Untracked
            };

            if (string.IsNullOrWhiteSpace(sourcePath)) return inspection;
            inspection.SourceExists = File.Exists(sourcePath);
            if (!inspection.SourceExists)
            {
                inspection.State = string.IsNullOrEmpty(asset.LastSynchronizedHash)
                    ? TriggerAuthoringSyncState.Untracked
                    : TriggerAuthoringSyncState.SourceMissing;
                return inspection;
            }

            try
            {
                inspection.SourceHash = TriggerAuthoringTemplateSourceCodec.ComputeContentHash(
                    TriggerAuthoringTemplateSourceCodec.ReadFile(sourcePath));
            }
            catch (Exception ex)
            {
                inspection.State = TriggerAuthoringSyncState.InvalidSource;
                inspection.Error = ex.Message;
                return inspection;
            }

            var baseline = asset.LastSynchronizedHash;
            if (string.IsNullOrEmpty(baseline)) return inspection;
            var assetChanged = !string.Equals(assetHash, baseline, StringComparison.Ordinal);
            var sourceChanged = !string.Equals(inspection.SourceHash, baseline, StringComparison.Ordinal);
            if (assetChanged && sourceChanged) inspection.State = TriggerAuthoringSyncState.Conflict;
            else if (assetChanged) inspection.State = TriggerAuthoringSyncState.AssetChanged;
            else if (sourceChanged) inspection.State = TriggerAuthoringSyncState.JsonChanged;
            else inspection.State = TriggerAuthoringSyncState.InSync;
            return inspection;
        }

        public static TriggerAuthoringSyncResult Export(
            TriggerAuthoringTemplateAsset asset,
            string sourcePath = null,
            bool force = false)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.Untracked, "Source JSON path is required.");

            var diagnostics = TriggerAuthoringTemplateValidator.Validate(
                asset.Template,
                TriggerAuthoringValidationContext.Create(asset));
            if (TriggerAuthoringValidator.HasErrors(diagnostics))
                return TriggerAuthoringSyncResult.Failed(
                    TriggerAuthoringSyncState.AssetChanged,
                    TriggerAuthoringTemplateValidator.BuildMessage(diagnostics));

            var inspection = Inspect(asset, sourcePath);
            if (!force && inspection.State == TriggerAuthoringSyncState.Untracked && inspection.SourceExists)
                return TriggerAuthoringSyncResult.Failed(inspection.State, "Existing untracked Source JSON would be overwritten.", true);
            if (!force && (inspection.State == TriggerAuthoringSyncState.JsonChanged ||
                           inspection.State == TriggerAuthoringSyncState.Conflict ||
                           inspection.State == TriggerAuthoringSyncState.InvalidSource))
                return TriggerAuthoringSyncResult.Failed(inspection.State, inspection.Error ?? "Source JSON contains changes that would be overwritten.", true);

            var document = TriggerAuthoringTemplateSourceCodec.CreateDocument(asset);
            TriggerAuthoringTemplateSourceCodec.WriteFileAtomic(sourcePath, document);
            var hash = TriggerAuthoringTemplateSourceCodec.ComputeContentHash(document);
            asset.MarkSynchronized(NormalizePath(sourcePath), hash);
            EditorUtility.SetDirty(asset);
            return TriggerAuthoringSyncResult.Succeeded(TriggerAuthoringSyncState.InSync, hash);
        }

        public static TriggerAuthoringSyncResult Import(
            TriggerAuthoringTemplateAsset asset,
            string sourcePath = null,
            bool force = false)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.SourceMissing, "Source JSON file does not exist.");

            TriggerAuthoringTemplateSourceDocument document;
            try
            {
                document = TriggerAuthoringTemplateSourceCodec.ReadFile(sourcePath);
            }
            catch (Exception ex)
            {
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.InvalidSource, ex.Message);
            }

            var currentId = asset.Template != null ? asset.Template.TemplateId : null;
            var incomingId = document.Template.TemplateId;
            if (!string.IsNullOrWhiteSpace(currentId) && !string.Equals(currentId, incomingId, StringComparison.Ordinal))
                return TriggerAuthoringSyncResult.Failed(
                    TriggerAuthoringSyncState.Conflict,
                    $"Template identity mismatch. Asset='{currentId}', Source='{incomingId ?? string.Empty}'.");

            var diagnostics = TriggerAuthoringTemplateValidator.Validate(
                document.Template,
                TriggerAuthoringValidationContext.Create(asset));
            if (TriggerAuthoringValidator.HasErrors(diagnostics))
                return TriggerAuthoringSyncResult.Failed(
                    TriggerAuthoringSyncState.InvalidSource,
                    TriggerAuthoringTemplateValidator.BuildMessage(diagnostics));

            var inspection = Inspect(asset, sourcePath);
            if (!force && inspection.State == TriggerAuthoringSyncState.Untracked && HasAuthoredContent(asset.Template))
                return TriggerAuthoringSyncResult.Failed(inspection.State, "Untracked Template Asset content would be overwritten.", true);
            if (!force && (inspection.State == TriggerAuthoringSyncState.AssetChanged || inspection.State == TriggerAuthoringSyncState.Conflict))
                return TriggerAuthoringSyncResult.Failed(inspection.State, "Template Asset contains changes that would be overwritten.", true);

            Undo.RecordObject(asset, "Import Trigger Template Source JSON");
            asset.Metadata = document.Metadata ?? new TriggerAuthoringSourceMetadata();
            asset.Template = document.Template;
            var hash = TriggerAuthoringTemplateSourceCodec.ComputeContentHash(document);
            asset.MarkSynchronized(NormalizePath(sourcePath), hash);
            EditorUtility.SetDirty(asset);
            return TriggerAuthoringSyncResult.Succeeded(TriggerAuthoringSyncState.InSync, hash);
        }

        private static bool HasAuthoredContent(TriggerAuthoringTemplateData template)
        {
            return template != null &&
                   (!string.IsNullOrWhiteSpace(template.TemplateId) ||
                    (template.Parameters != null && template.Parameters.Count > 0) ||
                    template.Condition != null || template.Actions != null);
        }

        private static string ResolveSourcePath(TriggerAuthoringTemplateAsset asset, string sourcePath)
        {
            if (!string.IsNullOrWhiteSpace(sourcePath)) return Path.GetFullPath(sourcePath);
            if (string.IsNullOrWhiteSpace(asset.SourceJsonPath)) return string.Empty;
            if (Path.IsPathRooted(asset.SourceJsonPath)) return Path.GetFullPath(asset.SourceJsonPath);
            return Path.GetFullPath(Path.Combine(GetProjectRoot(), asset.SourceJsonPath));
        }

        private static string NormalizePath(string sourcePath)
        {
            var fullPath = Path.GetFullPath(sourcePath);
            var projectRoot = AppendDirectorySeparator(Path.GetFullPath(GetProjectRoot()));
            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Replace('\\', '/');
            var rootUri = new Uri(projectRoot, UriKind.Absolute);
            var fileUri = new Uri(fullPath, UriKind.Absolute);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('\\', '/');
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
#endif
