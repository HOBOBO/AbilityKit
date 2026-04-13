using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Config.BattleDemo.MO
{
    /// <summary>
    /// 鍩轰簬 Luban 浜岃繘鍒堕厤缃殑灞炴€фā鏉?MO
    /// </summary>
    public sealed class AttributeTemplateLubanMO
    {
        /// <summary>
        /// 妯℃澘缂栧彿
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// 鍗囩骇鎴樻枟灞炴€ф柟妗堢紪鍙?
        /// </summary>
        public int UpgradeCode { get; }

        /// <summary>
        /// 涓诲姩鎶€鑳藉垪琛?
        /// </summary>
        public IReadOnlyList<int> ActiveSkills { get; }

        /// <summary>
        /// 琚姩鎶€鑳藉垪琛?
        /// </summary>
        public IReadOnlyList<int> PassiveSkills { get; }

        /// <summary>
        /// 鐢熷懡鍊?
        /// </summary>
        public int Hp { get; }

        /// <summary>
        /// 鏈€澶х敓鍛藉€?
        /// </summary>
        public int MaxHp { get; }

        /// <summary>
        /// 棰濆鐢熷懡鍊?
        /// </summary>
        public int ExtraHp { get; }

        /// <summary>
        /// 鐗╃悊鏀诲嚮
        /// </summary>
        public int PhysicsAttack { get; }

        /// <summary>
        /// 娉曟湳鏀诲嚮
        /// </summary>
        public int MagicAttack { get; }

        /// <summary>
        /// 棰濆鐗╃悊鏀诲嚮
        /// </summary>
        public int ExtraPhysicsAttack { get; }

        /// <summary>
        /// 棰濆娉曟湳鏀诲嚮
        /// </summary>
        public int ExtraMagicAttack { get; }

        /// <summary>
        /// 鐗╃悊闃插尽
        /// </summary>
        public int PhysicsDefense { get; }

        /// <summary>
        /// 娉曟湳闃插尽
        /// </summary>
        public int MagicDefense { get; }

        /// <summary>
        /// 娉曞姏鍊?
        /// </summary>
        public int Mana { get; }

        /// <summary>
        /// 鏈€澶ф硶鍔涘€?
        /// </summary>
        public int MaxMana { get; }

        /// <summary>
        /// 鏆村嚮鐜?
        /// </summary>
        public int CriticalR { get; }

        /// <summary>
        /// 鏀婚€熷€嶇巼
        /// </summary>
        public int AttackSpeedR { get; }

        /// <summary>
        /// 鍐峰嵈缂╁噺
        /// </summary>
        public int CooldownReduceR { get; }

        /// <summary>
        /// 鐗╃悊绌块€?
        /// </summary>
        public int PhysicsPenetrationR { get; }

        /// <summary>
        /// 娉曟湳绌块€?
        /// </summary>
        public int MagicPenetrationR { get; }

        /// <summary>
        /// 绉诲姩閫熷害
        /// </summary>
        public int MoveSpeed { get; }

        /// <summary>
        /// 鐗╃悊鍚歌
        /// </summary>
        public int PhysicsBloodsuckingR { get; }

        /// <summary>
        /// 娉曟湳鍚歌
        /// </summary>
        public int MagicBloodsuckingR { get; }

        /// <summary>
        /// 鏀诲嚮鑼冨洿
        /// </summary>
        public int AttackRange { get; }

        /// <summary>
        /// 姣忕鍥炶
        /// </summary>
        public int PerSecondBloodR { get; }

        /// <summary>
        /// 姣忕鍥炶摑
        /// </summary>
        public int PerSecondManaR { get; }

        /// <summary>
        /// 闊ф€?
        /// </summary>
        public int ResilienceR { get; }

        public AttributeTemplateLubanMO(global::cfg.DRAttributeTemplates dr)
        {
            if (dr == null) throw new ArgumentNullException(nameof(dr));
            Id = dr.Code;
            UpgradeCode = dr.UpgradeCode;
            ActiveSkills = dr.ActiveSkills ?? new List<int>();
            PassiveSkills = dr.PassiveSkills ?? new List<int>();
            Hp = dr.Hp;
            MaxHp = dr.MaxHp;
            ExtraHp = dr.ExtraHp;
            PhysicsAttack = dr.PhysicsAttack;
            MagicAttack = dr.MagicAttack;
            ExtraPhysicsAttack = dr.ExtraPhysicsAttack;
            ExtraMagicAttack = dr.ExtraMagicAttack;
            PhysicsDefense = dr.PhysicsDefense;
            MagicDefense = dr.MagicDefense;
            Mana = dr.Mana;
            MaxMana = dr.MaxMana;
            CriticalR = dr.CriticalR;
            AttackSpeedR = dr.AttackSpeedR;
            CooldownReduceR = dr.CooldownReduceR;
            PhysicsPenetrationR = dr.PhysicsPenetrationR;
            MagicPenetrationR = dr.MagicPenetrationR;
            MoveSpeed = dr.MoveSpeed;
            PhysicsBloodsuckingR = dr.PhysicsBloodsuckingR;
            MagicBloodsuckingR = dr.MagicBloodsuckingR;
            AttackRange = dr.AttackRange;
            PerSecondBloodR = dr.PerSecondBloodR;
            PerSecondManaR = dr.PerSecondManaR;
            ResilienceR = dr.ResilienceR;
        }
    }
}
