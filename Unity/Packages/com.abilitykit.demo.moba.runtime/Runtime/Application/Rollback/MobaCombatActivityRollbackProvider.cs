using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Services;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    /// <summary>
    /// 恢复 Actor 的最近战斗时间。完整快照可使用联合生命周期验证的目标名单，
    /// 单份导入仍要求当前完整名单一致。
    /// </summary>
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaCombatActivityRollbackProvider : IRollbackStateProvider, IRollbackSnapshotPreflightProvider
    {
        public const int DefaultKey = 10045;
        private readonly MobaCombatActivityService _combat;
        private readonly MobaActorRegistry _actors;

        public MobaCombatActivityRollbackProvider(MobaCombatActivityService combat, MobaActorRegistry actors)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        }

        public int Key => DefaultKey;
        public byte[] Export(FrameIndex frame)
        {
            var times = _combat.CaptureCombatTimes();
            var ids = new List<int>(times.Keys);
            ids.Sort();
            var entries = new MobaCombatActivityRollbackEntry[ids.Count];
            for (var i = 0; i < ids.Count; i++)
                entries[i] = new MobaCombatActivityRollbackEntry(ids[i], times[ids[i]]);
            var state = new MobaCombatActivityRollbackPayload(1, frame.Value, _combat.CurrentTime,
                _actors.CopyActorIdsInOrder().ToArray(), entries);
            Prepare(frame, state);
            return MemoryPackSerializer.Serialize(state);
        }

        /// <summary>
        /// 独立预检保持严格名单校验，不使用快照预检的历史放行状态。
        /// </summary>
        public void ValidateImport(FrameIndex frame, byte[] payload) => Read(frame, payload);
        public void Import(FrameIndex frame, byte[] payload) => _combat.RestoreCombatTimes(Read(frame, payload));

        /// <summary>
        /// 跨名单必须获得同份联合生命周期及其 Command 证明；没有联合所有者时保持原地合同。
        /// </summary>
        public void ValidateSnapshot(RollbackSnapshotPreflightContext context)
        {
            var payload = context.RequirePayload(Key).ToArray();
            if (context.Providers.TryGetValue(MobaSkillLifecycleRollbackProvider.DefaultKey, out var provider) &&
                provider is MobaSkillLifecycleRollbackProvider lifecycle)
            {
                var state = MemoryPackSerializer.Deserialize<MobaCombatActivityRollbackPayload>(payload);
                lifecycle.ValidateProjectedActorRoster(context, state.ActorIds);
                Read(context.Frame, payload, state.ActorIds);
            }
            else Read(context.Frame, payload);
        }

        private Dictionary<int, float> Read(FrameIndex frame, byte[] payload, int[] projectedActors = null)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing combat activity rollback payload.");
            var state = MemoryPackSerializer.Deserialize<MobaCombatActivityRollbackPayload>(payload);
            var prepared = Prepare(frame, state, projectedActors);
            if (!payload.AsSpan().SequenceEqual(MemoryPackSerializer.Serialize(state)))
                throw new InvalidOperationException("Invalid combat activity rollback encoding.");
            return prepared;
        }

        private Dictionary<int, float> Prepare(FrameIndex frame, MobaCombatActivityRollbackPayload state,
            int[] projectedActors = null)
        {
            if (state.Version != 1 || state.Frame != frame.Value || state.Frame < 0 ||
                !MobaWorldClockRollbackProvider.IsValidTime(state.CapturedTime) ||
                state.ActorIds == null || state.Entries == null || state.Entries.Length > state.ActorIds.Length)
                throw new InvalidOperationException("Invalid combat activity rollback state.");
            IReadOnlyList<int> live = projectedActors ?? (IReadOnlyList<int>)_actors.CopyActorIdsInOrder();
            if (live.Count != state.ActorIds.Length)
                throw new InvalidOperationException("Combat activity rollback requires the same complete actor roster.");
            for (var i = 0; i < live.Count; i++)
                if (state.ActorIds[i] <= 0 || state.ActorIds[i] != live[i] ||
                    (i > 0 && state.ActorIds[i - 1] >= state.ActorIds[i]) ||
                    projectedActors == null &&
                    (!_actors.TryGet(state.ActorIds[i], out var actor) || !actor.isEnabled))
                    throw new InvalidOperationException("Invalid combat activity rollback actor roster.");

            var prepared = new Dictionary<int, float>(state.Entries.Length);
            for (var i = 0; i < state.Entries.Length; i++)
            {
                var entry = state.Entries[i];
                if (Array.BinarySearch(state.ActorIds, entry.ActorId) < 0 ||
                    (i > 0 && state.Entries[i - 1].ActorId >= entry.ActorId) ||
                    !MobaWorldClockRollbackProvider.IsValidTime(entry.Time) || entry.Time > state.CapturedTime)
                    throw new InvalidOperationException("Invalid combat activity rollback entry.");
                prepared.Add(entry.ActorId, entry.Time);
            }
            return prepared;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaCombatActivityRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly int Frame;
        [MemoryPackOrder(2)] public readonly float CapturedTime;
        [MemoryPackOrder(3)] public readonly int[] ActorIds;
        [MemoryPackOrder(4)] public readonly MobaCombatActivityRollbackEntry[] Entries;

        [MemoryPackConstructor]
        public MobaCombatActivityRollbackPayload(int version, int frame, float capturedTime,
            int[] actorIds, MobaCombatActivityRollbackEntry[] entries)
        { Version = version; Frame = frame; CapturedTime = capturedTime; ActorIds = actorIds; Entries = entries; }
    }

    [MemoryPackable]
    public readonly partial struct MobaCombatActivityRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly float Time;
        public MobaCombatActivityRollbackEntry(int actorId, float time) { ActorId = actorId; Time = time; }
    }
}
