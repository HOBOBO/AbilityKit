using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO
{
    public sealed class BattleAttributeTemplateMO
    {
        public int Id { get; }
        public IReadOnlyList<int> ActiveSkills { get; }
        public IReadOnlyList<int> PassiveSkills { get; }
        public int Hp { get; }
        public int MaxHp { get; }
        public int ExtraHp { get; }
        public int PhysicsAttack { get; }
        public int MagicAttack { get; }
        public int ExtraPhysicsAttack { get; }
        public int ExtraMagicAttack { get; }
        public int PhysicsDefense { get; }
        public int MagicDefense { get; }
        public int Mana { get; }
        public int MaxMana { get; }
        public int CriticalR { get; }
        public int AttackSpeedR { get; }
        public int CooldownReduceR { get; }
        public int PhysicsPenetrationR { get; }
        public int MagicPenetrationR { get; }
        public int MoveSpeed { get; }
        public int PhysicsBloodsuckingR { get; }
        public int MagicBloodsuckingR { get; }
        public int AttackRange { get; }
        public int PerSecondBloodR { get; }
        public int PerSecondManaR { get; }
        public int ResilienceR { get; }

        public BattleAttributeTemplateMO(BattleAttributeTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            ActiveSkills = dto.ActiveSkills ?? Array.Empty<int>();
            PassiveSkills = dto.PassiveSkills ?? Array.Empty<int>();
            Hp = dto.Hp;
            MaxHp = dto.MaxHp;
            ExtraHp = dto.ExtraHp;
            PhysicsAttack = dto.PhysicsAttack;
            MagicAttack = dto.MagicAttack;
            ExtraPhysicsAttack = dto.ExtraPhysicsAttack;
            ExtraMagicAttack = dto.ExtraMagicAttack;
            PhysicsDefense = dto.PhysicsDefense;
            MagicDefense = dto.MagicDefense;
            Mana = dto.Mana;
            MaxMana = dto.MaxMana;
            CriticalR = dto.CriticalR;
            AttackSpeedR = dto.AttackSpeedR;
            CooldownReduceR = dto.CooldownReduceR;
            PhysicsPenetrationR = dto.PhysicsPenetrationR;
            MagicPenetrationR = dto.MagicPenetrationR;
            MoveSpeed = dto.MoveSpeed;
            PhysicsBloodsuckingR = dto.PhysicsBloodsuckingR;
            MagicBloodsuckingR = dto.MagicBloodsuckingR;
            AttackRange = dto.AttackRange;
            PerSecondBloodR = dto.PerSecondBloodR;
            PerSecondManaR = dto.PerSecondManaR;
            ResilienceR = dto.ResilienceR;
        }
    }
}
