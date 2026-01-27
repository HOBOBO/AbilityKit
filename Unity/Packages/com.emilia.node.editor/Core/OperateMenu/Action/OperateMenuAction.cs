namespace Emilia.Node.Editor
{
    /// <summary>
    /// 操作菜单行为
    /// </summary>
    public abstract class OperateMenuAction : IOperateMenuAction
    {
        public virtual bool isOn => false;

        public virtual OperateMenuActionValidity GetValidity(OperateMenuContext context) => OperateMenuActionValidity.Valid;

        public abstract void Execute(OperateMenuActionContext context);
    }
}