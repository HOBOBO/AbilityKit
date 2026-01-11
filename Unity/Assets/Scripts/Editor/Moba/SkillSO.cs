using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Skill", fileName = "SkillCO")]
    public sealed class SkillSO : ScriptableObject, IMobaConfigTableAsset
    {
        public SkillDTO[] dataList;

        public string FileWithoutExt => MobaConfigPaths.SkillsFile;
        public Type EntryType => typeof(SkillDTO);
        public IEnumerable GetEntries() => dataList;
    }
}
