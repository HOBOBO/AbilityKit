using System;
using System.Collections;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Moba/CO/Character", fileName = "CharacterCO")]
    public sealed class CharacterSO : ScriptableObject, IMobaConfigTableAsset
    {
        public CharacterDTO[] dataList;

        public string FileWithoutExt => MobaConfigPaths.CharactersFile;
        public Type EntryType => typeof(CharacterDTO);
        public IEnumerable GetEntries() => dataList;
    }
}
