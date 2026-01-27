using System.Collections.Generic;
using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点菜单自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class CreateNodeMenuHandle
    {
        /// <summary>
        /// 初始化时
        /// </summary>
        public virtual void Initialize(EditorGraphView graphView) { }

        /// <summary>
        /// 初始化缓存构建
        /// </summary>
        public virtual void InitializeCache(EditorGraphView graphView, List<ICreateNodeHandle> createNodeHandles) { }

        /// <summary>
        /// 获取默认过滤器
        /// </summary>
        public virtual ICreateNodeCollector GetDefaultFilter(EditorGraphView graphView) => null;

        /// <summary>
        /// 显示创建节点菜单
        /// </summary>
        public virtual void ShowCreateNodeMenu(EditorGraphView graphView, CreateNodeContext createNodeContext) { }

        /// <summary>
        /// 释放时
        /// </summary>
        public virtual void Dispose(EditorGraphView graphView) { }
    }
}