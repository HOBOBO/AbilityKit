using System;
using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 连接自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class ConnectSystemHandle
    {
        /// <summary>
        /// 获取连接器监听器类型（重写此函数对EdgeConnectorListener进行设置）
        /// </summary>
        public virtual Type GetConnectorListenerType(EditorGraphView graphView) => typeof(GraphEdgeConnectorListener);

        /// <summary>
        /// 根据端口获取EdgeAsset类型
        /// </summary>
        public virtual Type GetEdgeAssetTypeByPort(EditorGraphView graphView, IEditorPortView portView) => null;

        /// <summary>
        /// 判断两个端口是否可以连接
        /// </summary>
        public virtual bool CanConnect(EditorGraphView graphView, IEditorPortView inputPort, IEditorPortView outputPort) => false;

        /// <summary>
        /// 连接前的回调
        /// </summary>
        public virtual bool BeforeConnect(EditorGraphView graphView, IEditorPortView input, IEditorPortView output) => false;

        /// <summary>
        /// 连接后的回调
        /// </summary>
        public virtual void AfterConnect(EditorGraphView graphView, IEditorEdgeView edgeView) { }

        /// <summary>
        /// 断开连接后的回调
        /// </summary>
        public virtual void AfterDisconnect(EditorGraphView graphView, EditorEdgeAsset edgeAsset) { }
    }
}