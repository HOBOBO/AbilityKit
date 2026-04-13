using System;
using AbilityKit.Demo.Moba;
using AbilityKit.Attributes.Core;

namespace AbilityKit.Demo.Moba.Attributes
{
    public static class MobaAttributeIds
    {
        private const string Prefix = "Battle.";

        public static readonly AttributeId HP = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.HP));
        public static readonly AttributeId MAX_HP = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.MAX_HP));
        public static readonly AttributeId EXTRA_HP = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.EXTRA_HP));
        public static readonly AttributeId PHYSICS_ATTACK = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.PHYSICS_ATTACK));
        public static readonly AttributeId MAGIC_ATTACK = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.MAGIC_ATTACK));
        public static readonly AttributeId EXTRA_PHYSICS_ATTACK = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.EXTRA_PHYSICS_ATTACK));
        public static readonly AttributeId EXTRA_MAGIC_ATTACK = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.EXTRA_MAGIC_ATTACK));
        public static readonly AttributeId PHYSICS_DEFENSE = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.PHYSICS_DEFENSE));
        public static readonly AttributeId MAGIC_DEFENSE = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.MAGIC_DEFENSE));
        public static readonly AttributeId MANA = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.MANA));
        public static readonly AttributeId MAX_MANA = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.MAX_MANA));
        public static readonly AttributeId CRITICAL_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.CRITICAL_R));
        public static readonly AttributeId ATTACK_SPEED_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.ATTACK_SPEED_R));
        public static readonly AttributeId COOLDOWN_REDUCE_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.COOLDOWN_REDUCE_R));
        public static readonly AttributeId PHYSICS_PENETRATION_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.PHYSICS_PENETRATION_R));
        public static readonly AttributeId MAGIC_PENETRATION_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.MAGIC_PENETRATION_R));
        public static readonly AttributeId MOVE_SPEED = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.MOVE_SPEED));
        public static readonly AttributeId PHYSICS_BLOODSUCKING_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.PHYSICS_BLOODSUCKING_R));
        public static readonly AttributeId MAGIC_BLOODSUCKING_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.MAGIC_BLOODSUCKING_R));
        public static readonly AttributeId ATTACK_RANGE = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.ATTACK_RANGE));
        public static readonly AttributeId PER_SECOND_BLOOD_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.PER_SECOND_BLOOD_R));
        public static readonly AttributeId PER_SECOND_MANA_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.PER_SECOND_MANA_R));
        public static readonly AttributeId RESILIENCE_R = AttributeRegistry.DefaultRegistry.Request(Prefix + nameof(BattleAttributeType.RESILIENCE_R));

        public static AttributeId Get(BattleAttributeType type)
        {
            switch (type)
            {
                case BattleAttributeType.HP: return HP;
                case BattleAttributeType.MAX_HP: return MAX_HP;
                case BattleAttributeType.EXTRA_HP: return EXTRA_HP;
                case BattleAttributeType.PHYSICS_ATTACK: return PHYSICS_ATTACK;
                case BattleAttributeType.MAGIC_ATTACK: return MAGIC_ATTACK;
                case BattleAttributeType.EXTRA_PHYSICS_ATTACK: return EXTRA_PHYSICS_ATTACK;
                case BattleAttributeType.EXTRA_MAGIC_ATTACK: return EXTRA_MAGIC_ATTACK;
                case BattleAttributeType.PHYSICS_DEFENSE: return PHYSICS_DEFENSE;
                case BattleAttributeType.MAGIC_DEFENSE: return MAGIC_DEFENSE;
                case BattleAttributeType.MANA: return MANA;
                case BattleAttributeType.MAX_MANA: return MAX_MANA;
                case BattleAttributeType.CRITICAL_R: return CRITICAL_R;
                case BattleAttributeType.ATTACK_SPEED_R: return ATTACK_SPEED_R;
                case BattleAttributeType.COOLDOWN_REDUCE_R: return COOLDOWN_REDUCE_R;
                case BattleAttributeType.PHYSICS_PENETRATION_R: return PHYSICS_PENETRATION_R;
                case BattleAttributeType.MAGIC_PENETRATION_R: return MAGIC_PENETRATION_R;
                case BattleAttributeType.MOVE_SPEED: return MOVE_SPEED;
                case BattleAttributeType.PHYSICS_BLOODSUCKING_R: return PHYSICS_BLOODSUCKING_R;
                case BattleAttributeType.MAGIC_BLOODSUCKING_R: return MAGIC_BLOODSUCKING_R;
                case BattleAttributeType.ATTACK_RANGE: return ATTACK_RANGE;
                case BattleAttributeType.PER_SECOND_BLOOD_R: return PER_SECOND_BLOOD_R;
                case BattleAttributeType.PER_SECOND_MANA_R: return PER_SECOND_MANA_R;
                case BattleAttributeType.RESILIENCE_R: return RESILIENCE_R;
                case BattleAttributeType.None:
                default:
                    return default;
            }
        }
    }
}
