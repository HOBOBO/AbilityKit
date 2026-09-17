using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services.Combat.Transactions;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Demo.Moba;
    [WorldService(typeof(MobaDamageService))]
    public sealed class MobaDamageService : IService
    {
        private readonly MobaActorLookupService _actors;
        private readonly MobaDamageEventSnapshotService _snapshots;
        private readonly MobaCombatRulesService _rules;
        private readonly IMobaBattleDiagnosticEventSink _eventCollector;
        private readonly AbilityKit.Triggering.Eventing.IEventBus _eventBus;
        private readonly MobaCombatTransactionPipeline _transactions;

        public MobaDamageService(
            MobaActorLookupService actors,
            MobaDamageEventSnapshotService snapshots,
            MobaCombatRulesService rules = null,
            IMobaBattleDiagnosticEventSink eventCollector = null,
            AbilityKit.Triggering.Eventing.IEventBus eventBus = null,
            MobaCombatTransactionPipeline transactions = null)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
            _rules = rules;
            _eventCollector = eventCollector;
            _eventBus = eventBus;
            _transactions = transactions;
        }

        internal MobaHealthChangeResult CommitDamage(
            int attackerActorId,
            int targetActorId,
            int damageType,
            Fixed64 value,
            int reasonKind = 0,
            int reasonParam = 0,
            MobaGameplayOrigin origin = default,
            bool collectDiagnostic = true)
        {
            if (targetActorId <= 0 || value <= Fixed64.Zero) return default;
            if (_rules != null && !_rules.CanReceiveDamage(attackerActorId, targetActorId).Passed) return default;
            if (!_actors.TryGetActorEntity(targetActorId, out var target) || target == null) return default;

            var attrs = target.GetMobaAttrs();
            var oldHp = attrs.FixedHp;
            var maxHp = MobaResourceFixedConvert.ToFixed(attrs.MaxHp);
            var newHp = DeterministicMath.Clamp(oldHp - value, Fixed64.Zero, maxHp);
            var actual = oldHp - newHp;
            if (actual <= Fixed64.Zero) return default;

            attrs.FixedHp = newHp;
            var result = new MobaHealthChangeResult(
                MobaHealthChangeKind.Damage,
                attackerActorId,
                targetActorId,
                damageType,
                reasonKind,
                reasonParam,
                MobaResourceFixedConvert.ToSingle(value),
                MobaResourceFixedConvert.ToSingle(actual),
                MobaResourceFixedConvert.ToSingle(oldHp),
                MobaResourceFixedConvert.ToSingle(newHp),
                attrs.MaxHp,
                in origin);
            _snapshots.ReportDamage(attackerActorId, targetActorId, damageType, MobaResourceFixedConvert.ToSingle(actual), reasonKind, reasonParam, MobaResourceFixedConvert.ToSingle(newHp), attrs.MaxHp);
            if (collectDiagnostic)
                CollectDirectDamage(in result);
            PublishCommitted(in result);
            return result;
        }

        /// <summary>float 边界重载（事件/测试入口，单次换算）。</summary>
        internal MobaHealthChangeResult CommitDamage(
            int attackerActorId,
            int targetActorId,
            int damageType,
            float value,
            int reasonKind = 0,
            int reasonParam = 0,
            MobaGameplayOrigin origin = default)
        {
            if (!IsFinitePositive(value)) return default;
            return CommitDamage(attackerActorId, targetActorId, damageType, MobaResourceFixedConvert.ToFixed(value), reasonKind, reasonParam, origin);
        }

        internal MobaHealthChangeResult CommitHeal(
            int healerActorId,
            int targetActorId,
            int healType,
            float value,
            int reasonKind = 0,
            int reasonParam = 0,
            MobaGameplayOrigin origin = default,
            bool allowDeadTarget = false)
        {
            var request = new MobaHealRequest(
                healerActorId,
                targetActorId,
                healType,
                value,
                reasonKind,
                reasonParam,
                origin,
                allowDeadTarget);
            return HealPipelineService.Execute(this, _eventBus, _transactions, in request);
        }

        internal MobaHealthChangeResult CommitHealCore(
            int healerActorId,
            int targetActorId,
            int healType,
            float value,
            int reasonKind = 0,
            int reasonParam = 0,
            MobaGameplayOrigin origin = default,
            bool allowDeadTarget = false)
        {
            if (targetActorId <= 0 || !IsFinitePositive(value)) return default;
            return CommitHealFixed(healerActorId, targetActorId, healType, MobaResourceFixedConvert.ToFixed(value), reasonKind, reasonParam, origin, allowDeadTarget);
        }

        internal MobaHealthChangeResult CommitHealFixed(
            int healerActorId,
            int targetActorId,
            int healType,
            Fixed64 value,
            int reasonKind = 0,
            int reasonParam = 0,
            MobaGameplayOrigin origin = default,
            bool allowDeadTarget = false)
        {
            if (targetActorId <= 0 || value <= Fixed64.Zero) return default;
            if (!allowDeadTarget && _rules != null && (!_rules.TryGetActor(targetActorId, out _) || !_rules.IsAlive(targetActorId))) return default;
            if (!_actors.TryGetActorEntity(targetActorId, out var target) || target == null) return default;

            var attrs = target.GetMobaAttrs();
            var oldHp = attrs.FixedHp;
            var maxHp = MobaResourceFixedConvert.ToFixed(attrs.MaxHp);
            var newHp = DeterministicMath.Clamp(oldHp + value, Fixed64.Zero, maxHp);
            var actual = newHp - oldHp;
            if (actual <= Fixed64.Zero) return default;

            attrs.FixedHp = newHp;
            var kind = allowDeadTarget ? MobaHealthChangeKind.Respawn : MobaHealthChangeKind.Heal;
            var result = new MobaHealthChangeResult(
                kind,
                healerActorId,
                targetActorId,
                healType,
                reasonKind,
                reasonParam,
                MobaResourceFixedConvert.ToSingle(value),
                MobaResourceFixedConvert.ToSingle(actual),
                MobaResourceFixedConvert.ToSingle(oldHp),
                MobaResourceFixedConvert.ToSingle(newHp),
                attrs.MaxHp,
                in origin);
            _snapshots.ReportHeal(healerActorId, targetActorId, healType, MobaResourceFixedConvert.ToSingle(actual), reasonKind, reasonParam, MobaResourceFixedConvert.ToSingle(newHp), attrs.MaxHp);
            CollectHeal(in result);
            PublishCommitted(in result);
            return result;
        }

        internal static MobaBattleDiagnosticEventDraft CreateDirectDamageDraft(
            int attackerActorId,
            int targetActorId,
            int damageType,
            float value,
            int reasonKind,
            int reasonParam,
            float targetHp,
            float maxHp)
        {
            var configId = reasonParam;
            var summary = $"directDamage={value:0.###}, damageType={damageType}, reasonKind={reasonKind}, targetHp={targetHp:0.###}, maxHp={maxHp:0.###}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded,
                attackerActorId,
                targetActorId,
                configId,
                summary: summary);
        }

        internal static MobaBattleDiagnosticEventDraft CreateHealDraft(
            int healerActorId,
            int targetActorId,
            int healType,
            float value,
            int reasonKind,
            int reasonParam,
            float targetHp,
            float maxHp)
        {
            var configId = reasonParam;
            var summary = $"heal={value:0.###}, healType={healType}, reasonKind={reasonKind}, targetHp={targetHp:0.###}, maxHp={maxHp:0.###}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Heal,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded,
                healerActorId,
                targetActorId,
                configId,
                summary: summary);
        }

        internal static MobaBattleDiagnosticEventDraft CreateDirectDamageDraft(in MobaHealthChangeResult result)
        {
            var origin = result.Origin;
            var handle = origin.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage, BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded, result.SourceActorId, result.TargetActorId,
                result.ReasonParam != 0 ? result.ReasonParam : origin.ImmediateConfigId,
                origin.EffectiveRootContextId,
                origin.ImmediateContextId != 0L ? origin.ImmediateContextId : origin.EffectiveParentContextId,
                runtime,
                summary: $"directDamage requested={result.RequestedValue:0.###}, applied={result.AppliedValue:0.###}, " +
                         $"damageType={result.ValueType}, reasonKind={result.ReasonKind}, " +
                         $"hp={result.OldHp:0.###}->{result.TargetHp:0.###}, maxHp={result.TargetMaxHp:0.###}");
        }

        internal static MobaBattleDiagnosticEventDraft CreateHealDraft(in MobaHealthChangeResult result)
        {
            var origin = result.Origin;
            var handle = origin.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new AbilityKit.Demo.Moba.Diagnostics.BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var contextId = origin.ImmediateContextId != 0L
                ? origin.ImmediateContextId : origin.EffectiveParentContextId;
            return new MobaBattleDiagnosticEventDraft(
                AbilityKit.Demo.Moba.Diagnostics.BattleDiagnosticEventKind.Heal,
                AbilityKit.Demo.Moba.Diagnostics.BattleDiagnosticEventChannel.DamageAndHeal,
                AbilityKit.Demo.Moba.Diagnostics.BattleDiagnosticEventOutcome.Succeeded,
                result.SourceActorId, result.TargetActorId,
                result.ReasonParam != 0 ? result.ReasonParam : origin.ImmediateConfigId,
                origin.EffectiveRootContextId, contextId, runtime,
                summary: $"heal requested={result.RequestedValue:0.###}, applied={result.AppliedValue:0.###}, " +
                         $"healType={result.ValueType}, reasonKind={result.ReasonKind}, " +
                         $"hp={result.OldHp:0.###}->{result.TargetHp:0.###}, maxHp={result.TargetMaxHp:0.###}");
        }

        private void CollectDirectDamage(in MobaHealthChangeResult result)
        {
            if (_eventCollector == null) return;

            try
            {
                var draft = CreateDirectDamageDraft(in result);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响直接伤害流程，静默吞掉异常。
            }
        }

        private void CollectHeal(in MobaHealthChangeResult result)
        {
            if (_eventCollector == null) return;

            try
            {
                var draft = CreateHealDraft(in result);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响治疗流程，静默吞掉异常。
            }
        }

        private void PublishCommitted(in MobaHealthChangeResult result)
        {
            if (_eventBus == null || !result.Succeeded) return;
            var eid = TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted);
            _eventBus.Publish(new EventKey<MobaHealthChangeResult>(eid), in result);
            var objectKey = new EventKey<object>(eid);
            if (_eventBus.HasSubscribers(objectKey))
            {
                object boxed = result;
                _eventBus.Publish(objectKey, in boxed);
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public void Dispose()
        {
        }
    }
}
