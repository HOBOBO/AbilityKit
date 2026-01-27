using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 粘贴
    /// </summary>
    [Action("Paste", 5200, OperateMenuTagDefine.BaseActionTag)]
    public class PasteAction : OperateMenuAction
    {
        public override OperateMenuActionValidity GetValidity(OperateMenuContext context) =>
            context.graphView.graphCopyPaste.CanPasteSerializedDataCallback(context.graphView.GetSerializedData_Internal()) ? OperateMenuActionValidity.Valid : OperateMenuActionValidity.Invalid;

        public override void Execute(OperateMenuActionContext context)
        {
            context.graphView.graphOperate.Paste();
        }
    }
}