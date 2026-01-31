using Orleans;

namespace AbilityKit.Orleans.Contracts.FrameSync;

public interface IBattleFrameSyncGrain : IGrainWithStringKey
{
    Task SubscribeAsync(IFrameSyncObserver observer);

    Task UnsubscribeAsync(IFrameSyncObserver observer);

    Task SubmitInputAsync(ulong worldId, int frame, FrameInputItem input);
}
