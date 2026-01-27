using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// GraphEdge的IEdgeConnectorListener
    /// </summary>
    public class GraphEdgeConnectorListener : IEdgeConnectorListener
    {
        public EditorGraphView graphView { get; private set; }

        public virtual void Initialize(EditorGraphView graphView)
        {
            this.graphView = graphView;
        }

        public virtual void OnDropOutsidePort(Edge edge, Vector2 position) { }
        public virtual void OnDrop(GraphView graphView, Edge edge) { }
    }
}