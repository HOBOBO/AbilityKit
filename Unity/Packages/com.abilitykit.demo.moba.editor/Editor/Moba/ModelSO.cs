using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Model", fileName = "ModelCO")]
    public sealed class ModelSO : MobaConfigTableAssetSO
    {
        public ModelDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.ModelsFile;
        public override Type EntryType => typeof(ModelDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
