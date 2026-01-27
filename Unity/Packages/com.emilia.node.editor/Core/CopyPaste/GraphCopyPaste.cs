using System.Collections.Generic;
using Emilia.Kit;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Graph拷贝粘贴系统（序列化系统）
    /// </summary>
    public class GraphCopyPaste : BasicGraphViewModule
    {
        private GraphCopyPasteHandle handle;
        public override int order => 300;

        public override void Initialize(EditorGraphView graphView)
        {
            base.Initialize(graphView);
            handle = EditorHandleUtility.CreateHandle<GraphCopyPasteHandle>(graphView.graphAsset.GetType());
        }

        /// <summary>
        /// 序列化处理
        /// </summary>
        public string SerializeGraphElementsCallback(IEnumerable<GraphElement> elements)
        {
            if (this.handle == null) return string.Empty;
            return handle.SerializeGraphElementsCallback(this.graphView, elements);
        }

        /// <summary>
        /// 是否可以序列化数据
        /// </summary>
        public bool CanPasteSerializedDataCallback(string serializedData)
        {
            if (this.handle == null || string.IsNullOrEmpty(serializedData)) return false;
            return this.handle.CanPasteSerializedDataCallback(graphView, serializedData);
        }

        /// <summary>
        /// 反序列化处理
        /// </summary>
        public IEnumerable<GraphElement> UnserializeAndPasteCallback(string operationName, string serializedData, Vector2? mousePosition = null)
        {
            if (this.handle == null) return null;
            GraphCopyPasteContext graphCopyPasteContext = new();
            graphCopyPasteContext.graphView = this.graphView;
            graphCopyPasteContext.createPosition = mousePosition;

            return this.handle.UnserializeAndPasteCallback(graphView, operationName, serializedData, graphCopyPasteContext);
        }

        /// <summary>
        /// 获取拷贝元素
        /// </summary>
        public IEnumerable<GraphElement> GetCopyGraphElements(string serializedData)
        {
            if (this.handle == null) return null;
            return handle.GetCopyGraphElements(graphView, serializedData);
        }

        /// <summary>
        /// 创建拷贝
        /// </summary>
        public object CreateCopy(object value)
        {
            if (handle == null) return null;
            return handle.CreateCopy(graphView, value);
        }

        public override void Dispose()
        {
            handle = null;
            base.Dispose();
        }
    }
}