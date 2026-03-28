using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/SkillButtonTemplate", fileName = "SkillButtonTemplateCO")]
    public sealed class SkillButtonTemplateSO : MobaConfigTableAssetSO
    {
        public SkillButtonTemplateDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.SkillButtonTemplatesFile;
        public override Type EntryType => typeof(SkillButtonTemplateDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
