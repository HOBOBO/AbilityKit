using Emilia.Kit;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Graph自定义基础操作处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class GraphOperateHandle
    {
        /// <summary>
        /// 打开创建节点菜单
        /// </summary>
        public virtual void OpenCreateNodeMenu(EditorGraphView graphView, Vector2 mousePosition, CreateNodeContext createNodeContext = default) { }

        /// <summary>
        /// 剪切选中内容
        /// </summary>
        public virtual void Cut(EditorGraphView graphView) { }

        /// <summary>
        /// 复制选中内容
        /// </summary>
        public virtual void Copy(EditorGraphView graphView) { }

        /// <summary>
        /// 粘贴
        /// </summary>
        public virtual void Paste(EditorGraphView graphView, Vector2? mousePosition = null) { }

        /// <summary>
        /// 删除选中内容
        /// </summary>
        public virtual void Delete(EditorGraphView graphView) { }

        /// <summary>
        /// 复制选中内容
        /// </summary>
        public virtual void Duplicate(EditorGraphView graphView) { }

        /// <summary>
        /// 保存
        /// </summary>
        public virtual void Save(EditorGraphView graphView) { }
    }
}