using Emilia.Node.Editor;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 加载中提示
    /// </summary>
    public class GraphLoadingContainer : VisualElement
    {
        private EditorGraphView graphView;
        private Label label;

        public GraphLoadingContainer(EditorGraphView graphView)
        {
            name = nameof(GraphLoadingContainer);
            pickingMode = PickingMode.Ignore;

            this.StretchToParentSize();

            this.graphView = graphView;

            label = new Label("Loading...");
            label.style.position = Position.Absolute;

            label.style.left = 10;
            label.style.bottom = 10;

            Add(label);
        }

        public void DisplayLoading()
        {
            label.schedule.Execute(() => label.text = $"Loading:{graphView.loadProgress * 100:0.00}%").Until(() => graphView.isInitialized);
        }
    }
}