using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 拷贝
    /// </summary>
    [Action("Copy", 5100, OperateMenuTagDefine.BaseActionTag)]
    public class CopyAction : OperateMenuAction
    {
        public override OperateMenuActionValidity GetValidity(OperateMenuContext context) =>
            context.graphView.selection.Count > 0 ? OperateMenuActionValidity.Valid : OperateMenuActionValidity.Invalid;

        public override void Execute(OperateMenuActionContext context)
        {
            context.graphView.graphOperate.Copy();
        }
    }
}