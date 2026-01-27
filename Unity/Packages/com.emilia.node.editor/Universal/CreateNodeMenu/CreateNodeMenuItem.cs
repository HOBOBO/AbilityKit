namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点菜单项数据结构
    /// </summary>
    public class CreateNodeMenuItem
    {
        /// <summary>
        /// 组目标
        /// </summary>
        public CreateNodeMenuItem parent;

        /// <summary>
        /// 节点信息
        /// </summary>
        public CreateNodeInfo info;

        /// <summary>
        /// 标题
        /// </summary>
        public string title;

        /// <summary>
        /// 菜单项层级
        /// </summary>
        public int level;

        public CreateNodeMenuItem() { }

        public CreateNodeMenuItem(CreateNodeInfo info, string title, int level)
        {
            this.info = info;
            this.title = title;
            this.level = level;
        }
    }
}