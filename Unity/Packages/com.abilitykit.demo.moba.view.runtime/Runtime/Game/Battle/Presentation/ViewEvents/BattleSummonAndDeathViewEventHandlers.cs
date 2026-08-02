using System;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow.Battle.View;
using UnityEngine;
using EC = AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow.Battle.ViewEvents
{
    /// <summary>
    /// Handles summon-related view events: spawn, die, despawn.
    /// Plays a VFX at the summon position for each lifecycle event.
    /// </summary>
    internal sealed class BattleSummonViewEventHandler
    {
        private readonly IBattleEntityQuery _query;
        private readonly BattleVfxManager _vfx;
        private readonly BattleSummonVfxResolver _vfxs;
        private readonly EC.IEntity _vfxNode;

        public BattleSummonViewEventHandler(
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode,
            BattleSummonVfxResolver vfxs = null)
        {
            _query = query;
            _vfx = vfx;
            _vfxNode = vfxNode;
            _vfxs = vfxs ?? new BattleSummonVfxResolver();
        }

        public void Handle(string eventId, in DemoMobaSummonEventPayload payload)
        {
            if (payload.SummonActorId <= 0) return;

            if (!TryResolvePosition(payload.SummonActorId, out var position))
            {
                // Fallback to owner position if summon position not yet available.
                TryResolvePosition(payload.OwnerActorId, out position);
            }

            if (eventId == MobaSummonTriggering.Events.Spawned || eventId.StartsWith("summon.spawn"))
            {
                OnSpawn(in payload, in position);
            }
            else if (eventId == MobaSummonTriggering.Events.Died || eventId.StartsWith("summon.die"))
            {
                OnDie(in payload, in position);
            }
            else if (eventId == MobaSummonTriggering.Events.Despawned || eventId.StartsWith("summon.despawn"))
            {
                OnDespawn(in payload, in position);
            }
        }

        private void OnSpawn(in DemoMobaSummonEventPayload payload, in Vector3 position)
        {
            if (_vfx == null || !_vfx.CanSpawn) return;

            var vfxId = _vfxs.ResolveSpawnVfxId(payload.SummonId);
            if (vfxId <= 0) return;

            _vfx.TryCreateAoeVfx(in _vfxNode, vfxId, in position, Quaternion.identity, durationMsOverride: 2000);
            Log.Info($"[SummonView] Spawn VFX {vfxId} at ({position.x:F1}, {position.z:F1}) for SummonId={payload.SummonId}");
        }

        private void OnDie(in DemoMobaSummonEventPayload payload, in Vector3 position)
        {
            if (_vfx == null || !_vfx.CanSpawn) return;

            var vfxId = _vfxs.ResolveDeathVfxId(payload.SummonId);
            if (vfxId <= 0) return;

            _vfx.TryCreateAoeVfx(in _vfxNode, vfxId, in position, Quaternion.identity, durationMsOverride: 1500);
            Log.Info($"[SummonView] Death VFX {vfxId} at ({position.x:F1}, {position.z:F1}) for SummonId={payload.SummonId}, Reason={payload.Reason}");
        }

        private void OnDespawn(in DemoMobaSummonEventPayload payload, in Vector3 position)
        {
            if (_vfx == null || !_vfx.CanSpawn) return;

            var vfxId = _vfxs.ResolveDespawnVfxId(payload.SummonId);
            if (vfxId <= 0) return;

            _vfx.TryCreateAoeVfx(in _vfxNode, vfxId, in position, Quaternion.identity, durationMsOverride: 1000);
            Log.Info($"[SummonView] Despawn VFX {vfxId} at ({position.x:F1}, {position.z:F1}) for SummonId={payload.SummonId}, Reason={payload.Reason}");
        }

        private bool TryResolvePosition(int actorId, out Vector3 position)
        {
            position = default;
            if (actorId <= 0 || _query == null) return false;

            var netId = new BattleNetId(actorId);
            if (_query.TryGetTransform(netId, out var tr) && tr != null)
            {
                position = tr.Position;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Resolves VFX IDs for summon lifecycle events.
    /// Currently returns placeholder IDs; wire to a summon-config-driven resolver once
    /// <c>SummonMO</c> has VFX fields.
    /// </summary>
    internal sealed class BattleSummonVfxResolver
    {
        // Placeholder VFX IDs for summon events. Wire to configs when available.
        private const int DefaultSpawnVfx = BattleViewPlaceholderIds.SummonSpawnVfx;
        private const int DefaultDeathVfx = BattleViewPlaceholderIds.ActorDeathVfx;
        private const int DefaultDespawnVfx = BattleViewPlaceholderIds.SummonDespawnVfx;

        public int ResolveSpawnVfxId(int summonId)
        {
            // TODO: resolve from summon config: configs.TryGetSummon(summonId, out var s) → s.SpawnVfxId
            return summonId > 0
                ? BattleViewFallbackPolicy.DevelopmentOnly(DefaultSpawnVfx, "summon.spawn:" + summonId)
                : 0;
        }

        public int ResolveDeathVfxId(int summonId)
        {
            // TODO: resolve from summon config
            return summonId > 0
                ? BattleViewFallbackPolicy.DevelopmentOnly(DefaultDeathVfx, "summon.death:" + summonId)
                : 0;
        }

        public int ResolveDespawnVfxId(int summonId)
        {
            // TODO: resolve from summon config
            return summonId > 0
                ? BattleViewFallbackPolicy.DevelopmentOnly(DefaultDespawnVfx, "summon.despawn:" + summonId)
                : 0;
        }
    }

    /// <summary>
    /// Handles actor death view events: plays a death VFX at the actor's last known position.
    /// Death events arrive via <see cref="IBattleViewEventSink.OnActorDeathEvent"/>.
    /// </summary>
    internal sealed class BattleActorDeathViewEventHandler
    {
        private readonly IBattleEntityQuery _query;
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;

        public BattleActorDeathViewEventHandler(
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            _query = query;
            _vfx = vfx;
            _vfxNode = vfxNode;
        }

        /// <summary>
        /// Called when an actor dies. Plays death VFX at the actor's last position.
        /// </summary>
        public void Handle(int actorId, int entityCode = 0)
        {
            if (actorId <= 0) return;
            if (_vfx == null || !_vfx.CanSpawn) return;

            if (!TryResolvePosition(actorId, out var position))
            {
                Log.Warning($"[DeathView] Cannot resolve position for actor {actorId}; using origin.");
                position = Vector3.zero;
            }

            // Resolve death VFX ID from entity config.
            var vfxId = ResolveDeathVfxId(entityCode);
            if (vfxId <= 0)
            {
                vfxId = BattleViewFallbackPolicy.DevelopmentOnly(
                    BattleViewPlaceholderIds.ActorDeathVfx,
                    "actor.death:" + entityCode);
            }

            _vfx.TryCreateAoeVfx(in _vfxNode, vfxId, in position, Quaternion.identity, durationMsOverride: 2000);
            Log.Info($"[DeathView] Death VFX {vfxId} at ({position.x:F1}, {position.y:F1}, {position.z:F1}) for ActorId={actorId}");
        }

        private bool TryResolvePosition(int actorId, out Vector3 position)
        {
            position = default;
            if (actorId <= 0 || _query == null) return false;

            var netId = new BattleNetId(actorId);
            if (_query.TryGetTransform(netId, out var tr) && tr != null)
            {
                position = tr.Position;
                return true;
            }
            return false;
        }

        private int ResolveDeathVfxId(int entityCode)
        {
            // TODO: resolve from character / entity config
            // configs.TryGetCharacter(entityCode, out var c) → c.DeathVfxId
            return entityCode > 0
                ? BattleViewFallbackPolicy.DevelopmentOnly(
                    BattleViewPlaceholderIds.ActorDeathVfx,
                    "actor.death:" + entityCode)
                : 0;
        }
    }

    /// <summary>
    /// Handles actor respawn view events: plays a respawn VFX at the respawn position.
    /// </summary>
    internal sealed class BattleActorRespawnViewEventHandler
    {
        private readonly IBattleEntityQuery _query;
        private readonly BattleVfxManager _vfx;
        private readonly EC.IEntity _vfxNode;

        public BattleActorRespawnViewEventHandler(
            IBattleEntityQuery query,
            BattleVfxManager vfx,
            in EC.IEntity vfxNode)
        {
            _query = query;
            _vfx = vfx;
            _vfxNode = vfxNode;
        }

        /// <summary>
        /// Called when an actor respawns. Plays respawn VFX at the actor's current position.
        /// </summary>
        public void Handle(int actorId, int entityCode = 0)
        {
            if (actorId <= 0) return;
            if (_vfx == null || !_vfx.CanSpawn) return;

            if (!TryResolvePosition(actorId, out var position))
            {
                Log.Warning($"[RespawnView] Cannot resolve position for actor {actorId}");
                return;
            }

            var vfxId = ResolveRespawnVfxId(entityCode);
            if (vfxId <= 0)
            {
                vfxId = BattleViewFallbackPolicy.DevelopmentOnly(
                    BattleViewPlaceholderIds.ActorRespawnVfx,
                    "actor.respawn:" + entityCode);
            }

            _vfx.TryCreateAoeVfx(in _vfxNode, vfxId, in position, Quaternion.identity, durationMsOverride: 1500);
            Log.Info($"[RespawnView] Respawn VFX {vfxId} at ({position.x:F1}, {position.y:F1}, {position.z:F1}) for ActorId={actorId}");
        }

        private bool TryResolvePosition(int actorId, out Vector3 position)
        {
            position = default;
            if (actorId <= 0 || _query == null) return false;

            var netId = new BattleNetId(actorId);
            if (_query.TryGetTransform(netId, out var tr) && tr != null)
            {
                position = tr.Position;
                return true;
            }
            return false;
        }

        private int ResolveRespawnVfxId(int entityCode)
        {
            // TODO: resolve from character config
            return entityCode > 0
                ? BattleViewFallbackPolicy.DevelopmentOnly(
                    BattleViewPlaceholderIds.ActorRespawnVfx,
                    "actor.respawn:" + entityCode)
                : 0;
        }
    }
}
