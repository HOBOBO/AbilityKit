using System;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    internal sealed class BattleSessionState
    {
        internal sealed class TickState
        {
            public int LastFrame;
            public float TickAcc;
            public bool FirstFrameReceived;

            public void Reset()
            {
                LastFrame = 0;
                TickAcc = 0f;
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
            public int LastLoggedFrame;
            public bool FirstSnapshotLogged;
            public bool FirstSpawnLogged;

            public void Reset()
            {
                LastTickedFrame = 0;
                LastLoggedFrame = 0;
                FirstSnapshotLogged = false;
                FirstSpawnLogged = false;
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
            public bool TickEnteredLogged;
            public bool AutoPlanLogged;

            public void Reset()
            {
                TickEnteredLogged = false;
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

        public Exception PendingModuleValidationFailure;

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
