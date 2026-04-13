using System;
using System.Collections;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/BattleAttributeTemplate", fileName = "BattleAttributeTemplateCO")]
    public sealed class BattleAttributeTemplateSO : MobaConfigTableAssetSO
    {
        public BattleAttributeTemplateDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.AttributeTemplatesFile;
        public override Type EntryType => typeof(BattleAttributeTemplateDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
