using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Gameplay.Structures;
using AbilityKit.Demo.Moba.Minions;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Projectile.Launch;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Gameplay
{
    [WorldService(typeof(MobaStructureService), WorldLifetime.Scoped)]
    public sealed class MobaStructureService : IWorldDeinitializable
    {
        private const int BaseDamageReason = 7001;
        private readonly ActorIdAllocator _actorIds;
        private readonly IMobaActorSpawnService _spawn;
        private readonly MobaActorRegistry _actors;
        private readonly MobaConfigDatabase _configs;
        private readonly MobaDamageService _damage;
        [WorldInject(required: false)] private MobaProjectileService _projectiles;
        [WorldInject(required: false)] private MobaCombatRulesService _combatRules;
        [WorldInject(required: false)] private MobaGameplayService _gameplay;
        private readonly MobaActorAttributeInitializer _attributes = new MobaActorAttributeInitializer();
        private readonly List<MobaActorSpawnResult> _spawnedStructures = new List<MobaActorSpawnResult>();
        private readonly Dictionary<int, StructureRuntime> _structures = new Dictionary<int, StructureRuntime>();
        private readonly Dictionary<int, StructureRuntime> _structuresByActor = new Dictionary<int, StructureRuntime>();
        private readonly Dictionary<int, int> _baseByTeam = new Dictionary<int, int>();
        private readonly Dictionary<int, List<int>> _towersByTeam = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, long> _nextAttackAtMs = new Dictionary<int, long>();
        private readonly List<int> _structureActorScratch = new List<int>();
        private IDisposable _healthCommittedSubscription;
        private IEventBus _events;
        [WorldInject(required: false)] private MobaActorSpawnSnapshotService _spawnSnapshots;

        public MobaStructureService(
            ActorIdAllocator actorIds,
            IMobaActorSpawnService spawn,
            MobaActorRegistry actors,
            MobaConfigDatabase configs,
            MobaDamageService damage,
            IEventBus events = null)
        {
            _actorIds = actorIds ?? throw new ArgumentNullException(nameof(actorIds));
            _spawn = spawn ?? throw new ArgumentNullException(nameof(spawn));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _damage = damage ?? throw new ArgumentNullException(nameof(damage));
            _events = events;
        }

        public IReadOnlyDictionary<int, int> BaseActorsByTeam => _baseByTeam;

        public bool TryGetNextTarget(int teamId, out int actorId)
        {
            return TryGetNextTargetInternal(teamId, out actorId);
        }

        public void CopyAttackCooldowns(List<MobaStructureAttackCooldownState> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            foreach (var item in _nextAttackAtMs)
                destination.Add(new MobaStructureAttackCooldownState(item.Key, item.Value));
            destination.Sort((left, right) => left.ActorId.CompareTo(right.ActorId));
        }

        public void RestoreAttackCooldowns(IReadOnlyList<MobaStructureAttackCooldownState> cooldowns)
        {
            if (cooldowns == null) throw new ArgumentNullException(nameof(cooldowns));
            _nextAttackAtMs.Clear();
            for (var i = 0; i < cooldowns.Count; i++)
            {
                var item = cooldowns[i];
                if (item.ActorId <= 0 || item.NextAttackAtMs < 0 ||
                    (i > 0 && cooldowns[i - 1].ActorId >= item.ActorId))
                    throw new InvalidOperationException("Invalid structure attack cooldown state.");
                _nextAttackAtMs.Add(item.ActorId, item.NextAttackAtMs);
            }
        }

        public void Prepare(MobaMapStructureResource resource)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            Reset();
            try
            {
                for (var i = 0; i < resource.Structures.Count; i++)
                {
                    var definition = resource.Structures[i];
                    if (!_configs.TryGetAttributeTemplate(definition.AttributeTemplateId, out var template) || template == null)
                        throw new InvalidOperationException($"Structure attribute template was not found. structureId={definition.Id}, templateId={definition.AttributeTemplateId}");
                    if (definition.ModelId > 0 && !_configs.TryGetModel(definition.ModelId, out var model))
                        throw new InvalidOperationException($"Structure model was not found. structureId={definition.Id}, modelId={definition.ModelId}");

                    var kind = definition.ParsedKind;
                    var position = definition.Position ?? new AbilityKit.Demo.Moba.Share.Config.MapVector3DTO();
                    var info = new MobaEntityInfo(
                        _actorIds.Next(), MobaEntityKind.Building,
                        new Transform3(new Vec3(position.X, position.Y, position.Z),
                            Quat.FromAxisAngle(Vec3.Up, definition.YawDegrees * (float)Math.PI / 180f), Vec3.One),
                        (Team)definition.TeamId, EntityMainType.Unit,
                        kind == MobaStructureKind.Base ? UnitSubType.Base : UnitSubType.Tower,
                        default, definition.AttributeTemplateId);
                    var spec = new MobaActorBuildSpec(
                        in info, MobaActorBuildSourceKind.Unknown, definition.Id, 0);
                    var request = MobaActorSpawnRequest.FromSpec(in spec);
                    var capturedTemplate = template;
                    request.Initializer = (entity, _) => _attributes.ApplyTemplate(entity, capturedTemplate);
                    request.PostSetup = new MobaActorSpawnPostSetup { SetModelId = definition.ModelId > 0, ModelId = definition.ModelId };
                    if (!_spawn.TrySpawn(in request, out var result) || !result.Success || result.Entity == null)
                        throw new InvalidOperationException($"Could not spawn map structure. structureId={definition.Id}");

                    var runtime = new StructureRuntime(definition.Id, result.ActorId, kind, definition.TeamId);
                    _spawnedStructures.Add(result);
                    _structures.Add(definition.Id, runtime);
                    _structuresByActor.Add(result.ActorId, runtime);
                    if (kind == MobaStructureKind.Base)
                    {
                        if (_baseByTeam.ContainsKey(definition.TeamId))
                            throw new InvalidOperationException($"Only one base is allowed per team. teamId={definition.TeamId}");
                        _baseByTeam.Add(definition.TeamId, result.ActorId);
                    }
                    else
                    {
                        if (!_towersByTeam.TryGetValue(definition.TeamId, out var towers))
                        {
                            towers = new List<int>();
                            _towersByTeam.Add(definition.TeamId, towers);
                        }
                        towers.Add(result.ActorId);
                    }

                    if (_spawnSnapshots != null && !_spawnSnapshots.TryEnqueueActorSpawn(
                            _actors, result.ActorId, SpawnEntityKind.Building, definition.ModelId > 0 ? definition.ModelId : 90001004))
                        throw new InvalidOperationException($"Could not publish structure spawn snapshot. structureId={definition.Id}.");
                }

                if (_events != null)
                {
                    var healthEventId = TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted);
                    _healthCommittedSubscription = _events.Subscribe(
                        new EventKey<MobaHealthChangeResult>(healthEventId), OnHealthCommitted);
                }
            }
            catch
            {
                Reset();
                throw;
            }
        }

        public bool ApplyMinionBaseDamage(in MobaMinionBaseDamageEvent request)
        {
            if (!TryGetNextTarget(request.TargetTeamId, out var targetActorId)) return false;
            if (!_actors.TryGet(targetActorId, out var target) || target == null || !target.hasAttributeGroup) return false;
            _damage.CommitDamage(request.ActorId, targetActorId, 0,
                MobaResourceFixedConvert.ToFixed(request.Damage), BaseDamageReason, 0);
            return true;
        }

        public void Tick()
        {
            if (_gameplay == null || !_gameplay.IsRunning || _projectiles == null || _configs == null) return;
            var nowMs = _gameplay.GetClockState().ElapsedMilliseconds;
            _structureActorScratch.Clear();
            foreach (var actorId in _structuresByActor.Keys) _structureActorScratch.Add(actorId);
            _structureActorScratch.Sort();

            for (var i = 0; i < _structureActorScratch.Count; i++)
            {
                var attackerId = _structureActorScratch[i];
                if (!_structuresByActor.TryGetValue(attackerId, out var structure) || structure.Destroyed ||
                    !_actors.TryGet(attackerId, out var attacker) || attacker == null ||
                    !attacker.hasTransform || !attacker.hasAttributeGroup || !IsAlive(attackerId, attacker) ||
                    (_combatRules != null && !_combatRules.CanAttack(attackerId)))
                    continue;

                var attrs = attacker.GetMobaAttrs();
                var range = attrs.AttackRange;
                var damage = attrs.PhysicsAttack + attrs.ExtraPhysicsAttack;
                if (range <= 0f || damage <= 0f) continue;
                if (_nextAttackAtMs.TryGetValue(attackerId, out var nextAt) && nowMs < nextAt) continue;
                if (!TryFindEnemyInRange(attackerId, structure.TeamId, range, out var targetId) ||
                    !_actors.TryGet(targetId, out var target) || target == null || !target.hasTransform)
                    continue;
                if (!_configs.TryGetProjectileLauncher(31020001, out var launcher) || launcher == null ||
                    !_configs.TryGetProjectile(30020001, out var projectile) || projectile == null)
                    continue;

                var source = attacker.transform.Value.Position;
                var destination = target.transform.Value.Position;
                var direction = new Vec3(destination.X - source.X, 0f, destination.Z - source.Z);
                if (direction.SqrMagnitude <= 0.0001f) continue;
                var context = ProjectileSourceContextBuilder.Create()
                    .WithActors(attackerId, targetId)
                    .WithProjectileConfig(projectile.Id)
                    .Build();
                if (_projectiles.Launch(attackerId, launcher, projectile, in source, in direction, in context))
                    _nextAttackAtMs[attackerId] = nowMs + GetAttackCooldownMs(attrs.AttackSpeedR);
            }
        }

        private bool TryFindEnemyInRange(int attackerId, int teamId, float range, out int targetId)
        {
            targetId = 0;
            var source = _actors.TryGet(attackerId, out var attacker) && attacker != null && attacker.hasTransform
                ? attacker.transform.Value.Position : default;
            var bestDistanceSq = range * range;
            foreach (var pair in _actors.Entries)
            {
                var candidateId = pair.Key;
                var candidate = pair.Value;
                if (candidateId == attackerId || candidate == null || !candidate.hasTransform ||
                    !candidate.hasTeam || (int)candidate.team.Value == teamId ||
                    !candidate.hasAttributeGroup || !IsAlive(candidateId, candidate) ||
                    !candidate.hasEntityMainType || !candidate.hasUnitSubType)
                    continue;
                var kind = MobaEntityKindResolver.Resolve(candidate.entityMainType.Value, candidate.unitSubType.Value);
                if (kind != MobaEntityKind.Minion && kind != MobaEntityKind.Hero)
                    continue;
                var position = candidate.transform.Value.Position;
                var dx = position.X - source.X;
                var dz = position.Z - source.Z;
                var distanceSq = dx * dx + dz * dz;
                if (distanceSq > bestDistanceSq) continue;
                if (targetId == 0 || distanceSq < bestDistanceSq || candidateId < targetId)
                {
                    targetId = candidateId;
                    bestDistanceSq = distanceSq;
                }
            }
            return targetId > 0;
        }

        private bool IsAlive(int actorId, global::ActorEntity actor)
        {
            return _combatRules != null ? _combatRules.IsAlive(actorId) : actor.GetMobaAttrs().Hp > 0f;
        }

        private static long GetAttackCooldownMs(float attackSpeed)
        {
            const long baseCooldownMs = 1500;
            var multiplier = attackSpeed <= 0f ? 1f : Math.Max(0.25f, 1f - attackSpeed / 100f);
            return Math.Max(1L, (long)Math.Round(baseCooldownMs * multiplier));
        }

        private void OnHealthCommitted(MobaHealthChangeResult result)
        {
            if (!result.Succeeded || !_structuresByActor.TryGetValue(result.TargetActorId, out var structure)) return;
            if (structure.Destroyed) return;
            var args = new MobaStructureEventArgs(structure.ActorId, structure.StructureId,
                structure.Kind, structure.TeamId, result.SourceActorId, result.AppliedValue, result.TargetHp);
            Publish(MobaStructureEvents.Damaged, in args);
            if (result.TargetHp > 0f) return;

            structure.Destroyed = true;
            Publish(MobaStructureEvents.Destroyed, in args);
        }

        private bool TryGetNextTargetInternal(int teamId, out int actorId)
        {
            actorId = 0;
            if (_towersByTeam.TryGetValue(teamId, out var towers))
            {
                for (var i = 0; i < towers.Count; i++)
                {
                    if (_structuresByActor.TryGetValue(towers[i], out var tower) && !tower.Destroyed)
                    {
                        actorId = tower.ActorId;
                        return true;
                    }
                }
            }

            if (!_baseByTeam.TryGetValue(teamId, out actorId)) return false;
            return _structuresByActor.TryGetValue(actorId, out var baseStructure) && !baseStructure.Destroyed;
        }

        private void Publish(string eventName, in MobaStructureEventArgs args)
        {
            if (_events == null) return;
            var eid = TriggeringIdUtil.GetEventEid(eventName);
            _events.Publish(new EventKey<MobaStructureEventArgs>(eid), in args);
            var objectKey = new EventKey<object>(eid);
            if (_events.HasSubscribers(objectKey))
            {
                object boxed = args;
                _events.Publish(objectKey, in boxed);
            }
        }

        public void Reset()
        {
            _healthCommittedSubscription?.Dispose();
            _healthCommittedSubscription = null;
            if (_spawn is IMobaActorSpawnTransactionService transactions)
            {
                for (var i = _spawnedStructures.Count - 1; i >= 0; i--)
                {
                    var spawned = _spawnedStructures[i];
                    transactions.Rollback(in spawned);
                }
            }
            _spawnedStructures.Clear();
            _structures.Clear();
            _structuresByActor.Clear();
            _baseByTeam.Clear();
            _towersByTeam.Clear();
            _nextAttackAtMs.Clear();
            _structureActorScratch.Clear();
        }

        public void OnDeinit(IWorldResolver services) => Reset();
        public void Dispose() => Reset();

        private sealed class StructureRuntime
        {
            public StructureRuntime(int structureId, int actorId, MobaStructureKind kind, int teamId)
            {
                StructureId = structureId; ActorId = actorId; Kind = kind; TeamId = teamId;
            }
            public int StructureId { get; }
            public int ActorId { get; }
            public MobaStructureKind Kind { get; }
            public int TeamId { get; }
            public bool Destroyed { get; set; }
        }
    }

    public readonly struct MobaStructureAttackCooldownState
    {
        public MobaStructureAttackCooldownState(int actorId, long nextAttackAtMs)
        { ActorId = actorId; NextAttackAtMs = nextAttackAtMs; }
        public int ActorId { get; }
        public long NextAttackAtMs { get; }
    }
}
