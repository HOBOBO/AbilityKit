using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 创建节点菜单
    /// </summary>
    [Action("CreateNode", 1000, OperateMenuTagDefine.BaseActionTag)]
    public class CreateNodeAction : OperateMenuAction
    {
        public override void Execute(OperateMenuActionContext context)
        {
            context.graphView.graphOperate.OpenCreateNodeMenu(context.mousePosition);
        }
    }
}