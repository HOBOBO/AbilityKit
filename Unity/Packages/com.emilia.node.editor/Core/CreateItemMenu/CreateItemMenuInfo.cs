using System;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建Item菜单信息
    /// </summary>
    public struct CreateItemMenuInfo
    {
        /// <summary>
        /// itemAsset的类型
        /// </summary>
        public Type itemAssetType;

        /// <summary>
        /// 菜单路径
        /// </summary>
        public string path;
    }
}