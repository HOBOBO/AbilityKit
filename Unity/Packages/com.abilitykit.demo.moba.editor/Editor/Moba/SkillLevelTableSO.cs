using System;
using System.Collections;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/SkillLevelTable", fileName = "SkillLevelTableCO")]
    public sealed class SkillLevelTableSO : MobaConfigTableAssetSO
    {
        public SkillLevelTableDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.SkillLevelTablesFile;
        public override Type EntryType => typeof(SkillLevelTableDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
