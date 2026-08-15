using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.StateSync.Prediction
{

/// <summary>
/// 预测协调器
/// 协调多个处理器和快照存储，处理服务器快照和回滚
/// 通用实现，不依赖任何业务代码
/// 实现 IPredictionCoordinator 接口
/// </summary>
public sealed class PredictionCoordinator : IPredictionCoordinator, IDisposable
{
    private readonly int _localPlayerId;
    private readonly List<IPredictionHandler> _handlers = new List<IPredictionHandler>();
    private readonly ISnapshotStore _snapshotStore;
    private readonly InputHistory _inputHistory;
    private readonly StateSlots _currentSlots;

    private Frame _currentFrame;
    private Frame _confirmedFrame;

    private readonly List<IPredictionListener> _listeners = new List<IPredictionListener>();

    public int LocalPlayerId => _localPlayerId;
    public Frame CurrentFrame => _currentFrame;
    public Frame ConfirmedFrame => _confirmedFrame;
    public bool HasUnconfirmedPrediction => _currentFrame > _confirmedFrame;

    // IPredictionCoordinator 接口属性
    int IPredictionCoordinator.LocalPlayerId => _localPlayerId;
    int IPredictionCoordinator.CurrentPredictedFrame => _currentFrame.Value;
    int IPredictionCoordinator.ServerConfirmedFrame => _confirmedFrame.Value;
    bool IPredictionCoordinator.NeedsRollback => _currentFrame > _confirmedFrame;

    public event Action<Frame, Frame> OnFramesAdvanced;
    public event Action<Frame, StateSlots> OnPredictionApplied;
    public event Action<Frame, StateSlots> OnServerStateApplied;
    public event Action<Frame, ConflictLevel> OnRollbackExecuted;

    public PredictionCoordinator(int localPlayerId)
    {
        _localPlayerId = localPlayerId;
        _snapshotStore = new DictionarySnapshotStore(30);
        _inputHistory = new InputHistory(30);
        _currentSlots = new StateSlots();
        _currentFrame = Frame.Zero;
        _confirmedFrame = Frame.Invalid;
    }

    /// <summary>
    /// 注册预测处理器
    /// </summary>
    public void Register(IPredictionHandler handler)
    {
        if (handler != null)
            _handlers.Add(handler);
    }

    /// <summary>
    /// 注册监听器
    /// </summary>
    public void AddListener(IPredictionListener listener)
    {
        _listeners.Add(listener);
    }

    /// <summary>
    /// 获取当前状态槽位
    /// </summary>
    public StateSlots GetCurrentSlots() => _currentSlots;

    /// <summary>
    /// 处理输入
    /// </summary>
    public void ProcessInput(IInputCommand input)
    {
        _currentFrame = _currentFrame + 1;
        _inputHistory.Record(_currentFrame, input);

        foreach (var handler in _handlers)
        {
            if (handler.Strategy != PredictionStrategy.None)
            {
                handler.Predict(input, _currentSlots, _currentFrame);
            }
        }

        _snapshotStore.Record(_currentFrame, _currentSlots);

        var handlerAdvanced = OnFramesAdvanced;
        if (handlerAdvanced != null)
            handlerAdvanced(_currentFrame, _confirmedFrame);
        NotifyListeners(l => l.OnPredictionApplied(_currentFrame, _currentSlots));
    }

    /// <summary>
    /// IPredictionCoordinator 接口实现
    /// </summary>
    void IPredictionCoordinator.RecordInput(int frame, IInputCommand input)
    {
        if (frame > _currentFrame.Value)
        {
            _currentFrame = new Frame(frame);
        }
        _inputHistory.Record(new Frame(frame), input);
    }

    void IPredictionCoordinator.AdvancePrediction()
    {
        _currentFrame = _currentFrame + 1;
    }

    void IPredictionCoordinator.ExecuteRollback()
    {
        // 由 ApplyServerSnapshot 自动处理
    }

    /// <summary>
    /// 应用服务器快照
    /// </summary>
    public void ApplyServerSnapshot(int serverFrame, int objectId, StateSlots serverSlots)
    {
        if (objectId != _localPlayerId) return;
        if (serverSlots == null) throw new ArgumentNullException(nameof(serverSlots));

        var serverFrameObj = new Frame(serverFrame);

        // Ignore confirmations that cannot advance the acknowledged timeline.
        // A late packet must not roll the client back over a newer confirmation.
        if (_confirmedFrame != Frame.Invalid && serverFrameObj <= _confirmedFrame)
            return;

        var predictedSlots = _snapshotStore.Get(serverFrameObj);
        if (predictedSlots == null)
            predictedSlots = _currentSlots;

        var conflictLevel = ValidateAll(predictedSlots, serverSlots);

        if (conflictLevel == ConflictLevel.None)
        {
            _confirmedFrame = serverFrameObj;
        }
        else
        {
            var predictedFrame = _currentFrame;
            var replayInputs = _inputHistory.GetInputs(serverFrameObj, predictedFrame);
            var handlerRollback = OnRollbackExecuted;
            if (handlerRollback != null)
                handlerRollback(_currentFrame, conflictLevel);
            NotifyListeners(l => l.OnRollbackStarted(serverFrameObj, conflictLevel));

            _currentFrame = serverFrameObj;
            _currentSlots.OverwriteFrom(serverSlots);
            foreach (var handler in _handlers)
            {
                if (handler.Strategy != PredictionStrategy.None)
                    handler.ApplyServerState(serverSlots, _currentSlots);
            }
            _confirmedFrame = serverFrameObj;

            _snapshotStore.PruneBefore(serverFrameObj);
            _inputHistory.Clear();

            ReplayInputs(replayInputs);
        }

        var handlerServerApplied = OnServerStateApplied;
        if (handlerServerApplied != null)
            handlerServerApplied(serverFrameObj, _currentSlots);
        NotifyListeners(l => l.OnServerStateApplied(serverFrameObj, _currentSlots));
    }

    /// <summary>
    /// 校验所有处理器
    /// </summary>
    private ConflictLevel ValidateAll(StateSlots predicted, StateSlots server)
    {
        var worstLevel = ConflictLevel.None;

        foreach (var handler in _handlers)
        {
            if (handler.Strategy == PredictionStrategy.None) continue;

            var result = handler.Validate(predicted, server);
            if (!result.Success && result.Level > worstLevel)
            {
                worstLevel = result.Level;
            }
        }

        return worstLevel;
    }

    /// <summary>
    /// 重演输入
    /// </summary>
    private void ReplayInputs(IReadOnlyList<IInputCommand> inputs)
    {
        foreach (var input in inputs)
        {
            _currentFrame = _currentFrame + 1;
            _inputHistory.Record(_currentFrame, input);
            foreach (var handler in _handlers)
            {
                if (handler.Strategy != PredictionStrategy.None)
                {
                    handler.Predict(input, _currentSlots, _currentFrame);
                }
            }
            _snapshotStore.Record(_currentFrame, _currentSlots);
        }
    }

    /// <summary>
    /// 重置
    /// </summary>
    public void Reset()
    {
        _currentFrame = Frame.Zero;
        _confirmedFrame = Frame.Invalid;
        _snapshotStore.PruneBefore(Frame.Invalid);
        _inputHistory.Clear();
    }

    private void NotifyListeners(Action<IPredictionListener> action)
    {
        foreach (var listener in _listeners)
        {
            try
            {
                action(listener);
            }
            catch
            {
                // 忽略监听器异常
            }
        }
    }

    public void Dispose()
    {
        _listeners.Clear();
        _handlers.Clear();
    }
}

}
