#if UNITY_EDITOR
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    [CreateAssetMenu(menuName = "AbilityKit/Vfx/VfxTable", fileName = "VfxTable")]
    public sealed class VfxSO : ScriptableObject
    {
        public VfxDTO[] dataList;
    }
}
#endif
