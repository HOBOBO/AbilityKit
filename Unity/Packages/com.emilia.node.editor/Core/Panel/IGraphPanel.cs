using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 面板接口
    /// </summary>
    public interface IGraphPanel
    {
        /// <summary>
        /// 面板id
        /// </summary>
        string id { get; set; }

        /// <summary>
        /// 面板功能
        /// </summary>
        GraphPanelCapabilities panelCapabilities { get; }

        /// <summary>
        /// 停靠模式下的父视图
        /// </summary>
        GraphTwoPaneSplitView parentView { get; set; }

        /// <summary>
        /// 根视图
        /// </summary>
        VisualElement rootView { get; }

        /// <summary>
        /// 初始化处理
        /// </summary>
        void Initialize(EditorGraphView graphView);

        /// <summary>
        /// 销毁处理
        /// </summary>
        void Dispose();
    }
}