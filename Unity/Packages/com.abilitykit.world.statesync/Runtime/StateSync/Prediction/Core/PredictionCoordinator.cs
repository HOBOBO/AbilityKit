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
    private readonly PredictionCoordinatorBufferOptions _bufferOptions;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IInputHistory _inputHistory;
    private readonly bool _enableRollbackReplay;
    private readonly StateSlots _currentSlots;

    private Frame _currentFrame;
    private Frame _confirmedFrame;

    private readonly List<IPredictionListener> _listeners = new List<IPredictionListener>();

    public int LocalPlayerId => _localPlayerId;
    public Frame CurrentFrame => _currentFrame;
    public Frame ConfirmedFrame => _confirmedFrame;
    public bool HasUnconfirmedPrediction => _currentFrame > _confirmedFrame;
    public PredictionCoordinatorBufferOptions BufferOptions => _bufferOptions;
    public bool RollbackReplayEnabled =>
        _enableRollbackReplay && _snapshotStore != null && _inputHistory != null;

    // IPredictionCoordinator 接口属性
    int IPredictionCoordinator.LocalPlayerId => _localPlayerId;
    int IPredictionCoordinator.CurrentPredictedFrame => _currentFrame.Value;
    int IPredictionCoordinator.ServerConfirmedFrame => _confirmedFrame.Value;
    bool IPredictionCoordinator.NeedsRollback => _currentFrame > _confirmedFrame;

    public event Action<Frame, Frame> OnFramesAdvanced;
    public event Action<Frame, StateSlots> OnPredictionApplied;
    public event Action<Frame, StateSlots> OnServerStateApplied;
    public event Action<Frame, ConflictLevel> OnRollbackExecuted;

    public PredictionCoordinator(
        int localPlayerId,
        IStateSlotValueCloner slotValueCloner = null,
        PredictionCoordinatorBufferOptions bufferOptions = null,
        bool enableRollbackReplay = true)
    {
        _localPlayerId = localPlayerId;
        _bufferOptions = bufferOptions ?? PredictionCoordinatorBufferOptions.Default;
        _snapshotStore = _bufferOptions.CreateSnapshotStore();
        _inputHistory = _bufferOptions.CreateInputHistory();
        _enableRollbackReplay = enableRollbackReplay;
        _currentSlots = new StateSlots(slotValueCloner);
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
        if (input == null) throw new ArgumentNullException(nameof(input));

        _currentFrame = _currentFrame + 1;
        RecordAndPredict(_currentFrame, input);
        CompletePredictedFrame();
    }

    /// <summary>
    /// Processes all commands in one prediction frame and captures one resulting snapshot.
    /// </summary>
    public void ProcessInputs(IReadOnlyList<IInputCommand> inputs)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        for (var i = 0; i < inputs.Count; i++)
        {
            if (inputs[i] == null)
                throw new ArgumentException("Prediction input batches cannot contain null commands.", nameof(inputs));
        }

        _currentFrame = _currentFrame + 1;
        for (var i = 0; i < inputs.Count; i++)
            RecordAndPredict(_currentFrame, inputs[i]);
        CompletePredictedFrame();
    }

    private void RecordAndPredict(Frame frame, IInputCommand input)
    {
        _inputHistory?.Record(frame, input);
        foreach (var handler in _handlers)
        {
            if (handler.Strategy != PredictionStrategy.None)
                handler.Predict(input, _currentSlots, frame);
        }
    }

    private void CompletePredictedFrame()
    {
        _snapshotStore?.Record(_currentFrame, _currentSlots);

        var handlerAdvanced = OnFramesAdvanced;
        if (handlerAdvanced != null)
            handlerAdvanced(_currentFrame, _confirmedFrame);
        var handlerPredictionApplied = OnPredictionApplied;
        if (handlerPredictionApplied != null)
            handlerPredictionApplied(_currentFrame, _currentSlots);
        NotifyListeners(l => l.OnPredictionApplied(_currentFrame, _currentSlots));
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

        var predictedSlots = _snapshotStore?.Get(serverFrameObj);
        if (predictedSlots == null)
            predictedSlots = _currentSlots;

        var conflictLevel = ValidateAll(predictedSlots, serverSlots);

        if (conflictLevel == ConflictLevel.None)
        {
            _confirmedFrame = serverFrameObj;
        }
        else
        {
            if (!RollbackReplayEnabled)
            {
                ApplyAuthoritativeCorrection(serverFrameObj, serverSlots);
                PublishServerStateApplied(serverFrameObj);
                return;
            }

            var predictedFrame = _currentFrame;
            var replayFrames = _inputHistory.GetFrameBatches(serverFrameObj, predictedFrame);
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

            ReplayInputFrames(replayFrames);
        }

        PublishServerStateApplied(serverFrameObj);
    }

    private void ApplyAuthoritativeCorrection(Frame serverFrame, StateSlots serverSlots)
    {
        _currentSlots.OverwriteFrom(serverSlots);
        foreach (var handler in _handlers)
        {
            if (handler.Strategy != PredictionStrategy.None)
                handler.ApplyServerState(serverSlots, _currentSlots);
        }

        _confirmedFrame = serverFrame;
        _snapshotStore?.PruneBefore(serverFrame);
        _inputHistory?.Clear();
    }

    private void PublishServerStateApplied(Frame serverFrame)
    {
        var handlerServerApplied = OnServerStateApplied;
        if (handlerServerApplied != null)
            handlerServerApplied(serverFrame, _currentSlots);
        NotifyListeners(l => l.OnServerStateApplied(serverFrame, _currentSlots));
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
    private void ReplayInputFrames(IReadOnlyList<InputFrameBatch> frames)
    {
        foreach (var frame in frames)
        {
            _currentFrame = frame.Frame;
            for (var i = 0; i < frame.Inputs.Count; i++)
                RecordAndPredict(_currentFrame, frame.Inputs[i]);
            _snapshotStore?.Record(_currentFrame, _currentSlots);
        }
    }

    /// <summary>
    /// 重置
    /// </summary>
    public void Reset()
    {
        _currentFrame = Frame.Zero;
        _confirmedFrame = Frame.Invalid;
        _snapshotStore?.Clear();
        _inputHistory?.Clear();
        _currentSlots.Clear();
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
        _snapshotStore?.Clear();
        _inputHistory?.Clear();
        _currentSlots.Clear();
        _listeners.Clear();
        _handlers.Clear();
        OnFramesAdvanced = null;
        OnPredictionApplied = null;
        OnServerStateApplied = null;
        OnRollbackExecuted = null;
    }
}

}
