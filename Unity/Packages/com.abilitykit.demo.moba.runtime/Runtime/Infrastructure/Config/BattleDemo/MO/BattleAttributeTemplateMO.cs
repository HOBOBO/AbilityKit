using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    public sealed class BattleAttributeTemplateMO
    {
        public int Id { get; }
        public int BasicAttackSkillId { get; }
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
        public float MoveSpeed { get; }
        public int PhysicsBloodsuckingR { get; }
        public int MagicBloodsuckingR { get; }
        public int AttackRange { get; }
        public int PerSecondBloodR { get; }
        public int PerSecondManaR { get; }
        public int ResilienceR { get; }
        public int HpGrowth { get; }
        public int MaxHpGrowth { get; }
        public int ExtraHpGrowth { get; }
        public int PhysicsAttackGrowth { get; }
        public int MagicAttackGrowth { get; }
        public int ExtraPhysicsAttackGrowth { get; }
        public int ExtraMagicAttackGrowth { get; }
        public int PhysicsDefenseGrowth { get; }
        public int MagicDefenseGrowth { get; }
        public int ManaGrowth { get; }
        public int MaxManaGrowth { get; }
        public int CriticalRGrowth { get; }
        public int AttackSpeedRGrowth { get; }
        public int CooldownReduceRGrowth { get; }
        public int PhysicsPenetrationRGrowth { get; }
        public int MagicPenetrationRGrowth { get; }
        public float MoveSpeedGrowth { get; }
        public int PhysicsBloodsuckingRGrowth { get; }
        public int MagicBloodsuckingRGrowth { get; }
        public int AttackRangeGrowth { get; }
        public int PerSecondBloodRGrowth { get; }
        public int PerSecondManaRGrowth { get; }
        public int ResilienceRGrowth { get; }

        public BattleAttributeTemplateMO(BattleAttributeTemplateDTO dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            Id = dto.Id;
            BasicAttackSkillId = dto.BasicAttackSkillId;
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
            HpGrowth = dto.HpGrowth;
            MaxHpGrowth = dto.MaxHpGrowth;
            ExtraHpGrowth = dto.ExtraHpGrowth;
            PhysicsAttackGrowth = dto.PhysicsAttackGrowth;
            MagicAttackGrowth = dto.MagicAttackGrowth;
            ExtraPhysicsAttackGrowth = dto.ExtraPhysicsAttackGrowth;
            ExtraMagicAttackGrowth = dto.ExtraMagicAttackGrowth;
            PhysicsDefenseGrowth = dto.PhysicsDefenseGrowth;
            MagicDefenseGrowth = dto.MagicDefenseGrowth;
            ManaGrowth = dto.ManaGrowth;
            MaxManaGrowth = dto.MaxManaGrowth;
            CriticalRGrowth = dto.CriticalRGrowth;
            AttackSpeedRGrowth = dto.AttackSpeedRGrowth;
            CooldownReduceRGrowth = dto.CooldownReduceRGrowth;
            PhysicsPenetrationRGrowth = dto.PhysicsPenetrationRGrowth;
            MagicPenetrationRGrowth = dto.MagicPenetrationRGrowth;
            MoveSpeedGrowth = dto.MoveSpeedGrowth;
            PhysicsBloodsuckingRGrowth = dto.PhysicsBloodsuckingRGrowth;
            MagicBloodsuckingRGrowth = dto.MagicBloodsuckingRGrowth;
            AttackRangeGrowth = dto.AttackRangeGrowth;
            PerSecondBloodRGrowth = dto.PerSecondBloodRGrowth;
            PerSecondManaRGrowth = dto.PerSecondManaRGrowth;
            ResilienceRGrowth = dto.ResilienceRGrowth;
        }
    }
}
