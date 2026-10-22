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
        private readonly MobaActorAttributeInitializer _attributes = new MobaActorAttributeInitializer();
        private readonly List<MobaActorSpawnResult> _spawnedStructures = new List<MobaActorSpawnResult>();
        private readonly Dictionary<int, StructureRuntime> _structures = new Dictionary<int, StructureRuntime>();
        private readonly Dictionary<int, StructureRuntime> _structuresByActor = new Dictionary<int, StructureRuntime>();
        private readonly Dictionary<int, int> _baseByTeam = new Dictionary<int, int>();
        private readonly Dictionary<int, List<int>> _towersByTeam = new Dictionary<int, List<int>>();
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

        private bool TryGetNextTarget(int teamId, out int actorId)
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
}
