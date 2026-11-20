using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services.EntityConstruction;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// 在隔离名单中验证既有 Actor 逆命令的结果，不创建、销毁或恢复任何业务对象。
    /// 仅证明差异来自检查点之后新增或已销毁的受支持弹体或 Launcher。
    /// </summary>
    public sealed partial class MobaActorRollbackCommandRuntime
    {
        internal void ValidateSupportedActorRosterProjection(FrameIndex frame, byte[] commandPayload,
            int[] targetActorIds, IReadOnlyDictionary<int, int> targetVersions,
            ISet<int> targetProjectileIds, ISet<int> targetLauncherIds, ISet<int> targetRetainedIds = null)
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
                if (target.Contains(id)) continue;
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
