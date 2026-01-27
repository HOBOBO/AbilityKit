using System;
using Emilia.Kit;
using Emilia.Kit.Editor;
using Sirenix.Serialization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Item拷贝粘贴Pack
    /// </summary>
    [Serializable]
    public class ItemCopyPastePack : IItemCopyPastePack
    {
        [OdinSerialize, NonSerialized]
        private UnityAssetSerializationPack assetPack;

        protected EditorItemAsset _copyAsset;
        protected EditorItemAsset _pasteAsset;

        public EditorItemAsset copyAsset
        {
            get
            {
                if (this._copyAsset == null) this._copyAsset = UnityAssetSerializationUtility.DeserializeUnityAsset<EditorItemAsset>(this.assetPack);
                return this._copyAsset;
            }
        }

        public EditorItemAsset pasteAsset => this._pasteAsset;

        public ItemCopyPastePack(EditorItemAsset asset)
        {
            assetPack = UnityAssetSerializationUtility.SerializeUnityAsset(asset);
        }

        public virtual bool CanDependency(ICopyPastePack pack) => false;

        public virtual void Paste(CopyPasteContext copyPasteContext)
        {
            GraphCopyPasteContext graphCopyPasteContext = (GraphCopyPasteContext) copyPasteContext.userData;
            EditorGraphView graphView = graphCopyPasteContext.graphView;

            if (graphView == null) return;

            EditorItemAsset copy = copyAsset;
            if (copy == null) return;

            // 实例化新的资产并分配新的ID
            _pasteAsset = Object.Instantiate(copy);
            this._pasteAsset.id = Guid.NewGuid().ToString();

            // 设置粘贴位置：默认偏移(20, 20)，如果有指定位置则使用指定位置
            Rect rect = _pasteAsset.position;
            rect.position += new Vector2(20, 20);
            if (graphCopyPasteContext.createPosition != null) rect.position = graphCopyPasteContext.createPosition.Value;

            _pasteAsset.position = rect;

            // 粘贴子元素和依赖项
            this._pasteAsset.PasteChild();
            PasteDependency(copyPasteContext);

            graphView.RegisterCompleteObjectUndo("Graph Paste");
            IEditorItemView itemView = graphView.AddItem(_pasteAsset);

            copyPasteContext.pasteContent.Add(itemView);

            Undo.RegisterCreatedObjectUndo(this._pasteAsset, "Graph Pause");
        }

        protected virtual void PasteDependency(CopyPasteContext copyPasteContext) { }
    }
}