using System;

namespace AbilityKit.Game.Battle
{
    [Flags]
    public enum BattleRuntimeCapability
    {
        None = 0,
        GameStart = 1 << 0,
        Input = 1 << 1,
        Simulation = 1 << 2,
        SnapshotOutput = 1 << 3,
        SnapshotInput = 1 << 4,
        StateReadModel = 1 << 5,
        StateHash = 1 << 6,
        BotControl = 1 << 7,
    }

    public enum BattleRuntimeState
    {
        Unknown = 0,
        Created = 1,
        Ready = 2,
        Running = 3,
        Completed = 4,
        Faulted = 5,
        Disposed = 6,
    }

    public readonly struct BattleRuntimeStatus
    {
        public BattleRuntimeStatus(
            BattleRuntimeCapability capabilities,
            BattleRuntimeState state,
            string missingDependencies = null,
            string detail = null)
        {
            Capabilities = capabilities;
            State = state;
            MissingDependencies = missingDependencies ?? string.Empty;
            Detail = detail ?? string.Empty;
        }

        public BattleRuntimeCapability Capabilities { get; }

        public BattleRuntimeState State { get; }

        public string MissingDependencies { get; }

        public string Detail { get; }

        public bool Has(BattleRuntimeCapability capability)
        {
            return (Capabilities & capability) == capability;
        }

        public bool Meets(in BattleReadinessRequirement requirement)
        {
            return Has(requirement.RequiredCapabilities) &&
                   (requirement.AllowedStates & State.ToMask()) != 0;
        }

        public override string ToString()
        {
            var result = $"State={State}, Capabilities={Capabilities}";
            if (!string.IsNullOrEmpty(MissingDependencies))
            {
                result += $", Missing={MissingDependencies}";
            }

            return string.IsNullOrEmpty(Detail) ? result : $"{result}, Detail={Detail}";
        }
    }

    [Flags]
    public enum BattleRuntimeStateMask
    {
        None = 0,
        Unknown = 1 << 0,
        Created = 1 << 1,
        Ready = 1 << 2,
        Running = 1 << 3,
        Completed = 1 << 4,
        Faulted = 1 << 5,
        Disposed = 1 << 6,
        Operational = Ready | Running,
        Any = Unknown | Created | Ready | Running | Completed | Faulted | Disposed,
    }

    public readonly struct BattleReadinessRequirement
    {
        public BattleReadinessRequirement(
            BattleRuntimeCapability requiredCapabilities,
            BattleRuntimeStateMask allowedStates = BattleRuntimeStateMask.Operational)
        {
            RequiredCapabilities = requiredCapabilities;
            AllowedStates = allowedStates;
        }

        public BattleRuntimeCapability RequiredCapabilities { get; }

        public BattleRuntimeStateMask AllowedStates { get; }

        public static BattleReadinessRequirement BattleLoop { get; } =
            new BattleReadinessRequirement(
                BattleRuntimeCapability.Input |
                BattleRuntimeCapability.Simulation |
                BattleRuntimeCapability.SnapshotOutput);

        public static BattleReadinessRequirement GameStart { get; } =
            new BattleReadinessRequirement(
                BattleRuntimeCapability.GameStart,
                BattleRuntimeStateMask.Created | BattleRuntimeStateMask.Ready);
    }

    public interface IBattleRuntimeStatusProvider
    {
        BattleRuntimeStatus BattleStatus { get; }
    }

    public static class BattleRuntimeStateExtensions
    {
        public static BattleRuntimeStateMask ToMask(this BattleRuntimeState state)
        {
            return state switch
            {
                BattleRuntimeState.Unknown => BattleRuntimeStateMask.Unknown,
                BattleRuntimeState.Created => BattleRuntimeStateMask.Created,
                BattleRuntimeState.Ready => BattleRuntimeStateMask.Ready,
                BattleRuntimeState.Running => BattleRuntimeStateMask.Running,
                BattleRuntimeState.Completed => BattleRuntimeStateMask.Completed,
                BattleRuntimeState.Faulted => BattleRuntimeStateMask.Faulted,
                BattleRuntimeState.Disposed => BattleRuntimeStateMask.Disposed,
                _ => BattleRuntimeStateMask.None,
            };
        }
    }
}
