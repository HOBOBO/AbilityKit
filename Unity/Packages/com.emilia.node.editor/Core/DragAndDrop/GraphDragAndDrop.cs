using Emilia.Kit;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 拖拽系统
    /// </summary>
    public class GraphDragAndDrop : BasicGraphViewModule
    {
        private GraphDragAndDropHandle handle;
        public override int order => 1500;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            this.handle = EditorHandleUtility.CreateHandle<GraphDragAndDropHandle>(graphView.graphAsset.GetType());

            graphView.UnregisterCallback<DragUpdatedEvent>(DragUpdatedCallback);
            graphView.UnregisterCallback<DragPerformEvent>(DragPerformedCallback);

            graphView.RegisterCallback<DragUpdatedEvent>(DragUpdatedCallback);
            graphView.RegisterCallback<DragPerformEvent>(DragPerformedCallback);
        }

        private void DragUpdatedCallback(DragUpdatedEvent evt)
        {
            handle?.DragUpdatedCallback(this.graphView, evt);
        }

        private void DragPerformedCallback(DragPerformEvent evt)
        {
            handle?.DragPerformedCallback(graphView, evt);
        }

        public override void Dispose()
        {
            if (graphView == null) return;

            graphView.UnregisterCallback<DragUpdatedEvent>(DragUpdatedCallback);
            graphView.UnregisterCallback<DragPerformEvent>(DragPerformedCallback);

            handle = null;
            base.Dispose();
        }
    }
}