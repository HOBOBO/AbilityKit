#if UNITY_EDITOR
using System;
using System.Collections;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    public interface IMobaConfigTableAsset
    {
        string FileWithoutExt { get; }
        Type EntryType { get; }
        IEnumerable GetEntries();
    }
}
#endif
