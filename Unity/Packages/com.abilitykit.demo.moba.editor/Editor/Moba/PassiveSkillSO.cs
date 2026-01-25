#if UNITY_EDITOR
using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/PassiveSkill", fileName = "PassiveSkillCO")]
    public sealed class PassiveSkillSO : MobaConfigTableAssetSO
    {
        public PassiveSkillDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.PassiveSkillsFile;
        public override Type EntryType => typeof(PassiveSkillDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
#endif
