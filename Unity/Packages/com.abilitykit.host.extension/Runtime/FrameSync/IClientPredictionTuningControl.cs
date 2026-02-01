using System;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    public interface IClientPredictionTuningControl
    {
        int MaxPredictionAheadFrames { get; }

        int MinPredictionWindow { get; }

        float BacklogEwmaAlpha { get; }

        void SetMaxPredictionAheadFrames(int value);

        void SetMinPredictionWindow(int value);

        void SetBacklogEwmaAlpha(float value);

        void ResetDefaults();
    }
}
