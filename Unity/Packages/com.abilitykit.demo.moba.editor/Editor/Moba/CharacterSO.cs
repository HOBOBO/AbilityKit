using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Character", fileName = "CharacterCO")]
    public sealed class CharacterSO : MobaConfigTableAssetSO
    {
        public CharacterDTO[] dataList;

        public override string FileWithoutExt => MobaConfigPaths.CharactersFile;
        public override Type EntryType => typeof(CharacterDTO);
        public override IEnumerable GetEntries() => dataList;
    }
}
