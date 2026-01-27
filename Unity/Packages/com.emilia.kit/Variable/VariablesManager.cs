using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emilia.Variables
{
    [Serializable]
    public class VariablesManager
    {
        [SerializeField]
        private Dictionary<string, Variable> _variableMap = new Dictionary<string, Variable>();

        private VariablesManager _parentManager;

        /// <summary>
        /// 所有参数
        /// </summary>
        public IReadOnlyDictionary<string, Variable> variableMap => this._variableMap;

        /// <summary>
        /// 父级参数管理器
        /// </summary>
        public VariablesManager parentManager
        {
            get => this._parentManager;
            set => this._parentManager = value;
        }

        /// <summary>
        /// 获取值
        /// </summary>
        public T GetValue<T>(string key)
        {
            if (this._variableMap.TryGetValue(key, out var value) == false)
            {
                if (parentManager != null) return parentManager.GetValue<T>(key);
                return default;
            }

            Variable<T> parameterValue = value as Variable<T>;
            if (parameterValue == null) return default;
            return parameterValue.value;
        }

        /// <summary>
        /// 获取值（仅自身）
        /// </summary>
        public T GetThisValue<T>(string key)
        {
            if (this._variableMap.TryGetValue(key, out var value) == false) return default;
            Variable<T> parameterValue = value as Variable<T>;
            if (parameterValue != null) return parameterValue.value;
            VariableObject varObject = value as VariableObject;
            if (varObject != null) return (T) varObject.GetValue();
            return default;
        }

        /// <summary>
        /// 设置值
        /// </summary>
        public bool SetValue<T>(string key, T value)
        {
            if (parentManager != null && parentManager.HasKey(key)) return parentManager.SetValue(key, value);

            if (this._variableMap.TryGetValue(key, out Variable variable) == false)
            {
                variable = VariableUtility.Create<T>();
                this._variableMap[key] = variable;
            }

            Variable<T> parameterValue = variable as Variable<T>;
            if (parameterValue == null) return false;
            parameterValue.value = value;
            return true;
        }

        /// <summary>
        /// 设置值（仅自身）
        /// </summary>
        public bool SetThisValue<T>(string key, T value)
        {
            if (this._variableMap.TryGetValue(key, out Variable parameter) == false)
            {
                parameter = VariableUtility.Create<T>();
                this._variableMap[key] = parameter;
            }

            Variable<T> parameterValue = parameter as Variable<T>;
            if (parameterValue == null)
            {
                VariableObject varObject = parameter as VariableObject;
                if (varObject != null) varObject.SetValue(value);
                return false;
            }

            parameterValue.value = value;
            return true;
        }

        /// <summary>
        /// 设置值（仅自身）
        /// </summary>
        public void SetThisValue(string key, Variable value)
        {
            this._variableMap[key] = value;
        }

        /// <summary>
        /// 是否存在Key
        /// </summary>
        public bool HasKey(string key)
        {
            if (this._variableMap.ContainsKey(key)) return true;
            if (parentManager != null) return parentManager.HasKey(key);
            return false;
        }

        /// <summary>
        /// 是否存在Key（仅自身）
        /// </summary>
        public bool HasThisKey(string key)
        {
            return this._variableMap.ContainsKey(key);
        }

        /// <summary>
        /// 尝试获取值（仅自身）
        /// </summary>
        public Variable GetThisValue(string key)
        {
            return this._variableMap.GetValueOrDefault(key);
        }

        /// <summary>
        /// 移除Key（仅自身）
        /// </summary>
        public void Remove(string key)
        {
            if (this._variableMap.ContainsKey(key)) this._variableMap.Remove(key);
        }

        /// <summary>
        /// 克隆
        /// </summary>
        public VariablesManager Clone()
        {
            VariablesManager cloneVariablesManage = new VariablesManager();

            foreach (var item in variableMap)
            {
                Variable variable = VariableUtility.Create(item.Value.type);
                variable.SetValue(item.Value.GetValue());
                cloneVariablesManage.SetThisValue(item.Key, variable);
            }

            return cloneVariablesManage;
        }

        /// <summary>
        /// 清理数据（仅自身）
        /// </summary>
        public void Clear()
        {
            this._variableMap.Clear();
        }
    }
}