using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.CO;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.MO
{
    public sealed class BattleAttributeTemplateMO
    {
        public int Id { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int MoveSpeed { get; }

        public BattleAttributeTemplateMO(IBattleAttributeTemplateCO co)
        {
            if (co == null) throw new ArgumentNullException(nameof(co));
            Id = co.Key;
            MaxHp = co.MaxHp;
            Attack = co.Attack;
            Defense = co.Defense;
            MoveSpeed = co.MoveSpeed;
        }

        public BattleAttributeTemplateMO(BattleAttributeTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            MaxHp = dto.MaxHp;
            Attack = dto.Attack;
            Defense = dto.Defense;
            MoveSpeed = dto.MoveSpeed;
        }
    }
}
