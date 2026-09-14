using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaSkillWindowEndReason
    {
        None = 0,
        Completed = 1,
        Recast = 2,
        InputReleased = 3,
        EventReceived = 4,
        Timeout = 5,
        Cancelled = 6,
        Failed = 7,
    }

    public readonly struct MobaSkillWindowSnapshot
    {
        public MobaSkillWindowSnapshot(
            int windowId,
            SkillWindowKind kind,
            int elapsedMs,
            int chargeTier,
            int channelTick,
            int recastSequence,
            int eventSequence,
            int eventId,
            int commitSequence,
            int commitId,
            MobaSkillWindowEndReason endReason)
        {
            WindowId = windowId;
            Kind = kind;
            ElapsedMs = elapsedMs;
            ChargeTier = chargeTier;
            ChannelTick = channelTick;
            RecastSequence = recastSequence;
            EventSequence = eventSequence;
            EventId = eventId;
            CommitSequence = commitSequence;
            CommitId = commitId;
            EndReason = endReason;
        }

        public int WindowId { get; }
        public SkillWindowKind Kind { get; }
        public int ElapsedMs { get; }
        public int ChargeTier { get; }
        public int ChannelTick { get; }
        public int RecastSequence { get; }
        public int EventSequence { get; }
        public int EventId { get; }
        public int CommitSequence { get; }
        public int CommitId { get; }
        public MobaSkillWindowEndReason EndReason { get; }
        public bool IsOpen => WindowId != 0 && EndReason == MobaSkillWindowEndReason.None;
        public bool IsCommitted => CommitSequence > 0;
    }

    /// <summary>
    /// Owns stable skill-window facts. Data is stored on the cast runtime Blackboard and therefore
    /// follows the existing runtime lifetime, diagnostics and rollback path.
    /// </summary>
    [WorldService(typeof(MobaSkillWindowRuntimeService))]
    public sealed class MobaSkillWindowRuntimeService : IService
    {
        private const int OwnerModuleId = 1001;

        public static readonly MobaSkillRuntimeBlackboardKey ActiveWindowId = Key(100, "skill.window.id", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey WindowKind = Key(101, "skill.window.kind", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey WindowElapsedMs = Key(102, "skill.window.elapsedMs", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey ChargeTier = Key(103, "skill.window.chargeTier", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey ChannelTick = Key(104, "skill.window.channelTick", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey RecastSequence = Key(105, "skill.window.recastSequence", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey EventSequence = Key(106, "skill.window.eventSequence", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey LastEventId = Key(107, "skill.window.lastEventId", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey WindowEndReason = Key(108, "skill.window.endReason", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey CommitSequence = Key(109, "skill.commit.sequence", MobaSkillRuntimeValueKind.Int);
        public static readonly MobaSkillRuntimeBlackboardKey LastCommitId = Key(110, "skill.commit.id", MobaSkillRuntimeValueKind.Int);

        private readonly MobaSkillCastRuntimeService _runtimes;

        public MobaSkillWindowRuntimeService(MobaSkillCastRuntimeService runtimes)
        {
            _runtimes = runtimes;
        }

        public bool Open(in MobaSkillCastRuntimeHandle handle, string windowId, SkillWindowKind kind)
        {
            if (!TryGetBoard(in handle, out var board)) return false;
            board.SetInt(in ActiveWindowId, StableId("window:", windowId));
            board.SetInt(in WindowKind, (int)kind);
            board.SetInt(in WindowElapsedMs, 0);
            board.SetInt(in ChargeTier, 0);
            board.SetInt(in ChannelTick, 0);
            board.SetInt(in WindowEndReason, (int)MobaSkillWindowEndReason.None);
            return true;
        }

        public bool Advance(in MobaSkillCastRuntimeHandle handle, int elapsedMs, int chargeTier, int channelTick)
        {
            if (!TryGetBoard(in handle, out var board)) return false;
            board.SetInt(in WindowElapsedMs, elapsedMs < 0 ? 0 : elapsedMs);
            board.SetInt(in ChargeTier, chargeTier < 0 ? 0 : chargeTier);
            board.SetInt(in ChannelTick, channelTick < 0 ? 0 : channelTick);
            return true;
        }

        public bool Close(in MobaSkillCastRuntimeHandle handle, MobaSkillWindowEndReason reason)
        {
            if (!TryGetBoard(in handle, out var board)) return false;
            board.SetInt(in WindowEndReason, (int)(reason == MobaSkillWindowEndReason.None ? MobaSkillWindowEndReason.Completed : reason));
            return true;
        }

        public bool TrySignalRecast(in MobaSkillCastRuntimeHandle handle)
        {
            if (!TryGetBoard(in handle, out var board) || !IsOpenKind(board, SkillWindowKind.Recast)) return false;
            board.AddInt(in RecastSequence);
            return true;
        }

        public bool SignalEvent(in MobaSkillCastRuntimeHandle handle, string eventId)
        {
            if (!TryGetBoard(in handle, out var board)) return false;
            board.SetInt(in LastEventId, StableId("event:", eventId));
            board.AddInt(in EventSequence);
            return true;
        }

        public bool Commit(in MobaSkillCastRuntimeHandle handle, string commitId)
        {
            if (!TryGetBoard(in handle, out var board)) return false;
            board.SetInt(in LastCommitId, StableId("commit:", commitId));
            board.AddInt(in CommitSequence);
            return true;
        }

        public bool TryGetSnapshot(in MobaSkillCastRuntimeHandle handle, out MobaSkillWindowSnapshot snapshot)
        {
            snapshot = default;
            if (!TryGetBoard(in handle, out var board)) return false;
            board.TryGetInt(in ActiveWindowId, out var windowId);
            board.TryGetInt(in WindowKind, out var kind);
            board.TryGetInt(in WindowElapsedMs, out var elapsedMs);
            board.TryGetInt(in ChargeTier, out var chargeTier);
            board.TryGetInt(in ChannelTick, out var channelTick);
            board.TryGetInt(in RecastSequence, out var recastSequence);
            board.TryGetInt(in EventSequence, out var eventSequence);
            board.TryGetInt(in LastEventId, out var eventId);
            board.TryGetInt(in CommitSequence, out var commitSequence);
            board.TryGetInt(in LastCommitId, out var commitId);
            board.TryGetInt(in WindowEndReason, out var endReason);
            snapshot = new MobaSkillWindowSnapshot(windowId, (SkillWindowKind)kind, elapsedMs, chargeTier,
                channelTick, recastSequence, eventSequence, eventId, commitSequence, commitId,
                (MobaSkillWindowEndReason)endReason);
            return true;
        }

        public void Dispose()
        {
        }

        private bool TryGetBoard(in MobaSkillCastRuntimeHandle handle, out MobaSkillRuntimeBlackboard board)
        {
            board = null;
            return _runtimes != null && _runtimes.TryGetBlackboard(in handle, out board) && board != null;
        }

        private static bool IsOpenKind(MobaSkillRuntimeBlackboard board, SkillWindowKind expected)
        {
            return board.TryGetInt(in ActiveWindowId, out var windowId) && windowId != 0 &&
                   board.TryGetInt(in WindowKind, out var kind) && kind == (int)expected &&
                   (!board.TryGetInt(in WindowEndReason, out var endReason) || endReason == 0);
        }

        private static MobaSkillRuntimeBlackboardKey Key(int id, string name, MobaSkillRuntimeValueKind kind)
        {
            return new MobaSkillRuntimeBlackboardKey(id, name, kind, MobaSkillRuntimeBlackboardScope.Cast,
                MobaSkillRuntimeBlackboardFlags.Rollback | MobaSkillRuntimeBlackboardFlags.Debug | MobaSkillRuntimeBlackboardFlags.NetworkSync,
                OwnerModuleId);
        }

        private static int StableId(string prefix, string value)
        {
            return string.IsNullOrWhiteSpace(value) ? 0 : StableStringId.Get(prefix + value.Trim());
        }
    }
}
