using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Logging;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Trace;

namespace AbilityKit.Demo.Moba.Systems.EntityManager
{
    [WorldSystem(order: MobaSystemOrder.ActorDespawnCleanup, Phase = WorldSystemPhase.PostExecute)]
    public sealed class MobaActorDespawnCleanupSystem : WorldSystemBase
    {
        private MobaAuthorityFrameService _authority;
        private IFrameTime _time;
        private MobaActorRegistry _registry;
        private MobaEntityManager _entities;
        private MobaActorDespawnSnapshotService _despawnSnapshots;
        private MobaProjectileLinkService _projectileLinks;
        private MobaSkillCastRuntimeService _skillRuntimes;
        private MobaTraceRegistry _trace;
        private MobaSummonService _summons;
        private SkillCastCoordinator _skills;
        private MobaSkillLoadoutService _skillLoadouts;
        private MobaSkillParamModifierService _skillModifiers;
        private MobaEffectiveTagQueryService _effectiveTags;
        private MobaCombatActivityService _combatActivity;
        private MobaShieldService _shields;

        private global::Entitas.IGroup<global::ActorEntity> _group;

        public MobaActorDespawnCleanupSystem(global::Entitas.IContexts contexts, IWorldResolver services)
            : base(contexts, services)
        {
        }

        protected override void OnInit()
        {
            Services.TryResolve(out _authority);
            Services.TryResolve(out _time);
            Services.TryResolve(out _registry);
            Services.TryResolve(out _entities);
            Services.TryResolve(out _despawnSnapshots);
            Services.TryResolve(out _projectileLinks);
            Services.TryResolve(out _skillRuntimes);
            Services.TryResolve(out _trace);
            Services.TryResolve(out _summons);
            Services.TryResolve(out _skills);
            Services.TryResolve(out _skillLoadouts);
            Services.TryResolve(out _skillModifiers);
            Services.TryResolve(out _effectiveTags);
            Services.TryResolve(out _combatActivity);
            Services.TryResolve(out _shields);
            _group = Contexts.Actor().GetGroup(ActorMatcher.ActorDespawnRequest);
        }

        protected override void OnExecute()
        {
            if (_group == null) return;

            if (!TryGetConfirmedFrame(out var confirmed))
            {
                throw new System.InvalidOperationException("MobaActorDespawnCleanupSystem requires authority or frame time before cleaning actor despawn requests.");
            }

            const int maxPasses = 8;

            for (int pass = 0; pass < maxPasses; pass++)
            {
                var processed = DrainReadyRequests(confirmed);
                if (processed == 0) break;
            }
        }

        private int DrainReadyRequests(int confirmed)
        {
            var entities = _group.GetEntities();
            if (entities == null || entities.Length == 0) return 0;

            var processed = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == null || !entity.hasActorDespawnRequest) continue;

                var request = entity.actorDespawnRequest;
                if (confirmed < request.MinConfirmedFrame) continue;

                entity.RemoveActorDespawnRequest();
                CleanupActor(entity, request);
                processed++;
            }

