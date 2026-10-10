using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Map;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Deterministic;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Gameplay
{
    [WorldService(typeof(MobaGameplayRespawnService))]
    [MobaRollbackProvider(DefaultKey, autoResolve: true)]
    public sealed class MobaGameplayRespawnService : IService, IRollbackStateProvider, IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10022;

        private readonly MobaGameplayService _gameplay;
        private readonly MobaActorRegistry _actors;
        private readonly IMobaMapRuntimeService _map;
        private readonly MobaUnitLifecycleService _lifecycle;
        private readonly Dictionary<int, PendingRespawn> _pending = new Dictionary<int, PendingRespawn>();
        private readonly Dictionary<int, int> _respawnCounts = new Dictionary<int, int>();
        private readonly IDisposable _startedSubscription;
        private readonly IDisposable _endedSubscription;
        private readonly IDisposable _respawnedSubscription;

        public MobaGameplayRespawnService(MobaGameplayService gameplay, MobaActorRegistry actors,
            IMobaMapRuntimeService map, MobaUnitLifecycleService lifecycle,
            AbilityKit.Triggering.Eventing.IEventBus events)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            _actors = actors ?? throw new ArgumentNullException(nameof(actors));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            if (events != null)
            {
                _startedSubscription = events.Subscribe(GameplayTriggerEvents.GetKey(
                    GameplayTriggerEvents.Started), _ => Reset());
                _endedSubscription = events.Subscribe(GameplayTriggerEvents.GetKey(
                    GameplayTriggerEvents.Ended), _ => ClearPending());
                var respawnId = TriggeringIdUtil.GetEventEid(MobaUnitTriggering.Events.Respawn);
                _respawnedSubscription = events.Subscribe(new EventKey<UnitEventPayload>(respawnId),
                    payload => _pending.Remove(payload.ActorId));
            }
        }

        public int Key => DefaultKey;
        public string Name => "GameplayRespawn";

        public bool TrySchedule(in UnitDieEventPayload death, int delayMilliseconds,
            int spawnIndex, float healthRatio, int maxRespawns)
        {
            if (!_gameplay.IsRunning || death.ActorId <= 0 || delayMilliseconds < 0 ||
                spawnIndex < 0 || healthRatio <= 0f || healthRatio > 1f || maxRespawns < 0 ||
                _pending.ContainsKey(death.ActorId) ||
                !_actors.TryGet(death.ActorId, out var actor) || actor == null ||
                !actor.hasTeam || !actor.hasUnitSubType || !actor.hasAttributeGroup ||
                !actor.hasResourceContainer || actor.resourceContainer.Value == null ||
                actor.unitSubType.Value != UnitSubType.Hero ||
                (actor.team.Value != Team.Team1 && actor.team.Value != Team.Team2) ||
                new MobaAttrs(actor).Hp > 0f ||
                !_map.TryGetTeamSpawnPoint((int)actor.team.Value, spawnIndex, out _))
                return false;

            _respawnCounts.TryGetValue(death.ActorId, out var count);
            if (maxRespawns > 0 && count >= maxRespawns) return false;

            var dueRaw = checked(_gameplay.ElapsedRaw + Fixed64.FromRatio(delayMilliseconds, 1000).RawValue);
            _pending.Add(death.ActorId, new PendingRespawn(death.ActorId, (int)actor.team.Value,
                spawnIndex, healthRatio, maxRespawns, dueRaw));
            return true;
        }

        public void Tick()
        {
            if (!_gameplay.IsRunning)
            {
                ClearPending();
                return;
            }

            var due = new List<int>();
            foreach (var item in _pending)
                if (item.Value.DueElapsedRaw <= _gameplay.ElapsedRaw) due.Add(item.Key);
            due.Sort();
            foreach (var actorId in due)
            {
                if (!_pending.TryGetValue(actorId, out var pending)) continue;
                _pending.Remove(actorId);
                if (!_map.TryGetTeamSpawnPoint(pending.TeamId, pending.SpawnIndex, out var spawn))
                    continue;
                try
                {
                    var position = spawn.Position;
                    if (!_lifecycle.TryRespawn(actorId, in position, pending.HealthRatio).Succeeded)
                        continue;
                    _respawnCounts.TryGetValue(actorId, out var count);
                    _respawnCounts[actorId] = checked(count + 1);
                }
                catch (Exception ex)
                {
                    Log.Exception(ex, $"[MobaGameplayRespawnService] respawn failed. actorId={actorId}");
                }
            }
        }

        public GameplayRespawnEntry[] CapturePending()
        {
            if (!_gameplay.IsRunning) return Array.Empty<GameplayRespawnEntry>();
            var entries = new GameplayRespawnEntry[_pending.Count];
            var index = 0;
            foreach (var item in _pending)
            {
                var raw = Math.Max(0L, item.Value.DueElapsedRaw - _gameplay.ElapsedRaw);
                var milliseconds = Fixed64.FromRaw(raw).ToDouble() * 1000d;
                entries[index++] = new GameplayRespawnEntry(item.Key,
                    (int)Math.Min(int.MaxValue, Math.Ceiling(milliseconds)));
            }
            Array.Sort(entries, (left, right) => left.ActorId.CompareTo(right.ActorId));
            return entries;
        }

        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var pending = new MobaPendingRespawnState[_pending.Count];
            var index = 0;
            foreach (var item in _pending) pending[index++] = item.Value.ToState();
            Array.Sort(pending, (left, right) => left.ActorId.CompareTo(right.ActorId));

            var counts = new MobaRespawnCountState[_respawnCounts.Count];
            index = 0;
            foreach (var item in _respawnCounts)
                counts[index++] = new MobaRespawnCountState(item.Key, item.Value);
            Array.Sort(counts, (left, right) => left.ActorId.CompareTo(right.ActorId));
            return MemoryPackSerializer.Serialize(new MobaGameplayRespawnRollbackPayload(1, pending, counts));
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing gameplay respawn rollback payload.");
            var state = MemoryPackSerializer.Deserialize<MobaGameplayRespawnRollbackPayload>(payload);
            if (state.Version != 1 || state.Pending == null || state.Counts == null)
                throw new InvalidOperationException("Invalid gameplay respawn rollback payload.");

            var pending = new Dictionary<int, PendingRespawn>();
            foreach (var item in state.Pending)
            {
                if (item.ActorId <= 0 || item.TeamId < 1 || item.TeamId > 2 ||
                    item.SpawnIndex < 0 || item.HealthRatio <= 0f || item.HealthRatio > 1f ||
                    item.MaxRespawns < 0 || item.DueElapsedRaw < 0 ||
                    !pending.TryAdd(item.ActorId, PendingRespawn.FromState(in item)))
                    throw new InvalidOperationException("Invalid pending respawn rollback entry.");
            }
            var counts = new Dictionary<int, int>();
            foreach (var item in state.Counts)
                if (item.ActorId <= 0 || item.Count < 0 || !counts.TryAdd(item.ActorId, item.Count))
                    throw new InvalidOperationException("Invalid respawn count rollback entry.");

            _pending.Clear();
            foreach (var item in pending) _pending.Add(item.Key, item.Value);
            _respawnCounts.Clear();
            foreach (var item in counts) _respawnCounts.Add(item.Key, item.Value);
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            foreach (var value in payload) hash.AddByte(value);
        }

        private void ClearPending() => _pending.Clear();

        private void Reset()
        {
            _pending.Clear();
            _respawnCounts.Clear();
        }

        public void Dispose()
        {
            _startedSubscription?.Dispose();
            _endedSubscription?.Dispose();
            _respawnedSubscription?.Dispose();
            Reset();
        }

        private readonly struct PendingRespawn
        {
            public readonly int ActorId;
            public readonly int TeamId;
            public readonly int SpawnIndex;
            public readonly float HealthRatio;
            public readonly int MaxRespawns;
            public readonly long DueElapsedRaw;

            public PendingRespawn(int actorId, int teamId, int spawnIndex, float healthRatio,
                int maxRespawns, long dueElapsedRaw)
            {
                ActorId = actorId;
                TeamId = teamId;
                SpawnIndex = spawnIndex;
                HealthRatio = healthRatio;
                MaxRespawns = maxRespawns;
                DueElapsedRaw = dueElapsedRaw;
            }

            public MobaPendingRespawnState ToState() =>
                new MobaPendingRespawnState(ActorId, TeamId, SpawnIndex, HealthRatio,
                    MaxRespawns, DueElapsedRaw);

            public static PendingRespawn FromState(in MobaPendingRespawnState state) =>
                new PendingRespawn(state.ActorId, state.TeamId, state.SpawnIndex,
                    state.HealthRatio, state.MaxRespawns, state.DueElapsedRaw);
        }
    }

    public readonly struct GameplayRespawnEntry
    {
        public readonly int ActorId;
        public readonly int RemainingMilliseconds;

        public GameplayRespawnEntry(int actorId, int remainingMilliseconds)
        {
            ActorId = actorId;
            RemainingMilliseconds = remainingMilliseconds;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaGameplayRespawnRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly MobaPendingRespawnState[] Pending;
        [MemoryPackOrder(2)] public readonly MobaRespawnCountState[] Counts;

        [MemoryPackConstructor]
        public MobaGameplayRespawnRollbackPayload(int version, MobaPendingRespawnState[] pending,
            MobaRespawnCountState[] counts)
        {
            Version = version;
            Pending = pending;
            Counts = counts;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaPendingRespawnState
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly int TeamId;
        [MemoryPackOrder(2)] public readonly int SpawnIndex;
        [MemoryPackOrder(3)] public readonly float HealthRatio;
        [MemoryPackOrder(4)] public readonly int MaxRespawns;
        [MemoryPackOrder(5)] public readonly long DueElapsedRaw;

        public MobaPendingRespawnState(int actorId, int teamId, int spawnIndex,
            float healthRatio, int maxRespawns, long dueElapsedRaw)
        {
            ActorId = actorId;
            TeamId = teamId;
            SpawnIndex = spawnIndex;
            HealthRatio = healthRatio;
            MaxRespawns = maxRespawns;
            DueElapsedRaw = dueElapsedRaw;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaRespawnCountState
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly int Count;

        public MobaRespawnCountState(int actorId, int count)
        {
            ActorId = actorId;
            Count = count;
        }
    }
}
