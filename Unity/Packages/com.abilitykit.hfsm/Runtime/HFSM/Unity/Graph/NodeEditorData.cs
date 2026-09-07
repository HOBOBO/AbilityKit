// ============================================================================
// Editor Data - 编辑器数据
// 定义编辑器专用数据的抽象，允许在运行时完全排除编辑器代码
// ============================================================================

// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;
using System.Collections.Generic;
#if HFSM_UNITY
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
#endif


namespace AbilityKit.HFSM.Graph
{

    /// <summary>
    /// 节点编辑器数据实现
    /// </summary>
    [Serializable]
    public class NodeEditorData : INodeEditorData
    {
        [SerializeField]
        private string _nodeId;

        [SerializeField]
        private Vector2 _position;

        [SerializeField]
        private Vector2 _size = new Vector2(150, 60);

        [SerializeField]
        private bool _isExpanded = true;

        [SerializeField]
        private bool _hasCustomColor;

        [SerializeField]
        private Color _customColor = Color.white;

        public string NodeId => _nodeId;

        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        public Vector2 Size
        {
            get => _size;
            set => _size = value;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set => _isExpanded = value;
        }

        public Color? CustomColor
        {
            get => _hasCustomColor ? (Color?)_customColor : null;
            set
            {
                if (value.HasValue)
                {
                    _hasCustomColor = true;
                    _customColor = value.Value;
                }
                else
                {
                    _hasCustomColor = false;
                }
            }
        }

        public NodeEditorData() { }

        public NodeEditorData(string nodeId)
        {
            _nodeId = nodeId;
        }

        public NodeEditorData(string nodeId, Vector2 position, Vector2 size)
        {
            _nodeId = nodeId;
            _position = position;
            _size = size;
        }

        public NodeEditorData Clone(string newNodeId)
        {
            return new NodeEditorData
            {
                _nodeId = newNodeId,
                _position = _position + new Vector2(50, 50),
                _size = _size,
                _isExpanded = _isExpanded,
                _hasCustomColor = _hasCustomColor,
                _customColor = _customColor
            };
        }
    }
}
