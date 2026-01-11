using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Buff", fileName = "BuffCO")]
    public sealed class BuffSO : ScriptableObject, IMobaConfigTableAsset
    {
        public BuffDTO[] dataList;

        public string FileWithoutExt => MobaConfigPaths.BuffsFile;
        public Type EntryType => typeof(BuffDTO);
        public IEnumerable GetEntries() => dataList;
    }
}
