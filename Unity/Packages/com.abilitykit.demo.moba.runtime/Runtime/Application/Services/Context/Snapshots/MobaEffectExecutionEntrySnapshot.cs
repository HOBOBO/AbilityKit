using System;
using AbilityKit.Context;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaEffectExecutionSnapshotHook
    {
        bool IsEnabled { get; }
        void OnExecutionStarted(long traceContextId, int effectConfigId, int triggerId,
            in MobaCombatExecutionContext context);
    }

    /// <summary>Entry facts only. No original payload or mutable runtime reference is retained.</summary>
    public sealed class MobaEffectExecutionEntrySnapshot : IImmutableContextSnapshot
    {
        public MobaEffectExecutionEntrySnapshot(long traceContextId, int effectConfigId, int triggerId,
            in MobaCombatExecutionContext context)
        {
            EntityId = traceContextId;
            CreatedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Frame = context.Frame;
            EffectConfigId = effectConfigId;
            TriggerId = triggerId;
            PayloadTypeName = context.PayloadTypeName ?? string.Empty;
            HasRuntimeContext = context.TryGetRuntimeContext(out var runtime);
            RuntimeContextId = HasRuntimeContext ? runtime.ContextId : 0;
            RuntimeContextVersion = HasRuntimeContext ? runtime.Version : 0;
            // A provider can explicitly report a valid all-zero stage; do not use IsValid's value heuristic.
            if (context.Payload is IMobaTriggerStageSnapshotProvider provider && provider.TryGetStageSnapshot(out var stage))
            {
                HasStageSnapshot = true;
                Stage = stage;
            }
        }

        // EntityId belongs to the separate Trace-facts store, never ContextRegistry's ID namespace.
        public long EntityId { get; }
        public long TraceContextId => EntityId;
        public long CreatedAtMs { get; }
        public long Version => 1;
        public int Frame { get; }
        public int EffectConfigId { get; }
        public int TriggerId { get; }
        public string PayloadTypeName { get; }
        public bool HasRuntimeContext { get; }
        public long RuntimeContextId { get; }
        public long RuntimeContextVersion { get; }
        public bool HasStageSnapshot { get; }
        public MobaTriggerStageSnapshot Stage { get; }

        public bool TryGetValue<T>(string key, out T value)
        {
            object raw;
            switch (key)
            {
                case "EffectConfigId": raw = EffectConfigId; break;
                case "TriggerId": raw = TriggerId; break;
                case "RuntimeContextId": raw = RuntimeContextId; break;
                case "RuntimeContextVersion": raw = RuntimeContextVersion; break;
                case "StackCount" when HasStageSnapshot: raw = Stage.StackCount; break;
                case "ElapsedSeconds" when HasStageSnapshot: raw = Stage.ElapsedSeconds; break;
                case "RemainingSeconds" when HasStageSnapshot: raw = Stage.RemainingSeconds; break;
                case "DurationSeconds" when HasStageSnapshot: raw = Stage.DurationSeconds; break;
                default: value = default; return false;
            }
            if (raw is T typed) { value = typed; return true; }
            value = default;
            return false;
        }
    }
}
