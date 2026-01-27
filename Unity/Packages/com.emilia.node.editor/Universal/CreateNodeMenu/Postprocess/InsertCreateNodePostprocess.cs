using System;
using System.Collections.Generic;
using System.Linq;
using Emilia.Kit.Editor;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点后插入处理器
    /// </summary>
    public class InsertCreateNodePostprocess : ICreateNodePostprocess
    {
        protected string insertEdgeId;

        protected string inputPortId;
        protected string outputPortId;

        public InsertCreateNodePostprocess(string insertEdgeId, string inputPortId = null, string outputPortId = null)
        {
            this.insertEdgeId = insertEdgeId;
            this.inputPortId = inputPortId;
            this.outputPortId = outputPortId;
        }

        public void Postprocess(EditorGraphView graphView, IEditorNodeView nodeView, CreateNodeContext createNodeContext)
        {
            // 从图视图缓存中获取要插入的边视图
            IEditorEdgeView edgeView = graphView.graphElementCache.GetEditorEdgeView(insertEdgeId);

            IEditorPortView inputPortView = null;
            if (string.IsNullOrEmpty(inputPortId) == false) inputPortView = nodeView.GetPortView(inputPortId);

            IEditorPortView outputPortView = null;
            if (string.IsNullOrEmpty(this.outputPortId) == false) outputPortView = nodeView.GetPortView(outputPortId);

            // 如果端口视图为空,尝试自动查找可连接的端口
            if (inputPortView == null || outputPortView == null)
            {
                // 获取节点上所有能与当前边连接的输入和输出端口
                if (nodeView.GetCanConnectPort(edgeView, out List<IEditorPortView> canConnectInput, out List<IEditorPortView> canConnectOutput))
                {
                    if (inputPortView == null) inputPortView = canConnectInput.FirstOrDefault();
                    if (outputPortView == null) outputPortView = canConnectOutput.FirstOrDefault();
                }
            }

            if (inputPortView == null || outputPortView == null) return;

            IEdgeCopyPastePack copyPastePack = edgeView.GetPack() as IEdgeCopyPastePack;

            // 创建两个新的边资源实例(一个连接输入端口,一个连接输出端口)
            EditorEdgeAsset inputPasteAsset = Object.Instantiate(copyPastePack.copyAsset);
            EditorEdgeAsset outputPasteAsset = Object.Instantiate(copyPastePack.copyAsset);

            inputPasteAsset.name = copyPastePack.copyAsset.name;
            inputPasteAsset.id = Guid.NewGuid().ToString();

            outputPasteAsset.name = copyPastePack.copyAsset.name;
            outputPasteAsset.id = Guid.NewGuid().ToString();

            // 粘贴子资源(复制边可能包含的子资源)
            inputPasteAsset.PasteChild();
            outputPasteAsset.PasteChild();

            inputPasteAsset.inputNodeId = inputPortView.master.asset.id;
            inputPasteAsset.inputPortId = inputPortView.info.id;

            outputPasteAsset.outputNodeId = outputPortView.master.asset.id;
            outputPasteAsset.outputPortId = outputPortView.info.id;

            nodeView.graphView.RegisterCompleteObjectUndo("Graph Insert");
            
            // 将两条新边添加到图视图中
            nodeView.graphView.AddEdge(inputPasteAsset);
            nodeView.graphView.AddEdge(outputPasteAsset);

            Undo.RegisterCreatedObjectUndo(inputPasteAsset, "Graph Insert");
            Undo.RegisterCreatedObjectUndo(outputPasteAsset, "Graph Insert");
            
            // 删除原来的边(因为已经被两条新边替换)
            edgeView.Delete();
        }
    }
}