using System;
using System.Collections;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/AttrType", fileName = "AttrTypeCO")]
    public sealed class AttrTypeSO : MobaConfigTableAssetSO
    {
        public AttrTypeDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.AttributeTypesFile;
        public override Type EntryType => typeof(AttrTypeDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
