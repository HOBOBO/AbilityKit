using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/SkillLevelTable", fileName = "SkillLevelTableCO")]
    public sealed class SkillLevelTableSO : ScriptableObject, IMobaConfigTableAsset
    {
        public SkillLevelTableDTO[] dataList;

        public string FileWithoutExt => MobaConfigPaths.SkillLevelTablesFile;
        public Type EntryType => typeof(SkillLevelTableDTO);
        public IEnumerable GetEntries() => dataList;
    }
}
