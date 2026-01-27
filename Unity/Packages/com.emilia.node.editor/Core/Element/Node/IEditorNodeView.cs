using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Node表现元素接口
    /// </summary>
    public interface IEditorNodeView : IDeleteGraphElement, IRemoveViewElement, IGraphCopyPasteElement, IGraphSelectable
    {
        /// <summary>
        /// 所属GraphView
        /// </summary>
        EditorGraphView graphView { get; }

        /// <summary>
        /// 节点资产
        /// </summary>
        EditorNodeAsset asset { get; }

        /// <summary>
        /// 节点元素
        /// </summary>
        GraphElement element { get; }

        /// <summary>
        /// 是否展开
        /// </summary>
        bool expanded { get; set; }

        /// <summary>
        /// 端口View列表
        /// </summary>
        IReadOnlyList<IEditorPortView> portViews { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize(EditorGraphView graphView, EditorNodeAsset asset);

        /// <summary>
        /// 收集静态端口资源
        /// </summary>
        List<EditorPortInfo> CollectStaticPortAssets();

        /// <summary>
        /// 根据id获取IEditorPortView
        /// </summary>
        IEditorPortView GetPortView(string portId);

        /// <summary>
        /// 添加IEditorPortView
        /// </summary>
        IEditorPortView AddPortView(int index, EditorPortInfo asset);

        /// <summary>
        /// 设置位置
        /// </summary>
        void SetPosition(Rect position);

        /// <summary>
        /// 设置位置，不记录撤销
        /// </summary>
        void SetPositionNoUndo(Rect position);

        /// <summary>
        /// 设置是否展开，不记录撤销
        /// </summary>
        void SetExpandedNoUndo(bool expanded, bool isRebuild = false);
        
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