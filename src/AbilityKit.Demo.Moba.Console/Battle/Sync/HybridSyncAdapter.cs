using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Demo.Moba.Console.Battle.Context;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Prediction;
using Pred = AbilityKit.Ability.StateSync.Prediction;
using PredHandlers = AbilityKit.Demo.Moba.Console.Battle.Prediction.Handlers;
using PredictionSlots = AbilityKit.Demo.Moba.Share.Prediction.MobaPredictionSlotNames;
using AK = AbilityKit;

namespace AbilityKit.Demo.Moba.Console.Battle.Sync;

/// <summary>
/// 混合同步适配器
/// 整合帧同步 + 客户端预测 + 回滚机制
/// 适用于需要低延迟响应的战斗场景
/// </summary>
public sealed class HybridSyncAdapter : IBattleSyncAdapter
{
    private ConsoleBattleContext _context;
    private BattleStartConfig _config;
    private PredictionCoordinator _coordinator;
    private bool _initialized;
    private bool _connected;
    private int _currentFrame;
    private double _logicTimeSeconds;
    private double _renderTimeSeconds;
    private int _localActorId;

    private readonly List<ActorStateSnapshot> _actorStates = new();
    private readonly Dictionary<int, ActorStateSnapshot> _latestActorStates = new();
    private readonly object _statesLock = new();

    public SyncMode Mode => SyncMode.Hybrid;
    public bool IsConnected => _connected;
    public int CurrentFrame => _currentFrame;
    public double LogicTimeSeconds => _logicTimeSeconds;
    public double RenderTimeSeconds => _renderTimeSeconds;
    public int LocalActorId => _localActorId;
    public long TotalServerStateApplications { get; private set; }
    public long TotalRollbackCorrections { get; private set; }
    public ConflictLevel LastConflictLevel { get; private set; }

    public event Action<bool> OnConnectionChanged;
    public event Action<int, double> OnFrameSync;
    public event Action<ActorStateSnapshot[]> OnActorStateSnapshot;

    public HybridSyncAdapter()
    {
        _coordinator = new PredictionCoordinator(0);
    }

    public void Initialize(ConsoleBattleContext context, BattleStartConfig config)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _initialized = true;
        _connected = true; // 本地模式
        _currentFrame = 0;
        _logicTimeSeconds = 0;
        TotalServerStateApplications = 0;
        TotalRollbackCorrections = 0;
        LastConflictLevel = ConflictLevel.None;
        _localActorId = config.Players?.Count > 0
            ? DeterministicHash.StringToActorId(config.Players[0].PlayerId)
            : 1;

        // 初始化预测协调器
        _coordinator = new PredictionCoordinator(_localActorId);

        // 注册预测处理器
        if (config.EnableClientPrediction)
        {
            _coordinator.Register(new MobaMovementPredictionHandler());
            _coordinator.Register(new PredHandlers.CooldownHandler());
            _coordinator.Register(new PredHandlers.HealthHandler());

            // 订阅事件
            _coordinator.OnServerStateApplied += OnServerStateApplied;
            _coordinator.OnRollbackExecuted += OnRollbackExecuted;

            Platform.Log.Sync($"[HybridSync] Prediction enabled with {3} handlers");
        }

