using System;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaActionExecutionSnapshotHook
    {
        void OnActionStarted(long contextId, int actionIndex, long actionId, long sourceActorId, long targetActorId, int frame);
        void OnActionEnded(long contextId, int actionIndex, long actionId, bool succeeded, bool aborted, int frame);
    }

    public sealed class MobaActionExecutionSnapshot : IImmutableContextSnapshot
    {
        public MobaActionExecutionSnapshot(long contextId, in BattleDiagnosticActionExecutionFacts facts)
        {
            EntityId = contextId; Facts = facts;
            CreatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        public long EntityId { get; }
        public long CreatedAtMs { get; }
        public long Version => 2;
        public int Frame => Facts.HasAfter ? Facts.EndFrame : Facts.Frame;
        public BattleDiagnosticActionExecutionFacts Facts { get; }
        public bool TryGetValue<T>(string key, out T value)
        {
            object raw;
            switch (key)
            {
                case "ActionId": raw = Facts.ActionId; break;
                case "ActionIndex": raw = Facts.ActionIndex; break;
                case "Outcome": raw = Facts.Outcome; break;
                default: value = default; return false;
            }
            if (raw is T typed) { value = typed; return true; }
            value = default; return false;
        }
    }
}
