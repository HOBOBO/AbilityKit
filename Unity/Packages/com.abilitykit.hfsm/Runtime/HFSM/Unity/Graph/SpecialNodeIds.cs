// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;

#if HFSM_UNITY
using Vector2 = UnityEngine.Vector2;
#endif


namespace AbilityKit.HFSM.Graph
{

    /// <summary>
    /// Contains special node IDs used for pseudo nodes in the graph.
    /// </summary>
    public static class SpecialNodeIds
    {
        /// <summary>
        /// The node ID for the "Any State" pseudo node.
        /// </summary>
        public const string AnyState = "__ANY_STATE__";
    }
}
