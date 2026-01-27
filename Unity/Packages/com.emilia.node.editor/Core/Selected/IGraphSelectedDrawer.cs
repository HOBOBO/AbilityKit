using System.Collections.Generic;
using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 选中绘制器接口
    /// </summary>
    public interface IGraphSelectedDrawer
    {
        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize(EditorGraphView graphView);

        /// <summary>
        /// 更新选中状态
        /// </summary>
        void SelectedUpdate(List<ISelectedHandle> selection);

        /// <summary>
        /// 释放
        /// </summary>
        void Dispose();
    }
}