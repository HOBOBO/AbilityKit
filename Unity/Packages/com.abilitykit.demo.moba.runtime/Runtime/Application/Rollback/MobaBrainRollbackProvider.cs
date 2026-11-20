using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// Brain 完整状态的可选回滚适配。仅恢复同会话同代存活实例，不调用正常激活或停用。
    /// v3 拒绝旧载荷；运行依赖、值覆盖和失败诊断由所属服务负责。
    /// </summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaBrainRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider,
        IRollbackSnapshotPreflightProvider, IMobaStateRecoveryProvider
    {
        /// <summary>
        /// Brain 权威状态的稳定 Provider 键。
        /// </summary>
        public const int DefaultKey = 10006;
        private readonly MobaBrainService _brains;

        /// <summary>
        /// 为同一世界的 Actor 与 Brain 服务创建可选适配。
        /// </summary>
        public MobaBrainRollbackProvider(MobaActorRegistry actors, MobaBrainService brains)
        {
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            _brains = brains ?? throw new ArgumentNullException(nameof(brains));
        }

        /// <summary>
        /// 稳定键。
        /// </summary>
        public int Key => DefaultKey;

        /// <summary>
        /// 恢复域名称。
        /// </summary>
        public string Name => "Brain";

        /// <summary>
        /// 独立备份完整状态；失败显式传播，不将不支持项写为空。
        /// </summary>
        public byte[] Export(FrameIndex frame) => MemoryPackSerializer.Serialize(_brains.CaptureRollbackState());

        /// <summary>
        /// 只读预检载荷中的目标锚点。结构恢复前允许多余无 Brain Actor 和待重建无 Brain 骨架；
        /// 独立 Import 仍要求当前完整名单相符，完整快照另与实体载荷交叉验证。
        /// </summary>
        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            var state = Read(payload);
            var anchors = new Dictionary<int, int>();
            foreach (var actor in state.Actors)
                if (actor == null || !anchors.TryAdd(actor.ActorId, actor.EntityVersion))
                    throw new InvalidOperationException("Invalid Brain actor anchors.");
            _brains.ValidateRollbackState(state, anchors, restoreFrame: frame.Value);
        }

        /// <summary>
        /// 按目标实体名单预检，允许结构恢复删除多余无运行时 Actor。
        /// 不能跨 Brain 创建、销毁或替换恢复。
        /// </summary>
        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            Dictionary<int, int> projected = null;
            if (context.TryGetPayload(MobaEntitasEntityRollbackProvider.DefaultKey, out var entities))
                projected = MobaEntitasEntityRollbackProvider.ReadSnapshotActorEntityVersions(entities.ToArray());
            _brains.ValidateRollbackState(Read(context.RequirePayload(Key).ToArray()), projected, restoreFrame: context.Frame.Value);
        }

        /// <summary>
        /// 静默恢复值与关联，不消费 Output、不结算移动或 HFSM 生命周期。
        /// </summary>
        public void Import(FrameIndex frame, byte[] payload) => _brains.RestoreRollbackState(Read(payload));

        /// <summary>
        /// 状态恢复装配的导出入口。
        /// </summary>
        public byte[] ExportState(FrameIndex frame) => Export(frame);

        /// <summary>
        /// 状态恢复装配的导入入口。
        /// </summary>
        public void ImportState(FrameIndex frame, byte[] payload) => Import(frame, payload);

        /// <summary>
        /// 对完整权威状态及执行次序求哈希；本地实例代数不参与确定性比较。
        /// </summary>
        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = MemoryPackSerializer.Serialize(_brains.CaptureRollbackState(includeLocalIdentity: false));
            hash.AddInt(Key); hash.AddInt(payload.Length);
            foreach (var value in payload) hash.AddByte(value);
        }

        private static BrainRecoveryState Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > 16 * 1024 * 1024)
                throw new InvalidOperationException("Missing or oversized Brain rollback payload.");
            var state = MemoryPackSerializer.Deserialize<BrainRecoveryState>(payload);
            if (state == null || state.Version != 3 || state.Actors == null || state.Manager == null || state.Failures == null ||
                !payload.AsSpan().SequenceEqual(MemoryPackSerializer.Serialize(state)))
                throw new InvalidOperationException("Invalid Brain rollback payload; v3 is required and legacy payloads are rejected.");
            return state;
        }
    }
}
