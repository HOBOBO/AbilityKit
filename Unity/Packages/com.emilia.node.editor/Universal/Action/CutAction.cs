using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 剪切
    /// </summary>
    [Action("Cut", 5400, OperateMenuTagDefine.BaseActionTag)]
    public class CutAction : OperateMenuAction
    {
        public override OperateMenuActionValidity GetValidity(OperateMenuContext context) =>
            context.graphView.selection.Count > 0 ? OperateMenuActionValidity.Valid : OperateMenuActionValidity.Invalid;

        public override void Execute(OperateMenuActionContext context)
        {
            context.graphView.graphOperate.Cut();
        }
    }
}