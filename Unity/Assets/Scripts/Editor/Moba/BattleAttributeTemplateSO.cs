using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/BattleAttributeTemplate", fileName = "BattleAttributeTemplateCO")]
    public sealed class BattleAttributeTemplateSO : ScriptableObject, IMobaConfigTableAsset
    {
        public BattleAttributeTemplateDTO[] dataList;

        public string FileWithoutExt => MobaConfigPaths.AttributeTemplatesFile;
        public Type EntryType => typeof(BattleAttributeTemplateDTO);
        public IEnumerable GetEntries() => dataList;
    }
}