        OnConnectionChanged?.Invoke(_connected);
        Platform.Log.Sync($"[HybridSync] Initialized - Mode: {Mode}, LocalActorId: {_localActorId}");
    }

    public void Connect(string host, int port, string roomId, string playerId)
    {
        if (!_initialized)
            throw new InvalidOperationException("HybridSyncAdapter not initialized. Call Initialize first.");

        // TODO: 实现网络连接
        throw new NotSupportedException("Hybrid network mode not yet implemented.");
    }

    public void Disconnect()
    {
        _connected = false;
        OnConnectionChanged?.Invoke(_connected);
        Platform.Log.Sync("[HybridSync] Disconnected");
    }

    public void SubmitInput(PlayerInput input)
    {
        if (!_initialized || !_connected) return;

        // 转换为预测输入
        var interruptEpoch = _coordinator.GetCurrentSlots().GetInt(
            PredictionSlots.ActionInterruptEpoch);
        var moveInput = MobaMovePredictionInput.FromPayload(input.Payload, interruptEpoch);

        // 处理输入
        _coordinator.ProcessInput(moveInput);
    }

    public void Tick(float deltaTime)
    {
        if (!_initialized || !_connected) return;

        _currentFrame = _context.LastFrame;
        _logicTimeSeconds = _context.LogicTimeSeconds;

        // 渲染时间比逻辑时间滞后一帧
        _renderTimeSeconds = _logicTimeSeconds - (1.0 / _config.TickRate);

        // 触发帧同步事件
        OnFrameSync?.Invoke(_currentFrame, _logicTimeSeconds);
    }

    /// <summary>
    /// 收到服务器快照（网络模式下调用）
    /// </summary>
    public void OnServerSnapshot(int serverFrame, ActorStateSnapshot[] snapshots)
    {
        lock (_statesLock)
        {
            _currentFrame = serverFrame;

            foreach (var snapshot in snapshots)
            {
                _latestActorStates[snapshot.ActorId] = snapshot;
            }

            // 更新表现层
            _actorStates.Clear();
            foreach (var kvp in _latestActorStates)
            {
                _actorStates.Add(kvp.Value);
            }

            // 通知观察者
            OnActorStateSnapshot?.Invoke(_actorStates.ToArray());
        }

        // 应用到预测协调器
        foreach (var snapshot in snapshots)
        {
            var slots = ConvertToStateSlots(snapshot);
            _coordinator.ApplyServerSnapshot(serverFrame, snapshot.ActorId, slots);
        }

        Platform.Log.Sync($"[HybridSync] Server snapshot - Frame: {serverFrame}, Actors: {snapshots.Length}");
    }

    /// <summary>
    /// 收到服务器帧确认
    /// </summary>
    public void OnServerFrameConfirmed(int serverFrame, ActorStateSnapshot[] snapshots)
    {
        OnServerSnapshot(serverFrame, snapshots);
    }

    private Pred.StateSlots ConvertToStateSlots(ActorStateSnapshot snapshot)
    {
        var slots = new Pred.StateSlots();

        // 位置
        slots.Set(PredictionSlots.Position,
            new AK.Ability.StateSync.Vector3(snapshot.X, snapshot.Y, snapshot.Z));

        // 速度
        slots.Set(PredictionSlots.Velocity,
            new AK.Ability.StateSync.Vector3(snapshot.VelocityX, 0, snapshot.VelocityZ));

        // 生命值
        slots.Set(PredictionSlots.Health, snapshot.Hp);
        slots.Set(PredictionSlots.MaxHealth, snapshot.HpMax);

        return slots;
    }

    private void OnServerStateApplied(Frame frame, StateSlots state)
    {
        TotalServerStateApplications++;
        Platform.Log.Prediction($"[HybridSync] Server state applied - Frame: {frame}");
    }

    private void OnRollbackExecuted(Frame frame, ConflictLevel level)
    {
        TotalRollbackCorrections++;
        LastConflictLevel = level;
        Platform.Log.Prediction($"[HybridSync] Rollback executed - Frame: {frame}, Level: {level}");
    }

    public ActorStateSnapshot[] GetAllActorStates()
    {
        lock (_statesLock)
        {
            _actorStates.Clear();
            foreach (var kvp in _latestActorStates)
            {
                _actorStates.Add(kvp.Value);
            }
            return _actorStates.ToArray();
        }
    }

    /// <summary>
    /// 获取预测协调器（用于调试）
    /// </summary>
    public PredictionCoordinator GetCoordinator()
    {
        return _coordinator;
    }

    /// <summary>
    /// 获取本地玩家的预测状态槽位
    /// </summary>
    public StateSlots GetPredictedSlots()
    {
        return _coordinator.GetCurrentSlots();
    }

    /// <summary>
    /// 获取调试信息
    /// </summary>
    public string GetDebugInfo()
    {
        var slots = _coordinator.GetCurrentSlots();
        return $"=== HybridSyncAdapter Debug ===\n" +
               $"Mode: {Mode}\n" +
               $"Connected: {_connected}\n" +
               $"CurrentFrame: {_currentFrame}\n" +
               $"LocalActorId: {_localActorId}\n" +
               $"ServerStateApplications: {TotalServerStateApplications}\n" +
               $"RollbackCorrections: {TotalRollbackCorrections}\n" +
               $"LastConflictLevel: {LastConflictLevel}";
    }

    public void Dispose()
    {
        _coordinator?.Dispose();
        _coordinator = null!;
        _initialized = false;
        _connected = false;
        _latestActorStates.Clear();
        OnConnectionChanged = null;
        OnFrameSync = null;
        OnActorStateSnapshot = null;
        Platform.Log.Sync("[HybridSync] Disposed");
    }
}
