using System;
using Emilia.Kit;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点适配器
    /// </summary>
    [EditorHandleGenerate]
    public interface ICreateNodeHandle
    {
        /// <summary>
        /// 节点数据
        /// </summary>
        object nodeData { get; }

        /// <summary>
        /// 编辑器节点类型
        /// </summary>
        Type editorNodeType { get; }

        /// <summary>
        /// 有效性
        /// </summary>
        bool validity { get; }

        /// <summary>
        /// 菜单路径
        /// </summary>
        string path { get; }

        /// <summary>
        /// 优先级
        /// </summary>
        int priority { get; }

        /// <summary>
        /// 菜单图标
        /// </summary>
        Texture2D icon { get; }

        /// <summary>
        /// 初始化
        /// </summary>
        void Initialize(object arg);
    }
}