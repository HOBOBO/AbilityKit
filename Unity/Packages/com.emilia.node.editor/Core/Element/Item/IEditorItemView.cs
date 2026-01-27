using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Item表现元素接口
    /// </summary>
    public interface IEditorItemView : IDeleteGraphElement, IRemoveViewElement, IGraphCopyPasteElement, IGraphSelectable
    {
        /// <summary>
        /// 资产
        /// </summary>
        EditorItemAsset asset { get; }

        /// <summary>
        /// Item元素
        /// </summary>
        GraphElement element { get; }

        /// <summary>
        /// 图形视图
        /// </summary>
        EditorGraphView graphView { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize(EditorGraphView graphView, EditorItemAsset asset);

        /// <summary>
        /// 设置位置
        /// </summary>
        /// <param name="position"></param>
        void SetPosition(Rect position);

        /// <summary>
        /// 设置位置，不记录撤销
        /// </summary>
        void SetPositionNoUndo(Rect position);

        /// <summary>
        /// 值改变
        /// </summary>
        void OnValueChanged(bool isSilent = false);

        /// <summary>
        /// 释放
        /// </summary>
        void Dispose();
    }
}