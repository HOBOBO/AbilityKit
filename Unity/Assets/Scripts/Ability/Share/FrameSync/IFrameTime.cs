using System;

namespace AbilityKit.Ability.FrameSync
{
    public interface IFrameTime
    {
        FrameIndex Frame { get; }
        float DeltaTime { get; }
        float Time { get; }

        float FrameToTime(FrameIndex frame);
        FrameIndex TimeToFrame(float time);
    }
}
