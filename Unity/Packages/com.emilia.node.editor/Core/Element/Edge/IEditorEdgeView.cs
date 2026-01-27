using UnityEditor.Experimental.GraphView;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Edge表现元素接口
    /// </summary>
    public interface IEditorEdgeView : IDeleteGraphElement, IRemoveViewElement, IGraphCopyPasteElement, IGraphSelectable
    {
        /// <summary>
        /// 资产
        /// </summary>
        EditorEdgeAsset asset { get; }

        /// <summary>
        /// 所属GraphView
        /// </summary>
        EditorGraphView graphView { get; }

        /// <summary>
        /// Input端口
        /// </summary>
        IEditorPortView inputPortView { get; set; }

        /// <summary>
        /// Output端口
        /// </summary>
        IEditorPortView outputPortView { get; set; }

        /// <summary>
        /// 是否正在拖拽
        /// </summary>
        bool isDrag { get; set; }

        /// <summary>
        /// 元素
        /// </summary>
        Edge edgeElement { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize(EditorGraphView graphView, EditorEdgeAsset asset);

        /// <summary>
        /// 值改变
        /// </summary>
        void OnValueChanged(bool isSilent = false);

        /// <summary>
        /// 强制更新
        /// </summary>
        void ForceUpdateView();

        /// <summary>
        /// 释放
        /// </summary>
        void Dispose();
    }
}