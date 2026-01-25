using System;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public enum AttrValueKind
    {
        Flat = 0,
        Percent = 1,
    }

    [Serializable]
    public sealed class AttrTypeDTO
    {
        public int Id;
        public string Key;
        public int ValueKind;
        public float DefaultValue;
    }
}
