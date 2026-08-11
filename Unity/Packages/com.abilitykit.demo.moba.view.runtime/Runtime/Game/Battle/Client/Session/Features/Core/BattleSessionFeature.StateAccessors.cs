using System;
using AbilityKit.Game.Battle;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private BattleLogicSession _session
        {
            get => _handles.Session;
            set => _handles.Session = value;
        }

        private BattleStartPlan _plan
        {
            get => _state.Plan;
            set => _state.Plan = value;
        }

        private int _lastFrame
        {
            get => _state.Tick.LastFrame;
            set => _state.Tick.LastFrame = value;
        }

        private float _tickAcc
        {
            get => _state.Tick.TickAcc;
            set => _state.Tick.TickAcc = value;
        }

        private bool _firstFrameReceived
        {
            get => _state.Tick.FirstFrameReceived;
            set => _state.Tick.FirstFrameReceived = value;
        }

        private bool _autoPlanLogged
        {
            get => _state.Flags.AutoPlanLogged;
            set => _state.Flags.AutoPlanLogged = value;
        }

        private Exception _pendingSubFeatureValidationFailure
        {
            get => _state.PendingSubFeatureValidationFailure;
            set => _state.PendingSubFeatureValidationFailure = value;
        }
    }
}
