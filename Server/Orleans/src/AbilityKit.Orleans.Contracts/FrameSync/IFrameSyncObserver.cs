using Orleans;

namespace AbilityKit.Orleans.Contracts.FrameSync;

public interface IFrameSyncObserver : IGrainObserver
{
    void OnFramePushed(FramePushedEvent evt);
}

public interface ISettledFrameSyncObserver : IGrainObserver
{
    void OnSettledFramePushed(FrameSyncSettledFrame frame);
}
