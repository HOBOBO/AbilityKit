using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 删除
    /// </summary>
    [Action("Delete", 6000, OperateMenuTagDefine.BaseActionTag)]
    public class DeleteAction : OperateMenuAction
    {
        public override OperateMenuActionValidity GetValidity(OperateMenuContext context) =>
            context.graphView.selection.Count > 0 ? OperateMenuActionValidity.Valid : OperateMenuActionValidity.Invalid;

        public override void Execute(OperateMenuActionContext context)
        {
            context.graphView.graphOperate.Delete();
        }
    }
}