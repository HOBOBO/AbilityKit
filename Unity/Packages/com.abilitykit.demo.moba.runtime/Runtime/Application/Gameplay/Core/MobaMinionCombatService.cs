using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Minions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Gameplay
{
    /// <summary>
    /// Deterministic minion combat loop. Targeting is intentionally restricted to
    /// active enemy minions; damage and death remain owned by the shared combat pipeline.
    /// </summary>
    [WorldService(typeof(MobaMinionCombatService))]
    public sealed class MobaMinionCombatService : IWorldDeinitializable
    {
        private const long MeleeAttackCooldownMs = 1200;
        private const long RangedAttackCooldownMs = 1500;

        private readonly MobaGameplayService _gameplay;
        private readonly IMobaMinionWaveService _waves;
        private readonly MobaActorRegistry _actors;
        private readonly MobaCombatRulesService _rules;
        private readonly MobaCombatEffectService _combat;
        private readonly Dictionary<int, long> _nextAttackAtMs = new Dictionary<int, long>();
        private readonly HashSet<int> _reportedDeaths = new HashSet<int>();
        private readonly List<MobaMinionRuntimeView> _runtimeScratch = new List<MobaMinionRuntimeView>(64);
        private readonly List<MobaMinionAttackEvent> _attacks = new List<MobaMinionAttackEvent>(64);
        private readonly List<MobaMinionKilledEvent> _kills = new List<MobaMinionKilledEvent>(32);
        private IDisposable _deathSubscription;
        private long _gameplayId;
        [WorldInject(required: false)] private IEventBus _eventBus;

        public MobaMinionCombatService(
            MobaGameplayService gameplay,
            IMobaMinionWaveService waves,
            MobaActorRegistry actors,
            MobaCombatRulesService rules,
            MobaCombatEffectService combat,
            IEventBus eventBus = null)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            _waves = waves ?? throw new ArgumentNullException(nameof(waves));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _rules = rules;
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _eventBus = eventBus;

            if (_eventBus != null)
            {
                var eid = TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted);
                _deathSubscription = _eventBus.Subscribe(
                    new EventKey<MobaHealthChangeResult>(eid), OnHealthCommitted);
            }
        }

        public IReadOnlyList<MobaMinionAttackEvent> Attacks => _attacks;
        public IReadOnlyList<MobaMinionKilledEvent> Kills => _kills;

        public int DrainAttacks(List<MobaMinionAttackEvent> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            destination.AddRange(_attacks);
            _attacks.Clear();
            return destination.Count;
        }

        public int DrainKills(List<MobaMinionKilledEvent> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            destination.AddRange(_kills);
            _kills.Clear();
            return destination.Count;
        }

        public void Reset()
        {
            ResetRuntimeState();
            _gameplayId = 0;
        }

        public MobaMinionCombatRollbackState CaptureRollbackState()
        {
            var cooldowns = new List<MobaMinionAttackRollbackState>(_nextAttackAtMs.Count);
            foreach (var item in _nextAttackAtMs)
                cooldowns.Add(new MobaMinionAttackRollbackState(item.Key, item.Value));
            cooldowns.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));

            var deaths = new List<int>(_reportedDeaths);
            deaths.Sort();
            return new MobaMinionCombatRollbackState(_gameplayId, cooldowns, deaths);
        }

        public void RestoreRollbackState(in MobaMinionCombatRollbackState state)
        {
            if (state.GameplayId < 0) throw new InvalidOperationException("Invalid minion combat gameplay id.");
            ResetRuntimeState();
            _gameplayId = state.GameplayId;
            for (var i = 0; i < state.AttackCooldowns.Count; i++)
            {
                var item = state.AttackCooldowns[i];
                if (item.ActorId > 0) _nextAttackAtMs[item.ActorId] = item.NextAttackAtMs;
            }
            for (var i = 0; i < state.ReportedDeaths.Count; i++)
                if (state.ReportedDeaths[i] > 0) _reportedDeaths.Add(state.ReportedDeaths[i]);
        }

        public void Tick()
        {
            if (!_gameplay.IsRunning)
            {
                ResetRuntimeState();
                return;
            }

            var gameplayId = _gameplay.CurrentGameplayId;
            if (_gameplayId != gameplayId)
            {
                ResetRuntimeState();
                _gameplayId = gameplayId;
            }

            EnsureDeathSubscription();

            var nowMs = _gameplay.GetClockState().ElapsedMilliseconds;
            var frame = _gameplay.CurrentFrame;
            _waves.CopyActiveRuntimeViews(_runtimeScratch);
            for (var i = 0; i < _runtimeScratch.Count; i++)
            {
                var attacker = _runtimeScratch[i];
                if (!_actors.TryGet(attacker.ActorId, out var attackerEntity) || attackerEntity == null ||
                    !attackerEntity.hasTransform || !IsAlive(attacker.ActorId, attackerEntity) ||
                    !CanAttack(attacker.ActorId))
                    continue;

                if (!TryFindNearestEnemyMinion(in attacker, out var targetId, out var distance))
                {
                    _waves.SetAiState(attacker.ActorId, MobaMinionAiState.FollowingRoute);
                    continue;
                }

                var attrs = attackerEntity.GetMobaAttrs();
                var attackRange = attrs.AttackRange > 0f
                    ? attrs.AttackRange
                    : attacker.Archetype == MobaMinionArchetype.Ranged ? 5f : 1f;
                if (distance > attackRange)
                {
                    _waves.SetAiState(attacker.ActorId, MobaMinionAiState.AcquiringTarget);
                    continue;
                }

                _waves.SetAiState(attacker.ActorId, MobaMinionAiState.Attacking);
                StopMovement(attackerEntity);
                if (_nextAttackAtMs.TryGetValue(attacker.ActorId, out var nextAt) && nowMs < nextAt)
                    continue;

                var damage = attrs.PhysicsAttack + attrs.ExtraPhysicsAttack;
                if (damage <= 0f) continue;

                var attack = new AttackInfo
                {
                    AttackerActorId = attacker.ActorId,
                    TargetActorId = targetId,
                    DamageType = DamageType.Physical,
                    CritType = CritType.None,
                    ReasonKind = DamageReasonKind.BasicAttack,
                    ReasonParam = MobaMinionArchetypeCatalog.GetCharacterId(attacker.Archetype),
                    FormulaKind = (int)DamageFormulaKind.Standard,
                };
                attack.BaseDamage.FixedBaseValue = MobaResourceFixedConvert.ToFixed(damage);
                var result = _combat.DealDamage(attack);
                _nextAttackAtMs[attacker.ActorId] = nowMs + GetCooldownMs(attacker.Archetype, attrs.AttackSpeedR);
                if (result == null || result.Value <= 0f) continue;

                var payload = new MobaMinionAttackEvent(
                    attacker.ActorId, targetId, attacker.WaveId, attacker.Archetype,
                    result.Value, frame);
                _attacks.Add(payload);
                Publish(MobaMinionEvents.Attack, in payload);
            }
        }

        private bool TryFindNearestEnemyMinion(
            in MobaMinionRuntimeView attacker,
            out int targetId,
            out float distance)
        {
            targetId = 0;
            distance = float.MaxValue;
            if (!_actors.TryGet(attacker.ActorId, out var source) || source == null || !source.hasTransform)
                return false;

            var sourcePosition = source.transform.Value.Position;
            for (var i = 0; i < _runtimeScratch.Count; i++)
            {
                var candidate = _runtimeScratch[i];
                if (candidate.Team == attacker.Team || candidate.ActorId == attacker.ActorId ||
                    (_rules != null
                        ? !_rules.AreEnemies(attacker.ActorId, candidate.ActorId)
                        : candidate.Team == attacker.Team) ||
                    !_actors.TryGet(candidate.ActorId, out var entity) || entity == null ||
                    !entity.hasTransform || !IsAlive(candidate.ActorId, entity))
                    continue;

                var position = entity.transform.Value.Position;
                var dx = position.X - sourcePosition.X;
                var dz = position.Z - sourcePosition.Z;
                var candidateDistance = (float)Math.Sqrt(dx * dx + dz * dz);
                if (candidateDistance < distance ||
                    (Math.Abs(candidateDistance - distance) < 0.0001f && candidate.ActorId < targetId))
                {
                    distance = candidateDistance;
                    targetId = candidate.ActorId;
                }
            }

            return targetId > 0;
        }

        private bool CanAttack(int actorId)
        {
            return _rules == null || _rules.CanAttack(actorId);
        }

        private bool IsAlive(int actorId, global::ActorEntity entity)
        {
            if (_rules != null) return _rules.IsAlive(actorId);
            return entity != null && (!entity.hasAttributeGroup || entity.GetMobaAttrs().Hp > 0f);
        }

        private static long GetCooldownMs(MobaMinionArchetype archetype, float attackSpeedR)
        {
            var baseCooldown = archetype == MobaMinionArchetype.Ranged
                ? RangedAttackCooldownMs : MeleeAttackCooldownMs;
            if (attackSpeedR <= 0f) return baseCooldown;
            var multiplier = Math.Max(0.25f, 1f - attackSpeedR / 100f);
            return Math.Max(1L, (long)Math.Round(baseCooldown * multiplier));
        }

        private void OnHealthCommitted(MobaHealthChangeResult result)
        {
            if (!result.BecameDead || result.TargetActorId <= 0 ||
                !_waves.TryGetRuntime(result.TargetActorId, out var victim) ||
                !_reportedDeaths.Add(result.TargetActorId))
                return;

            var frame = _gameplay.CurrentFrame;
            var payload = new MobaMinionKilledEvent(
                victim.ActorId,
                result.SourceActorId,
                victim.WaveId,
                victim.Archetype,
                frame,
                rewardPoints: 0);
            _kills.Add(payload);
            _waves.SetAiState(victim.ActorId, MobaMinionAiState.Dead);
            _nextAttackAtMs.Remove(victim.ActorId);

            if (_actors.TryGet(victim.ActorId, out var entity) && entity != null)
            {
                ActorLifecycleRequests.RequestDespawn(
                    entity, frame, ActorDespawnReason.MinionKilled,
                    result.SourceActorId, victim.WaveId);
            }

            Publish(MobaMinionEvents.Killed, in payload);
        }

        private void EnsureDeathSubscription()
        {
            if (_deathSubscription != null || _eventBus == null) return;
            var eid = TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted);
            _deathSubscription = _eventBus.Subscribe(
                new EventKey<MobaHealthChangeResult>(eid), OnHealthCommitted);
        }

        private void Publish<T>(string eventName, in T payload)
        {
            if (_eventBus == null) return;
            var eid = TriggeringIdUtil.GetEventEid(eventName);
            _eventBus.Publish(new EventKey<T>(eid), in payload);
            var objectKey = new EventKey<object>(eid);
            if (_eventBus.HasSubscribers(objectKey))
            {
                object boxed = payload;
                _eventBus.Publish(objectKey, in boxed);
            }
        }

        private static void StopMovement(global::ActorEntity entity)
        {
            if (entity != null && entity.hasMoveInput)
                entity.ReplaceMoveInput(0f, 0f);
        }

        public void OnDeinit(IWorldResolver services)
        {
            _deathSubscription?.Dispose();
            _deathSubscription = null;
            ResetRuntimeState();
            _gameplayId = 0;
        }

        public void Dispose()
        {
            _deathSubscription?.Dispose();
            _deathSubscription = null;
            ResetRuntimeState();
            _gameplayId = 0;
        }

        private void ResetRuntimeState()
        {
            _nextAttackAtMs.Clear();
            _reportedDeaths.Clear();
            _runtimeScratch.Clear();
            _attacks.Clear();
            _kills.Clear();
        }
    }
}
