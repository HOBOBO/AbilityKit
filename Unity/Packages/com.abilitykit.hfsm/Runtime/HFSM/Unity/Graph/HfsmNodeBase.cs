// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;

#if HFSM_UNITY
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
#endif

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Base class for all nodes in the HFSM graph.
    /// </summary>
    [Serializable]
    public abstract class HfsmNodeBase
    {
        [SerializeField]
        protected string _id;

        [SerializeField]
        protected string _displayName;

        [SerializeField]
        protected HfsmNodeType _nodeType;

        [SerializeField]
        protected Vector2 _position;

        [SerializeField]
        protected Vector2 _size = new Vector2(150, 60);

        /// <summary>
        /// Unique identifier for this node.
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Display name shown in the editor.
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        /// <summary>
        /// The type of this node.
        /// </summary>
        public HfsmNodeType NodeType => _nodeType;

        /// <summary>
        /// Position of this node in the graph view.
        /// </summary>
        public Vector2 Position
        {
            get => _position;
            set => _position = value;
        }

        /// <summary>
        /// Size of this node in the graph view.
        /// </summary>
        public Vector2 Size
        {
            get => _size;
            set => _size = value;
        }

        /// <summary>
        /// The graph this node belongs to.
        /// </summary>
        public HfsmGraphAsset Graph { get; set; }

        /// <summary>
        /// Whether this node is the default start state of its parent state machine.
        /// </summary>
        public bool isDefault { get; set; }

        /// <summary>
        /// Parent state machine ID for nested state machines.
        /// </summary>
        public string ParentStateMachineId { get; set; }

        protected HfsmNodeBase()
        {
            _id = Guid.NewGuid().ToString("N");
            _displayName = "New Node";
        }

        protected HfsmNodeBase(string displayName, HfsmNodeType nodeType)
        {
            _id = Guid.NewGuid().ToString("N");
            _displayName = displayName;
            _nodeType = nodeType;
        }

        /// <summary>
        /// Gets the name used for the state machine key.
        /// </summary>
        public virtual string GetName() => _displayName ?? "Unnamed";

        /// <summary>
        /// Gets a description of this node type.
        /// </summary>
        public abstract string GetNodeTypeDescription();

        /// <summary>
        /// Validates this node.
        /// </summary>
        public virtual bool Validate()
        {
            if (string.IsNullOrEmpty(_id))
            {
                HfsmLog.LogError("Node has an empty ID.");
                return false;
            }

            if (string.IsNullOrEmpty(_displayName))
            {
                HfsmLog.LogError($"Node with ID '{_id}' has an empty display name.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Called when the node is created.
        /// </summary>
        public virtual void OnNodeCreated() { }

        /// <summary>
        /// Called when the node is destroyed.
        /// </summary>
        public virtual void OnNodeDestroyed() { }

        /// <summary>
        /// Creates a deep clone of this node.
        /// </summary>
        public abstract HfsmNodeBase Clone();
    }
}
