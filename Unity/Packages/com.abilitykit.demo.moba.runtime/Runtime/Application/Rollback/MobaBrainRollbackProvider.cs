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
    public sealed class MobaBrainRollbackProvider : IRollbackStateProvider,
        IRollbackStatePreflightProvider, IRollbackSnapshotPreflightProvider, IMobaStateRecoveryProvider
    {
        /// <summary>
        /// Brain 权威状态的稳定 Provider 键。
        /// </summary>
        public const int DefaultKey = 10006;
        private readonly MobaBrainService _brains;
        private readonly MobaActorRollbackCommandRuntime _commands;
        private readonly MobaSummonService _summons;

        /// <summary>
        /// 为同一世界的 Actor 与 Brain 服务创建可选适配。
        /// </summary>
        public MobaBrainRollbackProvider(MobaActorRegistry actors, MobaBrainService brains)
            : this(actors, brains, null, null) { }

        internal MobaBrainRollbackProvider(MobaActorRegistry actors, MobaBrainService brains,
            MobaActorRollbackCommandRuntime commands, MobaSummonService summons)
        {
            if (actors == null) throw new ArgumentNullException(nameof(actors));
            _brains = brains ?? throw new ArgumentNullException(nameof(brains));
            _commands = commands;
            _summons = summons;
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
        /// 单份预检不推定任何活体 Brain 会被 Actor 命令撤销。
        /// 允许既有的无 Brain 骨架差异；实际导入仍要求当前完整名单相符。
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

        void IRollbackStatePreflightProvider.ValidateImport(FrameIndex frame, byte[] payload)
        {
            /*
             * 联合装配已在整份快照中验证结构投影；独立装配保留单份严格预检。
             * 实际导入在结构恢复后重新验证，不保留跨调用的投影结果。
             */
            if (_commands == null) ValidateImport(frame, payload);
            else Read(payload);
        }

        /// <summary>
        /// 完整快照预检以实际 Actor 命令检查点证明可撤销的活体召唤。
        /// 其余 Brain 必须继续满足同实例恢复，独立导入不接受结构投影。
        /// </summary>
        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            Dictionary<int, int> projected = null;
            if (context.TryGetPayload(MobaEntitasEntityRollbackProvider.DefaultKey, out var entities))
                projected = MobaEntitasEntityRollbackProvider.ReadSnapshotActorEntityVersions(entities.ToArray());
            var discards = projected == null ? null : _commands?.ProjectLiveSummonDiscards(context, projected, _summons);
            _brains.ValidateRollbackState(Read(context.RequirePayload(Key).ToArray()), projected,
                restoreFrame: context.Frame.Value, discardActors: discards);
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
