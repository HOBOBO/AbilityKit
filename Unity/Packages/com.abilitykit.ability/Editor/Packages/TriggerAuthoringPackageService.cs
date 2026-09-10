#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Packages
{
    internal sealed class TriggerAuthoringPackageCreateRequest
    {
        internal TriggerAuthoringProjectAsset Project;
        internal string DomainId;
        internal string ContentKey;
        internal string DisplayName;
        internal string ModuleId;
        internal TriggerModuleKind Kind = TriggerModuleKind.Ability;
        internal string Owner;
        internal IReadOnlyList<string> Tags;
        internal string AssetDirectory;
    }

    internal sealed class TriggerAuthoringPackageCreatePlan
    {
        internal string Error;
        internal string DomainId;
        internal string ContentKey;
        internal string ModuleId;
        internal string AssetDirectory;
        internal string FileStem;
        internal bool IsValid => string.IsNullOrEmpty(Error);
    }

    internal sealed class TriggerAuthoringPackageCreateResult
    {
        internal TriggerAuthoringModuleAsset Package;
        internal string Error;
        internal bool Success => Package != null && string.IsNullOrEmpty(Error);
    }

    /// <summary>内容包创建的唯一写入口，集中维护目录、ID、项目成员关系和 Source 绑定。</summary>
    internal static class TriggerAuthoringPackageService
    {
        internal static TriggerAuthoringPackageCreatePlan BuildPlan(TriggerAuthoringPackageCreateRequest request)
        {
            var plan = new TriggerAuthoringPackageCreatePlan();
            if (request?.Project == null)
            {
                plan.Error = "必须选择触发器项目。";
                return plan;
            }

            plan.DomainId = NormalizeIdentifier(request.DomainId);
            plan.ContentKey = NormalizeIdentifier(request.ContentKey);
            if (string.IsNullOrEmpty(plan.DomainId)) plan.Error = "必须填写有效的业务域 ID。";
            else if (string.IsNullOrEmpty(plan.ContentKey)) plan.Error = "必须填写有效的内容标识。";
            else if (string.IsNullOrWhiteSpace(request.DisplayName)) plan.Error = "必须填写内容包名称。";
            if (!string.IsNullOrEmpty(plan.Error)) return plan;

            var requestedId = NormalizeIdentifier(request.ModuleId);
            var baseId = string.IsNullOrEmpty(requestedId)
                ? BuildModuleId(plan.DomainId, plan.ContentKey)
                : requestedId;
            plan.ModuleId = CreateUniqueModuleId(request.Project, baseId);
            plan.AssetDirectory = NormalizeAssetDirectory(
                string.IsNullOrWhiteSpace(request.AssetDirectory)
                    ? GetDefaultAssetDirectory(request.Project, plan.DomainId, plan.ContentKey)
                    : request.AssetDirectory);
            plan.FileStem = SanitizeFileName(plan.ModuleId.Replace('.', '_'));

            if (!string.Equals(plan.AssetDirectory, "Assets", StringComparison.Ordinal) &&
                !plan.AssetDirectory.StartsWith("Assets/", StringComparison.Ordinal))
                plan.Error = "内容包必须保存在当前 Unity 工程的 Assets 目录中。";
            else if (ContainsContentIdentity(request.Project, plan.DomainId, plan.ContentKey))
                plan.Error = $"业务域“{plan.DomainId}”中已经存在内容标识“{plan.ContentKey}”。";
            return plan;
        }

        internal static TriggerAuthoringPackageCreateResult Create(TriggerAuthoringPackageCreateRequest request)
        {
            var plan = BuildPlan(request);
            if (!plan.IsValid) return new TriggerAuthoringPackageCreateResult { Error = plan.Error };

            TriggerAuthoringModuleAsset asset = null;
            string assetPath = null;
            string sourceAssetPath = null;
            string sourcePath = null;
            try
            {
                EnsureAssetFolder(plan.AssetDirectory);
                assetPath = AssetDatabase.GenerateUniqueAssetPath(
                    plan.AssetDirectory + "/" + plan.FileStem + ".Module.asset");
                sourceAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                    plan.AssetDirectory + "/" + plan.FileStem + ".trigger.json");
                sourcePath = ToAbsoluteProjectPath(sourceAssetPath);

                asset = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
                asset.name = request.DisplayName.Trim();
                asset.Module = new TriggerAuthoringModuleData
                {
                    ModuleId = plan.ModuleId,
                    DisplayName = request.DisplayName.Trim(),
                    Kind = request.Kind
                };
                var metadata = new TriggerAuthoringPackageMetadata();
                metadata.SetIdentity(plan.DomainId, plan.ContentKey);
                metadata.SetOwner(request.Owner);
                metadata.SetTags(request.Tags);
                asset.SetPackageMetadata(metadata);

                AssetDatabase.CreateAsset(asset, assetPath);
                TriggerAuthoringProjectMembership.Assign(asset, request.Project);
                var sync = TriggerAuthoringSourceSync.Export(asset, sourcePath);
                if (!sync.Success) throw new InvalidOperationException(sync.Message);

                EditorUtility.SetDirty(asset);
                EditorUtility.SetDirty(request.Project);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return new TriggerAuthoringPackageCreateResult { Package = asset };
            }
            catch (Exception ex)
            {
                if (asset != null) TriggerAuthoringProjectMembership.Detach(asset);
                if (!string.IsNullOrEmpty(assetPath)) AssetDatabase.DeleteAsset(assetPath);
                if (!string.IsNullOrEmpty(sourceAssetPath) && !AssetDatabase.DeleteAsset(sourceAssetPath) &&
                    !string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath)) File.Delete(sourcePath);
                EditorUtility.SetDirty(request.Project);
                return new TriggerAuthoringPackageCreateResult { Error = ex.Message };
            }
        }

        internal static string CreateUniqueModuleId(TriggerAuthoringProjectAsset project, string requestedId)
        {
            var stem = NormalizeIdentifier(requestedId);
            if (string.IsNullOrEmpty(stem)) return string.Empty;
            var candidate = stem;
            var suffix = 2;
            while (ContainsModuleId(project, candidate)) candidate = stem + "." + suffix++;
            return candidate;
        }

        internal static string BuildModuleId(string domainId, string contentKey)
        {
            domainId = NormalizeIdentifier(domainId);
            contentKey = NormalizeIdentifier(contentKey);
            if (string.IsNullOrEmpty(domainId)) return contentKey;
            if (string.IsNullOrEmpty(contentKey)) return domainId;
            return contentKey.StartsWith(domainId + ".", StringComparison.OrdinalIgnoreCase)
                ? contentKey
                : domainId + "." + contentKey;
        }

        internal static string NormalizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var builder = new StringBuilder(value.Length);
            var separatorPending = false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    if (separatorPending && builder.Length > 0) builder.Append('.');
                    builder.Append(c);
                    separatorPending = false;
                }
                else if (c == '.' || c == '-' || c == '/' || char.IsWhiteSpace(c))
                {
                    separatorPending = builder.Length > 0;
                }
            }
            return builder.ToString().Trim('.');
        }

        internal static string GetDefaultAssetDirectory(
            TriggerAuthoringProjectAsset project,
            string domainId,
            string contentKey)
        {
            var projectPath = project != null ? AssetDatabase.GetAssetPath(project) : string.Empty;
            var root = string.IsNullOrEmpty(projectPath)
                ? "Assets"
                : Path.GetDirectoryName(projectPath)?.Replace('\\', '/') ?? "Assets";
            var domainFolder = SanitizeFileName(NormalizeIdentifier(domainId).Replace('.', '_'));
            var contentFolder = SanitizeFileName(NormalizeIdentifier(contentKey).Replace('.', '_'));
            return root + "/Triggers/" + domainFolder + "/" + contentFolder;
        }

        private static bool ContainsModuleId(TriggerAuthoringProjectAsset project, string moduleId)
        {
            var modules = project?.Modules;
            if (modules == null) return false;
            for (var i = 0; i < modules.Count; i++)
                if (string.Equals(modules[i]?.Module?.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static bool ContainsContentIdentity(
            TriggerAuthoringProjectAsset project,
            string domainId,
            string contentKey)
        {
            var modules = project?.Modules;
            if (modules == null) return false;
            for (var i = 0; i < modules.Count; i++)
            {
                var package = modules[i];
                if (package == null) continue;
                if (string.Equals(TriggerAuthoringPackageCatalog.ResolveDomainId(package), domainId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(package.PackageMetadata.ContentKey, contentKey, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string NormalizeAssetDirectory(string value)
        {
            return (value ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value?.Length ?? 0);
            for (var i = 0; i < (value?.Length ?? 0); i++)
                builder.Append(Array.IndexOf(invalid, value[i]) >= 0 ? '_' : value[i]);
            return builder.Length > 0 ? builder.ToString() : "trigger_package";
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parts = assetFolder.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
                throw new InvalidOperationException("内容包目录必须位于 Assets 中。");
            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i])) continue;
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }
    }
}
#endif
