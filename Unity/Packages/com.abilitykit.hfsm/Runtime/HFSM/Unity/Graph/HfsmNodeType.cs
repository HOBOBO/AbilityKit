// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;

#if HFSM_UNITY
using Vector2 = UnityEngine.Vector2;
#endif

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Represents the type of a node in the HFSM graph.
    /// </summary>
    public enum HfsmNodeType
    {
        /// <summary>
        /// A leaf state that can execute actions.
        /// </summary>
        State,

        /// <summary>
        /// A state machine that contains child states.
        /// </summary>
        StateMachine,

        /// <summary>
        /// The entry point of a state machine.
        /// </summary>
        Entry,

        /// <summary>
        /// Special "any state" transitions that can trigger from any state.
        /// </summary>
        AnyState
    }

    /// <summary>
    /// Contains special node IDs used for pseudo nodes in the graph.
    /// </summary>
    public static class HfsmSpecialNodeIds
    {
        /// <summary>
        /// The node ID for the "Any State" pseudo node.
        /// </summary>
        public const string AnyState = "__ANY_STATE__";
    }
}
