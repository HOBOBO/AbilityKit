using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/OngoingEffect", fileName = "OngoingEffectCO")]
    public sealed class OngoingEffectSO : MobaConfigTableAssetSO
    {
        public OngoingEffectDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.OngoingEffectsFile;
        public override Type EntryType => typeof(OngoingEffectDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
