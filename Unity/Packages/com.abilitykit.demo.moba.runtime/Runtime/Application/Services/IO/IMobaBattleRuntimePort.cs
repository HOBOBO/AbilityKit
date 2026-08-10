using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.World.Services;
using AbilityKit.Game.Battle;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(IMobaBattleRuntimePort))]
    [WorldService(typeof(MobaBattleRuntimePort))]
    public sealed class MobaBattleRuntimePort : IService, IMobaBattleRuntimePort, IBattleRuntimeStatusProvider
    {
        private readonly IMobaGameStartPort _gameStart;
        private readonly IMobaBattleInputPort _input;
        private readonly IMobaBattleOutputPort _output;
        private readonly IMobaLogicWorldStateReadModel _stateReadModel;

        public MobaBattleRuntimePort(
            IMobaGameStartPort gameStart,
            IMobaBattleInputPort input,
            IMobaBattleOutputPort output,
            IMobaLogicWorldStateReadModel stateReadModel)
        {
            _gameStart = gameStart;
            _input = input;
            _output = output;
            _stateReadModel = stateReadModel;
        }

        public MobaBattleRuntimeStatus Status => BuildStatus();

        public BattleRuntimeStatus BattleStatus
        {
            get
            {
                var status = BuildStatus();
                var capabilities = BattleRuntimeCapability.None;
                if (status.Has(MobaBattleRuntimeCapability.GameStart)) capabilities |= BattleRuntimeCapability.GameStart;
                if (status.Has(MobaBattleRuntimeCapability.Input)) capabilities |= BattleRuntimeCapability.Input;
                if (status.Has(MobaBattleRuntimeCapability.SnapshotOutput)) capabilities |= BattleRuntimeCapability.SnapshotOutput;
                if (status.Has(MobaBattleRuntimeCapability.StateReadModel)) capabilities |= BattleRuntimeCapability.StateReadModel;

                var state = status.IsReadyForBattleLoop
                    ? BattleRuntimeState.Ready
                    : BattleRuntimeState.Created;
                return new BattleRuntimeStatus(capabilities, state, status.MissingServices);
            }
        }

        public MobaGameStartResult TryStartGame(in MobaGameStartSpec spec)
        {
            if (_gameStart == null)
            {
                return MobaGameStartResult.Fail(MobaGameStartFailureCode.MissingGameStartPort, "IMobaGameStartPort is not resolved");
            }

            return _gameStart.TryStartGame(in spec);
        }

        public MobaInputSubmitResult Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs)
        {
            if (_input == null)
            {
                return MobaInputSubmitResult.Fail(MobaInputSubmitFailureCode.MissingInputPort, "IMobaBattleInputPort is not resolved");
            }

            return _input.Submit(frame, inputs);
        }

        /// <summary>
        /// 兼容单快照门面，生产同步循环请使用 <see cref="CollectSnapshots"/>。
        /// </summary>
        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (_output == null)
            {
                throw new InvalidOperationException("MobaBattleRuntimePort requires IMobaBattleOutputPort for snapshot output.");
            }

            ValidateSnapshotFrame(frame);
            return _output.TryGetSnapshot(frame, out snapshot);
        }

        /// <summary>
        /// 批量快照门面，转发到输出端口并复用调用方缓冲区。
        /// </summary>
        public int CollectSnapshots(FrameIndex frame, IList<WorldStateSnapshot> snapshots, int maxSnapshots = 32)
        {
            if (_output == null)
            {
                throw new InvalidOperationException("MobaBattleRuntimePort requires IMobaBattleOutputPort for snapshot output.");
            }

            ValidateSnapshotFrame(frame);

            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (maxSnapshots <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSnapshots), maxSnapshots, "maxSnapshots must be positive.");
            }

            return _output.CollectSnapshots(frame, snapshots, maxSnapshots);
        }

        private static void ValidateSnapshotFrame(FrameIndex frame)
        {
            if (frame.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frame), frame.Value, "frame must be non-negative.");
            }
        }

        /// <summary>
        /// 兼容数组读取门面，生产诊断采样请使用 <see cref="FillDiagnosticEntityStates"/>。
        /// </summary>
        public MobaDiagnosticEntityState[] GetDiagnosticEntityStates()
        {
            return _stateReadModel?.GetDiagnosticEntityStates() ?? Array.Empty<MobaDiagnosticEntityState>();
        }

        /// <summary>
        /// 填充诊断状态到调用方缓冲区，避免数组分配。
        /// </summary>
        public int FillDiagnosticEntityStates(IList<MobaDiagnosticEntityState> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return _stateReadModel?.FillDiagnosticEntityStates(buffer) ?? 0;
        }

        /// <summary>
        /// 兼容数组读取门面，生产状态采样请使用 <see cref="FillAllEntityStates"/>。
        /// </summary>
        public LogicWorldEntityState[] GetAllEntityStates()
        {
            return _stateReadModel?.GetAllEntityStates() ?? Array.Empty<LogicWorldEntityState>();
        }

        /// <summary>
        /// 填充逻辑世界状态到调用方缓冲区，作为高频状态读取推荐入口。
        /// </summary>
        public int FillAllEntityStates(IList<LogicWorldEntityState> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return _stateReadModel?.FillAllEntityStates(buffer) ?? 0;
        }

        public void Dispose()
        {
        }

        private MobaBattleRuntimeStatus BuildStatus()
        {
            var capabilities = MobaBattleRuntimeCapability.None;
            var missing = new List<string>(4);

            if (_gameStart != null) capabilities |= MobaBattleRuntimeCapability.GameStart;
            else missing.Add(nameof(IMobaGameStartPort));

            if (_input != null) capabilities |= MobaBattleRuntimeCapability.Input;
            else missing.Add(nameof(IMobaBattleInputPort));

            if (_output != null) capabilities |= MobaBattleRuntimeCapability.SnapshotOutput;
            else missing.Add(nameof(IMobaBattleOutputPort));

            if (_stateReadModel != null) capabilities |= MobaBattleRuntimeCapability.StateReadModel;
            else missing.Add(nameof(IMobaLogicWorldStateReadModel));

            return new MobaBattleRuntimeStatus(capabilities, string.Join(",", missing));
        }
    }
}
