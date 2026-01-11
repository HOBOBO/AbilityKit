using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Model", fileName = "ModelCO")]
    public sealed class ModelSO : ScriptableObject, IMobaConfigTableAsset
    {
        public ModelDTO[] dataList;

        public string FileWithoutExt => MobaConfigPaths.ModelsFile;
        public Type EntryType => typeof(ModelDTO);
        public IEnumerable GetEntries() => dataList;
    }
}
