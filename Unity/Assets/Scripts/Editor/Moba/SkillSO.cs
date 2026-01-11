using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Skill", fileName = "SkillCO")]
    public sealed class SkillSO : MobaConfigTableAssetSO
    {
        public SkillDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.SkillsFile;
        public override Type EntryType => typeof(SkillDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
