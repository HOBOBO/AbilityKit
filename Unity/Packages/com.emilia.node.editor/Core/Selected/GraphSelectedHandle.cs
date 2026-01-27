using System.Collections.Generic;
using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 选中自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class GraphSelectedHandle
    {
        /// <summary>
        /// 初始化
        /// </summary>
        public virtual void Initialize(EditorGraphView graphView) { }

        /// <summary>
        /// 刷新选中的Inspector
        /// </summary>
        public virtual void UpdateSelectedInspector(EditorGraphView graphView, List<ISelectedHandle> selection) { }

        /// <summary>
        /// 收集选中的绘制器
        /// </summary>
        public virtual void CollectSelectedDrawer(EditorGraphView graphView, List<IGraphSelectedDrawer> drawers) { }

        /// <summary>
        /// 在更新选中内容之前调用
        /// </summary>
        public virtual void BeforeUpdateSelected(EditorGraphView graphView, List<ISelectedHandle> selection) { }

        /// <summary>
        /// 在更新选中内容之后调用
        /// </summary>
        public virtual void AfterUpdateSelected(EditorGraphView graphView, List<ISelectedHandle> selection) { }

        /// <summary>
        /// 释放时
        /// </summary>
        public virtual void Dispose(EditorGraphView graphView) { }
    }
}