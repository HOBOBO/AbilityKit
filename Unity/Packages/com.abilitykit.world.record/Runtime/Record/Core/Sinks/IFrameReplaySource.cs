using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public interface IFrameReplaySource
    {
        bool TryGetInputs(FrameIndex frame, out IReadOnlyList<PlayerInputCommand> inputs);

        bool TryGetSnapshot(FrameIndex frame, out WorldStateSnapshot snapshot);

        bool TryGetStateHash(FrameIndex frame, out WorldStateHash hash, out int version);
    }
}
