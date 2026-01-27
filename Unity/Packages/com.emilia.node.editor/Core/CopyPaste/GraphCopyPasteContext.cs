using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 拷贝粘贴上下文
    /// </summary>
    public struct GraphCopyPasteContext
    {
        public EditorGraphView graphView;

        /// <summary>
        /// 粘贴时创建的位置（为空时以拷贝目标为基准）
        /// </summary>
        public Vector2? createPosition;
    }
}