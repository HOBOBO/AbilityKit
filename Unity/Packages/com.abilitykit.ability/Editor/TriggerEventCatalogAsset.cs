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
    [CreateAssetMenu(fileName = "TriggerEventCatalog", menuName = "AbilityKit/触发器编辑/事件目录")]
    public sealed class TriggerEventCatalogAsset : SerializedScriptableObject
    {
        [OdinSerialize, NonSerialized, LabelText("事件定义"), ListDrawerSettings(ShowIndexLabels = true)]
        public List<TriggerEventDefinitionData> Events = new List<TriggerEventDefinitionData>();

        [Button("加载 MOBA 默认配置")]
        private void LoadMobaDefaults()
        {
            Events = TriggerAuthoringProjectDefaults.CreateMobaEvents();
        }

        [Button("扫描程序集")]
        private void ScanAssemblies()
        {
            Events = Events ?? new List<TriggerEventDefinitionData>();
            var scan = TriggerEventCatalogAssemblyScanner.ScanLoadedAssemblies();
            var merge = TriggerEventCatalogAssemblyScanner.MergeInto(Events, scan.Events);
            EditorUtility.SetDirty(this);
            EditorUtility.DisplayDialog(
                "扫描触发器事件",
                $"已扫描 {scan.ScannedAttributeCount} 个事件特性。\n新增 {merge.AddedCount} 个，更新 {merge.UpdatedCount} 个。",
                "确定");
        }
    }
}
