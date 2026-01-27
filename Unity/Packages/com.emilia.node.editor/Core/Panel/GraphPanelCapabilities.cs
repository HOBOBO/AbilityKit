using System;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 面板功能
    /// </summary>
    [Flags]
    public enum GraphPanelCapabilities
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,

        /// <summary>
        /// 面板可以移动
        /// </summary>
        Movable = 1,

        /// <summary>
        /// 面板可以调整大小
        /// </summary>
        Resizable = 2
    }
}