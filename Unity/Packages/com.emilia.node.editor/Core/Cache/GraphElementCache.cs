using System;
using System.Collections.Generic;
using Emilia.Kit.Editor;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// GraphView元素的实例缓存
    /// </summary>
    public class GraphElementCache
    {
        private EditorGraphView editorGraphView;

        private Dictionary<string, IEditorNodeView> _nodeViewById = new();
        private Dictionary<string, IEditorEdgeView> _edgeViewById = new();
        private Dictionary<string, IEditorItemView> _itemViewById = new();

        private List<NodeCache> _nodeViewCache = new();

        /// <summary>
        /// 根据id获取IEditorNodeView
        /// </summary>
        public IReadOnlyDictionary<string, IEditorNodeView> nodeViewById => this._nodeViewById;

        /// <summary>
        /// 根据id获取IEditorEdgeView
        /// </summary>
        public IReadOnlyDictionary<string, IEditorEdgeView> edgeViewById => this._edgeViewById;

        /// <summary>
        /// 根据id获取IEditorItemView
        /// </summary>
        public IReadOnlyDictionary<string, IEditorItemView> itemViewById => this._itemViewById;

        /// <summary>
        /// 构建缓存
        /// </summary>
        public void BuildCache(EditorGraphView graphView)
        {
            this.editorGraphView = graphView;

            this._nodeViewById.Clear();
            this._edgeViewById.Clear();
            this._nodeViewCache.Clear();

            int amount = this.editorGraphView.createNodeMenu.createNodeHandleCacheList.Count;
            for (int i = 0; i < amount; i++)
            {
                ICreateNodeHandle createNodeHandle = this.editorGraphView.createNodeMenu.createNodeHandleCacheList[i];

                EditorNodeAsset nodeAsset = this.editorGraphView.nodeSystem.CreateNode(createNodeHandle.editorNodeType, Vector2.zero);

                object nodeData = createNodeHandle.nodeData;
                nodeAsset.userData = this.editorGraphView.graphCopyPaste.CreateCopy(nodeData);

                Type nodeViewType = GraphTypeCache.GetNodeViewType(nodeAsset.GetType());
                IEditorNodeView nodeView = ReflectUtility.CreateInstance(nodeViewType) as IEditorNodeView;
                nodeView.Initialize(this.editorGraphView, nodeAsset);

                NodeCache nodeCache = new(nodeData, nodeView);
                this._nodeViewCache.Add(nodeCache);
            }
        }

        /// <summary>
        /// 设置NodeView缓存
        /// </summary>
        public void SetNodeViewCache(string id, IEditorNodeView nodeView)
        {
            if (string.IsNullOrEmpty(id) || nodeView == null) return;
            this._nodeViewById[id] = nodeView;
        }

        /// <summary>
        /// 设置EdgeView缓存
        /// </summary>
        public void SetEdgeViewCache(string id, IEditorEdgeView edgeView)
        {
            if (string.IsNullOrEmpty(id) || edgeView == null) return;
            this._edgeViewById[id] = edgeView;
        }

        /// <summary>
        /// 设置ItemView缓存
        /// </summary>
        public void SetItemViewCache(string id, IEditorItemView itemView)
        {
            if (string.IsNullOrEmpty(id) || itemView == null) return;
            this._itemViewById[id] = itemView;
        }

        /// <summary>
        /// 移除NodeView缓存
        /// </summary>
        public void RemoveNodeViewCache(string id)
        {
            if (this._nodeViewById.ContainsKey(id)) this._nodeViewById.Remove(id);
        }

        /// <summary>
        /// 移除EdgeView缓存
        /// </summary>
        public void RemoveEdgeViewCache(string id)
        {
            if (_edgeViewById.ContainsKey(id)) this._edgeViewById.Remove(id);
        }

        /// <summary>
        /// 移除ItemView缓存
        /// </summary>
        public void RemoveItemViewCache(string id)
        {
            if (this._itemViewById.ContainsKey(id)) this._itemViewById.Remove(id);
        }

        /// <summary>
        /// 根据Id获取NodeView
        /// </summary>
        public IEditorNodeView GetEditorNodeView(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return nodeViewById.GetValueOrDefault(id);
        }

        /// <summary>
        /// 根据Id获取EdgeView
        /// </summary>
        public IEditorEdgeView GetEditorEdgeView(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return edgeViewById.GetValueOrDefault(id);
        }

        /// <summary>
        /// 根据Id获取ItemView
        /// </summary>
        public IEditorItemView GetEditorItemView(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return itemViewById.GetValueOrDefault(id);
        }

        /// <summary>
        /// 根据端口获取可连接的端口信息
        /// </summary>
        public List<PortInfo> GetPortInfoTypeByPort(IEditorPortView form)
        {
            List<PortInfo> portInfos = new();

            int nodeViewAmount = this._nodeViewCache.Count;
            for (int i = 0; i < nodeViewAmount; i++)
            {
                NodeCache nodeCache = this._nodeViewCache[i];

                int portViewAmount = nodeCache.nodeView.portViews.Count;
                for (var j = 0; j < portViewAmount; j++)
                {
                    IEditorPortView portView = nodeCache.nodeView.portViews[j];
                    bool canConnect = this.editorGraphView.connectSystem.CanConnect(form, portView);
                    if (canConnect == false) continue;
                    PortInfo portInfo = new();
                    portInfo.nodeAssetType = nodeCache.nodeView.asset.GetType();
                    portInfo.nodeData = nodeCache.nodeData;
                    portInfo.portId = portView.info.id;
                    portInfo.displayName = portView.info.displayName;
                    portInfos.Add(portInfo);
                }
            }

            return portInfos;
        }

        /// <summary>
        /// 根据端口获取IEditorEdgeView
        /// </summary>
        public IEditorEdgeView GetEdgeView(IEditorPortView xPort, IEditorPortView yPort)
        {
            int edgeAmount = this.editorGraphView.edgeViews.Count;
            for (int i = 0; i < edgeAmount; i++)
            {
                IEditorEdgeView edge = this.editorGraphView.edgeViews[i];
                bool hasInputNode = edge.inputPortView.master.asset.id == xPort.master.asset.id;
                bool hasOutputNode = edge.outputPortView.master.asset.id == yPort.master.asset.id;
                bool hasInputPort = edge.inputPortView.info.id == xPort.info.id;
                bool hasOutputPort = edge.outputPortView.info.id == yPort.info.id;
                if (hasInputNode && hasOutputNode && hasInputPort && hasOutputPort) return edge;

                hasInputNode = edge.inputPortView.master.asset.id == yPort.master.asset.id;
                hasOutputNode = edge.outputPortView.master.asset.id == xPort.master.asset.id;
                hasInputPort = edge.inputPortView.info.id == yPort.info.id;
                hasOutputPort = edge.outputPortView.info.id == xPort.info.id;
                if (hasInputNode && hasOutputNode && hasInputPort && hasOutputPort) return edge;
            }

            return null;
        }

        /// <summary>
        /// 根据类型获取节点缓存
        /// </summary>
        public NodeCache GetNodeCacheByEditorNodeType(Type editorNodeType)
        {
            int amount = this._nodeViewCache.Count;
            for (int i = 0; i < amount; i++)
            {
                NodeCache nodeCache = this._nodeViewCache[i];
                if (nodeCache.nodeView.asset.GetType() == editorNodeType) return nodeCache;
            }

            return null;
        }

        /// <summary>
        /// 根据数据获取节点缓存
        /// </summary>
        public NodeCache GetNodeCache(object nodeData, Type editorNodeType)
        {
            int amount = this._nodeViewCache.Count;
            for (int i = 0; i < amount; i++)
            {
                NodeCache nodeCache = this._nodeViewCache[i];

                bool typeEqual = nodeCache.nodeView.asset.GetType() == editorNodeType;

                bool dataEqual = false;
                if (nodeCache.nodeData == null && nodeData == null) dataEqual = true;
                else if (nodeCache.nodeData != null && nodeData != null) dataEqual = nodeCache.nodeData.GetType() == nodeData.GetType();

                if (typeEqual && dataEqual) return nodeCache;
            }

            return null;
        }

        /// <summary>
        /// 清空缓存
        /// </summary>
        public void Clear()
        {
            this.editorGraphView = null;
            this._nodeViewById.Clear();
            this._edgeViewById.Clear();
            this._itemViewById.Clear();
            this._nodeViewCache.Clear();
        }
    }
}