            return processed;
        }

        private void CleanupActor(global::ActorEntity entity, ActorDespawnRequestComponent request)
        {
            var actorId = entity.hasActorId ? entity.actorId.Value : 0;
            if (actorId <= 0)
            {
                TryDestroy(entity, 0, request.Reason);
                return;
            }

            if (entity.hasSummonMeta && _summons != null)
            {
                if (_summons.ExecuteRequestedDespawn(actorId, ToSummonReason(request.Reason))) return;
            }

            CleanupProjectile(actorId, request);
            ReclaimActorState(actorId);

            _despawnSnapshots?.Enqueue(actorId, (byte)request.Reason);
            new MobaActorSpawnRegistrar(_registry, _entities).Unregister(
                actorId,
                out _,
                publishDespawn: true);
            TryDestroy(entity, actorId, request.Reason);
        }

        private void ReclaimActorState(int actorId)
        {
            _skills?.RemoveActor(actorId);
            _skillLoadouts?.RemoveActor(actorId);
            _skillModifiers?.ClearActor(actorId);
            _effectiveTags?.RemoveActor(actorId);
            _combatActivity?.RemoveActor(actorId);
            _shields?.RemoveActor(actorId);
        }

        private void CleanupProjectile(int actorId, ActorDespawnRequestComponent request)
        {
            if (_projectileLinks == null) return;
            if (!_projectileLinks.TryGetProjectileId(actorId, out var projectileId)) return;

            EndProjectileTrace(projectileId, request.Reason);
            ReleaseSkillRuntime(projectileId);
            _projectileLinks.UnlinkByActorId(actorId);
        }

        private void EndProjectileTrace(ProjectileId projectileId, ActorDespawnReason reason)
        {
            if (_trace == null || _projectileLinks == null) return;
            if (!_projectileLinks.TryGetSource(projectileId, out var source)) return;
            if (source.SourceContextId == 0L) return;

            try
            {
                _trace.EndContext(source.SourceContextId, ToTraceReason(reason));
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, $"[MobaActorDespawnCleanupSystem] End projectile trace failed (projectileId={projectileId.Value}, sourceContextId={source.SourceContextId})");
            }
        }

        private void ReleaseSkillRuntime(ProjectileId projectileId)
        {
            if (_skillRuntimes == null || _projectileLinks == null) return;
            if (!_projectileLinks.TryConsumeRetain(projectileId, out var retainHandle)) return;

            try
            {
                _skillRuntimes.ReleaseChild(in retainHandle);
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, $"[MobaActorDespawnCleanupSystem] Release skill runtime retain failed (projectileId={projectileId.Value})");
            }
        }

        private void TryDestroy(global::ActorEntity entity, int actorId, ActorDespawnReason reason)
        {
            try
            {
                if (entity.hasActorStateMachine)
                {
                    entity.RemoveActorStateMachine();
                }

                entity.Destroy();
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, $"[MobaActorDespawnCleanupSystem] destroy actor failed (actorId={actorId}, reason={reason})");
            }
        }

        private bool TryGetConfirmedFrame(out int frame)
        {
            frame = 0;
            try
            {
                if (_authority != null)
                {
                    frame = _authority.ConfirmedFrame.Value;
                    return true;
                }

                if (_time != null)
                {
                    frame = _time.Frame.Value;
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Log.Exception(ex, "[MobaActorDespawnCleanupSystem] resolve confirmed frame failed");
                return false;
            }

            return false;
        }

        private static AbilityKit.Demo.Moba.Events.Summon.SummonDespawnReason ToSummonReason(ActorDespawnReason reason)
        {
            switch (reason)
            {
                case ActorDespawnReason.SummonTimeout:
                    return AbilityKit.Demo.Moba.Events.Summon.SummonDespawnReason.Timeout;
                case ActorDespawnReason.SummonOwnerDead:
                    return AbilityKit.Demo.Moba.Events.Summon.SummonDespawnReason.OwnerDead;
                case ActorDespawnReason.SummonReplacedByLimit:
                    return AbilityKit.Demo.Moba.Events.Summon.SummonDespawnReason.ReplacedByLimit;
                case ActorDespawnReason.SummonManualRemove:
                    return AbilityKit.Demo.Moba.Events.Summon.SummonDespawnReason.ManualRemove;
                case ActorDespawnReason.SummonKilled:
                    return AbilityKit.Demo.Moba.Events.Summon.SummonDespawnReason.Killed;
                case ActorDespawnReason.SceneCleanup:
                    return AbilityKit.Demo.Moba.Events.Summon.SummonDespawnReason.SceneCleanup;
                default:
                    return AbilityKit.Demo.Moba.Events.Summon.SummonDespawnReason.None;
            }
        }

        private static TraceLifecycleReason ToTraceReason(ActorDespawnReason reason)
        {
            switch (reason)
            {
                case ActorDespawnReason.RollbackCleanup:
                    return TraceLifecycleReason.Cancelled;
                default:
                    return TraceLifecycleReason.Completed;
            }
        }
    }
}
