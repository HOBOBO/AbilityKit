using Emilia.Kit;
using UnityEngine.UIElements;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 自定义快捷键处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class GraphHotKeysHandle
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void Initialize(EditorGraphView graphView) { }

        /// <summary>
        /// Graph处理按键点击事件
        /// </summary>
        public virtual void OnGraphKeyDown(EditorGraphView graphView, KeyDownEvent evt) { }

        /// <summary>
        /// Tree按键点击事件
        /// </summary>
        public virtual void OnTreeKeyDown(EditorGraphView graphView, KeyDownEvent evt) { }

        /// <summary>
        /// 释放
        /// </summary>
        public virtual void Dispose() { }
    }
}