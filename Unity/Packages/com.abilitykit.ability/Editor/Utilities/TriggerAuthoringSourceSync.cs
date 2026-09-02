#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Editor.Platform.Synchronization;
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
        public EditorSourceSyncInspection PlatformInspection;
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
            return TriggerSourceCodecs.ModuleDefault.Serialize(document);
        }

        public static TriggerAuthoringSourceDocument Deserialize(string json)
        {
            return TriggerSourceCodecs.ModuleDefault.Deserialize(json);
        }

        public static TriggerAuthoringSourceDocument ReadFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Source path is required.", nameof(path));
            return ResolveCodec(path).Deserialize(File.ReadAllText(path, Encoding.UTF8));
        }

        public static string ComputeContentHash(TriggerAuthoringSourceDocument document)
        {
            TriggerSourceDocumentRules.ValidateModuleHeader(document);
            return TriggerSourceCanonical.ComputeContentHash(document);
        }

        public static void WriteFileAtomic(string path, TriggerAuthoringSourceDocument document)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Source path is required.", nameof(path));
            TriggerSourceCanonical.WriteTextAtomic(path, ResolveCodec(path).Serialize(document));
        }

        private static ITriggerSourceCodec<TriggerAuthoringSourceDocument> ResolveCodec(string path)
        {
            if (!TriggerSourceCodecs.TryResolveModule(path, out var codec))
                throw new InvalidDataException(
                    "No Trigger Source codec is registered for extension '" +
                    (Path.GetExtension(path) ?? string.Empty) +
                    "'. Supported: " + TriggerSourceCodecs.DescribeModuleExtensions() + ".");
            return codec;
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
            var sourceIsValid = true;
            if (inspection.SourceExists)
            {
                try
                {
                    var source = TriggerAuthoringSourceCodec.ReadFile(sourcePath);
                    inspection.SourceHash = TriggerAuthoringSourceCodec.ComputeContentHash(source);
                }
                catch (Exception ex)
                {
                    sourceIsValid = false;
                    inspection.Error = ex.Message;
                }
            }

            var platformInspection = EditorSourceSyncClassifier.Inspect(
                new EditorSourceSyncSnapshot(
                    assetHash,
                    inspection.SourceHash ?? string.Empty,
                    asset.LastSynchronizedHash ?? string.Empty,
                    isTracked: inspection.SourceExists || !string.IsNullOrEmpty(asset.LastSynchronizedHash),
                    sourceExists: inspection.SourceExists,
                    sourceIsValid: sourceIsValid,
                    sourcePath: sourcePath,
                    error: inspection.Error));
            inspection.PlatformInspection = platformInspection;
            inspection.State = MapState(platformInspection.State);
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

            var diagnostics = TriggerAuthoringValidator.Validate(
                asset.Module,
                TriggerAuthoringValidationContext.Create(asset));
            if (TriggerAuthoringValidator.HasErrors(diagnostics))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.AssetChanged, BuildValidationMessage(diagnostics));

            var inspection = Inspect(asset, sourcePath);
            var assessment = EditorSourceSyncOperationPolicy.Assess(
                inspection.PlatformInspection,
                EditorSourceSyncDirection.Export);
            if (!force && assessment.RequiresForce)
                return TriggerAuthoringSyncResult.Failed(
                    inspection.State,
                    inspection.Error ?? "Source JSON contains changes that would be overwritten.",
                    true);

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

            var diagnostics = TriggerAuthoringValidator.Validate(
                document.Module,
                TriggerAuthoringValidationContext.Create(asset));
            if (TriggerAuthoringValidator.HasErrors(diagnostics))
                return TriggerAuthoringSyncResult.Failed(TriggerAuthoringSyncState.InvalidSource, BuildValidationMessage(diagnostics));

            var inspection = Inspect(asset, sourcePath);
            var assessment = EditorSourceSyncOperationPolicy.Assess(
                inspection.PlatformInspection,
                EditorSourceSyncDirection.Import,
                HasAuthoredContent(asset.Module));
            if (!force && assessment.RequiresForce)
                return TriggerAuthoringSyncResult.Failed(
                    inspection.State,
                    "Asset contains changes that would be overwritten.",
                    true);

            Undo.RecordObject(asset, "Import Trigger Authoring Source JSON");
            asset.Metadata = document.Metadata ?? new TriggerAuthoringSourceMetadata();
            asset.Module = document.Module;
            var hash = TriggerAuthoringSourceCodec.ComputeContentHash(document);
            asset.MarkSynchronized(NormalizePath(sourcePath), hash);
            EditorUtility.SetDirty(asset);
            return TriggerAuthoringSyncResult.Succeeded(TriggerAuthoringSyncState.InSync, hash);
        }

        private static TriggerAuthoringSyncState MapState(EditorSourceSyncState state)
        {
            return state switch
            {
                EditorSourceSyncState.Untracked => TriggerAuthoringSyncState.Untracked,
                EditorSourceSyncState.InSync => TriggerAuthoringSyncState.InSync,
                EditorSourceSyncState.LocalChanged => TriggerAuthoringSyncState.AssetChanged,
                EditorSourceSyncState.SourceChanged => TriggerAuthoringSyncState.JsonChanged,
                EditorSourceSyncState.Conflict => TriggerAuthoringSyncState.Conflict,
                EditorSourceSyncState.SourceMissing => TriggerAuthoringSyncState.SourceMissing,
                EditorSourceSyncState.InvalidSource => TriggerAuthoringSyncState.InvalidSource,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown source sync state.")
            };
        }

        private static bool HasAuthoredContent(TriggerAuthoringModuleData module)
        {
            return module != null &&
                   (!string.IsNullOrWhiteSpace(module.ModuleId) ||
                    (module.Blackboard != null && module.Blackboard.Count > 0) ||
                    (module.ConditionGroups != null && module.ConditionGroups.Count > 0) ||
                    (module.ActionGroups != null && module.ActionGroups.Count > 0) ||
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
