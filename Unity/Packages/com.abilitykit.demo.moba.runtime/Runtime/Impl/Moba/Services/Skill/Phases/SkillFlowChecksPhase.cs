using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Core.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo.MO;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    using AbilityKit.Ability;
    /// <summary>
    /// 基于技能条件系统的检查阶段
    /// 支持配置多个条件，全部通过才能继续
    /// </summary>
    public sealed class SkillFlowChecksPhase : AbilityInstantPhaseBase<SkillPipelineContext>
    {
        private readonly SkillChecksPhaseDTO _def;
        private readonly SkillConditionRegistry _conditionRegistry;
        private readonly List<ISkillCondition> _conditions;

        public SkillFlowChecksPhase(
            AbilityPipelinePhaseId phaseId,
            SkillChecksPhaseDTO def,
            SkillConditionRegistry conditionRegistry = null)
            : base(phaseId)
        {
            _def = def;
            _conditionRegistry = conditionRegistry;
            _conditions = new List<ISkillCondition>();
            BuildConditions();
        }

        /// <summary>
        /// 从DTO配置构建条件列表
        /// </summary>
        private void BuildConditions()
        {
            _conditions.Clear();
            if (_def == null) return;

            // 1. 冷却检查
            if (_def.CheckCooldown)
            {
                if (_conditionRegistry?.TryGet("cooldown", out var cdCondition) == true)
                {
                    _conditions.Add(cdCondition);
                }
            }

            // 2. 施法状态检查
            if (_def.CheckCastingState)
            {
                if (_conditionRegistry?.TryGet("casting_state", out var castCondition) == true)
                {
                    _conditions.Add(castCondition);
                }
            }

            // 3. 目标存在检查（如果有RequiredTags）
            if (_def.RequiredTags != null && _def.RequiredTags.Length > 0)
            {
                // TODO: 实现标签检查条件
                // if (_conditionRegistry?.TryGet("tag_required", out var tagCondition) == true)
                // {
                //     _conditions.Add(tagCondition);
                // }
            }

            // 4. 目标不存在检查（如果有BlockedTags）
            if (_def.BlockedTags != null && _def.BlockedTags.Length > 0)
            {
                // TODO: 实现标签阻塞检查条件
            }
        }

        protected override void OnInstantExecute(SkillPipelineContext context)
        {
            if (context == null) return;

            // 空配置表示不进行任何检查
            if (_conditions.Count == 0) return;

            // 执行所有条件检查
            foreach (var condition in _conditions)
            {
                if (condition == null) continue;

                var result = condition.Check(context);
                if (!result.Passed)
                {
                    context.FailReason = result.FailureReason ?? $"条件不满足: {condition.DisplayName}";
                    context.IsAborted = true;
                    return;
                }
            }
        }
    }
}
