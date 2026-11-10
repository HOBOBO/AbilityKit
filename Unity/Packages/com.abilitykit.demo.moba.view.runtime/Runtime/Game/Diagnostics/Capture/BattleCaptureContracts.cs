using AbilityKit.Ability.Host;

namespace AbilityKit.Game.Battle.Diagnostics.Capture
{
    /// <summary>
    /// Optional, read-only sink for local battle diagnostics. Implementations must
    /// never modify the command, input state, simulation or presentation result.
    /// </summary>
    internal interface IBattleCaptureSink
    {
        bool IsEnabled { get; }

        void RecordHudEvent(
            int frame,
            string kind,
            int slot,
            int skillId,
            float inputX,
            float inputY,
            float resolvedX,
            float resolvedY);

        void RecordInputCommand(in PlayerInputCommand command, bool accepted);

        void Mark(string label, int frame);
    }

    /// <summary>Optional binding surface implemented by BattleContext only.</summary>
    internal interface IBattleCaptureBindingHost
    {
        int BattleCaptureFrame { get; }
        void BindBattleCapture(IBattleCaptureSink sink);
        void ClearBattleCapture(IBattleCaptureSink sink);
    }
}
