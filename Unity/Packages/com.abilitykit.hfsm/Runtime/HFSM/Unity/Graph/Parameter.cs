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
    /// Represents a parameter in the HFSM graph.
    /// Parameters are used to define transition conditions.
    /// </summary>
    [Serializable]
    public class Parameter
    {
        [SerializeField]
        private string _name;

        [SerializeField]
        private ParameterValueType _parameterType;

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
        public ParameterValueType ParameterType
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

        public Parameter()
        {
            _name = "New Parameter";
            _parameterType = ParameterValueType.Bool;
        }

        public Parameter(string name, ParameterValueType parameterType)
        {
            _name = name;
            _parameterType = parameterType;
        }

        public Parameter Clone(string newName)
        {
            var clone = new Parameter();
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
                ParameterValueType.Bool => _defaultBoolValue,
                ParameterValueType.Float => _defaultFloatValue,
                ParameterValueType.Int => _defaultIntValue,
                ParameterValueType.Trigger => false,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public override string ToString()
        {
            return $"{_name} ({_parameterType})";
        }
    }
}
