using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 操作菜单行为上下文
    /// </summary>
    public struct OperateMenuActionContext
    {
        /// <summary>
        /// 所属GraphView
        /// </summary>
        public EditorGraphView graphView;

        /// <summary>
        /// 鼠标位置
        /// </summary>
        public Vector2 mousePosition;
    }
}