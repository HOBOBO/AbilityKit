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
    /// Item系统
    /// </summary>
    public class GraphItemSystem : BasicGraphViewModule
    {
        private ItemSystemHandle handle;
        public override int order => 1100;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            handle = EditorHandleUtility.CreateHandle<ItemSystemHandle>(graphView.graphAsset.GetType());
        }

        /// <summary>
        /// 创建Item
        /// </summary>
        public IEditorItemView CreateItem(Type type, Vector2 position)
        {
            if (typeof(EditorItemAsset).IsAssignableFrom(type) == false)
            {
                Debug.LogError($"CreateItem Error: {type}未继承EditorItemAsset");
                return null;
            }

            EditorItemAsset itemAsset = ScriptableObject.CreateInstance(type) as EditorItemAsset;
            itemAsset.id = Guid.NewGuid().ToString();
            itemAsset.position = new Rect(position, new Vector2(100, 100));

            Undo.IncrementCurrentGroup();
            Undo.RegisterCreatedObjectUndo(itemAsset, "Graph CreateItem");

            graphView.RegisterCompleteObjectUndo("Graph CreateItem");
            IEditorItemView editorItemView = this.graphView.AddItem(itemAsset);
            handle?.OnCreateItem(this.graphView, editorItemView);

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Undo.IncrementCurrentGroup();

            return editorItemView;
        }

        /// <summary>
        /// 删除Item
        /// </summary>
        public void DeleteItem(IEditorItemView itemView)
        {
            itemView.RemoveView();

            if (itemView.asset == null) return;
            graphView.RegisterCompleteObjectUndo("Graph RemoveItem");
            graphView.graphAsset.RemoveItem(itemView.asset);

            List<Object> assets = itemView.asset.CollectAsset();

            int amount = assets.Count;
            for (int i = 0; i < amount; i++)
            {
                Object asset = assets[i];
                if (asset == null) continue;
                Undo.DestroyObjectImmediate(asset);
            }
        }

        /// <summary>
        /// 删除Item，不记录Undo
        /// </summary>
        public void DeleteItemNoUndo(IEditorItemView itemView)
        {
            itemView.RemoveView();
            graphView.graphAsset.RemoveItem(itemView.asset);

            List<Object> assets = itemView.asset.CollectAsset();

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