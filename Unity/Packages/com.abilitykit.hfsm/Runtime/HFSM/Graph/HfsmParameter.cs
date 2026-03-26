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
    /// Represents the type of a parameter.
    /// </summary>
    public enum HfsmParameterType
    {
        Bool,
        Float,
        Int,
        Trigger
    }

    /// <summary>
    /// Represents a parameter in the HFSM graph.
    /// Parameters are used to define transition conditions.
    /// </summary>
    [Serializable]
    public class HfsmParameter
    {
        [SerializeField]
        private string _name;

        [SerializeField]
        private HfsmParameterType _parameterType;

        [SerializeField]
        private string _defaultValueJson;

        /// <summary>
        /// Name of this parameter.
        /// </summary>
        public string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <summary>
        /// Type of this parameter.
        /// </summary>
        public HfsmParameterType ParameterType
        {
            get => _parameterType;
            set => _parameterType = value;
        }

        /// <summary>
        /// JSON representation of the default value.
        /// </summary>
        public string DefaultValueJson
        {
            get => _defaultValueJson;
            set => _defaultValueJson = value;
        }

        public HfsmParameter()
        {
            _name = "New Parameter";
            _parameterType = HfsmParameterType.Bool;
        }

        public HfsmParameter(string name, HfsmParameterType parameterType)
        {
            _name = name;
            _parameterType = parameterType;
        }

        public HfsmParameter Clone(string newName)
        {
            var clone = new HfsmParameter();
            clone._name = newName ?? _name;
            clone._parameterType = _parameterType;
            clone._defaultValueJson = _defaultValueJson;
            return clone;
        }

        public override string ToString()
        {
            return $"{_name} ({_parameterType})";
        }
    }
}
