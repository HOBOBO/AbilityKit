using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Coordinator;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Services;

namespace AbilityKit.Demo.Moba.Session
{
    public interface ILogicWorldDriverHost : IFrameDriver
    {
        double LogicTimeSeconds { get; }
        bool IsRunning { get; }
        void BindLogicWorld(IWorld world, HostRuntime hostRuntime);
        void Start();
        void Stop();
        MobaInputSubmitResult SubmitCommands(IReadOnlyList<PlayerInputCommand> commands);
        MobaInputSubmitResult SubmitCommands(FrameIndex targetFrame, IReadOnlyList<PlayerInputCommand> commands);
        LogicWorldEntityState[] GetLogicWorldEntityStates();
        int FillLogicWorldEntityStates(IList<LogicWorldEntityState> buffer);
        bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot);
        int CollectSnapshots(FrameIndex frame, IList<WorldStateSnapshot> snapshots, int maxSnapshots = 32);
    }

    /// <summary>
    /// MOBA 逻辑世界驱动适配器（跨平台复用）。
    /// 连接 coordinator 的通用驱动桥与 moba.runtime 的统一运行时端口，不承载战斗规则。
    ///
    /// 设计原则（单一入口）：
    /// - 只通过 IMobaBattleRuntimePort 与 moba.runtime 交互
    /// - 输入、快照和状态读取都由统一运行时端口承载
    /// - 禁止直接访问 MobaEntityManager 或实体
    /// </summary>
    public sealed class MobaBattleDriverHost : ILogicWorldDriverHost, ILogicWorldDriverBridge
    {
        private IWorld _world;
        private HostRuntime _hostRuntime;
        private IMobaBattleRuntimePort _runtime;
        private ILogicWorldDriveGate _driveGate;
        private MobaPlayerInputCommandConverter _inputConverter;
        private MobaTransformSnapshotDispatcher _transformSnapshots;
        private MobaCoordinatorStateAdapter _stateAdapter;
        private FrameIndex _currentFrame;
        // 会话/表现域时间（对局时长显示、编辑器面板、测试时间戳），跨包接口 double 消费者众多；
        // double IEEE 累计位一致，不参与逐帧模拟判定，故保留 double 不迁定点。
        private double _logicTimeSeconds;
        private bool _isRunning;
        private bool _missingDriveGateLogged;

        // 位置快照事件回调（由 ET Logic 层设置）
        private System.Action<int, MobaActorTransformSnapshotEntry[]>? _onTransformSnapshot;

        public void Bind(IWorld world, HostRuntime hostRuntime)
        {
            BindLogicWorld(world, hostRuntime);
        }

        public void BindLogicWorld(IWorld world, HostRuntime hostRuntime)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _hostRuntime = hostRuntime;

            // 获取逻辑世界统一运行时端口，外部模块不直接依赖内部输入/快照服务。
            if (_world?.Services != null)
            {
                _world.Services.TryResolve(out _runtime);
                _world.Services.TryResolve(out _driveGate);
            }

            _missingDriveGateLogged = false;
            _inputConverter = new MobaPlayerInputCommandConverter();
            _transformSnapshots = new MobaTransformSnapshotDispatcher(_world);
            _stateAdapter = new MobaCoordinatorStateAdapter();
        }

        /// <summary>
        /// 设置位置快照回调（由 ET Logic 层调用）
        /// </summary>
        public void SetTransformSnapshotCallback(System.Action<int, MobaActorTransformSnapshotEntry[]>? callback)
        {
            _onTransformSnapshot = callback;
        }

        public void SetEntityStates(IEnumerable<EntityState> states)
        {
            // Coordinator compatibility entry point; runtime state is read from IMobaBattleRuntimePort snapshots.
        }

        public int CurrentFrame => _currentFrame.Value;

        public FrameIndex Frame => _currentFrame;

        public double LogicTimeSeconds => _logicTimeSeconds;

        public bool IsRunning => _isRunning;

        public void Start()
        {
            _isRunning = true;
            _currentFrame = new FrameIndex(0);
            _logicTimeSeconds = 0;
            MobaRuntimeLog.Info(MobaRuntimeLogModule.Session, MobaRuntimeLogPurpose.Lifecycle, nameof(MobaBattleDriverHost), "Started");
        }

        public void Stop()
        {
            _isRunning = false;
            MobaRuntimeLog.Info(MobaRuntimeLogModule.Session, MobaRuntimeLogPurpose.Lifecycle, nameof(MobaBattleDriverHost), "Stopped");
        }

        public void SubmitInputs(PlayerInput[] inputs)
        {
            if (!_isRunning)
            {
                LogInputSubmitFailure(MobaInputSubmitResult.Fail(MobaInputSubmitFailureCode.NotRunning, "battle driver is not running"));
                return;
            }

            if (inputs == null || inputs.Length == 0)
            {
                LogInputSubmitFailure(MobaInputSubmitResult.Fail(MobaInputSubmitFailureCode.NullOrEmptyCommands, "player input batch is null or empty"));
                return;
            }

            if (_runtime == null)
            {
                LogInputSubmitFailure(MobaInputSubmitResult.Fail(MobaInputSubmitFailureCode.MissingInputPort, "IMobaBattleRuntimePort is not resolved"));
                return;
            }

            MobaRuntimeLog.Trace(MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.RuntimeTrace, nameof(MobaBattleDriverHost), $"SubmitInputs: {inputs.Length} inputs");

            var targetFrame = GetDefaultInputTargetFrame();
            var commands = _inputConverter.Convert(inputs, targetFrame);
            var result = SubmitCommands(targetFrame, commands);
            if (!result.Succeeded)
            {
                LogInputSubmitFailure(result);
            }
        }

        public MobaInputSubmitResult SubmitCommands(IReadOnlyList<PlayerInputCommand> commands)
        {
            return SubmitCommands(GetDefaultInputTargetFrame(), commands);
        }

        public MobaInputSubmitResult SubmitCommands(FrameIndex targetFrame, IReadOnlyList<PlayerInputCommand> commands)
        {
            if (!_isRunning)
            {
                return MobaInputSubmitResult.Fail(MobaInputSubmitFailureCode.NotRunning, "battle driver is not running");
            }

            if (_runtime == null)
            {
                return MobaInputSubmitResult.Fail(MobaInputSubmitFailureCode.MissingInputPort, "IMobaBattleRuntimePort is not resolved");
            }

            var result = _runtime.Submit(targetFrame, commands);
            if (!result.Succeeded)
            {
                MobaRuntimeLog.Warning(MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Rejection, nameof(MobaBattleDriverHost), $"SubmitCommands rejected. {result}");
            }

            return result;
        }

        public SnapshotEntityState[] GetAllEntityStates()
        {
            return _stateAdapter != null
                ? _stateAdapter.ToCoordinatorStates(GetLogicWorldEntityStates())
                : Array.Empty<SnapshotEntityState>();
        }

        public LogicWorldEntityState[] GetLogicWorldEntityStates()
        {
            if (_runtime == null)
            {
                return Array.Empty<LogicWorldEntityState>();
            }

            var states = _runtime.GetAllEntityStates();
            return states ?? Array.Empty<LogicWorldEntityState>();
        }

        public int FillLogicWorldEntityStates(IList<LogicWorldEntityState> buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return _runtime?.FillAllEntityStates(buffer) ?? 0;
        }

        public bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot)
        {
            if (_runtime == null)
            {
                throw new InvalidOperationException("MobaBattleDriverHost requires IMobaBattleRuntimePort for snapshot output.");
            }

            return _runtime.TryGetSnapshot(frame, out snapshot);
        }

        public int CollectSnapshots(FrameIndex frame, IList<WorldStateSnapshot> snapshots, int maxSnapshots = 32)
        {
            if (_runtime == null)
            {
                throw new InvalidOperationException("MobaBattleDriverHost requires IMobaBattleRuntimePort for snapshot output.");
            }

            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (_runtime is IMobaBattleOutputPort snapshotReadModel)
            {
                return snapshotReadModel.CollectSnapshots(frame, snapshots, maxSnapshots);
            }

            if (maxSnapshots <= 0) return 0;

            var collected = 0;
            if (_runtime.TryGetSnapshot(frame, out var snapshot))
            {
                snapshots.Add(snapshot);
                collected = 1;
            }

            return collected;
        }

        public void AdvanceFrame(float deltaTime)
        {
            Step(deltaTime);
        }

        public void Step(float deltaTime)
        {
            if (!_isRunning || !CanDriveLogicWorld(deltaTime))
            {
                return;
            }

            _currentFrame = new FrameIndex(_currentFrame.Value + 1);
            _logicTimeSeconds += deltaTime;

            // 驱动 moba.core Tick
            _hostRuntime?.Tick(deltaTime);

            // 从战斗逻辑层统一运行时端口获取位置快照。
            TryGetTransformSnapshot();
        }

        private FrameIndex GetDefaultInputTargetFrame()
        {
            return new FrameIndex(_currentFrame.Value + 1);
        }

        private static void LogInputSubmitFailure(MobaInputSubmitResult result)
        {
            MobaRuntimeLog.Warning(MobaRuntimeLogModule.Input, MobaRuntimeLogPurpose.Rejection, nameof(MobaBattleDriverHost), $"SubmitInputs rejected. {result}");
        }

        private bool CanDriveLogicWorld(float deltaTime)
        {
            if (_driveGate != null)
            {
                return _driveGate.CanDriveLogicWorld(deltaTime);
            }

            if (!_missingDriveGateLogged)
            {
                _missingDriveGateLogged = true;
                MobaRuntimeLog.Warning(MobaRuntimeLogModule.Session, MobaRuntimeLogPurpose.Validation, nameof(MobaBattleDriverHost), "Logic world drive blocked: ILogicWorldDriveGate not resolved");
            }

            return false;
        }

        /// <summary>
        /// 从战斗逻辑层统一运行时端口获取位置快照。
        /// </summary>
        private void TryGetTransformSnapshot()
        {
            _transformSnapshots?.TryDispatch(_currentFrame, _onTransformSnapshot);
        }

    }
}
