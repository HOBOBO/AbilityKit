using System.Collections.Generic;
using System.Linq;
using Emilia.Node.Editor;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 节点拖拽复制
    /// </summary>
    public class NodeDuplicateDragger : MouseManipulator
    {
        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDownEvent);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDownEvent);
        }

        protected virtual void OnMouseDownEvent(MouseDownEvent evt)
        {
            bool isDown = evt.button == 0 && evt.shiftKey;
            if (isDown == false) return;

            IEditorNodeView editorNodeView = target as IEditorNodeView;
            string copy = editorNodeView.graphView.graphCopyPaste.SerializeGraphElementsCallback(new[] {editorNodeView.element});

            var pasteContent = editorNodeView.graphView.graphCopyPaste.UnserializeAndPasteCallback("Paste", copy);
            var nodeViews = pasteContent.OfType<IEditorNodeView>();

            IEditorNodeView pasteNode = nodeViews.FirstOrDefault();
            pasteNode.asset.position = editorNodeView.asset.position;
            pasteNode.SetPositionNoUndo(editorNodeView.asset.position);

            editorNodeView.graphView.SetSelection(new List<ISelectable> {pasteNode.element});
            editorNodeView.graphView.UpdateSelected();

            GraphSelectionDraggerForceSelectedNodeEvent selectEvent = GraphSelectionDraggerForceSelectedNodeEvent.Create(pasteNode.element, evt.mousePosition);
            editorNodeView.graphView.SendGraphEvent(selectEvent);
        }
    }
}