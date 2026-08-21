using System;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    internal enum BattleSessionLifecycleState
    {
        Created,
        Starting,
        Running,
        Stopping,
        Stopped,
        Faulted,
    }

    internal sealed class BattleSessionState
    {
        private readonly SessionLifecycleDiagnosticsRecorder _lifecycleDiagnostics =
            new SessionLifecycleDiagnosticsRecorder();

        public BattleSessionLifecycleState Lifecycle { get; private set; } = BattleSessionLifecycleState.Created;
        public Exception LastLifecycleFailure { get; private set; }
        public int Generation { get; private set; }
        internal SessionLifecycleDiagnosticsRecorder LifecycleDiagnostics => _lifecycleDiagnostics;
        internal SessionLifecycleDiagnosticsSnapshot LifecycleDiagnosticsSnapshot =>
            _lifecycleDiagnostics.Snapshot;

        public void BeginStart()
        {
            if (Lifecycle == BattleSessionLifecycleState.Starting || Lifecycle == BattleSessionLifecycleState.Stopping)
            {
                throw new InvalidOperationException($"Cannot start battle session while lifecycle is {Lifecycle}.");
            }

            Generation++;
            LastLifecycleFailure = null;
            Lifecycle = BattleSessionLifecycleState.Starting;
            _lifecycleDiagnostics.BeginGeneration(
                Generation,
                SessionLifecycleDiagnosticState.Starting);
        }

        public void CompleteStart()
        {
            if (Lifecycle != BattleSessionLifecycleState.Starting)
            {
                throw new InvalidOperationException($"Cannot complete battle session start while lifecycle is {Lifecycle}.");
            }

            Lifecycle = BattleSessionLifecycleState.Running;
            _lifecycleDiagnostics.Transition(SessionLifecycleDiagnosticState.Running);
        }

        public void BeginStop()
        {
            if (Lifecycle != BattleSessionLifecycleState.Stopping)
            {
                Lifecycle = BattleSessionLifecycleState.Stopping;
                _lifecycleDiagnostics.Transition(SessionLifecycleDiagnosticState.Stopping);
            }
        }

        public void CompleteStop()
        {
            LastLifecycleFailure = null;
            Lifecycle = BattleSessionLifecycleState.Stopped;
            _lifecycleDiagnostics.Transition(SessionLifecycleDiagnosticState.Stopped);
        }

        public void Fault(Exception exception)
        {
            LastLifecycleFailure = exception ?? throw new ArgumentNullException(nameof(exception));
            Lifecycle = BattleSessionLifecycleState.Faulted;
            _lifecycleDiagnostics.RecordFailure(exception);
            _lifecycleDiagnostics.Transition(SessionLifecycleDiagnosticState.Faulted);
        }
        internal sealed class TickState
        {
            public int LastFrame;
            public float TickAcc;
            public int LastUpdateSteps;
            public int BacklogSteps;
            public long OverBudgetUpdateCount;
            public double DroppedTimeSeconds;
            public long InvalidDeltaCount;
            public bool WorldReady;
            public bool FirstFrameReceived;

            public void Reset()
            {
                LastFrame = 0;
                TickAcc = 0f;
                LastUpdateSteps = 0;
                BacklogSteps = 0;
                OverBudgetUpdateCount = 0L;
                DroppedTimeSeconds = 0d;
                InvalidDeltaCount = 0L;
                WorldReady = false;
                FirstFrameReceived = false;
            }
        }

#if UNITY_EDITOR
        internal sealed class EditorHooksState
        {
            public bool PlayModeHookActive;

            public void Reset()
            {
                PlayModeHookActive = false;
            }
        }
#endif

        internal sealed class GatewayRoomTimeSyncState
        {
            public bool HasClockSync;
            public double ClockOffsetSecondsEwma;
            public double RttSecondsEwma;
            public int Samples;

            public void Reset()
            {
                HasClockSync = false;
                ClockOffsetSecondsEwma = 0;
                RttSecondsEwma = 0;
                Samples = 0;
            }
        }

        internal sealed class RemoteDrivenSimState
        {
            public int LastTickedFrame;

            public void Reset()
            {
                LastTickedFrame = 0;
            }
        }

        internal sealed class ConfirmedSimState
        {
            public int LastTickedFrame;

            public void Reset()
            {
                LastTickedFrame = 0;
            }
        }

        internal sealed class FlagsState
        {
            public bool AutoPlanLogged;

            public void Reset()
            {
                AutoPlanLogged = false;
            }
        }

        public BattleStartPlan Plan;

        public readonly TickState Tick = new TickState();
        public readonly RemoteDrivenSimState RemoteDriven = new RemoteDrivenSimState();
        public readonly ConfirmedSimState Confirmed = new ConfirmedSimState();
        public readonly FlagsState Flags = new FlagsState();

        public readonly GatewayRoomTimeSyncState GatewayRoomTimeSync = new GatewayRoomTimeSyncState();

#if UNITY_EDITOR
        public readonly EditorHooksState EditorHooks = new EditorHooksState();
#endif

        public Exception PendingSubFeatureValidationFailure;

        public void ResetSessionFlags()
        {
            Tick.Reset();
            RemoteDriven.Reset();
            Confirmed.Reset();
            Flags.Reset();
            GatewayRoomTimeSync.Reset();

#if UNITY_EDITOR
            EditorHooks.Reset();
#endif
        }
    }
}
