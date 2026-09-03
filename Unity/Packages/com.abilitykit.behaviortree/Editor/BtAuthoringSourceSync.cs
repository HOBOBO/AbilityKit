using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.BehaviorTree.Authoring;
using AbilityKit.Editor.Platform.Export;
using AbilityKit.Editor.Platform.Synchronization;

namespace AbilityKit.BehaviorTree.Editor
{
    public enum BtAuthoringSyncState
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
    public sealed class BtAuthoringSyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public bool CanForce { get; set; }

        public static BtAuthoringSyncResult Ok(string message) => new() { Success = true, Message = message };
        public static BtAuthoringSyncResult Fail(string message, bool canForce = false)
            => new() { Success = false, Message = message, CanForce = canForce };
    }

    public sealed class BtAuthoringSyncInspection
    {
        public BtAuthoringSyncState State { get; set; }
        public string SourcePath { get; set; } = "";
        internal EditorSourceSyncInspection PlatformInspection { get; set; }
    }

    /// <summary>
    /// 授权源同步：资产与外部 authoring JSON 是同一文档的两种载体。
    /// 比较规范化后的文档语义，避免缩进、换行或属性顺序制造伪冲突。
    /// </summary>
    public static class BtAuthoringSourceSync
    {
        public static BtAuthoringSyncInspection Inspect(BtAuthoringAsset asset)
            => Inspect(asset, asset?.SourceJsonPath ?? "");

        /// <summary>检查指定源路径；用于导入/导出新路径时避免错误复用旧绑定的基线。</summary>
        public static BtAuthoringSyncInspection Inspect(BtAuthoringAsset asset, string path)
        {
            var inspection = new BtAuthoringSyncInspection { SourcePath = path ?? "" };
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
                fileHash = HashDocument(BtAuthoringJson.Load(File.ReadAllText(resolved)));
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

        public static BtAuthoringSyncResult Import(BtAuthoringAsset asset, string path, bool force = false)
        {
            if (asset == null) return BtAuthoringSyncResult.Fail("Asset is null.");
            string resolved;
            try
            {
                resolved = ResolvePath(path);
            }
            catch (Exception ex)
            {
                return BtAuthoringSyncResult.Fail($"Invalid source path: {ex.Message}");
            }
            if (!File.Exists(resolved)) return BtAuthoringSyncResult.Fail($"Source file not found: {resolved}");

            var inspection = Inspect(asset, path);
            var assessment = AssessOperation(
                inspection,
                EditorSourceSyncDirection.Import,
                HasAuthoredContent(asset.LoadDocument()));
            if (!force && assessment.RequiresForce)
            {
                return BtAuthoringSyncResult.Fail(
                    "The asset contains changes that are not present in the source. Force import will overwrite them.",
                    canForce: true);
            }

            var fileContent = File.ReadAllText(resolved);
            BtAuthoringSourceDocument document;
            try
            {
                document = BtAuthoringJson.Load(fileContent);
            }
            catch (Exception ex)
            {
                return BtAuthoringSyncResult.Fail($"Invalid source JSON: {ex.Message}");
            }

            asset.SaveDocument(document);
            asset.MarkSynchronized(path, HashDocument(document));
            return BtAuthoringSyncResult.Ok("Imported.");
        }

        public static BtAuthoringSyncResult Export(BtAuthoringAsset asset, string path, bool force = false)
        {
            if (asset == null) return BtAuthoringSyncResult.Fail("Asset is null.");
            string resolved;
            try
            {
                resolved = ResolvePath(path);
            }
            catch (Exception ex)
            {
                return BtAuthoringSyncResult.Fail($"Invalid source path: {ex.Message}");
            }

            if (!force && File.Exists(resolved))
            {
                var inspection = Inspect(asset, path);
                var assessment = AssessOperation(inspection, EditorSourceSyncDirection.Export);
                if (assessment.RequiresForce)
                {
                    return BtAuthoringSyncResult.Fail(
                        "The source contains changes that are not present in the asset. Force export will overwrite them.",
                        canForce: true);
                }
            }

            var document = asset.LoadDocument();
            var json = BtAuthoringJson.Save(document);
            try
            {
                EditorAtomicFileWriter.WriteAllText(resolved, json);
            }
            catch (Exception ex)
            {
                return BtAuthoringSyncResult.Fail($"Unable to write source JSON: {ex.Message}");
            }
            asset.MarkSynchronized(path, HashDocument(document));
            return BtAuthoringSyncResult.Ok("Exported.");
        }

        private static EditorSourceSyncOperationAssessment AssessOperation(
            BtAuthoringSyncInspection inspection,
            EditorSourceSyncDirection direction,
            bool localHasAuthoredContent = false)
        {
            return EditorSourceSyncOperationPolicy.Assess(
                inspection.PlatformInspection,
                direction,
                localHasAuthoredContent);
        }

        private static bool HasAuthoredContent(BtAuthoringSourceDocument document)
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

        private static BtAuthoringSyncInspection CompleteInspection(
            BtAuthoringSyncInspection inspection,
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

        private static BtAuthoringSyncState MapState(EditorSourceSyncState state)
        {
            return state switch
            {
                EditorSourceSyncState.InSync => BtAuthoringSyncState.InSync,
                EditorSourceSyncState.LocalChanged => BtAuthoringSyncState.AssetChanged,
                EditorSourceSyncState.SourceChanged => BtAuthoringSyncState.JsonChanged,
                EditorSourceSyncState.Conflict => BtAuthoringSyncState.Conflict,
                EditorSourceSyncState.Untracked => BtAuthoringSyncState.Untracked,
                EditorSourceSyncState.SourceMissing => BtAuthoringSyncState.SourceMissing,
                EditorSourceSyncState.InvalidSource => BtAuthoringSyncState.InvalidSource,
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

        private static string HashDocument(BtAuthoringSourceDocument document)
            => Hash(BtAuthoringJson.Save(document));

        private static string Hash(string content)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
        }
    }
}
