// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;
using System.Collections.Generic;
using System.Linq;

#if HFSM_UNITY
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
#endif

namespace UnityHFSM.Graph
{
    /// <summary>
    /// The main asset class that stores an HFSM graph definition.
    /// This ScriptableObject contains all nodes, edges, and parameters for a hierarchical finite state machine.
    /// </summary>
#if HFSM_UNITY
    [CreateAssetMenu(fileName = "New HFSM Graph", menuName = "AbilityKit/HFSM/Graph Asset", order = 1)]
    public partial class HfsmGraphAsset : ScriptableObject, ISerializationCallbackReceiver
#else
    [System.Serializable]
    public partial class HfsmGraphAsset
#endif
    {
        [SerializeField]
        private string _graphName = "New HFSM Graph";

        [SerializeField]
        private List<HfsmNodeBase> _nodes = new List<HfsmNodeBase>();

        // Serialization helpers for polymorphic node types
#if HFSM_UNITY
        [SerializeField]
        private List<HfsmStateNode> _serializedStateNodes = new List<HfsmStateNode>();

        [SerializeField]
        private List<HfsmStateMachineNode> _serializedStateMachineNodes = new List<HfsmStateMachineNode>();
#endif

        [SerializeField]
        private List<HfsmTransitionEdge> _edges = new List<HfsmTransitionEdge>();

        [SerializeField]
        private List<HfsmParameter> _parameters = new List<HfsmParameter>();

        [SerializeField]
        private string _rootStateMachineId;

        [SerializeField]
        private HfsmGraphMetadata _metadata = new HfsmGraphMetadata();

        /// <summary>
        /// The display name of this graph.
        /// </summary>
        public string GraphName
        {
            get => _graphName;
            set => _graphName = value;
        }

        /// <summary>
        /// All nodes in this graph.
        /// </summary>
        public IReadOnlyList<HfsmNodeBase> Nodes => _nodes;

        /// <summary>
        /// All transition edges in this graph.
        /// </summary>
        public IReadOnlyList<HfsmTransitionEdge> Edges => _edges;

        /// <summary>
        /// All parameters in this graph.
        /// </summary>
        public IReadOnlyList<HfsmParameter> Parameters => _parameters;

        /// <summary>
        /// The ID of the root state machine node.
        /// </summary>
        public string RootStateMachineId
        {
            get => _rootStateMachineId;
            set => _rootStateMachineId = value;
        }

        /// <summary>
        /// Metadata for the graph editor view (zoom, pan, etc.).
        /// </summary>
        public HfsmGraphMetadata Metadata => _metadata;

        /// <summary>
        /// Event raised when the graph structure changes.
        /// </summary>
        public event Action<GraphChangeType, object> GraphChanged;

        /// <summary>
        /// Initializes the graph and sets up node references.
        /// </summary>
        public void Initialize()
        {
            foreach (var node in _nodes)
            {
                node.Graph = this;
            }
        }

        /// <summary>
        /// Adds a node to the graph.
        /// </summary>
        public void AddNode(HfsmNodeBase node)
        {
            if (node == null)
                return;

            node.Graph = this;
            _nodes.Add(node);
            node.OnNodeCreated();
            NotifyGraphChanged(GraphChangeType.NodeAdded, node);
        }

        /// <summary>
        /// Removes a node from the graph.
        /// </summary>
        public bool RemoveNode(HfsmNodeBase node)
        {
            if (node == null)
                return false;

            RemoveEdgesInvolvingNode(node.Id);
            _nodes.Remove(node);
            node.OnNodeDestroyed();

            if (_rootStateMachineId == node.Id)
            {
                _rootStateMachineId = null;
            }

            NotifyGraphChanged(GraphChangeType.NodeRemoved, node);
            return true;
        }

        /// <summary>
        /// Removes a node by its ID.
        /// </summary>
        public bool RemoveNodeById(string nodeId)
        {
            var node = GetNodeById(nodeId);
            return RemoveNode(node);
        }

        /// <summary>
        /// Gets a node by its ID.
        /// </summary>
        public HfsmNodeBase GetNodeById(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return null;

            foreach (var node in _nodes)
            {
                if (node.Id == nodeId)
                    return node;
            }
            return null;
        }

        /// <summary>
        /// Gets a node by its ID with type checking.
        /// </summary>
        public T GetNodeById<T>(string nodeId) where T : HfsmNodeBase
        {
            return GetNodeById(nodeId) as T;
        }

        /// <summary>
        /// Gets all nodes of a specific type.
        /// </summary>
        public IEnumerable<T> GetNodesOfType<T>() where T : HfsmNodeBase
        {
            foreach (var node in _nodes)
            {
                if (node is T typedNode)
                    yield return typedNode;
            }
        }

        /// <summary>
        /// Adds an edge to the graph.
        /// </summary>
        public void AddEdge(HfsmTransitionEdge edge)
        {
            if (edge == null)
                return;

            _edges.Add(edge);
            NotifyGraphChanged(GraphChangeType.EdgeAdded, edge);
        }

        /// <summary>
        /// Removes an edge from the graph.
        /// </summary>
        public bool RemoveEdge(HfsmTransitionEdge edge)
        {
            if (edge == null)
                return false;

            _edges.Remove(edge);
            NotifyGraphChanged(GraphChangeType.EdgeRemoved, edge);
            return true;
        }

        /// <summary>
        /// Removes an edge by its ID.
        /// </summary>
        public bool RemoveEdgeById(string edgeId)
        {
            var edge = GetEdgeById(edgeId);
            return RemoveEdge(edge);
        }

        /// <summary>
        /// Gets an edge by its ID.
        /// </summary>
        public HfsmTransitionEdge GetEdgeById(string edgeId)
        {
            if (string.IsNullOrEmpty(edgeId))
                return null;

            foreach (var edge in _edges)
            {
                if (edge.Id == edgeId)
                    return edge;
            }
            return null;
        }

        /// <summary>
        /// Gets all edges originating from a specific node.
        /// </summary>
        public IEnumerable<HfsmTransitionEdge> GetOutgoingEdges(string nodeId)
        {
            foreach (var edge in _edges)
            {
                if (edge.SourceNodeId == nodeId)
                    yield return edge;
            }
        }

        /// <summary>
        /// Gets all edges targeting a specific node.
        /// </summary>
        public IEnumerable<HfsmTransitionEdge> GetIncomingEdges(string nodeId)
        {
            foreach (var edge in _edges)
            {
                if (edge.TargetNodeId == nodeId)
                    yield return edge;
            }
        }

        /// <summary>
        /// Removes all edges involving a specific node.
        /// </summary>
        public void RemoveEdgesInvolvingNode(string nodeId)
        {
            _edges.RemoveAll(e => e.InvolvesNode(nodeId));
        }

        /// <summary>
        /// Adds a parameter to the graph.
        /// </summary>
        public void AddParameter(HfsmParameter parameter)
        {
            if (parameter == null)
                return;

            if (HasParameter(parameter.Name))
            {
                HfsmLog.LogWarning($"Parameter with name '{parameter.Name}' already exists in graph '{_graphName}'.");
                return;
            }

            _parameters.Add(parameter);
            NotifyGraphChanged(GraphChangeType.ParameterAdded, parameter);
        }

        /// <summary>
        /// Removes a parameter from the graph.
        /// </summary>
        public bool RemoveParameter(HfsmParameter parameter)
        {
            if (parameter == null)
                return false;

            _parameters.Remove(parameter);
            NotifyGraphChanged(GraphChangeType.ParameterRemoved, parameter);
            return true;
        }

        /// <summary>
        /// Gets a parameter by name.
        /// </summary>
        public HfsmParameter GetParameterByName(string parameterName)
        {
            if (string.IsNullOrEmpty(parameterName))
                return null;

            foreach (var parameter in _parameters)
            {
                if (parameter.Name == parameterName)
                    return parameter;
            }
            return null;
        }

        /// <summary>
        /// Checks if a parameter with the given name exists.
        /// </summary>
        public bool HasParameter(string parameterName)
        {
            return GetParameterByName(parameterName) != null;
        }

        /// <summary>
        /// Gets the root state machine node.
        /// </summary>
        public HfsmStateMachineNode GetRootStateMachine()
        {
            return GetNodeById<HfsmStateMachineNode>(_rootStateMachineId);
        }

        /// <summary>
        /// Sets the root state machine node.
        /// </summary>
        public void SetRootStateMachine(HfsmStateMachineNode stateMachine)
        {
            _rootStateMachineId = stateMachine?.Id;
            NotifyGraphChanged(GraphChangeType.RootChanged, stateMachine);
        }

        /// <summary>
        /// Creates a new state node and adds it to the graph.
        /// </summary>
        public HfsmStateNode CreateState(string displayName, Vector2 position)
        {
            var state = new HfsmStateNode(displayName);
            state.Position = position;
            AddNode(state);
            return state;
        }

        /// <summary>
        /// Creates a new state machine node and adds it to the graph.
        /// </summary>
        public HfsmStateMachineNode CreateStateMachine(string displayName, Vector2 position)
        {
            var stateMachine = new HfsmStateMachineNode(displayName);
            stateMachine.Position = position;
            AddNode(stateMachine);

            if (_rootStateMachineId == null)
            {
                SetRootStateMachine(stateMachine);
            }

            return stateMachine;
        }

        /// <summary>
        /// Creates a new transition edge between two nodes.
        /// </summary>
        public HfsmTransitionEdge CreateTransition(string sourceNodeId, string targetNodeId)
        {
            var edge = new HfsmTransitionEdge(sourceNodeId, targetNodeId);
            AddEdge(edge);
            return edge;
        }

        /// <summary>
        /// Validates the entire graph for consistency and errors.
        /// </summary>
        public bool Validate()
        {
            bool isValid = true;

            foreach (var node in _nodes)
            {
                if (!node.Validate())
                    isValid = false;
            }

            foreach (var edge in _edges)
            {
                if (!ValidateEdge(edge))
                    isValid = false;
            }

            if (string.IsNullOrEmpty(_rootStateMachineId))
            {
                HfsmLog.LogError($"Graph '{_graphName}' has no root state machine set.");
                isValid = false;
            }
            else if (GetNodeById(_rootStateMachineId) == null)
            {
                HfsmLog.LogError($"Graph '{_graphName}' has invalid root state machine ID.");
                isValid = false;
            }

            return isValid;
        }

        private bool ValidateEdge(HfsmTransitionEdge edge)
        {
            if (string.IsNullOrEmpty(edge.SourceNodeId))
            {
                HfsmLog.LogError($"Edge '{edge.Id}' has no source node.");
                return false;
            }

            if (string.IsNullOrEmpty(edge.TargetNodeId))
            {
                HfsmLog.LogError($"Edge '{edge.Id}' has no target node.");
                return false;
            }

            // Allow pseudo nodes like AnyState as a valid source.
            if (edge.SourceNodeId != HfsmSpecialNodeIds.AnyState && GetNodeById(edge.SourceNodeId) == null)
            {
                HfsmLog.LogError($"Edge '{edge.Id}' references non-existent source node '{edge.SourceNodeId}'.");
                return false;
            }

            if (GetNodeById(edge.TargetNodeId) == null)
            {
                HfsmLog.LogError($"Edge '{edge.Id}' references non-existent target node '{edge.TargetNodeId}'.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a deep copy of this graph.
        /// </summary>
        public HfsmGraphAsset Clone()
        {
#if HFSM_UNITY
            var clone = ScriptableObject.CreateInstance<HfsmGraphAsset>();
#else
            var clone = new HfsmGraphAsset();
#endif
            clone._graphName = _graphName + " (Clone)";
            clone._rootStateMachineId = _rootStateMachineId;
            clone._metadata = new HfsmGraphMetadata(_metadata);

            var nodeIdMap = new Dictionary<string, string>();

            foreach (var node in _nodes)
            {
                var clonedNode = node.Clone();
                clonedNode.Graph = clone;
                clone._nodes.Add(clonedNode);
                nodeIdMap[node.Id] = clonedNode.Id;
            }

            foreach (var edge in _edges)
            {
                var clonedEdge = edge.Clone();
                clonedEdge.SourceNodeId = nodeIdMap.TryGetValue(edge.SourceNodeId, out var newSourceId) ? newSourceId : edge.SourceNodeId;
                clonedEdge.TargetNodeId = nodeIdMap.TryGetValue(edge.TargetNodeId, out var newTargetId) ? newTargetId : edge.TargetNodeId;
                clone._edges.Add(clonedEdge);
            }

            foreach (var parameter in _parameters)
            {
                clone._parameters.Add(parameter.Clone(parameter.Name));
            }

            return clone;
        }

        private void NotifyGraphChanged(GraphChangeType changeType, object data)
        {
            GraphChanged?.Invoke(changeType, data);
        }

        /// <summary>
        /// Clears all nodes, edges, and parameters from the graph.
        /// </summary>
        public void Clear()
        {
            foreach (var node in _nodes)
            {
                node.OnNodeDestroyed();
            }

            _nodes.Clear();
            _edges.Clear();
            _parameters.Clear();
            _rootStateMachineId = null;
            NotifyGraphChanged(GraphChangeType.Cleared, null);
        }

#if HFSM_UNITY
        private void OnEnable()
        {
            Initialize();
        }

        private void OnValidate()
        {
            Initialize();
        }
#endif
    }

    /// <summary>
    /// Types of changes that can occur in the graph.
    /// </summary>
    public enum GraphChangeType
    {
        NodeAdded,
        NodeRemoved,
        EdgeAdded,
        EdgeRemoved,
        ParameterAdded,
        ParameterRemoved,
        RootChanged,
        MetadataChanged,
        Cleared
    }

    /// <summary>
    /// Metadata for the graph editor view.
    /// </summary>
    [Serializable]
    public class HfsmGraphMetadata
    {
        [SerializeField]
        private float _zoom = 1.0f;

        public float Zoom
        {
            get => _zoom;
            set => _zoom = Mathf.Clamp(value, 0.1f, 2.0f);
        }

        [SerializeField]
        private Vector2 _pan;

        public Vector2 Pan
        {
            get => _pan;
            set => _pan = value;
        }

        [SerializeField]
        private List<string> _expandedStateMachineIds = new List<string>();

        public IReadOnlyList<string> ExpandedStateMachineIds => _expandedStateMachineIds;

        public HfsmGraphMetadata() { }

        public HfsmGraphMetadata(HfsmGraphMetadata other)
        {
            _zoom = other._zoom;
            _pan = other._pan;
            _expandedStateMachineIds = new List<string>(other._expandedStateMachineIds);
        }

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
    }

    /// <summary>
    /// Implementation of ISerializationCallbackReceiver to properly serialize polymorphic node types.
    /// Unity cannot serialize abstract classes directly, so we use typed lists for serialization.
    /// </summary>
#if HFSM_UNITY
    public partial class HfsmGraphAsset : ISerializationCallbackReceiver
    {
        public void OnBeforeSerialize()
        {
            // Split polymorphic nodes into typed lists before serialization
            _serializedStateNodes.Clear();
            _serializedStateMachineNodes.Clear();

            foreach (var node in _nodes)
            {
                if (node is HfsmStateNode stateNode)
                {
                    _serializedStateNodes.Add(stateNode);
                }
                else if (node is HfsmStateMachineNode smNode)
                {
                    _serializedStateMachineNodes.Add(smNode);
                }
            }
        }

        public void OnAfterDeserialize()
        {
            // Reconstruct the polymorphic list from typed lists after deserialization
            _nodes.Clear();

            foreach (var node in _serializedStateNodes)
            {
                node.Graph = this;
                _nodes.Add(node);
            }

            foreach (var node in _serializedStateMachineNodes)
            {
                node.Graph = this;
                _nodes.Add(node);
            }

            // Reset condition cache for all edges to ensure proper deserialization
            // Note: _edges list is automatically restored by Unity's serialization
            foreach (var edge in _edges)
            {
                if (edge != null)
                {
                    edge.InvalidateConditionsCache();
                }
            }
        }
    }
#endif
}
