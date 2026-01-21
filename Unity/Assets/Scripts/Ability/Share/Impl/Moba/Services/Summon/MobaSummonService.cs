using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Ability.Impl.Moba.Util.Generator;
using AbilityKit.Ability.Share.Impl.Moba.Services.EntityManager;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.Share.Effect;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaSummonService : IService
    {
        private readonly IWorldServices _services;
        private readonly ActorIdAllocator _actorIds;
        private readonly MobaActorRegistry _registry;
        private readonly MobaEntityManager _entities;
        private readonly MobaActorLookupService _actors;
        private readonly MobaActorEntityGenerator _generator;
        private readonly MobaConfigDatabase _config;
        private readonly MobaComponentTemplateService _componentTemplates;
        private readonly IFrameTime _frameTime;
        private readonly IWorldClock _clock;
        private readonly IEventBus _eventBus;

        private readonly Dictionary<int, List<int>> _summonsByRootOwner = new Dictionary<int, List<int>>();

        public MobaSummonService(
            IWorldServices services,
            ActorIdAllocator actorIds,
            MobaActorRegistry registry,
            MobaEntityManager entities,
            MobaActorLookupService actors,
            MobaActorEntityGenerator generator,
            MobaConfigDatabase config,
            MobaComponentTemplateService componentTemplates,
            IEventBus eventBus)
        {
            _services = services;
            _actorIds = actorIds;
            _registry = registry;
            _entities = entities;
            _actors = actors;
            _generator = generator;
            _config = config;
            _componentTemplates = componentTemplates;
            _eventBus = eventBus;

            services?.TryGet(out _frameTime);
            services?.TryGet(out _clock);
        }

        public bool TrySummon(int casterActorId, int summonId, in Vec3 pos)
        {
            if (casterActorId <= 0) return false;
            if (summonId <= 0) return false;
            if (_actorIds == null || _registry == null || _entities == null) return false;
            if (_config == null) return false;

            if (!_config.TryGetSummon(summonId, out var summon) || summon == null) return false;

            if (!_entities.TryGetActorEntity(casterActorId, out var caster) || caster == null || !caster.hasTransform)
            {
                return false;
            }

            var spawnPos = pos.SqrMagnitude > 0f ? pos : caster.transform.Value.Position;
            var rot = caster.transform.Value.Rotation;
            var t = new Transform3(spawnPos, rot, Vec3.One);

            var actorId = _actorIds.Next();

            var team = caster.hasTeam ? caster.team.Value : Team.None;
            var ownerPlayer = caster.hasOwnerPlayerId ? caster.ownerPlayerId.Value : default(AbilityKit.Ability.Server.PlayerId);

            var unitSubType = (UnitSubType)summon.UnitSubType;
            var kind = MobaEntitySpawnFactory.CreateKindFromType(EntityMainType.Unit, unitSubType);

            var contexts = ContextsFromServices();
            var actorContext = contexts != null ? contexts.actor : null;
            if (actorContext == null) return false;

            var info = new MobaEntityInfo(
                actorId: actorId,
                kind: kind,
                transform: t,
                team: team,
                mainType: EntityMainType.Unit,
                unitSubType: unitSubType,
                ownerPlayer: ownerPlayer,
                templateId: summon.AttributeTemplateId);

            var entity = MobaEntitySpawnFactory.Create(actorContext, in info);
            if (entity == null) return false;

            var rootOwner = OwnerLinkUtil.ResolveRootOwner(caster);
            if (rootOwner <= 0) rootOwner = casterActorId;

            entity.AddOwnerLink(casterActorId, rootOwner);
            entity.AddSummonMeta(summonId, summon.DespawnOnOwnerDie);

            if (summon.LifetimeMs > 0)
            {
                var endMs = NowMs() + summon.LifetimeMs;
                entity.AddLifetime(endMs);
            }

            if (summon.ModelId > 0)
            {
                entity.AddModelId(summon.ModelId);
            }

            if (_generator != null)
            {
                _generator.InitializeFromAttributeTemplate(entity, summon.AttributeTemplateId);
            }

            TryApplyDefaultComponentTemplates(entity, summon.DefaultComponentTemplateIds);

            TryInitSkillLoadout(entity, summon.SkillIds, summon.PassiveSkillIds);

            _registry.Register(actorId, entity);
            try { _entities.TryRegisterFromEntity(entity); }
            catch { }

            TrackSummon(rootOwner, actorId, summon.MaxAlivePerOwner, summon.OverflowPolicy);

            PublishSummonEvent(MobaSummonTriggering.Events.Spawned, rootOwner, casterActorId, actorId, summonId, (int)SummonDespawnReason.None);
            PublishSummonEvent(MobaSummonTriggering.Events.SpawnedByOwner(rootOwner), rootOwner, casterActorId, actorId, summonId, (int)SummonDespawnReason.None);

            return true;
        }

        private void TryApplyDefaultComponentTemplates(global::ActorEntity entity, IReadOnlyList<int> templateIds)
        {
            if (_componentTemplates == null) return;
            if (entity == null) return;
            if (templateIds == null || templateIds.Count == 0) return;

            for (int i = 0; i < templateIds.Count; i++)
            {
                var id = templateIds[i];
                if (id <= 0) continue;
                try { _componentTemplates.TryApply(entity, id); }
                catch { }
            }
        }

        public bool TryDespawn(int summonActorId, SummonDespawnReason reason)
        {
            if (summonActorId <= 0) return false;
            if (_registry == null) return false;

            if (!_registry.TryGet(summonActorId, out var e) || e == null) return false;

            var rootOwner = 0;
            var owner = 0;
            var summonId = 0;

            if (e.hasOwnerLink && e.ownerLink != null)
            {
                owner = e.ownerLink.OwnerActorId;
                rootOwner = e.ownerLink.RootOwnerActorId;
            }
            if (rootOwner <= 0) rootOwner = owner;
            if (e.hasSummonMeta && e.summonMeta != null) summonId = e.summonMeta.SummonId;

            try { e.Destroy(); }
            catch { }

            _registry.Unregister(summonActorId);
            try { _entities?.Unregister(summonActorId); }
            catch { }

            UntrackSummon(rootOwner, summonActorId);

            if (reason == SummonDespawnReason.Killed)
            {
                PublishSummonEvent(MobaSummonTriggering.Events.Died, rootOwner, owner, summonActorId, summonId, (int)reason);
                if (rootOwner > 0)
                {
                    PublishSummonEvent(MobaSummonTriggering.Events.DiedByOwner(rootOwner), rootOwner, owner, summonActorId, summonId, (int)reason);
                }
            }

            PublishSummonEvent(MobaSummonTriggering.Events.Despawned, rootOwner, owner, summonActorId, summonId, (int)reason);
            if (rootOwner > 0)
            {
                PublishSummonEvent(MobaSummonTriggering.Events.DespawnedByOwner(rootOwner), rootOwner, owner, summonActorId, summonId, (int)reason);
            }

            return true;
        }

        private void TrackSummon(int rootOwnerActorId, int summonActorId, int maxAlivePerOwner, int overflowPolicy)
        {
            if (rootOwnerActorId <= 0) return;

            if (!_summonsByRootOwner.TryGetValue(rootOwnerActorId, out var list) || list == null)
            {
                list = new List<int>(8);
                _summonsByRootOwner[rootOwnerActorId] = list;
            }

            list.Add(summonActorId);

            if (maxAlivePerOwner <= 0) return;
            if (list.Count <= maxAlivePerOwner) return;

            var removeCount = list.Count - maxAlivePerOwner;
            for (int i = 0; i < removeCount; i++)
            {
                if (list.Count == 0) break;
                var oldest = list[0];
                list.RemoveAt(0);
                TryDespawn(oldest, SummonDespawnReason.ReplacedByLimit);
            }
        }

        private void UntrackSummon(int rootOwnerActorId, int summonActorId)
        {
            if (rootOwnerActorId <= 0) return;
            if (!_summonsByRootOwner.TryGetValue(rootOwnerActorId, out var list) || list == null) return;
            list.Remove(summonActorId);
        }

        private void PublishSummonEvent(string eventId, int rootOwnerActorId, int ownerActorId, int summonActorId, int summonId, int reason)
        {
            if (_eventBus == null) return;
            if (string.IsNullOrEmpty(eventId)) return;

            var args = PooledTriggerArgs.Rent();
            args[EffectTriggering.Args.Source] = rootOwnerActorId;
            args[EffectTriggering.Args.Target] = summonActorId;

            args[MobaSummonTriggering.Args.SummonActorId] = summonActorId;
            args[MobaSummonTriggering.Args.SummonId] = summonId;
            args[MobaSummonTriggering.Args.OwnerActorId] = ownerActorId;
            args[MobaSummonTriggering.Args.RootOwnerActorId] = rootOwnerActorId;
            args[MobaSummonTriggering.Args.Reason] = reason;

            var payload = new SummonEventPayload
            {
                SummonActorId = summonActorId,
                SummonId = summonId,
                OwnerActorId = ownerActorId,
                RootOwnerActorId = rootOwnerActorId,
                Reason = reason,
            };

            _eventBus.Publish(new TriggerEvent(eventId, payload: payload, args: args));
        }

        private void TryInitSkillLoadout(global::ActorEntity entity, IReadOnlyList<int> skillIds, IReadOnlyList<int> passiveSkillIds)
        {
            if (entity == null) return;

            var active = CreateActiveSkillRuntimes(skillIds);
            var passive = CreatePassiveSkillRuntimes(passiveSkillIds);

            if (entity.hasSkillLoadout) entity.ReplaceSkillLoadout(active, passive);
            else entity.AddSkillLoadout(active, passive);
        }

        private static AbilityKit.Ability.Impl.Moba.Conponents.ActiveSkillRuntime[] CreateActiveSkillRuntimes(IReadOnlyList<int> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0) return Array.Empty<AbilityKit.Ability.Impl.Moba.Conponents.ActiveSkillRuntime>();
            var list = new List<AbilityKit.Ability.Impl.Moba.Conponents.ActiveSkillRuntime>(skillIds.Count);
            for (int i = 0; i < skillIds.Count; i++)
            {
                var id = skillIds[i];
                if (id <= 0) continue;
                list.Add(new AbilityKit.Ability.Impl.Moba.Conponents.ActiveSkillRuntime { SkillId = id, Level = 1, CooldownEndTimeMs = 0L });
            }
            return list.Count == 0 ? Array.Empty<AbilityKit.Ability.Impl.Moba.Conponents.ActiveSkillRuntime>() : list.ToArray();
        }

        private static AbilityKit.Ability.Impl.Moba.Conponents.PassiveSkillRuntime[] CreatePassiveSkillRuntimes(IReadOnlyList<int> passiveSkillIds)
        {
            if (passiveSkillIds == null || passiveSkillIds.Count == 0) return Array.Empty<AbilityKit.Ability.Impl.Moba.Conponents.PassiveSkillRuntime>();
            var list = new List<AbilityKit.Ability.Impl.Moba.Conponents.PassiveSkillRuntime>(passiveSkillIds.Count);
            for (int i = 0; i < passiveSkillIds.Count; i++)
            {
                var id = passiveSkillIds[i];
                if (id <= 0) continue;
                list.Add(new AbilityKit.Ability.Impl.Moba.Conponents.PassiveSkillRuntime { PassiveSkillId = id, Level = 1, CooldownEndTimeMs = 0L });
            }
            return list.Count == 0 ? Array.Empty<AbilityKit.Ability.Impl.Moba.Conponents.PassiveSkillRuntime>() : list.ToArray();
        }

        private long NowMs()
        {
            if (_frameTime != null)
            {
                return (long)System.MathF.Round(_frameTime.Time * 1000f);
            }
            if (_clock != null)
            {
                return (long)System.MathF.Round(_clock.Time * 1000f);
            }
            return 0L;
        }

        private global::Contexts ContextsFromServices()
        {
            global::Contexts contexts = null;
            _services?.TryGet(out contexts);
            return contexts;
        }

        public void Dispose()
        {
            _summonsByRootOwner.Clear();
        }
    }
}
