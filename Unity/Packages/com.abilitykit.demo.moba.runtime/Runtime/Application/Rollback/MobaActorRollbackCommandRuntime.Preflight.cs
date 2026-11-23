using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.EntityConstruction;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// 在隔离名单中验证既有 Actor 逆命令的结果，不创建、销毁或恢复任何业务对象。
    /// 仅验证受支持的弹体、Launcher 与召唤物在各自恢复边界内的名单差异。
    /// </summary>
    public sealed partial class MobaActorRollbackCommandRuntime
    {
        internal HashSet<int> ProjectLiveSummonDiscards(RollbackSnapshotPreflightContext context,
            IReadOnlyDictionary<int, int> targetVersions, MobaSummonService summons)
        {
            var candidates = FindLiveSummonDiscards(targetVersions.ContainsKey);
            if (candidates.Count == 0) return candidates;

            context.RequirePayload(MobaBrainRollbackProvider.DefaultKey);
            var order = new List<int>(context.StructureRestoreKeys);
            var entityAt = order.IndexOf(MobaEntitasEntityRollbackProvider.DefaultKey);
            var journalAt = order.IndexOf(RollbackProviderKey);
            if (entityAt < 0 || journalAt <= entityAt || order.IndexOf(MobaSkillLifecycleRollbackProvider.DefaultKey) <= journalAt ||
                !context.Providers.TryGetValue(RollbackProviderKey, out var provider) || !ReferenceEquals(provider, StateProvider))
                throw new InvalidOperationException("Live summon rollback requires the world's ordered actor journal and lifecycle providers.");

            var commandPayload = context.RequirePayload(RollbackProviderKey).ToArray();
            StateProvider.ValidateImport(context.Frame, commandPayload);
            var checkpoint = CommandJournalCheckpointCodec.Decode(commandPayload);
            var target = MobaSkillLifecycleSnapshotIndex.Read(context.Frame,
                context.RequirePayload(MobaSkillLifecycleRollbackProvider.DefaultKey).ToArray());
            ValidateLiveSummonBirths(context.Frame, candidates, summons, checkpoint.NextOrder, target);
            return candidates;
        }

        internal HashSet<int> FindLiveSummonDiscards(Func<int, bool> targetContains)
        {
            var candidates = new HashSet<int>();
            if (!RetainsNaturalExpiry) return candidates;
            foreach (var actor in _context.GetEntities())
            {
                if (!actor.hasActorId || !actor.hasSummonMeta || actor.isFlyingProjectileTag || actor.hasProjectileLauncher)
                    continue;
                var id = actor.actorId.Value;
                if (targetContains(id)) continue;
                if (!_actors.TryGet(id, out var registered) || !ReferenceEquals(registered, actor) || !candidates.Add(id))
                    throw new InvalidOperationException($"Live summon {id} does not match its unique registered entity.");
            }
            return candidates;
        }

        internal void ValidateLiveSummonBirths(FrameIndex frame, HashSet<int> candidates,
            MobaSummonService summons, long? checkpointOrder = null, MobaSkillLifecycleSnapshotIndex target = null)
        {
            if (candidates.Count == 0) return;
            if (summons == null) throw new InvalidOperationException("Live summon rollback requires the summon source owner.");
            var births = new Dictionary<int, RollbackCommandRecord>();
            for (var index = Log.Count - 1; index >= 0; index--)
            {
                var record = Log.GetRecord(index);
                if (checkpointOrder.HasValue && record.Order < checkpointOrder.Value) break;
                if (!checkpointOrder.HasValue && record.Frame.Value <= frame.Value) continue;
                if (record.CommandType == RestoreDestroyedActorCommand &&
                    candidates.Contains(ReadTombstone(record.PayloadVersion, record.Payload).ActorId))
                    throw new InvalidOperationException("A live summon cannot be discarded across a recorded destruction of that identity.");
                if (record.CommandType != DestroyCreatedActorCommand) continue;
                var id = ReadId(record.PayloadVersion, record.Payload).ActorId;
                if (candidates.Contains(id) && (record.Frame.Value <= frame.Value || !births.TryAdd(id, record)))
                    throw new InvalidOperationException($"Live summon {id} does not have a unique birth after the completed target frame.");
            }

            foreach (var id in candidates)
            {
                ValidateRegistrationConsistency(id);
                if (!births.TryGetValue(id, out var birth) || !_actors.TryGet(id, out var actor) ||
                    !actor.hasActorId || actor.actorId.Value != id || !actor.hasSummonMeta ||
                    !_actors.MatchesSummonBirth(id, actor, Log.Epoch, birth.Order) || !_actorKinds.TryGetValue(id, out var kind) ||
                    kind != MobaEntityKind.Summon || ResolveKind(actor) != MobaEntityKind.Summon)
                    throw new InvalidOperationException($"Live summon {id} is missing its rollback birth command or tracked identity.");
                var sourceId = summons.ValidateLiveRollbackBirth(actor, birth.Frame.Value);
                _expiryBrains.ValidateCreatedActorBrainRollback(actor, retained: false);
                if (target != null && (target.ActorIds.Contains(id) || target.Contexts.ContainsKey(sourceId) || Array.Exists(target.Retains, retain =>
                        retain.ChildContextId == sourceId || retain.ChildKind == (int)MobaSkillRuntimeChildKind.Summon && retain.ChildId == id)))
                    throw new InvalidOperationException($"Target lifecycle still owns live summon {id} or its birth source.");
            }
        }

        internal void ValidateSupportedActorRosterProjection(FrameIndex frame, byte[] commandPayload,
            int[] targetActorIds, IReadOnlyDictionary<int, int> targetVersions,
            ISet<int> targetProjectileIds, ISet<int> targetLauncherIds, ISet<int> targetRetainedIds = null,
            ISet<int> liveSummonDiscards = null)
        {
            StateProvider.ValidateImport(frame, commandPayload);
            var checkpoint = CommandJournalCheckpointCodec.Decode(commandPayload);
            var target = new HashSet<int>(targetActorIds);
            var restoredTargetActors = new HashSet<int>();
            foreach (var id in targetActorIds)
            {
                ValidateRegistrationConsistency(id);
                if (!targetVersions.TryGetValue(id, out var version))
                    throw new InvalidOperationException("Lifecycle target actor entity version is missing.");
                if (_actors.TryGet(id, out _))
                {
                    if (!_actors.MatchesEntityVersion(id, version))
                        throw new InvalidOperationException("Lifecycle target actor entity identity changed.");
                }
                else if (!targetProjectileIds.Contains(id) && !targetLauncherIds.Contains(id) && targetRetainedIds?.Contains(id) != true)
                    throw new InvalidOperationException("Lifecycle target actor is missing and is not a supported flying projectile or ProjectileLauncher or retained summon.");
            }

            var projected = new HashSet<int>(_actors.CopyActorIdsInOrder());
            foreach (var id in projected)
            {
                ValidateRegistrationConsistency(id);
                if (target.Contains(id) || liveSummonDiscards?.Contains(id) == true) continue;
                if (!_actorKinds.TryGetValue(id, out var kind) || kind != MobaEntityKind.Projectile ||
                    !_actors.TryGet(id, out var actor) || !actor.isFlyingProjectileTag ||
                    ResolveKind(actor) != MobaEntityKind.Projectile)
                    throw new InvalidOperationException("Lifecycle extra actor is not a tracked flying projectile.");
            }

            /*
             * 仅目标弹体与 Launcher 可以重建，目标名单外的对象仍限定为飞行弹体。
             */
            for (var i = Log.Count - 1; i >= 0; i--)
            {
                var record = Log.GetRecord(i);
                if (record.Order < checkpoint.NextOrder) break;
                if (record.Frame.Value <= frame.Value)
                    throw new InvalidOperationException("Lifecycle actor command is not after the completed target frame.");
                if (record.CommandType == DestroyCreatedActorCommand)
                {
                    var id = ReadId(record.PayloadVersion, record.Payload).ActorId;
                    if (target.Contains(id) || !projected.Remove(id))
                        throw new InvalidOperationException("Lifecycle actor creation command does not match the projected roster.");
                }
                else if (record.CommandType == RestoreDestroyedActorCommand)
                {
                    var value = ReadTombstone(record.PayloadVersion, record.Payload);
                    var isTargetProjectile = target.Contains(value.ActorId) && targetProjectileIds.Contains(value.ActorId);
                    var isTargetLauncher = target.Contains(value.ActorId) && targetLauncherIds.Contains(value.ActorId);
                    var isRetained = targetRetainedIds?.Contains(value.ActorId) == true;
                    var isTargetActor = target.Contains(value.ActorId);
                    var expectedKind = isRetained ? MobaEntityKind.Summon :
                        isTargetLauncher ? MobaEntityKind.ProjectileLauncher : MobaEntityKind.Projectile;
                    var added = projected.Add(value.ActorId);
                    if (value.Kind != expectedKind || isTargetActor && !isTargetProjectile && !isTargetLauncher && !isRetained ||
                        (isTargetProjectile || isTargetLauncher || isRetained) &&
                            (!targetVersions.TryGetValue(value.ActorId, out var expectedVersion) ||
                             expectedVersion != value.EntityVersion || !restoredTargetActors.Add(value.ActorId)) ||
                        !added)
                        throw new InvalidOperationException("Lifecycle actor destruction command exceeds supported projectile/launcher projection scope.");
                }
                else throw new InvalidOperationException("Unsupported lifecycle actor projection command.");
            }

            var ordered = new List<int>(projected);
            ordered.Sort();
            if (ordered.Count != targetActorIds.Length)
                throw new InvalidOperationException("Actor commands do not restore the target lifecycle roster.");
            for (var i = 0; i < ordered.Count; i++)
                if (ordered[i] != targetActorIds[i])
                    throw new InvalidOperationException("Actor commands do not restore the ordered target lifecycle roster.");
            foreach (var id in targetProjectileIds)
                if (!restoredTargetActors.Contains(id) && !_actors.TryGet(id, out _))
                    throw new InvalidOperationException("Lifecycle missing projectile has no matching destruction tombstone.");
            foreach (var id in targetLauncherIds)
                if (!restoredTargetActors.Contains(id) && !_actors.TryGet(id, out _))
                    throw new InvalidOperationException("Lifecycle missing ProjectileLauncher has no matching destruction tombstone.");
            if (targetRetainedIds != null)
                foreach (var id in targetRetainedIds)
                    if (!restoredTargetActors.Contains(id))
                        throw new InvalidOperationException("Retained summon has no matching destruction tombstone.");
        }
    }
}
