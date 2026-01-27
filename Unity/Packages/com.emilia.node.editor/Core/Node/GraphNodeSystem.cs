using System;
using System.Collections.Generic;
using Emilia.Kit;
using Emilia.Kit.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 节点系统
    /// </summary>
    public class GraphNodeSystem : BasicGraphViewModule
    {
        private NodeSystemHandle handle;
        public override int order => 900;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            handle = EditorHandleUtility.CreateHandle<NodeSystemHandle>(graphView.graphAsset.GetType());
        }

        /// <summary>
        /// 创建节点
        /// </summary>
        public IEditorNodeView CreateNode(Type nodeType, Vector2 position, object userData)
        {
            EditorNodeAsset nodeAsset = CreateNode(nodeType, position);
            if (nodeAsset == null)
            {
                Debug.LogError("Create Node Error: " + nodeType + "需要经常EditorNodeAsset");
                return null;
            }
            Undo.RegisterCreatedObjectUndo(nodeAsset, "Graph CreateNode");

            if (userData != null) nodeAsset.userData = graphView.graphCopyPaste.CreateCopy(userData);

            graphView.RegisterCompleteObjectUndo("Graph CreateNode");
            IEditorNodeView nodeView = graphView.AddNode(nodeAsset);
            handle?.OnCreateNode(this.graphView, nodeView);

            return nodeView;
        }

        /// <summary>
        /// 创建节点
        /// </summary>
        public EditorNodeAsset CreateNode(Type nodeType, Vector2 position)
        {
            if (typeof(EditorNodeAsset).IsAssignableFrom(nodeType) == false) return null;

            EditorNodeAsset node = ScriptableObject.CreateInstance(nodeType) as EditorNodeAsset;
            node.id = Guid.NewGuid().ToString();
            node.position = new Rect(position, new Vector2(100, 100));

            return node;
        }

        /// <summary>
        /// 删除节点
        /// </summary>
        public void DeleteNode(IEditorNodeView nodeView)
        {
            nodeView.RemoveView();

            graphView.RegisterCompleteObjectUndo("Graph RemoveNode");

            graphView.graphAsset.RemoveNode(nodeView.asset);

            List<Object> assets = nodeView.asset.CollectAsset();

            int amount = assets.Count;
            for (int i = 0; i < amount; i++)
            {
                Object asset = assets[i];
                if (asset == null) continue;
                Undo.DestroyObjectImmediate(asset);
            }
        }

        /// <summary>
        /// 删除节点，不记录Undo
        /// </summary>
        public void DeleteNodeNoUndo(IEditorNodeView nodeView)
        {
            nodeView.RemoveView();

            graphView.graphAsset.RemoveNode(nodeView.asset);

            List<Object> assets = nodeView.asset.CollectAsset();

            int amount = assets.Count;
            for (int i = 0; i < amount; i++)
            {
                Object asset = assets[i];
                if (asset == null) continue;
                Object.DestroyImmediate(asset, true);
            }
        }

        public override void Dispose()
        {
            this.handle = null;
            base.Dispose();
        }
    }
}