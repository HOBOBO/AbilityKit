using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services.EntityConstruction;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// 在隔离名单中验证既有 Actor 逆命令的结果，不创建、销毁或恢复任何业务对象。
    /// 仅证明差异来自检查点之后新增或已销毁的飞行弹体。
    /// </summary>
    public sealed partial class MobaActorRollbackCommandRuntime
    {
        internal void ValidateProjectileRosterProjection(FrameIndex frame, byte[] commandPayload,
            int[] targetActorIds, IReadOnlyDictionary<int, int> targetVersions,
            ISet<int> targetProjectileIds)
        {
            StateProvider.ValidateImport(frame, commandPayload);
            var checkpoint = CommandJournalCheckpointCodec.Decode(commandPayload);
            var target = new HashSet<int>(targetActorIds);
            var restoredTargetProjectiles = new HashSet<int>();
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
                else if (!targetProjectileIds.Contains(id))
                    throw new InvalidOperationException("Lifecycle target actor is missing and is not a flying projectile.");
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
             * 按真实日志逆序只推导存在性；任何目标对象重建或其它类型的差异都不在本批范围内。
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
                    var targetProjectile = target.Contains(value.ActorId);
                    if (value.Kind != MobaEntityKind.Projectile ||
                        targetProjectile && (!targetProjectileIds.Contains(value.ActorId) ||
                            !targetVersions.TryGetValue(value.ActorId, out var expectedVersion) ||
                            expectedVersion != value.EntityVersion || !restoredTargetProjectiles.Add(value.ActorId)) ||
                        !projected.Add(value.ActorId))
                        throw new InvalidOperationException("Lifecycle actor destruction command exceeds projectile projection scope.");
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
                if (!restoredTargetProjectiles.Contains(id) && !_actors.TryGet(id, out _))
                    throw new InvalidOperationException("Lifecycle missing projectile has no matching destruction tombstone.");
        }
    }
}
