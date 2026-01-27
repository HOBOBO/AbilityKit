using Emilia.Kit;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 快捷键系统
    /// </summary>
    public class GraphHotKeys : BasicGraphViewModule
    {
        private GraphHotKeysHandle handle;
        public override int order => 800;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            this.handle = EditorHandleUtility.CreateHandle<GraphHotKeysHandle>(graphView.graphAsset.GetType());
            this.handle?.Initialize(graphView);

            graphView.UnregisterCallback<KeyDownEvent>(OnGraphKeyDown);
            graphView.RegisterCallback<KeyDownEvent>(OnGraphKeyDown);

            graphView.panel?.visualTree?.UnregisterCallback<KeyDownEvent>(OnTreeKeyDown);
            graphView.panel?.visualTree?.RegisterCallback<KeyDownEvent>(OnTreeKeyDown);
        }

        private void OnGraphKeyDown(KeyDownEvent evt)
        {
            this.handle?.OnGraphKeyDown(this.graphView, evt);
        }

        private void OnTreeKeyDown(KeyDownEvent evt)
        {
            this.handle?.OnTreeKeyDown(this.graphView, evt);
        }

        public override void Dispose()
        {
            if (this.handle != null)
            {
                this.handle.Dispose();
                this.handle = null;
            }

            if (graphView != null)
            {
                graphView.UnregisterCallback<KeyDownEvent>(OnGraphKeyDown);
                graphView.panel?.visualTree?.UnregisterCallback<KeyDownEvent>(OnTreeKeyDown);
            }

            base.Dispose();
        }
    }
}