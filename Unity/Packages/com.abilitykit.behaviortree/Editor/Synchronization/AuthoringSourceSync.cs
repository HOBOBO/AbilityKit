using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Export;
using AbilityKit.Editor.Platform.Synchronization;

using UnityEngine.Scripting.APIUpdating;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
namespace AbilityKit.BehaviorTree.Editor
{
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringSyncState")]
    public enum AuthoringSyncState
    {
        InSync = 0,
        AssetChanged = 1,
        JsonChanged = 2,
        Conflict = 3,
        InvalidSource = 4,
        Untracked = 5,
        SourceMissing = 6,
    }

    /// <summary>授权资产 ↔ 外部授权 JSON 源文件的同步状态与操作结果。</summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringSyncResult")]
    public sealed class AuthoringSyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public bool CanForce { get; set; }

        public static AuthoringSyncResult Ok(string message) => new() { Success = true, Message = message };
        public static AuthoringSyncResult Fail(string message, bool canForce = false)
            => new() { Success = false, Message = message, CanForce = canForce };
    }

    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringSyncInspection")]
    public sealed class AuthoringSyncInspection
    {
        public AuthoringSyncState State { get; set; }
        public string SourcePath { get; set; } = "";
        internal EditorSourceSyncInspection PlatformInspection { get; set; }
    }

    /// <summary>
    /// 授权源同步：资产与外部 authoring JSON 是同一文档的两种载体。
    /// 比较规范化后的文档语义，避免缩进、换行或属性顺序制造伪冲突。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringSourceSync")]
    public static class AuthoringSourceSync
    {
        public static AuthoringSyncInspection Inspect(AuthoringAsset asset)
            => Inspect(asset, asset?.SourceJsonPath ?? "");

        /// <summary>检查指定源路径；用于导入/导出新路径时避免错误复用旧绑定的基线。</summary>
        public static AuthoringSyncInspection Inspect(AuthoringAsset asset, string path)
        {
            var inspection = new AuthoringSyncInspection { SourcePath = path ?? "" };
            if (asset == null)
            {
                return CompleteInspection(
                    inspection,
                    localHash: string.Empty,
                    sourceHash: string.Empty,
                    baselineHash: string.Empty,
                    isTracked: true,
                    sourceExists: true,
                    sourceIsValid: false,
                    error: "Asset is null.");
            }

            var assetHash = HashDocument(asset.LoadDocument());
            if (string.IsNullOrWhiteSpace(path))
            {
                return CompleteInspection(
                    inspection,
                    assetHash,
                    string.Empty,
                    string.Empty,
                    isTracked: false,
                    sourceExists: false);
            }

            string resolved;
            try
            {
                resolved = ResolvePath(path);
            }
            catch (Exception ex)
            {
                return CompleteInspection(
                    inspection,
                    assetHash,
                    string.Empty,
                    asset.LastSynchronizedHash,
                    isTracked: true,
                    sourceExists: true,
                    sourceIsValid: false,
                    error: ex.Message);
            }

            var isBoundPath = PathsEqual(path, asset.SourceJsonPath);
            var baseline = isBoundPath ? asset.LastSynchronizedHash : string.Empty;
            if (!File.Exists(resolved))
            {
                return CompleteInspection(
                    inspection,
                    assetHash,
                    string.Empty,
                    baseline,
                    // A concrete requested path is tracked for availability even before a
                    // successful baseline exists, so the platform can distinguish missing
                    // source from an untracked but existing source.
                    isTracked: true,
                    sourceExists: false);
            }

            string fileHash;
            try
            {
                fileHash = HashDocument(AuthoringJson.Load(File.ReadAllText(resolved)));
            }
            catch (Exception ex)
            {
                return CompleteInspection(
                    inspection,
                    assetHash,
                    string.Empty,
                    baseline,
                    isTracked: true,
                    sourceExists: true,
                    sourceIsValid: false,
                    error: ex.Message);
            }

            return CompleteInspection(
                inspection,
                assetHash,
                fileHash,
                baseline,
                isTracked: true,
                sourceExists: true);
        }

        public static AuthoringSyncResult Import(AuthoringAsset asset, string path, bool force = false)
        {
            if (asset == null) return AuthoringSyncResult.Fail("Asset is null.");
            string resolved;
            try
            {
                resolved = ResolvePath(path);
            }
            catch (Exception ex)
            {
                return AuthoringSyncResult.Fail($"Invalid source path: {ex.Message}");
            }
            if (!File.Exists(resolved)) return AuthoringSyncResult.Fail($"Source file not found: {resolved}");

            var inspection = Inspect(asset, path);
            var assessment = AssessOperation(
                inspection,
                EditorSourceSyncDirection.Import,
                HasAuthoredContent(asset.LoadDocument()));
            if (!force && assessment.RequiresForce)
            {
                return AuthoringSyncResult.Fail(
                    "The asset contains changes that are not present in the source. Force import will overwrite them.",
                    canForce: true);
            }

            var fileContent = File.ReadAllText(resolved);
            AuthoringSourceDocument document;
            try
            {
                document = AuthoringJson.Load(fileContent);
            }
            catch (Exception ex)
            {
                return AuthoringSyncResult.Fail($"Invalid source JSON: {ex.Message}");
            }

            asset.SaveDocument(document);
            asset.MarkSynchronized(path, HashDocument(document));
            return AuthoringSyncResult.Ok("Imported.");
        }

        public static AuthoringSyncResult Export(AuthoringAsset asset, string path, bool force = false)
        {
            if (asset == null) return AuthoringSyncResult.Fail("Asset is null.");
            string resolved;
            try
            {
                resolved = ResolvePath(path);
            }
            catch (Exception ex)
            {
                return AuthoringSyncResult.Fail($"Invalid source path: {ex.Message}");
            }

            if (!force && File.Exists(resolved))
            {
                var inspection = Inspect(asset, path);
                var assessment = AssessOperation(inspection, EditorSourceSyncDirection.Export);
                if (assessment.RequiresForce)
                {
                    return AuthoringSyncResult.Fail(
                        "The source contains changes that are not present in the asset. Force export will overwrite them.",
                        canForce: true);
                }
            }

            var document = asset.LoadDocument();
            var json = AuthoringJson.Save(document);
            try
            {
                EditorAtomicFileWriter.WriteAllText(resolved, json);
            }
            catch (Exception ex)
            {
                return AuthoringSyncResult.Fail($"Unable to write source JSON: {ex.Message}");
            }
            asset.MarkSynchronized(path, HashDocument(document));
            return AuthoringSyncResult.Ok("Exported.");
        }

        private static EditorSourceSyncOperationAssessment AssessOperation(
            AuthoringSyncInspection inspection,
            EditorSourceSyncDirection direction,
            bool localHasAuthoredContent = false)
        {
            return EditorSourceSyncOperationPolicy.Assess(
                inspection.PlatformInspection,
                direction,
                localHasAuthoredContent);
        }

        private static bool HasAuthoredContent(AuthoringSourceDocument document)
        {
            if (document == null) return false;
            return document.Tree?.Nodes?.Count > 0
                || document.Tree?.Blackboard?.Keys?.Count > 0
                || document.NodeMetadata?.Count > 0
                || document.Layout?.Count > 0
                || document.Groups?.Count > 0
                || document.Notes?.Count > 0
                || !string.IsNullOrWhiteSpace(document.Metadata?.Description);
        }

        private static AuthoringSyncInspection CompleteInspection(
            AuthoringSyncInspection inspection,
            string localHash,
            string sourceHash,
            string baselineHash,
            bool isTracked,
            bool sourceExists,
            bool sourceIsValid = true,
            string error = null)
        {
            inspection.PlatformInspection = EditorSourceSyncClassifier.Inspect(
                new EditorSourceSyncSnapshot(
                    localHash,
                    sourceHash,
                    baselineHash,
                    isTracked,
                    sourceExists,
                    sourceIsValid,
                    inspection.SourcePath,
                    error));
            inspection.State = MapState(inspection.PlatformInspection.State);
            return inspection;
        }

        private static AuthoringSyncState MapState(EditorSourceSyncState state)
        {
            return state switch
            {
                EditorSourceSyncState.InSync => AuthoringSyncState.InSync,
                EditorSourceSyncState.LocalChanged => AuthoringSyncState.AssetChanged,
                EditorSourceSyncState.SourceChanged => AuthoringSyncState.JsonChanged,
                EditorSourceSyncState.Conflict => AuthoringSyncState.Conflict,
                EditorSourceSyncState.Untracked => AuthoringSyncState.Untracked,
                EditorSourceSyncState.SourceMissing => AuthoringSyncState.SourceMissing,
                EditorSourceSyncState.InvalidSource => AuthoringSyncState.InvalidSource,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown source sync state.")
            };
        }

        public static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
                ?? UnityEngine.Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            var comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            try
            {
                return string.Equals(ResolvePath(left), ResolvePath(right), comparison);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string HashDocument(AuthoringSourceDocument document)
            => Hash(AuthoringJson.Save(document));

        private static string Hash(string content)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
        }
    }
}
