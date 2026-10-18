using System;
using System.Collections.Generic;
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
    [AbilityKit.Demo.Moba.Rollback.MobaRollbackProvider(DefaultKey, autoResolve: true)]
    public sealed class MobaTriggerExecutionRuntimeService : IService, ITriggerExecutionScheduler,
        IRollbackStateProvider, IRollbackStatePreflightProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10019;

        private readonly TriggerExecutionScheduler _scheduler = new TriggerExecutionScheduler(retainFinishedEntries: true);
        private readonly Dictionary<long, int> _finishedAtFrame = new Dictionary<long, int>();

        public int Key => DefaultKey;
        public string Name => "TriggerExecutionScheduler";
        public int ActiveCount => _scheduler.ActiveCount;

        public bool ValidateDefinition(TriggerScheduledExecutionHandle handle) =>
            handle.IsValid && _scheduler.ContainsDefinition(handle.Id);

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

        public void TrimConfirmedHistory(FrameIndex confirmedFrame, FrameIndex predictedFrame, int rollbackHistoryFrames)
        {
            if (rollbackHistoryFrames < 0) throw new ArgumentOutOfRangeException(nameof(rollbackHistoryFrames));
            if (confirmedFrame.Value < 0 || predictedFrame.Value < confirmedFrame.Value) return;

            // A definition can be released only after its final waiting snapshot has left the ring.
            var safeFrame = Math.Min(confirmedFrame.Value, predictedFrame.Value - rollbackHistoryFrames);
            foreach (var entry in _scheduler.GetExecutions())
            {
                if (entry.State == TriggerScheduledExecutionState.Waiting)
                {
                    _finishedAtFrame.Remove(entry.Id);
                    continue;
                }

                if (!_finishedAtFrame.TryGetValue(entry.Id, out var finishedFrame))
                {
                    finishedFrame = predictedFrame.Value;
                    _finishedAtFrame.Add(entry.Id, finishedFrame);
                }
                if (finishedFrame > safeFrame) continue;
                _scheduler.Remove(entry.Id);
                _finishedAtFrame.Remove(entry.Id);
            }
        }

        public byte[] Export(FrameIndex frame) => ExportState(frame);

        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            ValidateState(ReadState(payload));
        }

        public byte[] ExportState(FrameIndex frame)
        {
            var source = _scheduler.GetExecutions();
            var entries = new List<MobaTriggerScheduledExecutionEntry>(source.Length);
            for (var i = 0; i < source.Length; i++)
            {
                var item = source[i];
                if (item.State != TriggerScheduledExecutionState.Waiting) continue;
                entries.Add(new MobaTriggerScheduledExecutionEntry(
                    item.Id,
                    (byte)item.State,
                    item.ElapsedMs,
                    item.LastExecutionMs,
                    item.ExecutionCount,
                    item.FailureReason));
            }

            return MemoryPackSerializer.Serialize(new MobaTriggerExecutionRuntimePayload(1, _scheduler.NextExecutionId, entries.ToArray()));
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                _scheduler.Clear();
                _finishedAtFrame.Clear();
                return;
            }

            var state = ReadState(payload);
            ValidateState(state);
            var retained = new HashSet<long>();
            foreach (var item in state.Entries ?? Array.Empty<MobaTriggerScheduledExecutionEntry>())
            {
                retained.Add(item.Id);
                _scheduler.SetExecutionProgress(new TriggerScheduledExecutionInfo(item.Id,
                    (TriggerScheduledExecutionState)item.State, item.ElapsedMs,
                    item.LastExecutionMs, item.ExecutionCount, item.FailureReason));
            }
            foreach (var existing in _scheduler.GetExecutions())
                if (!retained.Contains(existing.Id)) _scheduler.Remove(existing.Id);
            _scheduler.SetNextExecutionId(state.NextId);
            _finishedAtFrame.Clear();
            foreach (var item in state.Entries ?? Array.Empty<MobaTriggerScheduledExecutionEntry>())
                if (item.State != (byte)TriggerScheduledExecutionState.Waiting)
                    _finishedAtFrame[item.Id] = frame.Value;
        }

        private static MobaTriggerExecutionRuntimePayload ReadState(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing trigger execution rollback payload.");
            var state = MemoryPackSerializer.Deserialize<MobaTriggerExecutionRuntimePayload>(payload);
            if (state.Version != 1)
                throw new InvalidOperationException($"Unsupported trigger execution runtime payload version '{state.Version}'.");

            return state;
        }

        private void ValidateState(in MobaTriggerExecutionRuntimePayload state)
        {
            if (state.NextId <= 0)
                throw new InvalidOperationException("Invalid scheduled execution ID cursor.");
            var ids = new HashSet<long>();
            foreach (var entry in state.Entries ?? Array.Empty<MobaTriggerScheduledExecutionEntry>())
            {
                if (entry.Id <= 0 || entry.Id >= state.NextId || !ids.Add(entry.Id) ||
                    !_scheduler.ContainsDefinition(entry.Id) ||
                    entry.State > (byte)TriggerScheduledExecutionState.Failed ||
                    entry.ElapsedMs < 0 || double.IsNaN(entry.ElapsedMs) || double.IsInfinity(entry.ElapsedMs) ||
                    entry.LastExecutionMs < 0 || double.IsNaN(entry.LastExecutionMs) ||
                    double.IsInfinity(entry.LastExecutionMs) || entry.ExecutionCount < 0)
                    throw new InvalidOperationException(
                        $"Scheduled execution {entry.Id} cannot be restored without its in-process definition.");
            }
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }

        public void Dispose()
        {
            _scheduler.Clear();
            _finishedAtFrame.Clear();
        }
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
