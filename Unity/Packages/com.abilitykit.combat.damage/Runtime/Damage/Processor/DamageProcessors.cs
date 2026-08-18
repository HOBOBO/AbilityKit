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
        /// 暴击判定随机值，范围建议为 0..1。默认 1 表示不触发暴击。
        /// </summary>
        public static readonly DataflowSlot<float> CritRoll = new DataflowSlot<float>("Damage_CritRoll", 1f);

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
        protected static DamageResult GetCurrentResult(DamageRequest input, IDataflowContext context)
        {
            var damageContext = context as DamageCalculationContext;
            var result = damageContext?.Result ?? DamageResult.Create(input);
            result.Request = input;
            return result;
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
            var result = GetCurrentResult(input, context);

            // 验证基础条件
            if (input.Attacker == null)
            {
                context.Abort();
                return result;
            }

            if (input.Target == null)
            {
                context.Abort();
                return result;
            }

            if (input.BaseValue <= 0 && !IsDot(input))
            {
                context.Abort();
                return result;
            }

            return result;
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
            var result = GetCurrentResult(input, context);

            // 使用强类型槽位获取暴击数据
            var critChance = context.GetData(DamageSlots.CritChance);
            var critMultiplier = context.GetData(DamageSlots.CritMultiplier);
            var critRoll = context.GetData(DamageSlots.CritRoll);

            // 暴击计算：随机值由上层注入，便于纯逻辑测试、回放和确定性 sample。
            if (critChance > 0 && critRoll < critChance)
            {
                result.Request.Flags |= DamageFlags.Critical;
                result.CriticalMultiplier = critMultiplier;
            }
            else
            {
                result.CriticalMultiplier = 1f;
            }

            return result;
        }
    }

    /// <summary>
    /// 计算基础伤害处理器
    /// </summary>
    public class CalculateBaseDamageProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            var result = GetCurrentResult(input, context);
            var damageContext = context as DamageCalculationContext;
            result.RawDamage = input.BaseValue;
            result.PreArmorDamage = input.BaseValue;

            if (damageContext != null)
            {
                // 根据伤害类型应用对应的攻击力加成
                if (input.DamageType == DamageType.Physical)
                {
                    result.RawDamage += damageContext.AttackerPhysicalDamage;
                    result.PreArmorDamage = result.RawDamage;
                }
                else if (input.DamageType == DamageType.Magic)
                {
                    result.RawDamage += damageContext.AttackerMagicDamage;
                    result.PreArmorDamage = result.RawDamage;
                }

                // 应用暴击
                if (result.IsCritical)
                {
                    result.RawDamage *= result.CriticalMultiplier;
                    result.PreArmorDamage = result.RawDamage;
                }
            }

            return result;
        }
    }

    /// <summary>
    /// 应用伤害加成处理器
    /// </summary>
    public class ApplyDamageBonusProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            var result = GetCurrentResult(input, context);

            // 使用强类型槽位获取伤害加成数据
            var bonusPercent = context.GetData(DamageSlots.DamageBonusPercent);
            var bonusFlat = context.GetData(DamageSlots.DamageBonusFlat);

            // 应用百分比加成
            if (bonusPercent != 0)
            {
                result.BonusDamage = result.RawDamage * bonusPercent;
                result.RawDamage += result.BonusDamage;
            }

            // 应用固定加成
            if (bonusFlat != 0)
            {
                result.RawDamage += bonusFlat;
                result.BonusDamage += bonusFlat;
            }

            return result;
        }
    }

    /// <summary>
    /// 应用护甲减免处理器
    /// </summary>
    public class ApplyArmorReductionProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            var result = GetCurrentResult(input, context);

            // 只处理物理伤害
            if (input.DamageType != DamageType.Physical)
            {
                return result;
            }

            // 真实伤害和魔法伤害不受护甲影响
            if (input.DamageType == DamageType.True)
            {
                return result;
            }

            var damageContext = context as DamageCalculationContext;
            if (damageContext == null)
            {
                return result;
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
            result.ArmorReduction = result.RawDamage * reduction;
            result.RawDamage *= (1f - reduction);

            return result;
        }
    }

    /// <summary>
    /// 应用魔抗减免处理器
    /// </summary>
    public class ApplyMagicResistReductionProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            var result = GetCurrentResult(input, context);

            // 只处理魔法伤害
            if (input.DamageType != DamageType.Magic)
            {
                return result;
            }

            var damageContext = context as DamageCalculationContext;
            if (damageContext == null)
            {
                return result;
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
            result.ResistReduction = result.RawDamage * reduction;
            result.RawDamage *= (1f - reduction);

            return result;
        }
    }

    /// <summary>
    /// 计算最终伤害处理器
    /// </summary>
    public class CalculateFinalDamageProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            var result = GetCurrentResult(input, context);

            // 最终伤害 = 当前计算结果
            result.FinalDamage = result.RawDamage;

            // 向下取整避免浮点问题
            result.FinalDamage = (float)Math.Floor(result.FinalDamage);

            return result;
        }
    }

    /// <summary>
    /// 计算溢出伤害处理器
    /// </summary>
    public class CalculateOverkillProcessor : DamageProcessor
    {
        protected override DamageResult OnProcess(DamageRequest input, IDataflowContext context)
        {
            var result = GetCurrentResult(input, context);
            var damageContext = context as DamageCalculationContext;

            if (damageContext != null && damageContext.TargetCurrentHealth > 0)
            {
                // 计算溢出伤害
                if (result.FinalDamage > damageContext.TargetCurrentHealth)
                {
                    result.Overkill = result.FinalDamage - damageContext.TargetCurrentHealth;
                    result.ActualDamage = damageContext.TargetCurrentHealth;
                }
                else
                {
                    result.ActualDamage = result.FinalDamage;
                }

                // 使用强类型槽位获取护盾数据
                var targetShield = context.GetData(DamageSlots.TargetShield);
                if (targetShield > 0)
                {
                    if (result.FinalDamage <= targetShield)
                    {
                        result.ShieldDamage = result.FinalDamage;
                        result.ActualDamage = 0;
                    }
                    else
                    {
                        result.ShieldDamage = targetShield;
                        result.ActualDamage = result.FinalDamage - targetShield;
                    }
                }
            }
            else
            {
                result.ActualDamage = result.FinalDamage;
            }

            return result;
        }
    }
}
