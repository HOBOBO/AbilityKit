// Auto-define HFSM_UNITY based on Unity platform defines
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS || UNITY_SERVER || UNITY_SERVER
#define HFSM_UNITY
#endif

using System;
using System.Collections.Generic;
using UnityHFSM.Graph.Conditions;

#if HFSM_UNITY
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
#endif

namespace UnityHFSM.Graph
{
    /// <summary>
    /// Represents a transition edge between two nodes in the HFSM graph.
    /// </summary>
    [Serializable]
    public class HfsmTransitionEdge
    {
        [SerializeField]
        private string _id;

        [SerializeField]
        private string _sourceNodeId;

        [SerializeField]
        private string _targetNodeId;

        [SerializeField]
        private string _conditionConfigJson;

        [SerializeField]
        private int _priority;

        [SerializeField]
        private bool _isExitTransition;

        [SerializeField]
        private bool _forceInstantly;

        /// <summary>
        /// 条件列表（运行时使用，不序列化）
        /// </summary>
        [NonSerialized]
        private List<HfsmTransitionCondition> _conditions;

        /// <summary>
        /// 条件组合方式：true = AND（所有条件都满足），false = OR（任一条件满足）
        /// </summary>
        [SerializeField]
        private bool _useAndLogic = true;

        /// <summary>
        /// Unique identifier for this edge.
        /// </summary>
        public string Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>
        /// The ID of the source node (where the transition starts).
        /// </summary>
        public string SourceNodeId
        {
            get => _sourceNodeId;
            set => _sourceNodeId = value;
        }

        /// <summary>
        /// The ID of the target node (where the transition goes to).
        /// </summary>
        public string TargetNodeId
        {
            get => _targetNodeId;
            set => _targetNodeId = value;
        }

        /// <summary>
        /// JSON configuration for the transition conditions.
        /// </summary>
        public string ConditionConfigJson
        {
            get => _conditionConfigJson;
            set => _conditionConfigJson = value;
        }

        /// <summary>
        /// Priority of this transition (higher = checked first).
        /// </summary>
        public int Priority
        {
            get => _priority;
            set => _priority = value;
        }

        /// <summary>
        /// If true, this transition exits the current state machine.
        /// </summary>
        public bool IsExitTransition
        {
            get => _isExitTransition;
            set => _isExitTransition = value;
        }

        /// <summary>
        /// If true, this transition forces an immediate state change.
        /// </summary>
        public bool ForceInstantly
        {
            get => _forceInstantly;
            set => _forceInstantly = value;
        }

        /// <summary>
        /// 条件组合方式：true = AND（所有条件都满足），false = OR（任一条件满足）
        /// </summary>
        public bool UseAndLogic
        {
            get => _useAndLogic;
            set => _useAndLogic = value;
        }

        /// <summary>
        /// 条件是否已从JSON加载
        /// </summary>
        private bool _conditionsLoaded = false;

        /// <summary>
        /// 获取条件列表（从JSON加载后缓存）
        /// </summary>
        public List<HfsmTransitionCondition> Conditions
        {
            get
            {
                if (!_conditionsLoaded)
                {
                    _conditions = HfsmConditionSerializer.Deserialize(_conditionConfigJson);
                    _conditionsLoaded = true;
                }
                return _conditions;
            }
        }

        /// <summary>
        /// 获取条件列表（强制重新从JSON加载）
        /// </summary>
        public List<HfsmTransitionCondition> GetConditionsFresh()
        {
            _conditions = HfsmConditionSerializer.Deserialize(_conditionConfigJson);
            _conditionsLoaded = true;
            return _conditions;
        }

        /// <summary>
        /// 标记条件列表需要重新加载（当从反序列化恢复对象时调用）
        /// </summary>
        public void InvalidateConditionsCache()
        {
            _conditionsLoaded = false;
        }

        public HfsmTransitionEdge()
        {
            _id = Guid.NewGuid().ToString("N");
        }

        public HfsmTransitionEdge(string sourceNodeId, string targetNodeId)
        {
            _id = Guid.NewGuid().ToString("N");
            _sourceNodeId = sourceNodeId;
            _targetNodeId = targetNodeId;
        }

        /// <summary>
        /// 添加一个条件
        /// </summary>
        public void AddCondition(HfsmTransitionCondition condition)
        {
            if (condition == null)
                return;

            if (_conditions == null)
                _conditions = new List<HfsmTransitionCondition>();

            _conditions.Add(condition);
            SerializeConditions();
        }

        /// <summary>
        /// 移除一个条件
        /// </summary>
        public void RemoveCondition(HfsmTransitionCondition condition)
        {
            if (_conditions == null || condition == null)
                return;

            _conditions.Remove(condition);
            SerializeConditions();
            // 重新加载以确保 UI 显示正确
            GetConditionsFresh();
        }

        /// <summary>
        /// 清空所有条件
        /// </summary>
        public void ClearConditions()
        {
            _conditions?.Clear();
            SerializeConditions();
        }

        /// <summary>
        /// 将条件列表序列化到JSON
        /// </summary>
        private void SerializeConditions()
        {
            _conditionConfigJson = HfsmConditionSerializer.Serialize(_conditions);
        }

        /// <summary>
        /// Checks if this edge involves the specified node.
        /// </summary>
        public bool InvolvesNode(string nodeId)
        {
            return _sourceNodeId == nodeId || _targetNodeId == nodeId;
        }

        public HfsmTransitionEdge Clone()
        {
            var clone = new HfsmTransitionEdge();
            clone._id = Guid.NewGuid().ToString("N");
            clone._sourceNodeId = _sourceNodeId;
            clone._targetNodeId = _targetNodeId;
            clone._conditionConfigJson = _conditionConfigJson;
            clone._priority = _priority;
            clone._isExitTransition = _isExitTransition;
            clone._forceInstantly = _forceInstantly;
            clone._useAndLogic = _useAndLogic;

            // 深拷贝条件
            if (_conditions != null)
            {
                clone._conditions = new List<HfsmTransitionCondition>();
                foreach (var condition in _conditions)
                {
                    clone._conditions.Add(condition.Clone());
                }
            }

            return clone;
        }

        /// <summary>
        /// Checks if this transition has any conditions defined.
        /// </summary>
        public bool HasConditions
        {
            get
            {
                return Conditions != null && Conditions.Count > 0;
            }
        }

        /// <summary>
        /// 获取一个简短的条件描述
        /// </summary>
        public string GetConditionSummary()
        {
            var conditions = Conditions;
            if (conditions == null || conditions.Count == 0)
                return "Always";

            if (conditions.Count == 1)
                return conditions[0].GetDescription();

            return $"{conditions.Count} conditions ({(_useAndLogic ? "AND" : "OR")})";
        }
    }
}
