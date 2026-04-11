using System;
using AbilityKit.Dataflow;

namespace AbilityKit.Combat
{
    /// <summary>
    /// 伤害计算管线
    /// 完整的伤害计算流程
    /// </summary>
    public class DamageCalculationPipeline : DataflowPipeline<DamageRequest, DamageResult>
    {
        /// <summary>
        /// 创建默认的伤害计算管线
        /// </summary>
        public static DamageCalculationPipeline CreateDefault()
        {
            var pipeline = new DamageCalculationPipeline();

            // 1. 验证伤害请求
            pipeline.AddProcessor(new ValidateDamageProcessor());

            // 2. 计算暴击
            pipeline.AddProcessor(new CalculateCriticalProcessor());

            // 3. 计算基础伤害（包含攻击力加成）
            pipeline.AddProcessor(new CalculateBaseDamageProcessor());

            // 4. 应用伤害加成修正
            pipeline.AddProcessor(new ApplyDamageBonusProcessor());

            // 5. 应用护甲减免（物理伤害）
            pipeline.AddProcessor(new ApplyArmorReductionProcessor());

            // 6. 应用魔抗减免（魔法伤害）
            pipeline.AddProcessor(new ApplyMagicResistReductionProcessor());

            // 7. 计算最终伤害
            pipeline.AddProcessor(new CalculateFinalDamageProcessor());

            // 8. 计算溢出伤害
            pipeline.AddProcessor(new CalculateOverkillProcessor());

            return pipeline;
        }
    }

    /// <summary>
    /// 伤害计算相关的数据槽位
    /// 使用强类型槽位避免魔法字符串
    /// </summary>
    public static class DamageSlots
    {
        /// <summary>
        /// 暴击几率
        /// </summary>
        public static readonly DataflowSlot<float> CritChance = new DataflowSlot<float>("Damage_CritChance");

        /// <summary>
        /// 暴击倍数
        /// </summary>
        public static readonly DataflowSlot<float> CritMultiplier = new DataflowSlot<float>("Damage_CritMultiplier", 1.5f);

        /// <summary>
        /// 伤害加成百分比
        /// </summary>
        public static readonly DataflowSlot<float> DamageBonusPercent = new DataflowSlot<float>("Damage_BonusPercent");

        /// <summary>
        /// 伤害加成固定值
        /// </summary>
        public static readonly DataflowSlot<float> DamageBonusFlat = new DataflowSlot<float>("Damage_BonusFlat");

        /// <summary>
        /// 护甲穿透固定值
        /// </summary>
        public static readonly DataflowSlot<float> ArmorPenetration = new DataflowSlot<float>("Damage_ArmorPenetration");

        /// <summary>
        /// 护甲穿透百分比
        /// </summary>
        public static readonly DataflowSlot<float> ArmorPenetrationPercent = new DataflowSlot<float>("Damage_ArmorPenetrationPercent");

        /// <summary>
        /// 魔抗穿透固定值
        /// </summary>
        public static readonly DataflowSlot<float> MagicResistPenetration = new DataflowSlot<float>("Damage_MagicResistPenetration");

        /// <summary>
        /// 魔抗穿透百分比
        /// </summary>
        public static readonly DataflowSlot<float> MagicResistPenetrationPercent = new DataflowSlot<float>("Damage_MagicResistPenetrationPercent");

        /// <summary>
        /// 目标护盾值
        /// </summary>
        public static readonly DataflowSlot<float> TargetShield = new DataflowSlot<float>("Damage_TargetShield");
    }

    /// <summary>
    /// 伤害计算器接口
    /// 定义伤害处理器的行为
    /// </summary>
    public interface IDamageProcessor : IDataflowProcessor<DamageRequest, DamageResult>
    {
    }

    /// <summary>
    /// 伤害处理器基类
    /// </summary>
    public abstract class DamageProcessor : DataflowProcessor<DamageRequest, DamageResult>, IDamageProcessor
    {
        protected DamageResult _result;

        protected override void OnBeforeProcess(DamageRequest input, IDataflowContext context)
        {
            base.OnBeforeProcess(input, context);
            var damageContext = context as DamageCalculationContext;
            _result = damageContext?.Result ?? DamageResult.Create(input);
            _result.Request = input;
        }

        protected override void OnAfterProcess(DamageRequest input, IDataflowContext context, DamageResult result)
        {
            base.OnAfterProcess(input, context, result);
            var damageContext = context as DamageCalculationContext;
            if (damageContext != null)
            {
                damageContext.Result = result;
            }
        }
    }

    /// <summary>
    /// 验证伤害请求处理器
    /// </summary>
    public class ValidateDamageProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            // 验证基础条件
            if (input.Attacker == null)
            {
                context.Abort();
                return _result;
            }

            if (input.Target == null)
            {
                context.Abort();
                return _result;
            }

            if (input.BaseValue <= 0 && !IsDot(input))
            {
                context.Abort();
                return _result;
            }

