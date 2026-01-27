using System;
using System.Collections.Generic;
using Emilia.Node.Editor;
using UnityEngine;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 节点收藏设置
    /// </summary>
    [Serializable]
    public class NodeCollectionSetting
    {
        public const string NodeCollectionSettingSaveKey = "NodeCollectionSettingSaveKey";

        [SerializeField]
        private List<string> _createNodePath = new();

        private List<NodeCollectionInfo> _createNodeInfos = new();

        /// <summary>
        /// 收藏的节点路径列表
        /// </summary>
        public IReadOnlyList<string> createNodePath => _createNodePath;

        /// <summary>
        /// 收藏的节点信息列表
        /// </summary>
        public IReadOnlyList<NodeCollectionInfo> createNodeInfos => _createNodeInfos;

        public void Rebuild()
        {
            _createNodeInfos = new List<NodeCollectionInfo>();
            foreach (string path in _createNodePath) _createNodeInfos.Add(new NodeCollectionInfo(path));
        }

        public void Add(string path)
        {
            if (_createNodePath.Contains(path)) return;
            _createNodePath.Add(path);
            _createNodeInfos.Add(new NodeCollectionInfo(path));
        }

        public void Remove(string path)
        {
            if (_createNodePath.Contains(path) == false) return;
            _createNodePath.Remove(path);

            NodeCollectionInfo nodeCollectionInfo = _createNodeInfos.Find(x => x.nodePath == path);
            if (nodeCollectionInfo != null) _createNodeInfos.Remove(nodeCollectionInfo);
        }

        public void Save(EditorGraphView graphView)
        {
            graphView.graphLocalSettingSystem.SetTypeSettingValue(NodeCollectionSettingSaveKey, this);
        }

        public static NodeCollectionSetting Get(EditorGraphView graphView)
        {
            NodeCollectionSetting setting = graphView.graphLocalSettingSystem.GetTypeSettingValue(NodeCollectionSettingSaveKey, new NodeCollectionSetting());
            setting.Rebuild();
            return setting;
        }
    }
}