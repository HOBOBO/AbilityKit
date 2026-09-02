using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 行为树项目目录资产：一批授权树的管理单元。显式注册树资产、声明导出目标列表
    /// （相对仓库根，导出扇出到全部目标）；TreeId 唯一性由 Inspector/批量菜单校验。
    /// </summary>
    [CreateAssetMenu(fileName = "BtAuthoringProject", menuName = "AbilityKit/Behavior Tree Project")]
    public sealed class BtAuthoringProjectAsset : ScriptableObject
    {
        [SerializeField] private List<BtAuthoringAsset> _trees = new();

        [Tooltip("导出目标目录（相对仓库根），导出时扇出到全部目标。例如 Unity Resources 与 console Configs 各一条。")]
        [SerializeField] private List<string> _exportTargets = new();

        public List<BtAuthoringAsset> Trees => _trees;
        public List<string> ExportTargets => _exportTargets;

        public void Register(BtAuthoringAsset asset)
        {
            if (asset == null || _trees.Contains(asset)) return;
            _trees.Add(asset);
            MarkDirty();
        }

        public void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>收集本项目下全部树的 (TreeId, 文档)。</summary>
        public List<KeyValuePair<string, BtAuthoringSourceDocument>> CollectDocuments()
        {
            var result = new List<KeyValuePair<string, BtAuthoringSourceDocument>>();
            foreach (var tree in _trees)
            {
                if (tree == null) continue;
                var document = tree.LoadDocument();
                var treeId = document.Tree.TreeId;
                if (string.IsNullOrWhiteSpace(treeId)) treeId = tree.name;
                result.Add(new KeyValuePair<string, BtAuthoringSourceDocument>(treeId, document));
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
                var treeId = tree.LoadDocument().Tree.TreeId;
                treeIds.Add(string.IsNullOrWhiteSpace(treeId) ? tree.name : treeId);
            }

            errors.AddRange(BtAuthoringExportPipeline.ValidateUniqueTreeIds(treeIds));

            if (_exportTargets.Count == 0 && _trees.Count > 0)
            {
                errors.Add("未配置导出目标（ExportTargets）。");
            }
            return errors;
        }

        /// <summary>批量导出到全部目标，返回报告。</summary>
        public List<BtExportReportEntry> ExportAll(string repositoryRoot)
        {
            return BtAuthoringExportPipeline.ExportAll(
                CollectDocuments(), _exportTargets, BtEditorNodeCatalog.Registry, repositoryRoot);
        }
    }
}
