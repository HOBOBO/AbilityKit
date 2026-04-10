using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Log;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    /// <summary>
    /// 条件 DTO 到运行时条件的转换器
    /// 
    /// 设计思路：
    ///  1. 优先使用触发器（Triggering）包的通用 ICondition 实现
    ///  2. Moba 特有条件（如冷却、施法状态）单独实现 ISkillCondition
    ///  3. 通过类型标识路由到对应的转换逻辑
    /// </summary>
    public sealed class SkillConditionDtoConverter
    {
        private readonly SkillConditionRegistry _registry;
        private readonly Dictionary<string, Func<SkillConditionDTO, object>> _converters = new Dictionary<string, Func<SkillConditionDTO, object>>(StringComparer.OrdinalIgnoreCase);

        public SkillConditionDtoConverter(SkillConditionRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            RegisterTriggeringConverters();
            RegisterMobaConverters();
        }

        private void RegisterTriggeringConverters()
        {
            // 通用触发器条件
            RegisterConverter("Const", ConvertConst);
            RegisterConverter("HasTarget", ConvertHasTarget);

            // 复合条件
            RegisterConverter("And", ConvertAnd);
            RegisterConverter("Or", ConvertOr);
            RegisterConverter("Not", ConvertNot);
            RegisterConverter("Multi", ConvertMulti);

            // 数值比较
            RegisterConverter("NumericCompare", ConvertNumericCompare);
            RegisterConverter("PayloadCompare", ConvertPayloadCompare);
        }

        private void RegisterMobaConverters()
        {
            // Moba 特有条件（返回 ISkillCondition）
            RegisterConverter("Moba_Cooldown", ConvertMobaCooldown);
            RegisterConverter("Moba_CastingState", ConvertMobaCastingState);
            RegisterConverter("Moba_SelfOnly", ConvertMobaSelfOnly);
            RegisterConverter("Moba_Tag", ConvertMobaTag);
        }

        /// <summary>
        /// 注册自定义转换器
        /// </summary>
        public void RegisterConverter(string type, Func<SkillConditionDTO, object> converter)
        {
            if (string.IsNullOrEmpty(type)) return;
            _converters[type] = converter;
        }

        /// <summary>
        /// 根据 DTO 转换
        /// 返回值可能是 ICondition（通用触发器条件）或 ISkillCondition（Moba特有条件）
        /// </summary>
        public object Convert(SkillConditionDTO dto)
        {
            if (dto == null) return null;

            var type = dto.Type ?? dto.GetType().Name.Replace("ConditionDTO", "").Replace("DTO", "");
            if (!_converters.TryGetValue(type, out var converter))
            {
                Log.Warning($"[SkillConditionDtoConverter] No converter for type: {type}");
                return null;
            }

            return converter(dto);
        }

        /// <summary>
        /// 转换为通用触发器条件（ICondition）
        /// </summary>
        public ICondition ConvertToTriggeringCondition(SkillConditionDTO dto)
        {
            var result = Convert(dto);
            return result as ICondition;
        }

        /// <summary>
        /// 转换为技能特有条件（ISkillCondition）
        /// </summary>
        public ISkillCondition ConvertToSkillCondition(SkillConditionDTO dto)
        {
            var result = Convert(dto);
            if (result is ISkillCondition skillCondition)
                return skillCondition;

            // 尝试从注册表获取对应的 ISkillCondition 包装
            if (result is ICondition icondition)
            {
                Log.Warning($"[SkillConditionDtoConverter] Cannot convert ICondition to ISkillCondition for type: {dto?.Type}");
            }

            return null;
        }

        /// <summary>
        /// 将 DTO 数组转换为条件列表
        /// </summary>
        public List<object> ConvertAll(SkillConditionDTO[] dtos)
        {
            var results = new List<object>();
            if (dtos == null) return results;

            foreach (var dto in dtos)
            {
                var condition = Convert(dto);
                if (condition != null)
                {
                    results.Add(condition);
                }
            }

            return results;
        }

        // ========================================================================
        // 触发器通用条件转换
        // ========================================================================

        private ICondition ConvertConst(SkillConditionDTO dto)
        {
            var typed = dto as ConstConditionDTO;
            return new ConstCondition { Value = typed?.Value ?? true };
        }

        private ICondition ConvertHasTarget(SkillConditionDTO dto)
        {
            var typed = dto as HasTargetConditionDTO;
            return new HasTargetCondition { Negate = typed?.Negate ?? false };
        }

        private ICondition ConvertAnd(SkillConditionDTO dto)
        {
            var typed = dto as AndConditionDTO;
            return new AndCondition
            {
                Left = ConvertToTriggeringCondition(typed?.Left),
                Right = ConvertToTriggeringCondition(typed?.Right)
            };
        }

        private ICondition ConvertOr(SkillConditionDTO dto)
        {
            var typed = dto as OrConditionDTO;
            return new OrCondition
            {
                Left = ConvertToTriggeringCondition(typed?.Left),
                Right = ConvertToTriggeringCondition(typed?.Right)
            };
        }

        private ICondition ConvertNot(SkillConditionDTO dto)
        {
            var typed = dto as NotConditionDTO;
            return new NotCondition
            {
                Inner = ConvertToTriggeringCondition(typed?.Inner)
            };
        }

        private ICondition ConvertMulti(SkillConditionDTO dto)
        {
            var typed = dto as MultiConditionDTO;
            if (typed == null) return null;

            var multi = new MultiCondition
            {
                Combinator = (EConditionCombinator)(typed.Combinator > 0 ? 1 : 0)
            };

            if (typed.Conditions != null)
            {
                foreach (var childDto in typed.Conditions)
                {
                    var child = ConvertToTriggeringCondition(childDto);
                    if (child != null)
                    {
                        multi.Conditions.Add(child);
                    }
                }
            }

            return multi;
        }

        private NumericValueRef ConvertNumericRef(NumericRefDTO dto)
        {
            if (dto == null) return NumericValueRef.Const(0);

            return dto.Kind switch
            {
                ENumericRefKind.Const => NumericValueRef.Const(dto.ConstValue),
                ENumericRefKind.Blackboard => NumericValueRef.Blackboard(dto.BoardId, dto.KeyId),
                ENumericRefKind.PayloadField => NumericValueRef.PayloadField(dto.FieldId),
                ENumericRefKind.Var => NumericValueRef.Var(dto.DomainId, dto.Key),
                ENumericRefKind.Expr => NumericValueRef.Expr(dto.ExprText),
                _ => NumericValueRef.Const(0)
            };
        }

        private ICondition ConvertNumericCompare(SkillConditionDTO dto)
        {
            var typed = dto as NumericCompareConditionDTO;
            if (typed == null) return null;

            return new NumericCompareCondition
            {
                Op = (AbilityKit.Triggering.Runtime.Executable.ECompareOp)typed.Op,
                Left = ConvertNumericRef(typed.Left),
                Right = ConvertNumericRef(typed.Right)
            };
        }

        private ICondition ConvertPayloadCompare(SkillConditionDTO dto)
        {
            var typed = dto as PayloadCompareConditionDTO;
            if (typed == null) return null;

            return new PayloadCompareCondition
            {
                FieldId = typed.FieldId,
                Op = (AbilityKit.Triggering.Runtime.Executable.ECompareOp)typed.Op,
                CompareValue = ConvertNumericRef(typed.CompareValue),
                Negate = typed.Negate
            };
        }

        // ========================================================================
        // Moba 特有条件转换
        // ========================================================================

        private ISkillCondition ConvertMobaCooldown(SkillConditionDTO dto)
        {
            if (_registry.TryGet("cooldown", out var condition))
            {
                return condition;
            }
            Log.Warning("[SkillConditionDtoConverter] Cooldown condition not registered.");
            return null;
        }

        private ISkillCondition ConvertMobaCastingState(SkillConditionDTO dto)
        {
            if (_registry.TryGet("casting_state", out var condition))
            {
                return condition;
            }
            Log.Warning("[SkillConditionDtoConverter] CastingState condition not registered.");
            return null;
        }

        private ISkillCondition ConvertMobaSelfOnly(SkillConditionDTO dto)
        {
            if (_registry.TryGet("self_only", out var condition))
            {
                return condition;
            }
            Log.Warning("[SkillConditionDtoConverter] SelfOnly condition not registered.");
            return null;
        }

        private ISkillCondition ConvertMobaTag(SkillConditionDTO dto)
        {
            if (_registry.TryGet("not_silenced", out var condition))
            {
                return condition;
            }
            Log.Warning("[SkillConditionDtoConverter] Tag condition not registered.");
            return null;
        }
    }
}
