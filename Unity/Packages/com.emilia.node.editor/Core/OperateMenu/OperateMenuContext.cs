using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 操作菜单上下文
    /// </summary>
    public struct OperateMenuContext
    {
        public EditorGraphView graphView;
        public ContextualMenuPopulateEvent evt;
    }
}