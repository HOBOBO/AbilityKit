using Emilia.Node.Editor;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 刷新
    /// </summary>
    [Action("Refresh", 8000, OperateMenuTagDefine.UniversalActionTag)]
    public class RefreshAction : OperateMenuAction
    {
        public override void Execute(OperateMenuActionContext context)
        {
            context.graphView.Reload(context.graphView.graphAsset);
        }
    }
}