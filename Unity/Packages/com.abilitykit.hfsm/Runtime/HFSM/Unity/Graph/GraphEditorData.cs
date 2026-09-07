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
    /// 图编辑器数据实现
    /// </summary>
    [Serializable]
    public class GraphEditorData : IGraphEditorData
    {
        [SerializeField]
        private float _zoom = 1.0f;

        [SerializeField]
        private Vector2 _pan;

        [SerializeField]
        private List<string> _expandedStateMachineIds = new List<string>();

        [SerializeField]
        private List<NodeEditorData> _nodeEditorData = new List<NodeEditorData>();

        private Dictionary<string, NodeEditorData> _nodeDataCache;

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

        public INodeEditorData GetNodeEditorData(string nodeId)
        {
            EnsureCacheInitialized();
            _nodeDataCache.TryGetValue(nodeId, out var data);
            return data;
        }

        public INodeEditorData GetOrCreateNodeEditorData(string nodeId)
        {
            EnsureCacheInitialized();

            if (!_nodeDataCache.TryGetValue(nodeId, out var data))
            {
                data = new NodeEditorData(nodeId);
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

        public IEnumerable<INodeEditorData> GetAllNodeEditorData()
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
                _nodeDataCache = new Dictionary<string, NodeEditorData>();
                foreach (var data in _nodeEditorData)
                {
                    _nodeDataCache[data.NodeId] = data;
                }
            }
        }

        public GraphEditorData Clone()
        {
            return Clone(new Dictionary<string, string>());
        }

        internal GraphEditorData Clone(IReadOnlyDictionary<string, string> nodeIdMap)
        {
            var clone = new GraphEditorData
            {
                _zoom = _zoom,
                _pan = _pan,
                _expandedStateMachineIds = RemapIds(_expandedStateMachineIds, nodeIdMap)
            };

            clone._nodeDataCache = new Dictionary<string, NodeEditorData>();
            foreach (var data in _nodeEditorData)
            {
                var nodeId = nodeIdMap.TryGetValue(data.NodeId, out var remappedId)
                    ? remappedId
                    : data.NodeId;
                var clonedData = data.Clone(nodeId);
                clone._nodeEditorData.Add(clonedData);
                clone._nodeDataCache[clonedData.NodeId] = clonedData;
            }

            return clone;
        }

        private static List<string> RemapIds(
            List<string> ids,
            IReadOnlyDictionary<string, string> nodeIdMap)
        {
            var result = new List<string>(ids.Count);
            foreach (var id in ids)
            {
                result.Add(nodeIdMap.TryGetValue(id, out var remappedId) ? remappedId : id);
            }

            return result;
        }
    }
}
