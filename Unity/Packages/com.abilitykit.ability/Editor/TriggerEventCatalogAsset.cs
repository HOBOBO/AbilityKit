using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [CreateAssetMenu(fileName = "TriggerEventCatalog", menuName = "AbilityKit/Trigger Authoring/Event Catalog")]
    public sealed class TriggerEventCatalogAsset : SerializedScriptableObject
    {
        [OdinSerialize, NonSerialized, ListDrawerSettings(ShowIndexLabels = true)]
        public List<TriggerEventDefinitionData> Events = new List<TriggerEventDefinitionData>();

        [Button("Load MOBA Defaults")]
        private void LoadMobaDefaults()
        {
            Events = TriggerAuthoringProjectDefaults.CreateMobaEvents();
        }
    }
}
