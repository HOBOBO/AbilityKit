using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEditor;
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

        [Button("Scan Assemblies")]
        private void ScanAssemblies()
        {
            Events = Events ?? new List<TriggerEventDefinitionData>();
            var scan = TriggerEventCatalogAssemblyScanner.ScanLoadedAssemblies();
            var merge = TriggerEventCatalogAssemblyScanner.MergeInto(Events, scan.Events);
            EditorUtility.SetDirty(this);
            EditorUtility.DisplayDialog(
                "Scan Trigger Events",
                $"Scanned {scan.ScannedAttributeCount} event attributes.\nAdded {merge.AddedCount}, updated {merge.UpdatedCount}.",
                "OK");
        }
    }
}
