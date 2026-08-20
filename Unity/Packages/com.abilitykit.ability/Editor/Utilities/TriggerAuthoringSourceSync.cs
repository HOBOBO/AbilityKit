#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal enum TriggerAuthoringSyncState
    {
        Untracked = 0,
        InSync = 1,
        AssetChanged = 2,
        JsonChanged = 3,
        Conflict = 4,
        SourceMissing = 5,
        InvalidSource = 6
    }

    internal sealed class TriggerAuthoringSyncInspection
    {
        public TriggerAuthoringSyncState State;
        public string SourcePath;
        public bool SourceExists;
        public string AssetHash;
        public string SourceHash;
        public string Error;
    }

    internal sealed class TriggerAuthoringSyncResult
    {
        public bool Success;
        public TriggerAuthoringSyncState State;
        public string Message;
        public string ContentHash;
        public bool CanForce;

        public static TriggerAuthoringSyncResult Succeeded(TriggerAuthoringSyncState state, string hash)
        {
            return new TriggerAuthoringSyncResult
            {
                Success = true,
                State = state,
                ContentHash = hash,
                Message = string.Empty
            };
        }

        public static TriggerAuthoringSyncResult Failed(
            TriggerAuthoringSyncState state,
            string message,
            bool canForce = false)
        {
            return new TriggerAuthoringSyncResult
            {
                Success = false,
                State = state,
                Message = message ?? string.Empty,
                ContentHash = string.Empty,
                CanForce = canForce
            };
        }
    }

    internal static class TriggerAuthoringSourceCodec
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        private static readonly JsonSerializerSettings WriteSettings = CreateSettings(Formatting.Indented);
        private static readonly JsonSerializerSettings CanonicalSettings = CreateSettings(Formatting.None);
        private static readonly JsonSerializerSettings ReadSettings = CreateSettings(Formatting.None, true);

        public static TriggerAuthoringSourceDocument CreateDocument(TriggerAuthoringModuleAsset asset)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            return new TriggerAuthoringSourceDocument
            {
                Schema = TriggerAuthoringSchema.Id,
                Version = TriggerAuthoringSchema.Version,
                Metadata = asset.Metadata ?? new TriggerAuthoringSourceMetadata(),
                Module = asset.Module ?? new TriggerAuthoringModuleData()
            };
        }

        public static string Serialize(TriggerAuthoringSourceDocument document)
        {
            ValidateDocumentHeader(document);
            return JsonConvert.SerializeObject(document, WriteSettings) + Environment.NewLine;
        }

        public static TriggerAuthoringSourceDocument Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("Trigger authoring Source JSON is empty.");

            TriggerAuthoringSourceDocument document;
            try
            {
                var root = JObject.Parse(json);
                RequireProperty(root, "schema");
                RequireProperty(root, "version");
                RequireProperty(root, "module");
                document = JsonConvert.DeserializeObject<TriggerAuthoringSourceDocument>(json, ReadSettings);
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException("Trigger authoring Source JSON is invalid: " + ex.Message, ex);
            }

            ValidateDocumentHeader(document);
            return document;
        }

        private static void RequireProperty(JObject root, string propertyName)
        {
            if (root.Property(propertyName, StringComparison.Ordinal) == null)
                throw new InvalidDataException($"Trigger authoring Source JSON requires property '{propertyName}'.");
        }

        public static TriggerAuthoringSourceDocument ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Source path is required.", nameof(path));
            return Deserialize(File.ReadAllText(path, Encoding.UTF8));
        }

        public static string ComputeContentHash(TriggerAuthoringSourceDocument document)
        {
            ValidateDocumentHeader(document);
            var canonicalJson = JsonConvert.SerializeObject(document, CanonicalSettings);
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Utf8WithoutBom.GetBytes(canonicalJson));
                var builder = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2"));
                return builder.ToString();
            }
        }

        public static void WriteFileAtomic(string path, TriggerAuthoringSourceDocument document)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Source path is required.", nameof(path));
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Source directory could not be resolved.");
            Directory.CreateDirectory(directory);

            var temporaryPath = Path.Combine(directory, "." + Path.GetFileName(fullPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporaryPath, Serialize(document), Utf8WithoutBom);
                if (File.Exists(fullPath))
                    File.Replace(temporaryPath, fullPath, null);
                else
                    File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        private static JsonSerializerSettings CreateSettings(Formatting formatting, bool strict = false)
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

        private static void ValidateDocumentHeader(TriggerAuthoringSourceDocument document)
        {
            if (document == null) throw new InvalidDataException("Trigger authoring Source document is null.");
            if (!string.Equals(document.Schema, TriggerAuthoringSchema.Id, StringComparison.Ordinal))
                throw new InvalidDataException($"Unsupported trigger authoring schema: '{document.Schema ?? string.Empty}'.");
            if (!string.Equals(document.Version, TriggerAuthoringSchema.Version, StringComparison.Ordinal))
                throw new InvalidDataException($"Unsupported trigger authoring version: '{document.Version ?? string.Empty}'.");
            if (document.Module == null) throw new InvalidDataException("Trigger authoring Source document has no module.");
        }
    }

    internal static class TriggerAuthoringSourceSync
    {
        public static TriggerAuthoringSyncInspection Inspect(TriggerAuthoringModuleAsset asset, string sourcePath = null)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            var assetHash = TriggerAuthoringSourceCodec.ComputeContentHash(TriggerAuthoringSourceCodec.CreateDocument(asset));
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
                var source = TriggerAuthoringSourceCodec.ReadFile(sourcePath);
                inspection.SourceHash = TriggerAuthoringSourceCodec.ComputeContentHash(source);
            }
            catch (Exception ex)
            {
                inspection.State = TriggerAuthoringSyncState.InvalidSource;
                inspection.Error = ex.Message;
                return inspection;
            }

            var baseline = asset.LastSynchronizedHash;
            if (string.IsNullOrEmpty(baseline))
            {
                inspection.State = TriggerAuthoringSyncState.Untracked;
                return inspection;
            }

            var assetChanged = !string.Equals(assetHash, baseline, StringComparison.Ordinal);
            var sourceChanged = !string.Equals(inspection.SourceHash, baseline, StringComparison.Ordinal);
            if (assetChanged && sourceChanged) inspection.State = TriggerAuthoringSyncState.Conflict;
            else if (assetChanged) inspection.State = TriggerAuthoringSyncState.AssetChanged;
            else if (sourceChanged) inspection.State = TriggerAuthoringSyncState.JsonChanged;
            else inspection.State = TriggerAuthoringSyncState.InSync;
            return inspection;
        }

        public static TriggerAuthoringSyncResult Export(
            TriggerAuthoringModuleAsset asset,
            string sourcePath = null,
            bool force = false)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            if (string.IsNullOrWhiteSpace(sourcePath))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.Untracked, "Source JSON path is required.");

            var diagnostics = TriggerAuthoringValidator.Validate(asset.Module);
            if (TriggerAuthoringValidator.HasErrors(diagnostics))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.AssetChanged, BuildValidationMessage(diagnostics));

            var inspection = Inspect(asset, sourcePath);
            if (!force && inspection.State == TriggerAuthoringSyncState.Untracked && inspection.SourceExists)
                return TriggerAuthoringSyncResult.Failed(inspection.State, "Existing untracked Source JSON would be overwritten. Import it or force export.", true);
            if (!force && (inspection.State == TriggerAuthoringSyncState.JsonChanged ||
                           inspection.State == TriggerAuthoringSyncState.Conflict ||
                           inspection.State == TriggerAuthoringSyncState.InvalidSource))
                return TriggerAuthoringSyncResult.Failed(inspection.State, inspection.Error ?? "Source JSON contains changes that would be overwritten.", true);

            var document = TriggerAuthoringSourceCodec.CreateDocument(asset);
            TriggerAuthoringSourceCodec.WriteFileAtomic(sourcePath, document);
            var hash = TriggerAuthoringSourceCodec.ComputeContentHash(document);
            asset.MarkSynchronized(NormalizePath(sourcePath), hash);
            EditorUtility.SetDirty(asset);
            return TriggerAuthoringSyncResult.Succeeded(TriggerAuthoringSyncState.InSync, hash);
        }

        public static TriggerAuthoringSyncResult Import(
            TriggerAuthoringModuleAsset asset,
            string sourcePath = null,
            bool force = false)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            sourcePath = ResolveSourcePath(asset, sourcePath);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.SourceMissing, "Source JSON file does not exist.");

            TriggerAuthoringSourceDocument document;
            try
            {
                document = TriggerAuthoringSourceCodec.ReadFile(sourcePath);
            }
            catch (Exception ex)
            {
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.InvalidSource, ex.Message);
            }

            var currentModuleId = asset.Module != null ? asset.Module.ModuleId : null;
            var incomingModuleId = document.Module.ModuleId;
            if (!string.IsNullOrWhiteSpace(currentModuleId) &&
                !string.Equals(currentModuleId, incomingModuleId, StringComparison.Ordinal))
            {
                return TriggerAuthoringSyncResult.Failed(
                    TriggerAuthoringSyncState.Conflict,
                    $"Module identity mismatch. Asset='{currentModuleId}', Source='{incomingModuleId ?? string.Empty}'.");
            }

            var diagnostics = TriggerAuthoringValidator.Validate(document.Module);
            if (TriggerAuthoringValidator.HasErrors(diagnostics))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.InvalidSource, BuildValidationMessage(diagnostics));

            var inspection = Inspect(asset, sourcePath);
            if (!force && inspection.State == TriggerAuthoringSyncState.Untracked && HasAuthoredContent(asset.Module))
                return TriggerAuthoringSyncResult.Failed(inspection.State, "Untracked Asset content would be overwritten. Use force import after reviewing the Source JSON.", true);
            if (!force && (inspection.State == TriggerAuthoringSyncState.AssetChanged || inspection.State == TriggerAuthoringSyncState.Conflict))
                return TriggerAuthoringSyncResult.Failed(inspection.State, "Asset contains changes that would be overwritten.", true);

            Undo.RecordObject(asset, "Import Trigger Authoring Source JSON");
            asset.Metadata = document.Metadata ?? new TriggerAuthoringSourceMetadata();
            asset.Module = document.Module;
            var hash = TriggerAuthoringSourceCodec.ComputeContentHash(document);
            asset.MarkSynchronized(NormalizePath(sourcePath), hash);
            EditorUtility.SetDirty(asset);
            return TriggerAuthoringSyncResult.Succeeded(TriggerAuthoringSyncState.InSync, hash);
        }

        private static bool HasAuthoredContent(TriggerAuthoringModuleData module)
        {
            return module != null &&
                   (!string.IsNullOrWhiteSpace(module.ModuleId) ||
                    (module.Blackboard != null && module.Blackboard.Count > 0) ||
                    (module.Triggers != null && module.Triggers.Count > 0));
        }

        private static string BuildValidationMessage(IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            var builder = new StringBuilder("Trigger authoring validation failed:");
            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];
                if (diagnostic.Severity != TriggerAuthoringDiagnosticSeverity.Error) continue;
                builder.AppendLine();
                builder.Append(diagnostic.Code).Append(" ").Append(diagnostic.Path).Append(": ").Append(diagnostic.Message);
            }
            return builder.ToString();
        }

        private static string ResolveSourcePath(TriggerAuthoringModuleAsset asset, string sourcePath)
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
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) return path;
            return path + Path.DirectorySeparatorChar;
        }
    }
}
#endif
