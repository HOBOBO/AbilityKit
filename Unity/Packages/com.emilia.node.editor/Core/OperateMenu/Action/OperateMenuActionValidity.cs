namespace Emilia.Node.Editor
{
    /// <summary>
    /// 操作菜单行为有效性类型
    /// </summary>
    public enum OperateMenuActionValidity
    {
        /// <summary>
        /// 有效
        /// 正常显示
        /// </summary>
        Valid,

        /// <summary>
        /// 不适用
        /// 不显示
        /// </summary>
        NotApplicable,

        /// <summary>
        /// 无效
        /// 灰色显示
        /// </summary>
        Invalid,
    }
}