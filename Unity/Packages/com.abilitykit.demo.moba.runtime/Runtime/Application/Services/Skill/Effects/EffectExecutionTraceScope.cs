using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class EffectExecutionTraceScope
    {
        public long EffectContextId;
        public int EffectConfigId;
        public int TriggerId;
        public int SourceActorId;
        public int TargetActorId;
        public bool IsRoot;
        public int CurrentActionIndex = -1;
        public long CurrentActionContextId;
        public long CurrentActionId;
        public bool IsActionDiagnosticsSampled;
        public long ActionStartTimestamp;
        public long ActionAllocatedBytesStart;
        public readonly List<long> ActionContextIds = new List<long>();
    }
}
