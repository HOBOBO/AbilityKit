using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Summon", fileName = "SummonCO")]
    public sealed class SummonSO : MobaConfigTableAssetSO
    {
        public SummonDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.SummonsFile;
        public override Type EntryType => typeof(SummonDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
