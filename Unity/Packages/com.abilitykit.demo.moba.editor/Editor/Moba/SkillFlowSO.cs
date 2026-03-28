using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/SkillFlow", fileName = "SkillFlowCO")]
    public sealed class SkillFlowSO : MobaConfigTableAssetSO
    {
        public SkillFlowDef[] dataList;
        public SkillFlowDTO[] legacyDataList;

        public override string FileWithoutExt => MobaConfigPaths.SkillFlowsFile;
        public override Type EntryType => typeof(SkillFlowDTO);
        public override IEnumerable GetEntries()
        {
            if ((dataList == null || dataList.Length == 0) && legacyDataList != null && legacyDataList.Length > 0)
            {
                return legacyDataList;
            }
            if (dataList == null || dataList.Length == 0) return Array.Empty<SkillFlowDTO>();
            var list = new SkillFlowDTO[dataList.Length];
            for (int i = 0; i < dataList.Length; i++)
            {
                list[i] = dataList[i] != null ? dataList[i].ToDto() : null;
            }
            return list;
        }
    }
}
