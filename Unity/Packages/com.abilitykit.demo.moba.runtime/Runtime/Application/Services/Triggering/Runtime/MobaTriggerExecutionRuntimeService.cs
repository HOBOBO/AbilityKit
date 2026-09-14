using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Services.Triggering
{
    [WorldService(typeof(ITriggerExecutionScheduler))]
    [WorldService(typeof(MobaTriggerExecutionRuntimeService))]
    public sealed class MobaTriggerExecutionRuntimeService : IService, ITriggerExecutionScheduler, IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10011;

        private readonly TriggerExecutionScheduler _scheduler = new TriggerExecutionScheduler();

        public int Key => DefaultKey;
        public string Name => "TriggerExecutionScheduler";
        public int ActiveCount => _scheduler.ActiveCount;

        public TriggerScheduledExecutionHandle Schedule<TCtx>(
            ITriggerPlanExecutable executable,
            object args,
            in ExecCtx<TCtx> context,
            EScheduleMode mode,
            float intervalMs,
            int maxExecutions,
            bool canBeInterrupted)
            where TCtx : class
        {
            return _scheduler.Schedule(executable, args, in context, mode, intervalMs, maxExecutions, canBeInterrupted);
        }

        public void Tick(float deltaTimeMs) => _scheduler.Tick(deltaTimeMs);

        public bool ExecuteExternal(in TriggerScheduledExecutionHandle handle) => _scheduler.ExecuteExternal(in handle);

        public bool Interrupt(in TriggerScheduledExecutionHandle handle, string reason = null) => _scheduler.Interrupt(in handle, reason);

        public byte[] Export(FrameIndex frame) => ExportState(frame);

        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var snapshot = _scheduler.CaptureSnapshot();
            var source = snapshot.Entries;
            var entries = new MobaTriggerScheduledExecutionEntry[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var item = source[i];
                entries[i] = new MobaTriggerScheduledExecutionEntry(
                    item.Id,
                    (byte)item.State,
                    item.ElapsedMs,
                    item.LastExecutionMs,
                    item.ExecutionCount,
                    item.FailureReason);
            }

            return MemoryPackSerializer.Serialize(new MobaTriggerExecutionRuntimePayload(1, snapshot.NextId, entries));
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                _scheduler.Clear();
                return;
            }

            var state = MemoryPackSerializer.Deserialize<MobaTriggerExecutionRuntimePayload>(payload);
            if (state.Version != 1)
                throw new InvalidOperationException($"Unsupported trigger execution runtime payload version '{state.Version}'.");

            var source = state.Entries ?? Array.Empty<MobaTriggerScheduledExecutionEntry>();
            var entries = new TriggerScheduledExecutionSnapshot[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var item = source[i];
                entries[i] = new TriggerScheduledExecutionSnapshot(
                    item.Id,
                    (TriggerScheduledExecutionState)item.State,
                    item.ElapsedMs,
                    item.LastExecutionMs,
                    item.ExecutionCount,
                    item.FailureReason);
            }

            _scheduler.RestoreSnapshot(new TriggerExecutionSchedulerSnapshot(state.NextId, entries));
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }

        public void Dispose() => _scheduler.Clear();
    }

    [MemoryPackable]
    public readonly partial struct MobaTriggerExecutionRuntimePayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly long NextId;
        [MemoryPackOrder(2)] public readonly MobaTriggerScheduledExecutionEntry[] Entries;

        [MemoryPackConstructor]
        public MobaTriggerExecutionRuntimePayload(int version, long nextId, MobaTriggerScheduledExecutionEntry[] entries)
        {
            Version = version;
            NextId = nextId;
            Entries = entries ?? Array.Empty<MobaTriggerScheduledExecutionEntry>();
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaTriggerScheduledExecutionEntry
    {
        [MemoryPackOrder(0)] public readonly long Id;
        [MemoryPackOrder(1)] public readonly byte State;
        [MemoryPackOrder(2)] public readonly double ElapsedMs;
        [MemoryPackOrder(3)] public readonly double LastExecutionMs;
        [MemoryPackOrder(4)] public readonly int ExecutionCount;
        [MemoryPackOrder(5)] public readonly string FailureReason;

        [MemoryPackConstructor]
        public MobaTriggerScheduledExecutionEntry(long id, byte state, double elapsedMs, double lastExecutionMs, int executionCount, string failureReason)
        {
            Id = id;
            State = state;
            ElapsedMs = elapsedMs;
            LastExecutionMs = lastExecutionMs;
            ExecutionCount = executionCount;
            FailureReason = failureReason ?? string.Empty;
        }
    }
}
