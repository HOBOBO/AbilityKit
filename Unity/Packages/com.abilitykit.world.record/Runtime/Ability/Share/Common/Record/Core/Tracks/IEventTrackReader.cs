using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public interface IEventTrackReader
    {
        bool TryGetEvents(FrameIndex frame, out IReadOnlyList<RecordEvent> events);
    }
}
