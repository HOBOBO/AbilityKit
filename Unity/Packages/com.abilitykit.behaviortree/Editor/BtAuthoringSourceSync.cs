using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AbilityKit.BehaviorTree.Authoring;

namespace AbilityKit.BehaviorTree.Editor
{
    public enum BtAuthoringSyncState
    {
        InSync = 0,
        AssetChanged = 1,
        JsonChanged = 2,
        Conflict = 3,
        InvalidSource = 4,
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
    }

    /// <summary>
    /// 授权源同步：资产内文档为编辑权威，外部 JSON 文件为协作/版本化载体。
    /// 双向比对（资产改动 / 文件改动 / 冲突 / 无效源），外部变更横幅据此提示 Import。
    /// </summary>
    public static class BtAuthoringSourceSync
    {
        public static BtAuthoringSyncInspection Inspect(BtAuthoringAsset asset)
        {
            var inspection = new BtAuthoringSyncInspection { SourcePath = asset.SourceJsonPath };

            if (string.IsNullOrWhiteSpace(asset.SourceJsonPath))
            {
                inspection.State = BtAuthoringSyncState.InSync;
                return inspection;
            }

            var resolved = ResolvePath(asset.SourceJsonPath);
            if (!File.Exists(resolved))
            {
                inspection.State = BtAuthoringSyncState.InvalidSource;
                return inspection;
            }

            var fileContent = File.ReadAllText(resolved);
            var fileHash = Hash(fileContent);

            if (string.Equals(asset.LastSynchronizedHash, fileHash, StringComparison.Ordinal))
            {
                inspection.State = BtAuthoringSyncState.InSync;
                return inspection;
            }

            var assetJson = BtAuthoringJson.Save(asset.LoadDocument());
            var assetHash = Hash(assetJson);

            if (string.Equals(fileHash, assetHash, StringComparison.Ordinal))
            {
                inspection.State = BtAuthoringSyncState.Conflict;
                return inspection;
            }

            // 资产未变但文件变 → 外部改动；资产变但文件未变 → 本地改动
            inspection.State = string.Equals(asset.LastSynchronizedHash, assetHash, StringComparison.Ordinal)
                ? BtAuthoringSyncState.JsonChanged
                : BtAuthoringSyncState.AssetChanged;
            return inspection;
        }

        public static BtAuthoringSyncResult Import(BtAuthoringAsset asset, string path, bool force = false)
        {
            if (asset == null) return BtAuthoringSyncResult.Fail("Asset is null.");
            var resolved = ResolvePath(path);
            if (!File.Exists(resolved)) return BtAuthoringSyncResult.Fail($"Source file not found: {resolved}");

            var inspection = Inspect(asset);
            if (!force && inspection.State == BtAuthoringSyncState.AssetChanged)
            {
                return BtAuthoringSyncResult.Fail(
                    "Asset has local unsaved changes. Force import will overwrite them.", canForce: true);
            }

            var fileContent = File.ReadAllText(resolved);
            try
            {
                asset.ImportJson(fileContent);
            }
            catch (Exception ex)
            {
                return BtAuthoringSyncResult.Fail($"Invalid source JSON: {ex.Message}");
            }

            asset.MarkSynchronized(path, Hash(fileContent));
            return BtAuthoringSyncResult.Ok("Imported.");
        }

        public static BtAuthoringSyncResult Export(BtAuthoringAsset asset, string path, bool force = false)
        {
            if (asset == null) return BtAuthoringSyncResult.Fail("Asset is null.");
            var resolved = ResolvePath(path);

            if (!force && File.Exists(resolved))
            {
                var inspection = Inspect(asset);
                if (inspection.State == BtAuthoringSyncState.JsonChanged)
                {
                    return BtAuthoringSyncResult.Fail(
                        "Source file has external changes. Force export will overwrite them.", canForce: true);
                }
            }

            var json = BtAuthoringJson.Save(asset.LoadDocument());
            File.WriteAllText(resolved, json);
            asset.MarkSynchronized(path, Hash(json));
            return BtAuthoringSyncResult.Ok("Exported.");
        }

        public static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;
            if (Path.IsPathRooted(path)) return Path.GetFullPath(path);
            var projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
                ?? UnityEngine.Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static string Hash(string content)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
        }
    }
}
