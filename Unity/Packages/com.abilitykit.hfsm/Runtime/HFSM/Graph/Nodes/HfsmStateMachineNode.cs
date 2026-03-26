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
    /// <summary>
    /// Represents a state machine node in the HFSM graph.
    /// A state machine contains child states and can be nested.
    /// </summary>
    [Serializable]
    public class HfsmStateMachineNode : HfsmNodeBase
    {
        [SerializeField]
        private string _defaultStateId;

        [SerializeField]
        private List<string> _childNodeIds = new List<string>();

        [SerializeField]
        private List<string> _transitionIds = new List<string>();

        [SerializeField]
        private bool _rememberLastState;

        /// <summary>
        /// The ID of the default (start) state for this state machine.
        /// </summary>
        public string DefaultStateId
        {
            get => _defaultStateId;
            set => _defaultStateId = value;
        }

        /// <summary>
        /// IDs of child nodes in this state machine.
        /// </summary>
        public IReadOnlyList<string> ChildNodeIds => _childNodeIds;

        /// <summary>
        /// IDs of transitions in this state machine.
        /// </summary>
        public IReadOnlyList<string> TransitionIds => _transitionIds;

        /// <summary>
        /// If true, the state machine will return to its last active state instead of the start state.
        /// </summary>
        public bool RememberLastState
        {
            get => _rememberLastState;
            set => _rememberLastState = value;
        }

        public HfsmStateMachineNode()
        {
            _displayName = "New StateMachine";
            _nodeType = HfsmNodeType.StateMachine;
        }

        public HfsmStateMachineNode(string displayName) : base(displayName, HfsmNodeType.StateMachine)
        {
        }

        public override string GetName() => _displayName ?? "New StateMachine";

        public override string GetNodeTypeDescription() => "State Machine";

        public void AddChildNode(string nodeId)
        {
            if (!string.IsNullOrEmpty(nodeId) && !_childNodeIds.Contains(nodeId))
            {
                _childNodeIds.Add(nodeId);
            }
        }

        public void RemoveChildNode(string nodeId)
        {
            _childNodeIds.Remove(nodeId);
        }

        public void AddTransition(string transitionId)
        {
            if (!string.IsNullOrEmpty(transitionId) && !_transitionIds.Contains(transitionId))
            {
                _transitionIds.Add(transitionId);
            }
        }

        public void RemoveTransition(string transitionId)
        {
            _transitionIds.Remove(transitionId);
        }

        public override bool Validate()
        {
            if (!base.Validate())
                return false;

            if (Graph != null)
            {
                foreach (var childId in _childNodeIds)
                {
                    if (Graph.GetNodeById(childId) == null)
                    {
                        HfsmLog.LogError($"StateMachine '{DisplayName}' references non-existent child node '{childId}'.");
                        return false;
                    }
                }

                if (!string.IsNullOrEmpty(_defaultStateId) && Graph.GetNodeById(_defaultStateId) == null)
                {
                    HfsmLog.LogError($"StateMachine '{DisplayName}' has invalid default state ID '{_defaultStateId}'.");
                    return false;
                }
            }

            return true;
        }

        public override HfsmNodeBase Clone()
        {
            var clone = new HfsmStateMachineNode();
            clone._displayName = _displayName;
            clone._position = _position + new Vector2(50, 50);
            clone._size = _size;
            clone._defaultStateId = _defaultStateId;
            clone._childNodeIds = new List<string>(_childNodeIds);
            clone._transitionIds = new List<string>(_transitionIds);
            clone._rememberLastState = _rememberLastState;
            return clone;
        }
    }
}
