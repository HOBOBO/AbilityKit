using System;
using RuntimeBlackboard = AbilityKit.BehaviorTree.Blackboard.Blackboard;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation
{
    /// <summary>从行为树黑板读取并保存示例所关心的领域输出。</summary>
    [Serializable]
    public sealed class AgentDecisionOutputs
    {
        [SerializeField] private string _mode = "";
        [SerializeField] private bool _busy;

        public string Mode => _mode;
        public bool Busy => _busy;

        public void ReadFrom(RuntimeBlackboard blackboard)
        {
            if (blackboard == null) throw new ArgumentNullException(nameof(blackboard));

            _mode = blackboard.GetString(ObservationBlackboardKeys.Mode);
            _busy = blackboard.GetBool(ObservationBlackboardKeys.Busy);
        }

        public void Clear()
        {
            _mode = "";
            _busy = false;
        }
    }
}
