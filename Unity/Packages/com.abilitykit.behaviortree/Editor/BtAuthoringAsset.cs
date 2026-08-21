using System;
using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Editor
{
    /// <summary>
    /// 行为树授权资产：编辑态权威是一份授权源文档 JSON（含布局/分组，含运行时 IR 结构）。
    /// 用普通 ScriptableObject + 字符串字段承载，不引入 Odin——资产内容与导出格式同构，
    /// 源同步（外部 JSON 文件）以路径 + 哈希比对实现。
    /// </summary>
    [CreateAssetMenu(fileName = "BtAuthoring", menuName = "AbilityKit/Behavior Tree Authoring")]
    public sealed class BtAuthoringAsset : ScriptableObject
    {
        [SerializeField, TextArea(6, 40)]
        private string _documentJson = "";

        [SerializeField, HideInInspector]
        private string _sourceJsonPath = "";

        [SerializeField, HideInInspector]
        private string _lastSynchronizedHash = "";

        [SerializeField]
        private string _runtimeExportPath = "Assets/Resources/bt/";

        public string SourceJsonPath => _sourceJsonPath;
        public string LastSynchronizedHash => _lastSynchronizedHash;
        public string RuntimeExportPath => _runtimeExportPath;

        public BtAuthoringSourceDocument LoadDocument()
        {
            if (string.IsNullOrWhiteSpace(_documentJson))
            {
                return new BtAuthoringSourceDocument();
            }
            return BtAuthoringJson.Load(_documentJson);
        }

        public void SaveDocument(BtAuthoringSourceDocument document)
        {
            if (document == null) return;
            _documentJson = BtAuthoringJson.Save(document);
            MarkDirty();
        }

        public void ImportJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Authoring JSON must not be empty.", nameof(json));
            var document = BtAuthoringJson.Load(json);   // 反序列化校验
            _documentJson = BtAuthoringJson.Save(document);
            MarkDirty();
        }

        public void MarkSynchronized(string sourceJsonPath, string contentHash)
        {
            _sourceJsonPath = sourceJsonPath ?? string.Empty;
            _lastSynchronizedHash = contentHash ?? string.Empty;
            MarkDirty();
        }

        public void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>运行时 IR 导出目标（树 id → 相对路径），供编辑器与测试统一使用。</summary>
        public string ResolveRuntimeExportPath(string treeId)
        {
            var dir = string.IsNullOrWhiteSpace(_runtimeExportPath) ? "Assets/Resources/bt/" : _runtimeExportPath;
            var trimmed = dir.EndsWith("/", StringComparison.Ordinal) ? dir : dir + "/";
            return trimmed + treeId + ".json";
        }
    }
}
