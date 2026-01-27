using System.Collections.Generic;
using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建Item菜单自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class CreateItemMenuHandle
    {
        /// <summary>
        /// 收集Item的菜单项
        /// </summary>
        public virtual void CollectItemMenus(EditorGraphView graphView, List<CreateItemMenuInfo> itemTypes) { }
    }
}