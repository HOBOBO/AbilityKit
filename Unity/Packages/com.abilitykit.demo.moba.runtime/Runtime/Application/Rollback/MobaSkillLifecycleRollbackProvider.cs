using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Combat.Projectile;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// 技能生命周期结构的联合恢复所有者。导入前核对区域元数据和物理名单，
    /// 旧版仅上下文载荷仍按既定版本合同拒绝。
    /// </summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaSkillLifecycleRollbackProvider : IRollbackStructureRestoreProvider,
        IRollbackSnapshotPreflightProvider
    {
        public const int DefaultKey = 10011;
        private readonly MobaSkillLifecycleRollbackPreparation _lifecycle;
        private readonly Services.MobaCombatFrameGuard _frameGuard;
        internal MobaOwnerBlackboardRollbackProvider OwnerBlackboards { get; }

        public MobaSkillLifecycleRollbackProvider(IWorld world, MobaOwnerBlackboardRollbackProvider owners)
        {
            OwnerBlackboards = owners ?? throw new ArgumentNullException(nameof(owners));
            _lifecycle = new MobaSkillLifecycleRollbackPreparation(world, owners);
            world.Services.TryResolve(out _frameGuard);
        }

        public int Key => DefaultKey;
        /// <summary>
        /// 仅捕获完成帧；临时通知和失败状态不会成为可恢复的历史事件载荷。
        /// </summary>
        public byte[] Export(FrameIndex frame)
        {
            _frameGuard?.RequireCommit();
            return _lifecycle.Capture(frame);
        }
        /// <summary>
        /// 单份入口按当前名单严格校验；协调器通过完整快照入口完成联合预检，不缓存放行状态。
        /// </summary>
        public void ValidateImport(FrameIndex frame, byte[] payload) => _lifecycle.Prepare(frame, payload);
        public void Import(FrameIndex frame, byte[] payload) => _lifecycle.Commit(_lifecycle.Prepare(frame, payload));

        internal void ValidateProjectedActorRoster(RollbackSnapshotPreflightContext context, int[] actorIds)
        {
            var payload = context.RequirePayload(Key).ToArray();
            var target = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(payload);
            if (actorIds == null || target.ActorIds == null || !actorIds.AsSpan().SequenceEqual(target.ActorIds))
                throw new InvalidOperationException("Provider actor roster differs from target lifecycle roster.");
            _lifecycle.PrepareSnapshot(context, payload);
        }

        /// <summary>
        /// 完整校验生命周期及其同份目标快照引用。额外弹体仅在只读命令投影证明后接受，
        /// 区域物理与元数据仍须完全一致，任何导入发生前完成全部检查。
        /// </summary>
        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            _lifecycle.PrepareSnapshot(context, context.RequirePayload(Key).ToArray());
            var checkpoint = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(
                context.RequirePayload(Key).ToArray());
            var sections = MobaSkillLifecycleCheckpointCodec.ReadSections(checkpoint);
            var metadata = MobaSkillLifecycleCheckpointCodec.Decode<MobaAreaRuntimeRollbackCheckpoint>(sections["areas"]);
            if (metadata == null || metadata.Version != 1 || metadata.Areas == null)
                throw new InvalidOperationException("Invalid lifecycle Area checkpoint.");
            if (!context.TryGetPayload(ProjectileRollbackProvider.DefaultKey, out var physicalPayload))
            {
                if (metadata.Areas.Length != 0)
                    throw new InvalidOperationException("Area metadata has no target-frame physical provider.");
                return;
            }

            var physical = ProjectileRollbackProvider.ReadAreaSnapshot(physicalPayload.ToArray(), context.Frame);
            if (physical.Items.Length != metadata.Areas.Length)
                throw new InvalidOperationException("Target-frame Area physical and metadata membership differ.");
            var owners = new Dictionary<int, int>(physical.Items.Length);
            foreach (var item in physical.Items) owners.Add(item.Id, item.OwnerId);
            foreach (var area in metadata.Areas)
                if (area == null || !owners.TryGetValue(area.AreaId, out var owner) ||
                    owner != area.OwnerActorId || !owners.Remove(area.AreaId))
                    throw new InvalidOperationException("Target-frame Area physical identity or owner differs from metadata.");
        }
    }
}
