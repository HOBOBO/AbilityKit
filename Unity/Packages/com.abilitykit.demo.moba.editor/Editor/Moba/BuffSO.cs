using System;
using System.Collections;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Buff", fileName = "BuffCO")]
    public sealed class BuffSO : MobaConfigTableAssetSO
    {
        public BuffDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.BuffsFile;
        public override Type EntryType => typeof(BuffDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
