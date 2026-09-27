using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Host
{
    public interface IWorldInputSink : IService
    {
        void Submit(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);
    }

    /// <summary>
    /// Optional input boundary for rollback replay. The replay lifecycle lets a world distinguish
    /// a retransmitted network command from a deterministic re-execution of recorded input.
    /// </summary>
    public interface IWorldInputReplaySink : IWorldInputSink
    {
        void BeginReplay(FrameIndex restoredFrame, FrameIndex replayToFrame);
        void Replay(FrameIndex frame, IReadOnlyList<PlayerInputCommand> inputs);
        void EndReplay();
    }
}
