using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Minions;
using AbilityKit.Demo.Moba.Services.StateSync;
using MemoryPack;

namespace AbilityKit.Demo.Moba.Rollback
{
    [MobaRollbackProvider(DefaultKey)]
    public sealed class MobaMinionRollbackProvider : IRollbackStateProvider, IRollbackStatePreflightProvider,
        IMobaStateRecoveryProvider
    {
        public const int DefaultKey = 10041;
        private const int CurrentPayloadVersion = 1;

        private readonly MobaMinionWaveService _waves;
        private readonly MobaMinionCombatService _combat;

        public MobaMinionRollbackProvider(MobaMinionWaveService waves, MobaMinionCombatService combat)
        {
            _waves = waves ?? throw new ArgumentNullException(nameof(waves));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public int Key => DefaultKey;
        public string Name => "Minions";

        public byte[] Export(FrameIndex frame) => ExportState(frame);
        public void Import(FrameIndex frame, byte[] payload) => ImportState(frame, payload);

        public byte[] ExportState(FrameIndex frame)
        {
            var wave = _waves.CaptureRollbackState();
            var combat = _combat.CaptureRollbackState();
            var pending = new MobaMinionPendingRollbackEntry[wave.Pending.Count];
            for (var i = 0; i < pending.Length; i++)
            {
                var item = wave.Pending[i];
                pending[i] = new MobaMinionPendingRollbackEntry(
                    item.WaveId, (int)item.Team, (int)item.Archetype, item.DueElapsedMs, item.Slot);
            }

            var active = new MobaMinionActiveRollbackEntry[wave.Active.Count];
            for (var i = 0; i < active.Length; i++)
            {
                var item = wave.Active[i];
                active[i] = new MobaMinionActiveRollbackEntry(
                    item.ActorId, item.WaveId, (int)item.Team, (int)item.Archetype, item.Slot,
                    (int)item.AiState, item.WaypointIndex, item.IsTerminal, item.BlockedSinceMs,
                    item.StallReported, item.BlockingActorId);
            }

            var cooldowns = new MobaMinionCooldownRollbackEntry[combat.AttackCooldowns.Count];
            for (var i = 0; i < cooldowns.Length; i++)
            {
                var item = combat.AttackCooldowns[i];
                cooldowns[i] = new MobaMinionCooldownRollbackEntry(item.ActorId, item.NextAttackAtMs);
            }

            var reportedDeaths = new int[combat.ReportedDeaths.Count];
            for (var i = 0; i < reportedDeaths.Length; i++) reportedDeaths[i] = combat.ReportedDeaths[i];
            return MemoryPackSerializer.Serialize(new MobaMinionRollbackPayload(
                CurrentPayloadVersion, wave.GameplayId, wave.CommittedWaveCount, wave.NextWaveOrdinal,
                wave.LastFrame, wave.ElapsedMs, pending, active, cooldowns, reportedDeaths));
        }

        public void ValidateImport(FrameIndex frame, byte[] payload)
        {
            Read(payload);
        }

        internal static MobaMinionRollbackPayload ValidatePayload(byte[] payload)
        {
            return Read(payload);
        }

        public void ImportState(FrameIndex frame, byte[] payload)
        {
            var state = Read(payload);
            var pending = new MobaMinionPendingRollbackState[state.Pending.Length];
            for (var i = 0; i < pending.Length; i++)
            {
                var item = state.Pending[i];
                pending[i] = new MobaMinionPendingRollbackState(
                    item.WaveId, (Team)item.Team, (MobaMinionArchetype)item.Archetype,
                    item.DueElapsedMs, item.Slot);
            }

            var active = new MobaMinionActiveRollbackState[state.Active.Length];
            for (var i = 0; i < active.Length; i++)
            {
                var item = state.Active[i];
                active[i] = new MobaMinionActiveRollbackState(
                    item.ActorId, item.WaveId, (Team)item.Team, (MobaMinionArchetype)item.Archetype,
                    item.Slot, (MobaMinionAiState)item.AiState, item.WaypointIndex, item.IsTerminal,
                    item.BlockedSinceMs, item.StallReported, item.BlockingActorId);
            }

            var cooldowns = new MobaMinionAttackRollbackState[state.Cooldowns.Length];
            for (var i = 0; i < cooldowns.Length; i++)
                cooldowns[i] = new MobaMinionAttackRollbackState(state.Cooldowns[i].ActorId, state.Cooldowns[i].NextAttackAtMs);
            _waves.RestoreRollbackState(new MobaMinionWaveRollbackState(
                state.GameplayId, state.CommittedWaveCount, state.NextWaveOrdinal, state.LastFrame,
                state.ElapsedMs, pending, active));
            _combat.RestoreRollbackState(new MobaMinionCombatRollbackState(
                state.GameplayId, cooldowns, state.ReportedDeaths));
        }

        public void AddStateHash(FrameIndex frame, ref MobaStateHashBuilder hash)
        {
            var payload = ExportState(frame);
            hash.AddInt(Key);
            hash.AddInt(payload.Length);
            for (var i = 0; i < payload.Length; i++) hash.AddByte(payload[i]);
        }

        private static MobaMinionRollbackPayload Read(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidOperationException("Missing minion rollback payload.");
            var state = MemoryPackSerializer.Deserialize<MobaMinionRollbackPayload>(payload);
            if (state.Version != CurrentPayloadVersion || state.GameplayId < 0 ||
                state.CommittedWaveCount < 0 || state.NextWaveOrdinal < 0 ||
                state.LastFrame < 0 || state.ElapsedMs < 0L ||
                state.Pending == null || state.Active == null || state.Cooldowns == null ||
                state.ReportedDeaths == null)
                throw new InvalidOperationException("Invalid minion rollback payload.");
            ValidateSorted(state.Pending, state.Active, state.Cooldowns, state.ReportedDeaths);
            return state;
        }

        private static void ValidateSorted(
            MobaMinionPendingRollbackEntry[] pending,
            MobaMinionActiveRollbackEntry[] active,
            MobaMinionCooldownRollbackEntry[] cooldowns,
            int[] reportedDeaths)
        {
            for (var i = 0; i < pending.Length; i++)
            {
                var item = pending[i];
                if (item.WaveId <= 0 || item.Team <= 0 ||
                    !Enum.IsDefined(typeof(MobaMinionArchetype), item.Archetype) ||
                    item.Archetype == (int)MobaMinionArchetype.None || item.DueElapsedMs < 0 || item.Slot < 0)
                    throw new InvalidOperationException("Invalid minion pending rollback entry.");
                if (i > 0 && ComparePending(pending[i - 1], item) > 0)
                    throw new InvalidOperationException("Minion pending rollback entries are not sorted.");
            }
            for (var i = 0; i < active.Length; i++)
            {
                var item = active[i];
                if (item.ActorId <= 0 || item.WaveId <= 0 || item.Team <= 0 ||
                    !Enum.IsDefined(typeof(MobaMinionArchetype), item.Archetype) ||
                    item.Archetype == (int)MobaMinionArchetype.None ||
                    !Enum.IsDefined(typeof(MobaMinionAiState), item.AiState) ||
                    item.Slot < 0 || item.WaypointIndex < 0 || item.BlockingActorId < 0 || item.BlockedSinceMs < -1)
                    throw new InvalidOperationException("Invalid minion active rollback entry.");
                if (i > 0 && active[i - 1].ActorId >= item.ActorId)
                    throw new InvalidOperationException("Minion active rollback entries are not sorted.");
            }
            for (var i = 0; i < cooldowns.Length; i++)
            {
                if (cooldowns[i].ActorId <= 0 || cooldowns[i].NextAttackAtMs < 0 ||
                    (i > 0 && cooldowns[i - 1].ActorId >= cooldowns[i].ActorId))
                    throw new InvalidOperationException("Invalid minion cooldown rollback entries.");
            }
            for (var i = 0; i < reportedDeaths.Length; i++)
                if (reportedDeaths[i] <= 0 || (i > 0 && reportedDeaths[i - 1] >= reportedDeaths[i]))
                    throw new InvalidOperationException("Invalid minion death rollback entries.");
        }

        private static int ComparePending(MobaMinionPendingRollbackEntry left, MobaMinionPendingRollbackEntry right)
        {
            var result = left.DueElapsedMs.CompareTo(right.DueElapsedMs);
            if (result != 0) return result;
            result = left.Slot.CompareTo(right.Slot);
            if (result != 0) return result;
            result = left.Team.CompareTo(right.Team);
            if (result != 0) return result;
            result = left.WaveId.CompareTo(right.WaveId);
            return result != 0 ? result : left.Archetype.CompareTo(right.Archetype);
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaMinionRollbackPayload
    {
        [MemoryPackOrder(0)] public readonly int Version;
        [MemoryPackOrder(1)] public readonly long GameplayId;
        [MemoryPackOrder(2)] public readonly int CommittedWaveCount;
        [MemoryPackOrder(3)] public readonly int NextWaveOrdinal;
        [MemoryPackOrder(4)] public readonly int LastFrame;
        [MemoryPackOrder(5)] public readonly long ElapsedMs;
        [MemoryPackOrder(6)] public readonly MobaMinionPendingRollbackEntry[] Pending;
        [MemoryPackOrder(7)] public readonly MobaMinionActiveRollbackEntry[] Active;
        [MemoryPackOrder(8)] public readonly MobaMinionCooldownRollbackEntry[] Cooldowns;
        [MemoryPackOrder(9)] public readonly int[] ReportedDeaths;

        [MemoryPackConstructor]
        public MobaMinionRollbackPayload(int version, long gameplayId, int committedWaveCount,
            int nextWaveOrdinal, int lastFrame, long elapsedMs,
            MobaMinionPendingRollbackEntry[] pending, MobaMinionActiveRollbackEntry[] active,
            MobaMinionCooldownRollbackEntry[] cooldowns, int[] reportedDeaths)
        {
            Version = version; GameplayId = gameplayId; CommittedWaveCount = committedWaveCount;
            NextWaveOrdinal = nextWaveOrdinal; LastFrame = lastFrame; ElapsedMs = elapsedMs;
            Pending = pending; Active = active; Cooldowns = cooldowns; ReportedDeaths = reportedDeaths;
        }
    }

    [MemoryPackable]
    public readonly partial struct MobaMinionPendingRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly long WaveId;
        [MemoryPackOrder(1)] public readonly int Team;
        [MemoryPackOrder(2)] public readonly int Archetype;
        [MemoryPackOrder(3)] public readonly long DueElapsedMs;
        [MemoryPackOrder(4)] public readonly int Slot;
        public MobaMinionPendingRollbackEntry(long waveId, int team, int archetype, long dueElapsedMs, int slot)
        { WaveId = waveId; Team = team; Archetype = archetype; DueElapsedMs = dueElapsedMs; Slot = slot; }
    }

    [MemoryPackable]
    public readonly partial struct MobaMinionActiveRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly long WaveId;
        [MemoryPackOrder(2)] public readonly int Team;
        [MemoryPackOrder(3)] public readonly int Archetype;
        [MemoryPackOrder(4)] public readonly int Slot;
        [MemoryPackOrder(5)] public readonly int AiState;
        [MemoryPackOrder(6)] public readonly int WaypointIndex;
        [MemoryPackOrder(7)] public readonly bool IsTerminal;
        [MemoryPackOrder(8)] public readonly long BlockedSinceMs;
        [MemoryPackOrder(9)] public readonly bool StallReported;
        [MemoryPackOrder(10)] public readonly int BlockingActorId;
        public MobaMinionActiveRollbackEntry(int actorId, long waveId, int team, int archetype, int slot,
            int aiState, int waypointIndex, bool isTerminal, long blockedSinceMs, bool stallReported,
            int blockingActorId)
        { ActorId = actorId; WaveId = waveId; Team = team; Archetype = archetype; Slot = slot; AiState = aiState; WaypointIndex = waypointIndex; IsTerminal = isTerminal; BlockedSinceMs = blockedSinceMs; StallReported = stallReported; BlockingActorId = blockingActorId; }
    }

    [MemoryPackable]
    public readonly partial struct MobaMinionCooldownRollbackEntry
    {
        [MemoryPackOrder(0)] public readonly int ActorId;
        [MemoryPackOrder(1)] public readonly long NextAttackAtMs;
        public MobaMinionCooldownRollbackEntry(int actorId, long nextAttackAtMs)
        { ActorId = actorId; NextAttackAtMs = nextAttackAtMs; }
    }
}
