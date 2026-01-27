using System;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 用于创建端口时的信息
    /// </summary>
    public class EditorPortInfo
    {
        /// <summary>
        /// 端口Id
        /// </summary>
        public string id { get; set; }

        /// <summary>
        /// 端口显示名称
        /// </summary>
        public string displayName { get; set; }

        /// <summary>
        /// 端口Tips
        /// </summary>
        public string tips { get; set; }

        /// <summary>
        /// IEditorPortView的Type
        /// </summary>
        public Type nodePortViewType { get; set; } = typeof(EditorPortView);

        /// <summary>
        /// EdgeConnector的Type
        /// </summary>
        public Type edgeConnectorType { get; set; } = typeof(EditorEdgeConnector);

        /// <summary>
        /// 端口类型
        /// </summary>
        public Type portType { get; set; }

        /// <summary>
        /// 端口方向
        /// </summary>
        public EditorPortDirection direction { get; set; }

        /// <summary>
        /// 取向
        /// </summary>
        public EditorOrientation orientation { get; set; }

        /// <summary>
        /// 是否可以多连接
        /// </summary>
        public bool canMultiConnect { get; set; }

        /// <summary>
        /// 顺序
        /// </summary>
        public float order { get; set; }

        /// <summary>
        /// 优先级
        /// </summary>
        public int priority { get; set; }

        /// <summary>
        /// 端口颜色
        /// </summary>
        public Color color { get; set; } = Color.white;
    }
}