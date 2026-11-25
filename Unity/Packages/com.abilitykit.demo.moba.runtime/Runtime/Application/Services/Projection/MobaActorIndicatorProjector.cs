using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services.Projection
{
    /// <summary>
    /// 从当前世界值生成头顶指示器；身份只用于内容查表，算法仅由有限来源和转换决定。
    /// </summary>
    public sealed class MobaActorIndicatorProjector
    {
        private readonly MobaConfigDatabase _config;
        private readonly MobaGameplayVariableService _variables;
        private readonly MobaCharacterHfsmParameterService _parameters;

        /// <summary>
        /// 投影器不持有可变玩法副本，服务与世界同生命周期。
        /// </summary>
        public MobaActorIndicatorProjector(MobaConfigDatabase config, MobaGameplayVariableService variables,
            MobaCharacterHfsmParameterService parameters)
        { _config = config; _variables = variables; _parameters = parameters; }

        /// <summary>
        /// 读取 Actor 的明确绑定；非英雄分类只使用内容缺省，不推断英雄技能。
        /// </summary>
        public int ResolveLayout(ActorEntity actor)
        {
            if (actor == null || !actor.hasEntityMainType ||
                (actor.entityMainType.Value != EntityMainType.Unit && actor.entityMainType.Value != EntityMainType.Summon)) return 0;
            var kind = actor.hasSummonMeta ? 2 : actor.hasSkillLoadout &&
                (!actor.hasUnitSubType || actor.unitSubType.Value == UnitSubType.Hero) ? 1 : 3;
            var code = kind == 2 ? actor.summonMeta.SummonId : kind == 1 ? actor.skillLoadout.HeroId :
                actor.hasUnitSubType ? (int)actor.unitSubType.Value : 0;
            var fallback = 0;
            foreach (var binding in _config.GetTable<ActorOverheadBindingMO>().All())
            {
                var b = binding.Definition;
                if (b.ContentKind != kind) continue;
                if (b.ContentId == code) return b.LayoutId;
                if (b.ContentId == 0) fallback = b.LayoutId;
            }
            return fallback;
        }

        /// <summary>
        /// 将已声明布局中的全部指示器投影到完整集合，包括隐藏和不可用状态。
        /// </summary>
        public void Append(int actorId, ActorEntity actor, List<MobaActorIndicatorSnapshotEntry> output)
        {
            var layoutId = ResolveLayout(actor);
            if (layoutId == 0 || !_config.GetTable<OverheadLayoutMO>().TryGet(layoutId, out var layout)) return;
            foreach (var row in layout.Definition.Rows)
            {
                if (!_config.GetTable<IndicatorDefinitionMO>().TryGet(row.IndicatorId, out var definition))
                    throw new InvalidOperationException("Missing overhead indicator definition.");
                output.Add(Project(actorId, actor, layoutId, definition.Definition));
            }
        }

        /// <summary>
        /// 对单个定义求值。Availability：0 不可用、1 可用、2 按条件隐藏。
        /// </summary>
        public MobaActorIndicatorSnapshotEntry Project(int actorId, ActorEntity actor, int layoutId, IndicatorDefinitionDTO d)
        {
            var result = new MobaActorIndicatorSnapshotEntry { ActorId = actorId, LayoutId = layoutId, IndicatorId = d.Id };
            if (d.VisibilityBuffId > 0 && HasBuff(actor, d.VisibilityBuffId) != d.VisibilityBuffPresent)
            { result.Availability = 2; return result; }
            if (!TryRead(actorId, actor, d.SourceKind, d.SourceKey, d.MissingAsZero, d.AggregateMax, d.Max, out var value, out var max))
                return result;
            value = value * d.Scale + d.Offset;
            if (d.Mappings != null && d.Mappings.Length > 0)
            {
                var found = false;
                foreach (var mapping in d.Mappings)
                    if (value == mapping.Input) { value = mapping.Output; found = true; break; }
                if (!found) return result;
            }
            if (d.Overrides != null)
                foreach (var rule in d.Overrides)
                    if (TryRead(actorId, actor, rule.SourceKind, rule.SourceKey, true, false, -1, out var condition, out _) && condition == rule.Expected)
                    { value = rule.Value; break; }
            if (!MobaActorResourceReader.IsFinite(value) || !MobaActorResourceReader.IsFinite(max) || (d.Max >= 0 && max <= 0))
                return result;
            result.HasMax = d.Max >= 0;
            result.Max = result.HasMax ? max : 0;
            result.Current = Math.Max(0, result.HasMax ? Math.Min(value, max) : value);
            result.Ready = d.ReadyValue >= 0 && (d.ReadyAtLeast ? result.Current >= d.ReadyValue : result.Current == d.ReadyValue);
            result.Availability = 1;
            return result;
        }

        private bool TryRead(int actorId, ActorEntity actor, IndicatorSourceKind kind, int key, bool missingZero,
            bool aggregateMax, float fixedMax, out float value, out float max)
        {
            value = 0; max = fixedMax;
            switch (kind)
            {
                case IndicatorSourceKind.Resource:
                    return MobaActorResourceReader.TryRead(actor, (ResourceType)key, fixedMax, out value, out max);
                case IndicatorSourceKind.ActorCounter:
                    if (_variables.TryGet(MobaGameplayVariableKeys.ResolveScopedKey(key, actorId), out var counter))
                    { value = (float)counter; return MobaActorResourceReader.IsFinite(value); }
                    return missingZero;
                case IndicatorSourceKind.HfsmParameter:
                    if (!_parameters.IsDeclared(actorId, key)) return false;
                    value = _parameters.Resolve(actorId, key); return MobaActorResourceReader.IsFinite(value);
                case IndicatorSourceKind.BuffPresent:
                    value = HasBuff(actor, key) ? 1 : 0; return true;
                case IndicatorSourceKind.BuffStack:
                    var found = false;
                    if (actor != null && actor.hasBuffs && actor.buffs.Active != null)
                        foreach (var buff in actor.buffs.Active)
                            if (buff != null && buff.BuffId == key)
                            { found = true; value = aggregateMax ? Math.Max(value, buff.StackCount) : value + buff.StackCount; }
                    return found || missingZero;
                default: return false;
            }
        }

        private static bool HasBuff(ActorEntity actor, int key)
        {
            if (actor == null || !actor.hasBuffs || actor.buffs.Active == null) return false;
            foreach (var buff in actor.buffs.Active) if (buff != null && buff.BuffId == key) return true;
            return false;
        }
    }
}
