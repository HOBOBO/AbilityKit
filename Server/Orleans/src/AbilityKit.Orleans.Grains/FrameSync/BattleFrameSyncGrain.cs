using System;
using System.Collections.Generic;
using AbilityKit.Orleans.Contracts.FrameSync;
using Orleans;

namespace AbilityKit.Orleans.Grains.FrameSync;

public sealed class BattleFrameSyncGrain : Grain, IBattleFrameSyncGrain
{
    private readonly HashSet<IFrameSyncObserver> _observers = new();

    // Keyed by frame index.
    private readonly Dictionary<int, List<FrameInputItem>> _inputsByFrame = new();

    private IDisposable? _timer;

    private ulong _roomId;
    private ulong _worldId;
    private int _frame;

    private const int TickRate = 30;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var key = this.GetPrimaryKeyString();
        if (!ulong.TryParse(key, out _roomId))
        {
            throw new InvalidOperationException($"BattleFrameSyncGrain key must be numeric roomId. key='{key}'");
        }

        _frame = 0;
        var interval = TimeSpan.FromSeconds(1.0 / TickRate);
        _timer = RegisterTimer(_ => OnTickAsync(), state: null, dueTime: interval, period: interval);
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        _inputsByFrame.Clear();
        return Task.CompletedTask;
    }

    public Task SubscribeAsync(IFrameSyncObserver observer)
    {
        if (observer == null) throw new ArgumentNullException(nameof(observer));
        _observers.Add(observer);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(IFrameSyncObserver observer)
    {
        if (observer == null) return Task.CompletedTask;
        _observers.Remove(observer);
        return Task.CompletedTask;
    }

    public Task SubmitInputAsync(ulong worldId, int frame, FrameInputItem input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (frame < 0) return Task.CompletedTask;

        if (_worldId == 0) _worldId = worldId;

        if (!_inputsByFrame.TryGetValue(frame, out var list))
        {
            list = new List<FrameInputItem>(8);
            _inputsByFrame[frame] = list;
        }

        list.Add(input);
        return Task.CompletedTask;
    }

    private Task OnTickAsync()
    {
        var cur = _frame;

        List<FrameInputItem>? inputs = null;
        if (_inputsByFrame.TryGetValue(cur, out var list) && list != null && list.Count > 0)
        {
            inputs = list;
        }
        else
        {
            inputs = new List<FrameInputItem>(0);
        }

        _inputsByFrame.Remove(cur);

        var evt = new FramePushedEvent(
            RoomId: _roomId,
            WorldId: _worldId,
            Frame: cur,
            Inputs: inputs);

        foreach (var o in _observers)
        {
            o.OnFramePushed(evt);
        }

        _frame = cur + 1;
        return Task.CompletedTask;
    }
}
