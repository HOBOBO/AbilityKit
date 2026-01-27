using System.Collections.Generic;
using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 根据Edge收集创建的节点
    /// </summary>
    public class InsertNodeCollector : ICreateNodeCollector
    {
        private EditorGraphView graphView;
        private IEditorEdgeView edgeView;

        public InsertNodeCollector(EditorGraphView editorGraphView, IEditorEdgeView editorEdgeView)
        {
            this.graphView = editorGraphView;
            this.edgeView = editorEdgeView;
        }

        public List<CreateNodeInfo> Collect(List<MenuNodeInfo> allNodeInfos)
        {
            List<CreateNodeInfo> result = new();

            int count = allNodeInfos.Count;
            for (int i = 0; i < count; i++)
            {
                MenuNodeInfo menuNodeInfo = allNodeInfos[i];

                NodeCache nodeCache = this.graphView.graphElementCache.GetNodeCache(menuNodeInfo.nodeData, menuNodeInfo.editorNodeAssetType);
                if (nodeCache == null) continue;

                if (nodeCache.nodeView.GetCanConnectPort(edgeView, out _, out _))
                {
                    InsertCreateNodePostprocess insertPostprocess = new(edgeView.asset.id);
                    CreateNodeInfo createNodeInfo = new(menuNodeInfo, insertPostprocess);
                    result.Add(createNodeInfo);
                }
            }

            return result;
        }
    }
}