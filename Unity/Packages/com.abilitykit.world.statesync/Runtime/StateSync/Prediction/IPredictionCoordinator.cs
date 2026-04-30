using System;

namespace AbilityKit.Ability.StateSync
{
    public interface IPredictionCoordinator
    {
        int LocalPlayerId { get; }
        int CurrentPredictedFrame { get; }
        int ServerConfirmedFrame { get; }
        bool NeedsRollback { get; }

        void RecordInput(int frame, PlayerInputCommand input);
        void ProcessServerState(int serverFrame, ServerGameState state);
        void ExecuteRollback();
        void AdvancePrediction();
        void Reset();
    }
}
