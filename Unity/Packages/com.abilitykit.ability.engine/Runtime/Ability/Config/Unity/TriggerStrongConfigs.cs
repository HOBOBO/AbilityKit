using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using UnityEngine;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public class TriggerStrongConfigs : ScriptableObject
    {
        [SerializeField]
        private List<TriggerStrongConfig> _triggerConfigs = new List<TriggerStrongConfig>();

        public IReadOnlyList<TriggerStrongConfig> TriggerConfigs => _triggerConfigs;

        public TriggerStrongConfig GetTriggerConfig(int index)
        {
            if (_triggerConfigs == null || index < 0 || index >= _triggerConfigs.Count)
                return null;
            return _triggerConfigs[index];
        }
    }

    [Serializable]
    public class TriggerStrongConfig
    {
        public string Id;
        public string Description;

        [SerializeReference]
        private List<PhaseStrongConfig> _phaseConfigs = new List<PhaseStrongConfig>();

        public IReadOnlyList<PhaseStrongConfig> PhaseConfigs => _phaseConfigs;

        public PhaseStrongConfig GetPhaseConfig(int index)
        {
            if (_phaseConfigs == null || index < 0 || index >= _phaseConfigs.Count)
                return null;
            return _phaseConfigs[index];
        }
    }

    [Serializable]
    public class PhaseStrongConfig
    {
        public string PhaseId;
        public string PhaseType;
        public float Duration;
    }
}
