using System.Collections.Generic;
using Emilia.Kit;
using UnityEditor.Experimental.GraphView;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// Graph拷贝粘贴自定义处理器
    /// </summary>
    [EditorHandleGenerate]
    public abstract class GraphCopyPasteHandle
    {
        /// <summary>
        /// 序列化GraphElements回调
        /// </summary>
        public virtual string SerializeGraphElementsCallback(EditorGraphView graphView, IEnumerable<GraphElement> elements) => null;

        /// <summary>
        /// 检查是否可以粘贴序列化数据回调
        /// </summary>
        public virtual bool CanPasteSerializedDataCallback(EditorGraphView graphView, string serializedData) => false;

        /// <summary>
        /// 反序列化并粘贴GraphElements回调
        /// </summary>
        public virtual IEnumerable<GraphElement> UnserializeAndPasteCallback(EditorGraphView graphView, string operationName, string serializedData, GraphCopyPasteContext copyPasteContext) => null;

        /// <summary>
        /// 拷贝
        /// </summary>
        public virtual object CreateCopy(EditorGraphView graphView, object value) => null;

        /// <summary>
        /// 获取拷贝的GraphElements
        /// </summary>
        public virtual IEnumerable<GraphElement> GetCopyGraphElements(EditorGraphView graphView, string serializedData) => null;
    }
}