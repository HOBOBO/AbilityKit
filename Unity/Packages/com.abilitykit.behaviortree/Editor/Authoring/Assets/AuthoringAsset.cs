using System;
using System.Collections.Generic;
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
    /// 行为树授权资产：编辑态权威是一份授权源文档 JSON（含布局/分组，含运行时 IR 结构）。
    /// 用普通 ScriptableObject + 字符串字段承载，不引入 Odin——资产内容与导出格式同构，
    /// 源同步（外部 JSON 文件）以路径 + 哈希比对实现。
    /// </summary>
    [CreateAssetMenu(fileName = "BtAuthoring", menuName = "AbilityKit/Behavior Tree Authoring")]
    [MovedFrom(true, "AbilityKit.BehaviorTree.Editor", "AbilityKit.BehaviorTree.Editor", "BtAuthoringAsset")]
    public class AuthoringAsset : ScriptableObject
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

        public AuthoringSourceDocument LoadDocument()
        {
            if (string.IsNullOrWhiteSpace(_documentJson))
            {
                return new AuthoringSourceDocument();
            }
            return AuthoringJson.Load(_documentJson);
        }

        public void SaveDocument(AuthoringSourceDocument document)
        {
            if (document == null) return;
            _documentJson = AuthoringJson.Save(document);
            MarkDirty();
        }

        public void ImportJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Authoring JSON must not be empty.", nameof(json));
            var document = AuthoringJson.Load(json);   // 反序列化校验
            _documentJson = AuthoringJson.Save(document);
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
