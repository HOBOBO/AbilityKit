using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using UnityEngine;

namespace AbilityKit.Ability.Configs
{
    [CreateAssetMenu(menuName = "AbilityKit/Ability Runtime", fileName = "AbilityRuntime")]
    public sealed class AbilityRuntimeSO : ScriptableObject
    {
        public string AbilityId;
        public List<TriggerRuntimeConfig> Triggers = new List<TriggerRuntimeConfig>();

        public IReadOnlyList<TriggerDef> ToTriggerDefs()
        {
            var list = new List<TriggerDef>(Triggers.Count);
            for (int i = 0; i < Triggers.Count; i++)
            {
                list.Add(Triggers[i].ToTriggerDef());
            }
            return list;
        }
    }
}
