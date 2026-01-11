#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public abstract class MobaConfigTableAssetSO : ScriptableObject, IMobaConfigTableAsset
    {
        public abstract string FileWithoutExt { get; }
        public abstract Type EntryType { get; }
        public abstract IEnumerable GetEntries();
    }
}
#endif
