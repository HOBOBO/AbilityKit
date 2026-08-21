using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [CreateAssetMenu(fileName = "TriggerGlobalBlackboardCatalog", menuName = "AbilityKit/Trigger Authoring/Global Blackboard Catalog")]
    public sealed class TriggerGlobalBlackboardCatalogAsset : SerializedScriptableObject
    {
        [OdinSerialize, NonSerialized, ListDrawerSettings(ShowIndexLabels = true)]
        public List<TriggerGlobalBlackboardKeyData> Keys = new List<TriggerGlobalBlackboardKeyData>();

        [Button("Load MOBA Defaults")]
        private void LoadMobaDefaults()
        {
            Keys = TriggerAuthoringProjectDefaults.CreateMobaBlackboardKeys();
        }
    }
}
