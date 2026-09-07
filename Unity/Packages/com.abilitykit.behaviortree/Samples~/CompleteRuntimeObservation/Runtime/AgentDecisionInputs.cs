using System;
using RuntimeBlackboard = AbilityKit.BehaviorTree.Blackboard.Blackboard;
using AbilityKit.Deterministic;
using UnityEngine;

namespace AbilityKit.BehaviorTree.Samples.CompleteRuntimeObservation
{
    /// <summary>可在 Inspector 中修改并同步到行为树黑板的领域输入。</summary>
    [Serializable]
    public sealed class AgentDecisionInputs
    {
        [SerializeField, Range(0, 100)] private int _health = 100;
        [SerializeField] private bool _hasTarget;
        [SerializeField] private bool _canAct = true;
        [SerializeField, Min(0f)] private float _targetDistance = 8f;
        [SerializeField] private string _stance = "Guard";

        public void WriteTo(RuntimeBlackboard blackboard)
        {
            if (blackboard == null) throw new ArgumentNullException(nameof(blackboard));

            blackboard.SetInt64(ObservationBlackboardKeys.Health, _health);
            blackboard.SetBool(ObservationBlackboardKeys.HasTarget, _hasTarget);
            blackboard.SetBool(ObservationBlackboardKeys.CanAct, _canAct);
            blackboard.SetFixed64(
                ObservationBlackboardKeys.TargetDistance,
                Fixed64.FromSingle(_targetDistance));
            blackboard.SetString(ObservationBlackboardKeys.Stance, _stance ?? "");
        }
    }
}
