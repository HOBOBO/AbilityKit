using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/AttrType", fileName = "AttrTypeCO")]
    public sealed class AttrTypeSO : ScriptableObject, IMobaConfigTableAsset
    {
        public AttrTypeDTO[] dataList;

        public string FileWithoutExt => MobaConfigPaths.AttributeTypesFile;
        public Type EntryType => typeof(AttrTypeDTO);
        public IEnumerable GetEntries() => dataList;
    }
}
