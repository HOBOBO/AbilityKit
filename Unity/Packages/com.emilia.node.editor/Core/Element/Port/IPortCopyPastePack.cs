using System;
using System.Collections.Generic;
using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Port拷贝粘贴Pack接口
    /// </summary>
    public interface IPortCopyPastePack : ICopyPastePack
    {
        /// <summary>
        /// 拷贝的节点Id
        /// </summary>
        string nodeId { get; }

        /// <summary>
        /// 拷贝的节点中的端口Id
        /// </summary>
        string portId { get; }

        /// <summary>
        /// 端口类型
        /// </summary>
        Type portType { get; }

        /// <summary>
        /// 端口方向
        /// </summary>
        EditorPortDirection direction { get; }

        /// <summary>
        /// 拷贝的Edge
        /// </summary>
        List<IEdgeCopyPastePack> connectionPacks { get; }
    }
}