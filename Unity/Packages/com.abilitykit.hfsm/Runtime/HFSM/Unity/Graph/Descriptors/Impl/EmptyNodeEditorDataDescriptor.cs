// ============================================================================
// Editor Data Descriptor Implementations - 编辑器元数据描述器实现
// ============================================================================

using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Descriptor.Impl
{

    /// <summary>
    /// 空编辑器元数据描述器（当没有编辑器数据时返回）
    /// </summary>
    public class EmptyNodeEditorDataDescriptor : INodeEditorDataDescriptor
    {
        private static readonly EmptyNodeEditorDataDescriptor _instance = new EmptyNodeEditorDataDescriptor();

        public static EmptyNodeEditorDataDescriptor Instance => _instance;

        private EmptyNodeEditorDataDescriptor() { }

        public string NodeId => string.Empty;

        public UnityEngine.Vector2 Position
        {
            get => UnityEngine.Vector2.zero;
            set { }
        }

        public UnityEngine.Vector2 Size
        {
            get => new UnityEngine.Vector2(150, 60);
            set { }
        }

        public bool IsExpanded
        {
            get => true;
            set { }
        }

        public UnityEngine.Color? CustomColor
        {
            get => null;
            set { }
        }
    }
}
