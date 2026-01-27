using System;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 操作菜单项数据结构
    /// </summary>
    public struct OperateMenuItem
    {
        /// <summary>
        /// 菜单名称
        /// </summary>
        public string menuName;

        /// <summary>
        /// 菜单分组
        /// </summary>
        public string category;

        /// <summary>
        /// 菜单优先级
        /// </summary>
        public int priority;

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool isOn;

        /// <summary>
        /// 有效性
        /// </summary>
        public OperateMenuActionValidity state;

        /// <summary>
        /// 执行操作
        /// </summary>
        public Action onAction;
    }
}