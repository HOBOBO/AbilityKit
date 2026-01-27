using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 通用面板
    /// </summary>
    public class GraphPanel : GraphElement, IGraphPanel
    {
        protected GraphPanelCapabilities _panelCapabilities;

        /// <summary>
        /// 面板ID
        /// </summary>
        public virtual string id { get; set; }

        /// <summary>
        /// 功能
        /// </summary>
        public virtual GraphPanelCapabilities panelCapabilities
        {
            get => _panelCapabilities;
            set => _panelCapabilities = value;
        }

        /// <summary>
        /// 父视图
        /// </summary>
        public GraphTwoPaneSplitView parentView { get; set; }
        public VisualElement rootView => this;
        protected EditorGraphView graphView { get; private set; }

        public virtual void Initialize(EditorGraphView graphView)
        {
            this.graphView = graphView;
        }

        public virtual void Dispose() { }
    }
}