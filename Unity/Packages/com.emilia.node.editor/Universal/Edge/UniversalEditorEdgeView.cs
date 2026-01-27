using Emilia.Node.Editor;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 通用EditorEdgeView实现
    /// </summary>
    [EditorEdge(typeof(UniversalEditorEdgeAsset))]
    public class UniversalEditorEdgeView : EditorEdgeView
    {
        public override void Initialize(EditorGraphView graphView, EditorEdgeAsset asset)
        {
            base.Initialize(graphView, asset);
            RegisterCallback<MouseDownEvent>((_) => OnMouseDownEvent());
        }

        protected virtual void OnMouseDownEvent()
        {
            graphView?.UpdateSelected();
        }
    }
}