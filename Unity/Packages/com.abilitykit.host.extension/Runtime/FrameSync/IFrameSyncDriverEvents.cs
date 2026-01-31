using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Abstractions;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public interface IFrameSyncDriverEvents
    {
        void AddInputsFlushed(Action<WorldId, FrameIndex, PlayerInputCommand[]> handler);
        void RemoveInputsFlushed(Action<WorldId, FrameIndex, PlayerInputCommand[]> handler);

        void AddPostStep(Action<FrameIndex, float> handler);
        void RemovePostStep(Action<FrameIndex, float> handler);
    }
}
