using System;
using System.Collections.Generic;
using UnityHFSM.Graph;
using UnityHFSM.Graph.Compilation;

namespace UnityHFSM.Graph.Conditions
{
    /// <summary>
    /// Mutable parameter context for graph conditions. Trigger values are consumed
    /// after they are observed, matching one-shot trigger semantics.
    /// </summary>
    public sealed class HfsmParameterStore : IHfsmEvaluationContext
    {
        private readonly Dictionary<string, bool> _bools = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _floats = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _ints = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _triggers = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<bool>> _actionCompletion = new Dictionary<string, Func<bool>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<float>> _elapsedTime = new Dictionary<string, Func<float>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<string, bool>> _activeStates = new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal);

        public void LoadDefaults(IEnumerable<ParameterProgram> parameters, bool clearExisting = true)
        {
            if (clearExisting)
            {
                _bools.Clear();
                _floats.Clear();
                _ints.Clear();
                _triggers.Clear();
            }

            if (parameters == null)
                return;
            foreach (var parameter in parameters)
            {
                switch (parameter.ParameterType)
                {
                    case HfsmParameterType.Bool:
                        SetBool(parameter.Name, Convert.ToBoolean(parameter.DefaultValue));
                        break;
                    case HfsmParameterType.Float:
                        SetFloat(parameter.Name, Convert.ToSingle(parameter.DefaultValue));
                        break;
                    case HfsmParameterType.Int:
                        SetInt(parameter.Name, Convert.ToInt32(parameter.DefaultValue));
                        break;
                    case HfsmParameterType.Trigger:
                        ClearTrigger(parameter.Name);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public HfsmParameterStore SetBool(string name, bool value)
        {
            ValidateName(name);
            _bools[name] = value;
            return this;
        }

        public HfsmParameterStore SetFloat(string name, float value)
        {
            ValidateName(name);
            _floats[name] = value;
            return this;
        }

        public HfsmParameterStore SetInt(string name, int value)
        {
            ValidateName(name);
            _ints[name] = value;
            return this;
        }

        public HfsmParameterStore SetTrigger(string name)
        {
            ValidateName(name);
            _triggers.Add(name);
            return this;
        }

        public HfsmParameterStore ClearTrigger(string name)
        {
            if (!string.IsNullOrEmpty(name))
                _triggers.Remove(name);
            return this;
        }

        public HfsmParameterStore BindActionCompletion(string nodeId, Func<bool> evaluator)
        {
            Bind(_actionCompletion, nodeId, evaluator);
            return this;
        }

        public HfsmParameterStore BindNodeElapsedTime(string nodeId, Func<float> evaluator)
        {
            Bind(_elapsedTime, nodeId, evaluator);
            return this;
        }

        public HfsmParameterStore BindActiveState(string machineId, Func<string, bool> evaluator)
        {
            Bind(_activeStates, machineId, evaluator);
            return this;
        }

        public bool GetBool(string parameterName) => _bools.TryGetValue(parameterName ?? string.Empty, out var value) && value;
        public float GetFloat(string parameterName) => _floats.TryGetValue(parameterName ?? string.Empty, out var value) ? value : 0f;
        public int GetInt(string parameterName) => _ints.TryGetValue(parameterName ?? string.Empty, out var value) ? value : 0;

        public bool GetTrigger(string parameterName)
        {
            if (string.IsNullOrEmpty(parameterName) || !_triggers.Contains(parameterName))
                return false;
            _triggers.Remove(parameterName);
            return true;
        }

        public bool HasAllActionsCompleted(string nodeId)
        {
            return _actionCompletion.TryGetValue(nodeId ?? string.Empty, out var evaluator) && evaluator != null && evaluator();
        }

        public float GetNodeElapsedTime(string nodeId)
        {
            return _elapsedTime.TryGetValue(nodeId ?? string.Empty, out var evaluator) && evaluator != null ? evaluator() : 0f;
        }

        public bool IsStateActive(string stateMachineId, string stateId)
        {
            return _activeStates.TryGetValue(stateMachineId ?? string.Empty, out var evaluator) && evaluator != null && evaluator(stateId);
        }

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Parameter name cannot be empty.", nameof(name));
        }

        private static void Bind<T>(IDictionary<string, T> map, string key, T value)
        {
            ValidateName(key);
            if (ReferenceEquals(value, null))
                throw new ArgumentNullException(nameof(value));
            map[key] = value;
        }
    }
}
