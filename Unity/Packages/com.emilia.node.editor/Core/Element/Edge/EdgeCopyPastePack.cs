using System;
using Emilia.Kit;
using Emilia.Kit.Editor;
using Sirenix.Serialization;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Edge拷贝粘贴Pack
    /// </summary>
    [Serializable]
    public class EdgeCopyPastePack : IEdgeCopyPastePack
    {
        [OdinSerialize, NonSerialized]
        private UnityAssetSerializationPack assetPack;

        private EditorEdgeAsset _copyAsset;
        private EditorEdgeAsset _pasteAsset;


        public EditorEdgeAsset copyAsset
        {
            get
            {
                if (this._copyAsset == null) this._copyAsset = UnityAssetSerializationUtility.DeserializeUnityAsset<EditorEdgeAsset>(this.assetPack);
                return this._copyAsset;
            }
        }

        public EditorEdgeAsset pasteAsset => this._pasteAsset;

        public EdgeCopyPastePack(EditorEdgeAsset edgeAsset)
        {
            assetPack = UnityAssetSerializationUtility.SerializeUnityAsset(edgeAsset);
        }

        public virtual bool CanDependency(ICopyPastePack pack)
        {
            INodeCopyPastePack nodeCopyPastePack = pack as INodeCopyPastePack;
            if (nodeCopyPastePack == null) return false;
            bool isInput = copyAsset.inputNodeId == nodeCopyPastePack.copyAsset.id;
            bool isOutput = copyAsset.outputNodeId == nodeCopyPastePack.copyAsset.id;
            if (isInput || isOutput) return true;
            return false;
        }

        public void Paste(CopyPasteContext copyPasteContext)
        {
            GraphCopyPasteContext graphCopyPasteContext = (GraphCopyPasteContext) copyPasteContext.userData;
            EditorGraphView graphView = graphCopyPasteContext.graphView;

            if (graphView == null) return;

            EditorEdgeAsset copy = copyAsset;
            if (copy == null) return;

            // 实例化新的Edge资源并生成新的唯一ID
            _pasteAsset = Object.Instantiate(copy);
            _pasteAsset.name = copy.name;
            _pasteAsset.id = Guid.NewGuid().ToString();

            // 粘贴子级元素
            _pasteAsset.PasteChild();

            // 遍历依赖关系，重新建立边与节点之间的连接
            int amount = copyPasteContext.dependency.Count;
            for (int i = 0; i < amount; i++)
            {
                ICopyPastePack pack = copyPasteContext.dependency[i];
                INodeCopyPastePack nodeCopyPastePack = pack as INodeCopyPastePack;
                if (nodeCopyPastePack == null) continue;

                // 检查并更新输入节点ID
                bool isInput = copy.inputNodeId == nodeCopyPastePack.copyAsset.id;
                if (isInput)
                {
                    _pasteAsset.inputNodeId = nodeCopyPastePack.pasteAsset.id;
                    continue;
                }

                // 检查并更新输出节点ID
                bool isOutput = copy.outputNodeId == nodeCopyPastePack.copyAsset.id;
                if (isOutput)
                {
                    _pasteAsset.outputNodeId = nodeCopyPastePack.pasteAsset.id;
                    continue;
                }
            }

            // 注册撤销操作并将边添加到图表视图
            graphView.RegisterCompleteObjectUndo("Graph Paste");
            graphView.AddEdge(_pasteAsset);
            Undo.RegisterCreatedObjectUndo(this._pasteAsset, "Graph Pause");
        }
    }
}