            return _result;
        }

        private static bool IsDot(DamageRequest request)
        {
            return (request.Flags & DamageFlags.DamageOverTime) != 0 && request.BaseValue > 0;
        }
    }

    /// <summary>
    /// 计算暴击处理器
    /// </summary>
    public class CalculateCriticalProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            // 使用强类型槽位获取暴击数据
            var critChance = context.GetData(DamageSlots.CritChance);
            var critMultiplier = context.GetData(DamageSlots.CritMultiplier);

            // 暴击计算（实际项目中应该从属性系统获取暴击率）
            if (critChance > 0 && UnityEngine.Random.value < critChance)
            {
                _result.Request.Flags |= DamageFlags.Critical;
                _result.CriticalMultiplier = critMultiplier;
            }
            else
            {
                _result.CriticalMultiplier = 1f;
            }

            return _result;
        }
    }

    /// <summary>
    /// 计算基础伤害处理器
    /// </summary>
    public class CalculateBaseDamageProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            var damageContext = context as DamageCalculationContext;
            _result.RawDamage = input.BaseValue;
            _result.PreArmorDamage = input.BaseValue;

            if (damageContext != null)
            {
                // 根据伤害类型应用对应的攻击力加成
                if (input.DamageType == DamageType.Physical)
                {
                    _result.RawDamage += damageContext.AttackerPhysicalDamage;
                    _result.PreArmorDamage = _result.RawDamage;
                }
                else if (input.DamageType == DamageType.Magic)
                {
                    _result.RawDamage += damageContext.AttackerMagicDamage;
                    _result.PreArmorDamage = _result.RawDamage;
                }

                // 应用暴击
                if (_result.IsCritical)
                {
                    _result.RawDamage *= _result.CriticalMultiplier;
                    _result.PreArmorDamage = _result.RawDamage;
                }
            }

            return _result;
        }
    }

    /// <summary>
    /// 应用伤害加成处理器
    /// </summary>
    public class ApplyDamageBonusProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            // 使用强类型槽位获取伤害加成数据
            var bonusPercent = context.GetData(DamageSlots.DamageBonusPercent);
            var bonusFlat = context.GetData(DamageSlots.DamageBonusFlat);

            // 应用百分比加成
            if (bonusPercent != 0)
            {
                _result.BonusDamage = _result.RawDamage * bonusPercent;
                _result.RawDamage += _result.BonusDamage;
            }

            // 应用固定加成
            if (bonusFlat != 0)
            {
                _result.RawDamage += bonusFlat;
                _result.BonusDamage += bonusFlat;
            }

            return _result;
        }
    }

    /// <summary>
    /// 应用护甲减免处理器
    /// </summary>
    public class ApplyArmorReductionProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            // 只处理物理伤害
            if (input.DamageType != DamageType.Physical)
            {
                return _result;
            }

            // 真实伤害和魔法伤害不受护甲影响
            if (input.DamageType == DamageType.True)
            {
                return _result;
            }

            var damageContext = context as DamageCalculationContext;
            if (damageContext == null)
            {
                return _result;
            }

            // 使用强类型槽位获取护甲穿透数据
            var penetration = context.GetData(DamageSlots.ArmorPenetration);
            var percentPenetration = context.GetData(DamageSlots.ArmorPenetrationPercent);

            // 计算有效护甲
            var effectiveArmor = damageContext.TargetArmor;
            if (percentPenetration > 0)
            {
                effectiveArmor *= (1f - percentPenetration);
            }
            effectiveArmor -= penetration;
            effectiveArmor = Math.Max(0, effectiveArmor);

            // 护甲减免公式：damage * 100 / (100 + armor)
            var reduction = effectiveArmor / (100f + effectiveArmor);
            _result.ArmorReduction = _result.RawDamage * reduction;
            _result.RawDamage *= (1f - reduction);

            return _result;
        }
    }

    /// <summary>
    /// 应用魔抗减免处理器
    /// </summary>
    public class ApplyMagicResistReductionProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            // 只处理魔法伤害
            if (input.DamageType != DamageType.Magic)
            {
                return _result;
            }

            var damageContext = context as DamageCalculationContext;
            if (damageContext == null)
            {
                return _result;
            }

            // 使用强类型槽位获取魔抗穿透数据
            var penetration = context.GetData(DamageSlots.MagicResistPenetration);
            var percentPenetration = context.GetData(DamageSlots.MagicResistPenetrationPercent);

            // 计算有效魔抗
            var effectiveResist = damageContext.TargetMagicResist;
            if (percentPenetration > 0)
            {
                effectiveResist *= (1f - percentPenetration);
            }
            effectiveResist -= penetration;
            effectiveResist = Math.Max(0, effectiveResist);

            // 魔抗减免公式
            var reduction = effectiveResist / (100f + effectiveResist);
            _result.ResistReduction = _result.RawDamage * reduction;
            _result.RawDamage *= (1f - reduction);

            return _result;
        }
    }

    /// <summary>
    /// 计算最终伤害处理器
    /// </summary>
    public class CalculateFinalDamageProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            // 最终伤害 = 当前计算结果
            _result.FinalDamage = _result.RawDamage;

            // 向下取整避免浮点问题
            _result.FinalDamage = (float)Math.Floor(_result.FinalDamage);

            return _result;
        }
    }

    /// <summary>
    /// 计算溢出伤害处理器
    /// </summary>
    public class CalculateOverkillProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            var damageContext = context as DamageCalculationContext;

            if (damageContext != null && damageContext.TargetCurrentHealth > 0)
            {
                // 计算溢出伤害
                if (_result.FinalDamage > damageContext.TargetCurrentHealth)
                {
                    _result.Overkill = _result.FinalDamage - damageContext.TargetCurrentHealth;
                    _result.ActualDamage = damageContext.TargetCurrentHealth;
                }
                else
                {
                    _result.ActualDamage = _result.FinalDamage;
                }

                // 使用强类型槽位获取护盾数据
                var targetShield = context.GetData(DamageSlots.TargetShield);
                if (targetShield > 0)
                {
                    if (_result.FinalDamage <= targetShield)
                    {
                        _result.ShieldDamage = _result.FinalDamage;
                        _result.ActualDamage = 0;
                    }
                    else
                    {
                        _result.ShieldDamage = targetShield;
                        _result.ActualDamage = _result.FinalDamage - targetShield;
                    }
                }
            }
            else
            {
                _result.ActualDamage = _result.FinalDamage;
            }

            return _result;
        }
    }
}