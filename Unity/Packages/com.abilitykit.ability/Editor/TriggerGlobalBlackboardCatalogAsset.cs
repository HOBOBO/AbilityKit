using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [CreateAssetMenu(fileName = "TriggerGlobalBlackboardCatalog", menuName = "AbilityKit/触发器编辑/全局黑板目录")]
    public sealed class TriggerGlobalBlackboardCatalogAsset : SerializedScriptableObject
    {
        [OdinSerialize, NonSerialized, LabelText("全局黑板变量"), ListDrawerSettings(ShowIndexLabels = true)]
        public List<TriggerGlobalBlackboardKeyData> Keys = new List<TriggerGlobalBlackboardKeyData>();

        [Button("加载 MOBA 默认配置")]
        private void LoadMobaDefaults()
        {
            Keys = TriggerAuthoringProjectDefaults.CreateMobaBlackboardKeys();
        }
    }
}
