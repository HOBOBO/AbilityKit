using Emilia.Kit;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Graph基础操作系统
    /// </summary>
    public class GraphOperate : BasicGraphViewModule
    {
        private GraphOperateHandle handle;
        public override int order => 200;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            handle = EditorHandleUtility.CreateHandle<GraphOperateHandle>(graphView.graphAsset.GetType());
        }

        /// <summary>
        /// 打开创建节点菜单
        /// </summary>
        public void OpenCreateNodeMenu(Vector2 mousePosition, CreateNodeContext createNodeContext = default)
        {
            handle?.OpenCreateNodeMenu(this.graphView, mousePosition, createNodeContext);
        }

        /// <summary>
        /// 剪切
        /// </summary>
        public void Cut()
        {
            handle?.Cut(graphView);
        }

        /// <summary>
        /// 拷贝
        /// </summary>
        public void Copy()
        {
            handle?.Copy(graphView);
        }

        /// <summary>
        /// 粘贴
        /// </summary>
        public void Paste(Vector2? mousePosition = null)
        {
            handle?.Paste(graphView, mousePosition);
        }

        /// <summary>
        /// 删除
        /// </summary>
        public void Delete()
        {
            handle?.Delete(graphView);
        }

        /// <summary>
        /// 复制
        /// </summary>
        public void Duplicate()
        {
            handle?.Duplicate(graphView);
        }

        /// <summary>
        /// 保存
        /// </summary>
        public void Save()
        {
            handle?.Save(graphView);
        }

        public override void Dispose()
        {
            handle = null;
            base.Dispose();
        }
    }
}