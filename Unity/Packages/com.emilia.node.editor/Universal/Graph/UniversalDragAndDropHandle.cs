using Emilia.Kit;
using Emilia.Node.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用拖拽处理
    /// </summary>
    [EditorHandle(typeof(EditorUniversalGraphAsset))]
    public class UniversalDragAndDropHandle : GraphDragAndDropHandle
    {
        public const string CreateNodeDragAndDropType = "CreateNode";

        public override void DragPerformedCallback(EditorGraphView graphView, DragPerformEvent evt)
        {
            base.DragPerformedCallback(graphView, evt);
            object genericData = DragAndDrop.GetGenericData(CreateNodeDragAndDropType);
            if (genericData is ICreateNodeHandle createNodeHandle)
            {
                Vector2 mousePosition = evt.mousePosition;
                Vector2 graphMousePosition = graphView.contentViewContainer.WorldToLocal(mousePosition);

                graphView.nodeSystem.CreateNode(createNodeHandle.editorNodeType, graphMousePosition, createNodeHandle.nodeData);
            }
        }

        public override void DragUpdatedCallback(EditorGraphView graphView, DragUpdatedEvent evt)
        {
            base.DragUpdatedCallback(graphView, evt);
            object genericData = DragAndDrop.GetGenericData(CreateNodeDragAndDropType);
            if (genericData is ICreateNodeHandle createNodeHandle)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
                DragAndDrop.AcceptDrag();
            }
        }
    }
}