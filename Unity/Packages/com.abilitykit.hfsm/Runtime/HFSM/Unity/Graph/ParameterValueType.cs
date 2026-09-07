// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;

#if HFSM_UNITY
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
#endif


namespace AbilityKit.HFSM.Graph
{
    /// <summary>
    /// Represents the type of a parameter.
    /// </summary>
    public enum ParameterValueType
    {
        Bool,
        Float,
        Int,
        Trigger
    }
}
