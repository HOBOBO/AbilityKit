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
        private bool _defaultBoolValue;

        [SerializeField]
        private float _defaultFloatValue;

        [SerializeField]
        private int _defaultIntValue;

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

        public bool DefaultBoolValue
        {
            get => _defaultBoolValue;
            set => _defaultBoolValue = value;
        }

        public float DefaultFloatValue
        {
            get => _defaultFloatValue;
            set => _defaultFloatValue = value;
        }

        public int DefaultIntValue
        {
            get => _defaultIntValue;
            set => _defaultIntValue = value;
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
            clone._defaultBoolValue = _defaultBoolValue;
            clone._defaultFloatValue = _defaultFloatValue;
            clone._defaultIntValue = _defaultIntValue;
            return clone;
        }

        /// <summary>
        /// 获取序列化的默认值（用于导出）
        /// </summary>
        public object GetSerializedDefaultValue()
        {
            return _parameterType switch
            {
                HfsmParameterType.Bool => _defaultBoolValue,
                HfsmParameterType.Float => _defaultFloatValue,
                HfsmParameterType.Int => _defaultIntValue,
                HfsmParameterType.Trigger => false,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public override string ToString()
        {
            return $"{_name} ({_parameterType})";
        }
    }
}
