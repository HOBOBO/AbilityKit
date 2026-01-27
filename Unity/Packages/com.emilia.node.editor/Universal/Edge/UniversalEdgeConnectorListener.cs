using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 通用IEdgeConnectorListener实现
    /// </summary>
    public class UniversalEdgeConnectorListener : GraphEdgeConnectorListener
    {
        public override void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            IEditorEdgeView edgeView = edge as IEditorEdgeView;
            if (edgeView == null)
            {
                Debug.LogError($"{nameof(Edge)}必须继承{nameof(IEditorEdgeView)}");
                return;
            }

            if (edgeView.edgeElement.isGhostEdge == false)
            {
                bool isValid = string.IsNullOrEmpty(edgeView.asset?.inputNodeId) == false && string.IsNullOrEmpty(edgeView.asset?.inputPortId) == false;
                if (isValid)
                {
                    graphView.AddEdgeView(edgeView.asset);
                    return;
                }

                graphView.connectSystem.Disconnect(edgeView);
            }

            if (edge.input == null || edge.output == null)
            {
                Port port = edge.input ?? edge.output;
                IEditorPortView portView = port as IEditorPortView;
                if (portView == null) return;

                CreateNodeContext createNodeContext = new();
                CreateNodeByPortCollector createNodePortCollector = new(graphView, portView);
                createNodeContext.nodeCollector = createNodePortCollector;

                graphView.graphOperate.OpenCreateNodeMenu(position, createNodeContext);
            }
        }

        public override void OnDrop(GraphView graphView, Edge edge)
        {
            EditorGraphView editorGraphView = graphView as EditorGraphView;

            IEditorEdgeView edgeView = edge as IEditorEdgeView;
            if (edgeView == null)
            {
                Debug.LogError($"{nameof(Edge)}必须继承{nameof(IEditorEdgeView)}");
                return;
            }

            if (edge.input == null || edge.output == null) return;

            IEditorPortView inputPortView = edge.input as IEditorPortView;
            IEditorPortView outputPortView = edge.output as IEditorPortView;

            if (inputPortView == null || outputPortView == null) return;

            if (IsConnected(edgeView))
            {
                editorGraphView.RecordObjectUndo("Graph RedirectionEdge");

                edgeView.asset.inputNodeId = inputPortView.master.asset.id;
                edgeView.asset.inputPortId = inputPortView.info.id;

                edgeView.asset.outputNodeId = outputPortView.master.asset.id;
                edgeView.asset.outputPortId = outputPortView.info.id;

                edgeView.RemoveView();
                editorGraphView.AddEdgeView(edgeView.asset);
            }
            else
            {
                bool canConnect = this.graphView.connectSystem.CanConnect(inputPortView, outputPortView);
                if (canConnect) this.graphView.connectSystem.Connect(inputPortView, outputPortView);
                else this.graphView.connectSystem.Disconnect(edgeView);
            }
        }

        protected bool IsConnected(IEditorEdgeView editorEdgeView)
        {
            return graphView.graphAsset.edges.Any((i) => i.id.Equals(editorEdgeView.asset?.id));
        }
    }
}