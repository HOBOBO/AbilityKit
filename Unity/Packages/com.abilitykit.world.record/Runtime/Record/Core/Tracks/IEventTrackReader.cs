using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Core.Common.Record.Core
{
    public interface IEventTrackReader
    {
        bool TryGetEvents(FrameIndex frame, out IReadOnlyList<RecordEvent> events);
    }
}
