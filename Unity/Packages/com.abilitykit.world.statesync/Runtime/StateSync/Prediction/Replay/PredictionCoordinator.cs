using System;
using System.Collections.Generic;
using AbilityKit.Core.Buffers;

namespace AbilityKit.Ability.StateSync.Prediction
{

/// <summary>
/// 由单一执行线驱动的局部槽位预测协调器；按注册顺序执行处理器，并在权威校正后重演保留输入。
/// 不验证权威状态来源、输入终结或实体代际；处理器和事件异常不提供事务补偿。
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

    /// <summary>
    /// 返回用于匹配权威状态目标的本地玩家标识。
    /// </summary>
    public int LocalPlayerId => _localPlayerId;
    /// <summary>
    /// 返回当前局部预测帧；重演可先回退再推进。
    /// </summary>
    public Frame CurrentFrame => _currentFrame;
    /// <summary>
    /// 返回最近处理的权威状态帧，初始为负一；不是独立的输入确认游标。
    /// </summary>
    public Frame ConfirmedFrame => _confirmedFrame;
    /// <summary>
    /// 仅表示预测帧领先已处理的权威状态帧，不表示发生冲突。
    /// </summary>
    public bool HasUnconfirmedPrediction => _currentFrame > _confirmedFrame;
    /// <summary>
    /// 返回构造时选用的历史配置；配置容量不是动态调整后的容量。
    /// </summary>
    public PredictionCoordinatorBufferOptions BufferOptions => _bufferOptions;
    /// <summary>
    /// 返回预测快照存储的容量控制；未启用或后端不提供控制时为空。
    /// </summary>
    public IBufferCapacityControl PredictedStateHistoryCapacityControl =>
        _snapshotStore as IBufferCapacityControl;
    /// <summary>
    /// 返回输入历史的容量控制；未启用或后端不提供控制时为空。
    /// </summary>
    public IBufferCapacityControl InputHistoryCapacityControl =>
        _inputHistory as IBufferCapacityControl;
    /// <summary>
    /// 仅在允许重演且两种历史均存在时为真；不保证目标区间仍完整保留。
    /// </summary>
    public bool RollbackReplayEnabled =>
        _enableRollbackReplay && _snapshotStore != null && _inputHistory != null;

    int IPredictionCoordinator.LocalPlayerId => _localPlayerId;
    int IPredictionCoordinator.CurrentPredictedFrame => _currentFrame.Value;
    int IPredictionCoordinator.ServerConfirmedFrame => _confirmedFrame.Value;
    bool IPredictionCoordinator.NeedsRollback => _currentFrame > _confirmedFrame;

    /// <summary>
    /// 正常本地帧存档后同步通知当前预测帧和权威状态帧；校正重演不逐帧触发此事件。
    /// </summary>
    public event Action<Frame, Frame> OnFramesAdvanced;
    /// <summary>
    /// 正常本地帧完成后同步传递当前帧和活槽位引用；观察者不应修改、保留为快照或重入。
    /// </summary>
    public event Action<Frame, StateSlots> OnPredictionApplied;
    /// <summary>
    /// 成功处理一份新权威状态后同步传递权威帧与最终活槽位；槽位可能已包含重演结果。
    /// </summary>
    public event Action<Frame, StateSlots> OnServerStateApplied;
    /// <summary>
    /// 兼容事件：重演实际开始前同步通知校正前预测帧和冲突级别，不是回滚完成通知。
    /// </summary>
    public event Action<Frame, ConflictLevel> OnRollbackExecuted;

    /// <summary>
    /// 初始预测帧为零，权威状态帧为负一；默认启用双历史和可选重演路径。
    /// 可变引用槽位需要提供独立复制策略；构造时创建历史后端。
    /// </summary>
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
    /// 按调用顺序追加非空处理器，允许重复注册；不要在运行回调中变更注册集合。
    /// </summary>
    public void Register(IPredictionHandler handler)
    {
        if (handler != null)
            _handlers.Add(handler);
    }

    /// <summary>
    /// 追加监听器；单项监听器回调异常被隔离，直接订阅的事件不使用此隔离。
    /// </summary>
    public void AddListener(IPredictionListener listener)
    {
        _listeners.Add(listener);
    }

    /// <summary>
    /// 返回协调器正在使用的可变槽位对象，不是快照；外部只能在约定的单一执行线内使用。
    /// </summary>
    public StateSlots GetCurrentSlots() => _currentSlots;

    /// <summary>
    /// 将非空命令作为一个新本地帧执行并尝试存档，历史保留原始命令引用。
    /// </summary>
    public void ProcessInput(IInputCommand input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        _currentFrame = _currentFrame + 1;
        RecordAndPredict(_currentFrame, input);
        CompletePredictedFrame();
    }

    /// <summary>
    /// 先验证所有命令，再推进一帧，按列表顺序执行并保存一次结果；空列表仍推进一帧。
    /// 输入命令实例在历史有效期间不得变更。
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
    /// 忽略非本地目标和已确认帧之前或同帧的状态；缺少同帧快照时与当前槽位比较。
    /// 无冲突时只推进权威状态帧；冲突时覆盖槽位，若开启重演则重放权威帧之后仍保留的输入。
    /// </summary>
    public void ApplyServerSnapshot(int serverFrame, int objectId, StateSlots serverSlots)
    {
        if (objectId != _localPlayerId) return;
        if (serverSlots == null) throw new ArgumentNullException(nameof(serverSlots));

        var serverFrameObj = new Frame(serverFrame);

        /*
         * 已应用权威状态的进度不能被迟到或重复的状态回退。
         */
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
    /// 清空当前槽位及两类历史，并重置帧进度；保留处理器、监听器、事件订阅和配置。
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
                /*
                 * 监听器只用于观察；隔离单项异常，以便继续通知后续监听器。
                 */
            }
        }
    }

    /// <summary>
    /// 清空历史、槽位、处理器、监听器及事件引用；不重新设置帧号，也不处置注入的后端对象。
    /// </summary>
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
