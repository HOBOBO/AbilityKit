using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Ability.Host.Extensions.Moba.Runtime;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Coordinator;
using AbilityKit.Demo.Moba.Gameplay;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaLogicWorldDriveBlockReason
    {
        None = 0,
        InvalidDeltaTime = 1,
        MissingPhaseService = 2,
        NotInGame = 3,
        MissingRuntimePort = 4,
        RuntimePortNotReady = 5,
        RuntimeValidationBlocked = 6,
        MissingDriveState = 7,
        Paused = 8,
        SettlementReached = 9,
        ReplayNotReady = 10,
        AuthorityDoesNotOwnSimulation = 11,
    }

    [WorldService(typeof(MobaLogicWorldDriveStateService), WorldLifetime.Scoped)]
    public sealed class MobaLogicWorldDriveStateService : IService
    {
        public bool IsPaused { get; private set; }
        public bool IsReplayMode { get; private set; }
        public bool IsReplayReady { get; private set; } = true;
        public bool OwnsSimulation { get; private set; } = true;
        public MobaBattleLaunchSyncMode SyncMode { get; private set; } =
            MobaBattleLaunchSyncMode.FrameSync;
        public MobaBattleLaunchAuthorityMode AuthorityMode { get; private set; } =
            MobaBattleLaunchAuthorityMode.LocalAuthority;
        public string LastChangeReason { get; private set; }
        public long Revision { get; private set; }

        public void Configure(
            MobaBattleLaunchSyncMode syncMode,
            MobaBattleLaunchAuthorityMode authorityMode,
            bool ownsSimulation,
            bool replayMode,
            bool replayReady,
            string reason = null)
        {
            SyncMode = syncMode;
            AuthorityMode = authorityMode;
            OwnsSimulation = ownsSimulation;
            IsReplayMode = replayMode;
            IsReplayReady = !replayMode || replayReady;
            IsPaused = false;
            Touch(reason ?? "logic world drive profile configured");
        }

        public void SetPaused(bool paused, string reason = null)
        {
            if (IsPaused == paused) return;

            IsPaused = paused;
            Touch(reason ?? (paused ? "logic world paused" : "logic world resumed"));
        }

        public void SetReplayReady(bool ready, string reason = null)
        {
            if (IsReplayReady == ready) return;

            IsReplayReady = ready;
            Touch(reason ?? (ready ? "replay input ready" : "replay input unavailable"));
        }

        public void Dispose()
        {
            IsPaused = false;
            IsReplayMode = false;
            IsReplayReady = true;
            OwnsSimulation = true;
            SyncMode = MobaBattleLaunchSyncMode.FrameSync;
            AuthorityMode = MobaBattleLaunchAuthorityMode.LocalAuthority;
            LastChangeReason = "disposed";
            Revision = 0L;
        }

        public override string ToString()
        {
            return
                $"paused={IsPaused}, replay={IsReplayMode}, replayReady={IsReplayReady}, " +
                $"ownsSimulation={OwnsSimulation}, sync={SyncMode}, authority={AuthorityMode}, " +
                $"revision={Revision}, reason={LastChangeReason}";
        }

        private void Touch(string reason)
        {
            LastChangeReason = reason;
            Revision++;
        }
    }

    public readonly struct MobaLogicWorldDriveDecision
    {
        public static readonly MobaLogicWorldDriveDecision Allowed = new MobaLogicWorldDriveDecision(true, MobaLogicWorldDriveBlockReason.None, null);

        public readonly bool CanDrive;
        public readonly MobaLogicWorldDriveBlockReason BlockReason;
        public readonly string Message;

        public MobaLogicWorldDriveDecision(bool canDrive, MobaLogicWorldDriveBlockReason blockReason, string message)
        {
            CanDrive = canDrive;
            BlockReason = blockReason;
            Message = message;
        }

        public static MobaLogicWorldDriveDecision Block(MobaLogicWorldDriveBlockReason reason, string message)
        {
            return new MobaLogicWorldDriveDecision(false, reason, message);
        }

        public override string ToString()
        {
            return CanDrive ? "Allowed" : $"Blocked(reason={BlockReason}, message={Message})";
        }
    }

    [WorldService(typeof(ILogicWorldDriveGate), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaLogicWorldDriveGate), WorldLifetime.Scoped)]
    public sealed class MobaLogicWorldDriveGate : ILogicWorldDriveGate
    {
        [WorldInject(required: false)] private MobaLogicWorldRunGateService _phase = null;
        [WorldInject(required: false)] private MobaLogicWorldDriveStateService _driveState = null;
        [WorldInject(required: false)] private MobaGameplayService _gameplay = null;
        [WorldInject(required: false)] private IMobaBattleRuntimePort _runtime = null;
        [WorldInject(required: false)] private IMobaRuntimeValidationHistory _validationHistory = null;

        private MobaLogicWorldDriveBlockReason _lastLoggedReason = MobaLogicWorldDriveBlockReason.None;
        private string _lastLoggedMessage;

        public MobaLogicWorldDriveDecision Evaluate(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
            {
                return MobaLogicWorldDriveDecision.Block(MobaLogicWorldDriveBlockReason.InvalidDeltaTime, $"deltaTime must be finite and non-negative. deltaTime={deltaTime}");
            }

            if (_phase == null)
            {
                return MobaLogicWorldDriveDecision.Block(MobaLogicWorldDriveBlockReason.MissingPhaseService, "MobaLogicWorldRunGateService is required before driving the logic world.");
            }

            if (!_phase.InGame)
            {
                return MobaLogicWorldDriveDecision.Block(MobaLogicWorldDriveBlockReason.NotInGame, "logic world battle loop is not enabled. " + _phase);
            }

            if (_driveState == null)
            {
                return MobaLogicWorldDriveDecision.Block(MobaLogicWorldDriveBlockReason.MissingDriveState, "MobaLogicWorldDriveStateService is required before driving the logic world.");
            }

            if (_driveState.IsPaused)
            {
                return MobaLogicWorldDriveDecision.Block(MobaLogicWorldDriveBlockReason.Paused, "logic world drive is paused. " + _driveState);
            }

            if (_gameplay != null &&
                (_gameplay.Phase == MobaGameplayPhase.Ending ||
                 _gameplay.Phase == MobaGameplayPhase.Ended))
            {
                return MobaLogicWorldDriveDecision.Block(
                    MobaLogicWorldDriveBlockReason.SettlementReached,
                    $"gameplay settlement has been reached. phase={_gameplay.Phase}, result={_gameplay.LastResult}");
            }

            if (_driveState.IsReplayMode && !_driveState.IsReplayReady)
            {
                return MobaLogicWorldDriveDecision.Block(MobaLogicWorldDriveBlockReason.ReplayNotReady, "replay world is waiting for validated replay input. " + _driveState);
            }

            if (!_driveState.OwnsSimulation)
            {
                return MobaLogicWorldDriveDecision.Block(
                    MobaLogicWorldDriveBlockReason.AuthorityDoesNotOwnSimulation,
                    "current authority profile does not own local logic simulation. " + _driveState);
            }

            if (_runtime == null)
            {
                return MobaLogicWorldDriveDecision.Block(MobaLogicWorldDriveBlockReason.MissingRuntimePort, "IMobaBattleRuntimePort is required before driving the logic world.");
            }

            var status = _runtime.Status;
            if (!status.IsReadyForBattleLoop)
            {
                return MobaLogicWorldDriveDecision.Block(MobaLogicWorldDriveBlockReason.RuntimePortNotReady, "battle runtime port is not ready for the battle loop. " + status);
            }

            if (_validationHistory != null && _validationHistory.TryGetLastReport(out var report) && report != null && report.ShouldBlockStartup)
            {
                return MobaLogicWorldDriveDecision.Block(MobaLogicWorldDriveBlockReason.RuntimeValidationBlocked, "last runtime validation report blocks startup. " + report.FormatSummary());
            }

            return MobaLogicWorldDriveDecision.Allowed;
        }

        public bool CanDriveLogicWorld(float deltaTime)
        {
            var decision = Evaluate(deltaTime);
            if (decision.CanDrive)
            {
                _lastLoggedReason = MobaLogicWorldDriveBlockReason.None;
                _lastLoggedMessage = null;
                return true;
            }

            LogBlockedOnce(in decision);
            return false;
        }

        private void LogBlockedOnce(in MobaLogicWorldDriveDecision decision)
        {
            if (_lastLoggedReason == decision.BlockReason && string.Equals(_lastLoggedMessage, decision.Message, System.StringComparison.Ordinal))
            {
                return;
            }

            _lastLoggedReason = decision.BlockReason;
            _lastLoggedMessage = decision.Message;
            MobaRuntimeLog.Warning(MobaRuntimeLogModule.Session, MobaRuntimeLogPurpose.Validation, nameof(MobaLogicWorldDriveGate), "Logic world drive blocked. " + decision);
        }
    }
}
