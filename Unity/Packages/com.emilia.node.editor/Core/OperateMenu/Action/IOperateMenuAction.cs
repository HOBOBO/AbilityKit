namespace Emilia.Node.Editor
{
    /// <summary>
    /// 操作菜单行为接口
    /// </summary>
    public interface IOperateMenuAction
    {
        /// <summary>
        /// 是否选中
        /// </summary>
        bool isOn { get; }

        /// <summary>
        /// 获取有效性
        /// </summary>
        OperateMenuActionValidity GetValidity(OperateMenuContext context);

        /// <summary>
        /// 执行
        /// </summary>
        void Execute(OperateMenuActionContext context);
    }
}