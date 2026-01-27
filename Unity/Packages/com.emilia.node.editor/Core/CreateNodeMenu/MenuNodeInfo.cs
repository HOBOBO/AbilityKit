using System;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点菜单信息
    /// </summary>
    public struct MenuNodeInfo
    {
        /// <summary>
        /// 编辑器节点资源类型
        /// </summary>
        public Type editorNodeAssetType;

        /// <summary>
        /// 节点数据
        /// </summary>
        public object nodeData;

        /// <summary>
        /// 菜单路径
        /// </summary>
        public string path;

        /// <summary>
        /// 菜单优先级
        /// </summary>
        public int priority;

        /// <summary>
        /// 菜单图标
        /// </summary>
        public Texture2D icon;
    }
}