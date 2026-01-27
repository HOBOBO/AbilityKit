using System.Collections.Generic;
using System.Linq;
using Emilia.Kit;
using Emilia.Node.Editor;
using Sirenix.Serialization;
using UnityEditor.Experimental.GraphView;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用拷贝粘贴处理
    /// </summary>
    [EditorHandle(typeof(EditorUniversalGraphAsset))]
    public class UniversalGraphCopyPasteHandle : GraphCopyPasteHandle
    {
        public override string SerializeGraphElementsCallback(EditorGraphView graphView, IEnumerable<GraphElement> elements)
        {
            CopyPasteGraph graph = new();

            foreach (GraphElement element in elements)
            {
                IGraphCopyPasteElement copyPasteElement = element as IGraphCopyPasteElement;
                if (copyPasteElement == null) continue;
                graph.AddPack(copyPasteElement.GetPack());
            }

            return OdinSerializablePackUtility.ToJson(graph);
        }

        public override bool CanPasteSerializedDataCallback(EditorGraphView graphView, string serializedData)
        {
            try
            {
                return OdinSerializablePackUtility.FromJson<CopyPasteGraph>(serializedData) != null;
            }
            catch
            {
                return false;
            }
        }

        public override IEnumerable<GraphElement> UnserializeAndPasteCallback(EditorGraphView graphView, string operationName, string serializedData, GraphCopyPasteContext copyPasteContext)
        {
            CopyPasteGraph graph = OdinSerializablePackUtility.FromJson<CopyPasteGraph>(serializedData);
            List<object> pasteContent = graph.StartPaste(copyPasteContext);
            return pasteContent.OfType<GraphElement>();
        }

        public override IEnumerable<GraphElement> GetCopyGraphElements(EditorGraphView graphView, string serializedData)
        {
            try
            {
                CopyPasteGraph graph = OdinSerializablePackUtility.FromJson<CopyPasteGraph>(serializedData);
                if (graph == null) return null;

                List<GraphElement> graphElements = new();
                List<ICopyPastePack> copyPastePacks = graph.GetAllPacks();

                int amount = copyPastePacks.Count;
                for (int i = 0; i < amount; i++)
                {
                    ICopyPastePack copyPastePack = copyPastePacks[i];

                    switch (copyPastePack)
                    {
                        case INodeCopyPastePack nodeCopyPastePack:
                        {
                            IEditorNodeView nodeView = graphView.graphElementCache.nodeViewById.GetValueOrDefault(nodeCopyPastePack.copyAsset.id);
                            if (nodeView == null) continue;
                            graphElements.Add(nodeView.element);
                            break;
                        }
                        case IEdgeCopyPastePack edgeCopyPastePack:
                        {
                            IEditorEdgeView edgeView = graphView.graphElementCache.edgeViewById.GetValueOrDefault(edgeCopyPastePack.copyAsset.id);
                            if (edgeView == null) continue;
                            graphElements.Add(edgeView.edgeElement);
                            break;
                        }
                        case IItemCopyPastePack itemCopyPastePack:
                        {
                            IEditorItemView itemView = graphView.graphElementCache.itemViewById.GetValueOrDefault(itemCopyPastePack.copyAsset.id);
                            if (itemView == null) continue;
                            graphElements.Add(itemView.element);
                            break;
                        }
                        case IPortCopyPastePack portCopyPastePack:
                        {
                            IEditorNodeView nodeView = graphView.graphElementCache.nodeViewById.GetValueOrDefault(portCopyPastePack.nodeId);
                            if (nodeView == null) continue;
                            IEditorPortView portView = nodeView.GetPortView(portCopyPastePack.portId);
                            graphElements.Add(portView.portElement);
                            break;
                        }
                    }
                }

                return graphElements;
            }
            catch
            {
                return null;
            }
        }

        public override object CreateCopy(EditorGraphView graphView, object value) => SerializationUtility.CreateCopy(value);
    }
}