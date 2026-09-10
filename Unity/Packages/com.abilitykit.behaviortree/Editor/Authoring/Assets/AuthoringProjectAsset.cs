using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.BehaviorTree.Authoring;
using UnityEngine;

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
    /// <summary>
    /// 行为树项目目录资产：一批授权树的管理单元。显式注册树资产、声明导出目标列表
    /// （相对仓库根，导出扇出到全部目标）；TreeId 唯一性由 Inspector/批量菜单校验。
    /// </summary>
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringProjectAsset")]
    public sealed class AuthoringProjectAsset : ScriptableObject
    {
        [SerializeField] private List<AuthoringAsset> _trees = new();

        [Tooltip("导出目标目录（相对仓库根），导出时扇出到全部目标。例如 Unity Resources 与 console Configs 各一条。")]
        [SerializeField] private List<string> _exportTargets = new() { "Unity/Assets/Resources/bt" };

        [Tooltip("通过本项目创建行为树资产时使用的默认 Unity 资产目录。")]
        [SerializeField] private string _treeAssetDirectory = "Assets/BehaviorTrees";

        [Tooltip("通过本项目创建行为树时默认选中的模板。")]
        [SerializeField] private int _defaultTemplateIndex = 1;

        public List<AuthoringAsset> Trees => _trees;
        public List<string> ExportTargets => _exportTargets;
        public string TreeAssetDirectory
        {
            get => _treeAssetDirectory;
            set
            {
                var normalized = (value ?? string.Empty).Trim().Replace('\\', '/');
                if (string.Equals(_treeAssetDirectory, normalized, StringComparison.Ordinal)) return;
                _treeAssetDirectory = normalized;
                MarkDirty();
            }
        }
        public int DefaultTemplateIndex
        {
            get => _defaultTemplateIndex;
            set
            {
                if (_defaultTemplateIndex == value) return;
                _defaultTemplateIndex = value;
                MarkDirty();
            }
        }

        public void Register(AuthoringAsset asset)
        {
            if (asset == null || _trees.Contains(asset)) return;
            _trees.Add(asset);
            MarkDirty();
        }

        public void Unregister(AuthoringAsset asset)
        {
            if (asset == null || !_trees.Remove(asset)) return;
            MarkDirty();
        }

        public bool ContainsTreeId(string treeId, AuthoringAsset? except = null)
        {
            if (string.IsNullOrWhiteSpace(treeId)) return false;
            return _trees.Any(tree => tree != null
                && !ReferenceEquals(tree, except)
                && string.Equals(
                    ResolveTreeId(tree),
                    treeId,
                    StringComparison.Ordinal));
        }

        public void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>收集本项目下全部树的 (TreeId, 文档)。</summary>
        public List<KeyValuePair<string, AuthoringSourceDocument>> CollectDocuments()
        {
            var result = new List<KeyValuePair<string, AuthoringSourceDocument>>();
            foreach (var tree in _trees)
            {
                if (tree == null) continue;
                var document = tree.LoadDocument();
                var treeId = document.Tree.TreeId;
                if (string.IsNullOrWhiteSpace(treeId)) treeId = tree.name;
                result.Add(new KeyValuePair<string, AuthoringSourceDocument>(treeId, document));
            }
            return result;
        }

        /// <summary>项目级校验错误（TreeId 唯一 + 目标配置）。</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var treeIds = new List<string>();
            foreach (var tree in _trees)
            {
                if (tree == null)
                {
                    errors.Add("存在空引用的树资产（可能被删除）。");
                    continue;
                }
                treeIds.Add(ResolveTreeId(tree));
            }

            errors.AddRange(ExportPipeline.ValidateUniqueTreeIds(treeIds));

            if (!_trees.Any(tree => tree != null))
            {
                errors.Add("项目尚未注册行为树。");
            }

            if (_exportTargets.Count == 0)
            {
                errors.Add("未配置导出目标（ExportTargets）。");
            }
            else
            {
                if (_exportTargets.Any(string.IsNullOrWhiteSpace))
                    errors.Add("导出目标不能为空。");

                var duplicateTarget = _exportTargets
                    .Where(target => !string.IsNullOrWhiteSpace(target))
                    .GroupBy(target => target.Trim().Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(group => group.Count() > 1);
                if (duplicateTarget != null)
                    errors.Add($"导出目标 '{duplicateTarget.Key}' 重复配置。");
            }

            if (!IsValidAssetDirectory(_treeAssetDirectory))
            {
                errors.Add("行为树资产目录必须位于 Assets 下。");
            }
            return errors;
        }

        /// <summary>批量导出到全部目标，返回报告。</summary>
        public List<ExportReportEntry> ExportAll(string repositoryRoot)
        {
            var configurationErrors = Validate();
            if (configurationErrors.Count > 0)
                return ConfigurationErrorReport(configurationErrors);

            return ExportPipeline.ExportAll(
                CollectDocuments(), _exportTargets, EditorNodeCatalog.Registry, repositoryRoot);
        }

        /// <summary>使用项目配置仅导出指定树；图编辑器的 Export 命令走这条路径。</summary>
        public List<ExportReportEntry> ExportTree(AuthoringAsset asset, string repositoryRoot)
        {
            if (asset == null || !_trees.Contains(asset))
            {
                return ConfigurationErrorReport(new[] { "要导出的行为树未注册到当前项目。" });
            }

            var configurationErrors = Validate();
            if (configurationErrors.Count > 0)
                return ConfigurationErrorReport(configurationErrors);

            var document = asset.LoadDocument();
            return ExportPipeline.ExportAll(
                new[]
                {
                    new KeyValuePair<string, AuthoringSourceDocument>(ResolveTreeId(asset), document),
                },
                _exportTargets,
                EditorNodeCatalog.Registry,
                repositoryRoot,
                CollectDocuments());
        }

        private static string ResolveTreeId(AuthoringAsset tree)
        {
            var treeId = tree.LoadDocument().Tree.TreeId;
            return string.IsNullOrWhiteSpace(treeId) ? tree.name : treeId;
        }

        private static bool IsValidAssetDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return false;
            var normalized = directory.Trim().Replace('\\', '/');
            if (!(normalized.Equals("Assets", StringComparison.Ordinal)
                || normalized.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                return false;
            }

            return !normalized.Split('/').Any(segment => segment is "." or "..");
        }

        private static List<ExportReportEntry> ConfigurationErrorReport(IEnumerable<string> errors)
        {
            return errors
                .Select(error => new ExportReportEntry(
                    "<project>",
                    "<configuration>",
                    ExportStatus.Error,
                    error))
                .ToList();
        }
    }
}
