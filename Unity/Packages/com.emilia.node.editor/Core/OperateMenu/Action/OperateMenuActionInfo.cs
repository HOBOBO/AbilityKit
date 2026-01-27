namespace Emilia.Node.Editor
{
    /// <summary>
    /// 操作菜单行为信息
    /// </summary>
    public struct OperateMenuActionInfo
    {
        /// <summary>
        /// 行为
        /// </summary>
        public IOperateMenuAction action;

        /// <summary>
        /// 名称
        /// </summary>
        public string name;

        /// <summary>
        /// 分组
        /// </summary>
        public string category;

        /// <summary>
        /// 优先级
        /// </summary>
        public int priority;

        /// <summary>
        /// 标签
        /// </summary>
        public string[] tags;
    }
}