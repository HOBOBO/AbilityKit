using System;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

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

        public MobaDamageService(
            MobaActorLookupService actors,
            MobaDamageEventSnapshotService snapshots,
            MobaCombatRulesService rules = null,
            IMobaBattleDiagnosticEventSink eventCollector = null,
            AbilityKit.Triggering.Eventing.IEventBus eventBus = null)
        {
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
            _rules = rules;
            _eventCollector = eventCollector;
            _eventBus = eventBus;
        }

        internal MobaHealthChangeResult CommitDamage(
            int attackerActorId,
            int targetActorId,
            int damageType,
            float value,
            int reasonKind = 0,
            int reasonParam = 0,
            MobaGameplayOrigin origin = default)
        {
            if (targetActorId <= 0 || !IsFinitePositive(value)) return default;
            if (_rules != null && !_rules.CanReceiveDamage(attackerActorId, targetActorId).Passed) return default;
            if (!_actors.TryGetActorEntity(targetActorId, out var target) || target == null) return default;

            var attrs = target.GetMobaAttrs();
            var oldHp = attrs.Hp;
            var maxHp = attrs.MaxHp;
            var newHp = Clamp(oldHp - value, 0f, maxHp);
            var actual = oldHp - newHp;
            if (actual <= 0f) return default;

            attrs.Hp = newHp;
            var result = new MobaHealthChangeResult(
                MobaHealthChangeKind.Damage,
                attackerActorId,
                targetActorId,
                damageType,
                reasonKind,
                reasonParam,
                value,
                actual,
                oldHp,
                newHp,
                maxHp,
                in origin);
            _snapshots.ReportDamage(attackerActorId, targetActorId, damageType, actual, reasonKind, reasonParam, newHp, maxHp);
            CollectDirectDamage(attackerActorId, targetActorId, damageType, actual, reasonKind, reasonParam, newHp, maxHp);
            PublishCommitted(in result);
            return result;
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
            return HealPipelineService.Execute(this, _eventBus, in request);
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
            if (!allowDeadTarget && _rules != null && (!_rules.TryGetActor(targetActorId, out _) || !_rules.IsAlive(targetActorId))) return default;
            if (!_actors.TryGetActorEntity(targetActorId, out var target) || target == null) return default;

            var attrs = target.GetMobaAttrs();
            var oldHp = attrs.Hp;
            var maxHp = attrs.MaxHp;
            var newHp = Clamp(oldHp + value, 0f, maxHp);
            var actual = newHp - oldHp;
            if (actual <= 0f) return default;

            attrs.Hp = newHp;
            var kind = allowDeadTarget ? MobaHealthChangeKind.Respawn : MobaHealthChangeKind.Heal;
            var result = new MobaHealthChangeResult(
                kind,
                healerActorId,
                targetActorId,
                healType,
                reasonKind,
                reasonParam,
                value,
                actual,
                oldHp,
                newHp,
                maxHp,
                in origin);
            _snapshots.ReportHeal(healerActorId, targetActorId, healType, actual, reasonKind, reasonParam, newHp, maxHp);
            CollectHeal(healerActorId, targetActorId, healType, actual, reasonKind, reasonParam, newHp, maxHp);
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

        private void CollectDirectDamage(
            int attackerActorId,
            int targetActorId,
            int damageType,
            float value,
            int reasonKind,
            int reasonParam,
            float targetHp,
            float maxHp)
        {
            if (_eventCollector == null) return;

            try
            {
                var draft = CreateDirectDamageDraft(
                    attackerActorId,
                    targetActorId,
                    damageType,
                    value,
                    reasonKind,
                    reasonParam,
                    targetHp,
                    maxHp);
                _eventCollector.TryCollect(in draft);
            }
            catch (Exception)
            {
                // 诊断提交失败不应影响直接伤害流程，静默吞掉异常。
            }
        }

        private void CollectHeal(
            int healerActorId,
            int targetActorId,
            int healType,
            float value,
            int reasonKind,
            int reasonParam,
            float targetHp,
            float maxHp)
        {
            if (_eventCollector == null) return;

            try
            {
                var draft = CreateHealDraft(
                    healerActorId,
                    targetActorId,
                    healType,
                    value,
                    reasonKind,
                    reasonParam,
                    targetHp,
                    maxHp);
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

        private static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
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
