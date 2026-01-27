using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// GraphSelectionDragger的强制选中事件
    /// </summary>
    public class GraphSelectionDraggerForceSelectedNodeEvent : GraphEvent<GraphSelectionDraggerForceSelectedNodeEvent>
    {
        public GraphElement select;
        public Vector2 mousePosition;

        public static GraphSelectionDraggerForceSelectedNodeEvent Create(GraphElement select, Vector2 mousePosition)
        {
            GraphSelectionDraggerForceSelectedNodeEvent evt = GetPooled();
            evt.select = select;
            evt.mousePosition = mousePosition;
            return evt;
        }
    }
}