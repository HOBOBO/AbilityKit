namespace Emilia.Node.Editor
{
    /// <summary>
    /// 操作菜单工具类
    /// </summary>
    public static class OperateMenuUtility
    {
        /// <summary>
        /// 将路径转换为名称和组别
        /// </summary>
        public static void PathToNameAndCategory(string path, out string name, out string category)
        {
            if (path == null) path = string.Empty;

            var index = path.LastIndexOf('/');
            if (index >= 0)
            {
                name = index == path.Length - 1 ? string.Empty : path.Substring(index + 1);
                category = path.Substring(0, index + 1);
            }
            else
            {
                name = path;
                category = string.Empty;
            }
        }
    }
}