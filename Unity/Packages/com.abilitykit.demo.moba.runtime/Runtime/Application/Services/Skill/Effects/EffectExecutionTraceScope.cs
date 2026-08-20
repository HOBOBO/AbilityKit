using System.Collections.Generic;
using AbilityKit.Diagnostics;

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
        public EffectExecutionPerformanceScopes PerformanceScopes;
        public readonly List<long> ActionContextIds = new List<long>();
    }

    internal sealed class EffectExecutionPerformanceScopes
    {
        public ProbeScope Effect;
        public ProbeScope Action;
    }
}
