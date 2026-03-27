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

namespace UnityHFSM.Graph
{
    // ========================================================================
    // 节点编辑器数据
    // ========================================================================

    /// <summary>
    /// 节点编辑器数据接口 - 存储节点在编辑器中的可视化信息
    /// 运行时不需要此数据，可以完全排除
    /// </summary>
    public interface IHfsmNodeEditorData
    {
        /// <summary>
        /// 关联的节点 ID
        /// </summary>
        string NodeId { get; }

        /// <summary>
        /// 节点在图视图中的位置
        /// </summary>
        Vector2 Position { get; set; }

        /// <summary>
        /// 节点在图视图中的大小
        /// </summary>
        Vector2 Size { get; set; }

        /// <summary>
        /// 是否在编辑器中展开（用于复合节点）
        /// </summary>
        bool IsExpanded { get; set; }

        /// <summary>
        /// 节点颜色（自定义染色）
        /// </summary>
        Color? CustomColor { get; set; }
    }

    /// <summary>
    /// 节点编辑器数据实现
    /// </summary>
    [Serializable]
    public class HfsmNodeEditorData : IHfsmNodeEditorData
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

        public HfsmNodeEditorData() { }

        public HfsmNodeEditorData(string nodeId)
        {
            _nodeId = nodeId;
        }

        public HfsmNodeEditorData(string nodeId, Vector2 position, Vector2 size)
        {
            _nodeId = nodeId;
            _position = position;
            _size = size;
        }

        public HfsmNodeEditorData Clone(string newNodeId)
        {
            return new HfsmNodeEditorData
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

    // ========================================================================
    // 图编辑器数据
    // ========================================================================

    /// <summary>
    /// 图编辑器数据接口 - 存储整个图在编辑器中的状态
    /// </summary>
    public interface IHfsmGraphEditorData
    {
        /// <summary>
        /// 图缩放级别
        /// </summary>
        float Zoom { get; set; }

        /// <summary>
        /// 图平移偏移
        /// </summary>
        Vector2 Pan { get; set; }

        /// <summary>
        /// 展开的状态机 ID 列表
        /// </summary>
        IReadOnlyList<string> ExpandedStateMachineIds { get; }

        /// <summary>
        /// 切换状态机的展开状态
        /// </summary>
        void ToggleExpanded(string stateMachineId);

        /// <summary>
        /// 检查状态机是否展开
        /// </summary>
        bool IsExpanded(string stateMachineId);

        /// <summary>
        /// 获取节点的编辑器数据
        /// </summary>
        IHfsmNodeEditorData GetNodeEditorData(string nodeId);

        /// <summary>
        /// 创建或获取节点的编辑器数据
        /// </summary>
        IHfsmNodeEditorData GetOrCreateNodeEditorData(string nodeId);

        /// <summary>
        /// 移除节点的编辑器数据
        /// </summary>
        void RemoveNodeEditorData(string nodeId);

        /// <summary>
        /// 获取所有节点编辑器数据
        /// </summary>
        IEnumerable<IHfsmNodeEditorData> GetAllNodeEditorData();

        /// <summary>
        /// 清除所有编辑器数据
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// 图编辑器数据实现
    /// </summary>
    [Serializable]
    public class HfsmGraphEditorData : IHfsmGraphEditorData
    {
        [SerializeField]
        private float _zoom = 1.0f;

        [SerializeField]
        private Vector2 _pan;

        [SerializeField]
        private List<string> _expandedStateMachineIds = new List<string>();

        [SerializeField]
        private List<HfsmNodeEditorData> _nodeEditorData = new List<HfsmNodeEditorData>();

        private Dictionary<string, HfsmNodeEditorData> _nodeDataCache;

        public float Zoom
        {
            get => _zoom;
            set => _zoom = Mathf.Clamp(value, 0.1f, 2.0f);
        }

        public Vector2 Pan
        {
            get => _pan;
            set => _pan = value;
        }

        public IReadOnlyList<string> ExpandedStateMachineIds => _expandedStateMachineIds;

        public void ToggleExpanded(string stateMachineId)
        {
            if (_expandedStateMachineIds.Contains(stateMachineId))
            {
                _expandedStateMachineIds.Remove(stateMachineId);
            }
            else
            {
                _expandedStateMachineIds.Add(stateMachineId);
            }
        }

        public bool IsExpanded(string stateMachineId)
        {
            return _expandedStateMachineIds.Contains(stateMachineId);
        }

        public IHfsmNodeEditorData GetNodeEditorData(string nodeId)
        {
            EnsureCacheInitialized();
            _nodeDataCache.TryGetValue(nodeId, out var data);
            return data;
        }

        public IHfsmNodeEditorData GetOrCreateNodeEditorData(string nodeId)
        {
            EnsureCacheInitialized();

            if (!_nodeDataCache.TryGetValue(nodeId, out var data))
            {
                data = new HfsmNodeEditorData(nodeId);
                _nodeEditorData.Add(data);
                _nodeDataCache[nodeId] = data;
            }

            return data;
        }

        public void RemoveNodeEditorData(string nodeId)
        {
            EnsureCacheInitialized();

            if (_nodeDataCache.TryGetValue(nodeId, out var data))
            {
                _nodeEditorData.Remove(data);
                _nodeDataCache.Remove(nodeId);
            }
        }

        public IEnumerable<IHfsmNodeEditorData> GetAllNodeEditorData()
        {
            EnsureCacheInitialized();
            return _nodeDataCache.Values;
        }

        public void Clear()
        {
            _nodeEditorData.Clear();
            _expandedStateMachineIds.Clear();
            _nodeDataCache?.Clear();
            _nodeDataCache = null;
        }

        private void EnsureCacheInitialized()
        {
            if (_nodeDataCache == null)
            {
                _nodeDataCache = new Dictionary<string, HfsmNodeEditorData>();
                foreach (var data in _nodeEditorData)
                {
                    _nodeDataCache[data.NodeId] = data;
                }
            }
        }

        public HfsmGraphEditorData Clone()
        {
            var clone = new HfsmGraphEditorData
            {
                _zoom = _zoom,
                _pan = _pan,
                _expandedStateMachineIds = new List<string>(_expandedStateMachineIds)
            };

            clone._nodeDataCache = new Dictionary<string, HfsmNodeEditorData>();
            foreach (var data in _nodeEditorData)
            {
                var clonedData = data.Clone(Guid.NewGuid().ToString("N"));
                clone._nodeEditorData.Add(clonedData);
                clone._nodeDataCache[clonedData.NodeId] = clonedData;
            }

            return clone;
        }
    }
}
