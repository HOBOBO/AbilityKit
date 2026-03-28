using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    public sealed class AttrTypeMO
    {
        public int Id { get; }
        public string Key { get; }
        public int ValueKind { get; }
        public float DefaultValue { get; }

        public AttrTypeMO(AttrTypeDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            Key = dto.Key;
            ValueKind = dto.ValueKind;
            DefaultValue = dto.DefaultValue;
        }
    }
}
