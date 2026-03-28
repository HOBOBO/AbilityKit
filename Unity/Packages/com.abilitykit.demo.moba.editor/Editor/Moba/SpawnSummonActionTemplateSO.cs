#if UNITY_EDITOR
using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/SpawnSummonActionTemplate", fileName = "SpawnSummonActionTemplateCO")]
    public sealed class SpawnSummonActionTemplateSO : MobaConfigTableAssetSO
    {
        public SpawnSummonActionTemplateDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.SpawnSummonActionTemplatesFile;
        public override Type EntryType => typeof(SpawnSummonActionTemplateDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
#endif
