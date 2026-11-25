using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Services.Projection;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;

namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 帧末完整头顶状态投影；基线从当前世界重建，不保存事件累加结果。
    /// </summary>
    [MobaSnapshotEmitter(87)]
    [WorldService(typeof(MobaActorIndicatorSnapshotService))]
    public sealed class MobaActorIndicatorSnapshotService : IService, IMobaSnapshotEmitter, IActiveBaselineSnapshotProvider
    {
        private readonly MobaLogicWorldRunGateService _phase;
        private readonly MobaActorRegistry _actors;
        private readonly MobaActorIndicatorProjector _projector;
        private readonly List<MobaActorIndicatorSnapshotEntry> _entries = new List<MobaActorIndicatorSnapshotEntry>(128);
        private int _lastFrame = int.MinValue;
        private bool _baselineRequested;

        /// <summary>
        /// 服务只读取本世界权威状态。
        /// </summary>
        public MobaActorIndicatorSnapshotService(MobaLogicWorldRunGateService phase, MobaActorRegistry actors,
            MobaConfigDatabase config, MobaGameplayVariableService variables, MobaCharacterHfsmParameterService parameters)
        {
            _phase = phase; _actors = actors;
            _projector = new MobaActorIndicatorProjector(config, variables, parameters);
        }

        /// <summary>
        /// 每个正常输出帧最多发布一次，合法空集仍发布以清理旧显示。
        /// </summary>
        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            if (!_phase.InGame || _lastFrame == frame.Value) return false;
            _lastFrame = frame.Value;
            return TryGetActiveBaseline(out snapshot);
        }

        /// <summary>
        /// 恢复、晚绑定和显式刷新复用同一完整投影。
        /// </summary>
        public bool TryGetActiveBaseline(out WorldStateSnapshot snapshot)
        {
            snapshot = default;
            if (!_phase.InGame) return false;
            _entries.Clear();
            foreach (var id in _actors.CopyActorIdsInOrder())
                if (_actors.TryGet(id, out var actor)) _projector.Append(id, actor, _entries);
            snapshot = new WorldStateSnapshot(MobaOpCodes.Snapshot.ActorIndicatorState,
                MobaActorIndicatorSnapshotCodec.Serialize(_entries.ToArray()));
            return true;
        }

        /// <summary>
        /// 请求由既有快照路由发布基线。
        /// </summary>
        public void RequestActiveBaseline() => _baselineRequested = true;

        /// <summary>
        /// 原子消费本服务的基线请求标志。
        /// </summary>
        public bool ConsumeActiveBaselineRequest()
        { var requested = _baselineRequested; _baselineRequested = false; return requested; }

        /// <summary>
        /// 清除缓存，不触碰玩法状态。
        /// </summary>
        public void Dispose() { _entries.Clear(); _lastFrame = int.MinValue; _baselineRequested = false; }
    }
}
