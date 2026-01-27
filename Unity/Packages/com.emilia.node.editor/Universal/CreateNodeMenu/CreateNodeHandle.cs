using System;
using UnityEngine;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 创建节点适配器
    /// </summary>
    public class CreateNodeHandle : ICreateNodeHandle
    {
        public object nodeData { get; set; }
        public Type editorNodeType { get; set; }
        public bool validity { get; set; } = true;
        public string path { get; set; }
        public int priority { get; set; }
        public Texture2D icon { get; set; }

        public virtual void Initialize(object arg)
        {
            CreateNodeHandleContext context = (CreateNodeHandleContext) arg;
            editorNodeType = context.defaultEditorNodeType;
        }
    }
}