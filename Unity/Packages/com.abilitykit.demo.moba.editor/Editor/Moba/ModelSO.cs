using System;
using System.Collections;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
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
