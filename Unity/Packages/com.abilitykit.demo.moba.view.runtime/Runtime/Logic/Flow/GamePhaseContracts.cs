using System;
using AbilityKit.Game.View.Flow;

namespace AbilityKit.Game.Flow
{
    public readonly struct GamePhaseContext
    {
        public readonly IGameHost Entry;
        public readonly IGameFeatureStore Features;
        public readonly IBattleEntityRuntime BattleEntities;
        public readonly BattleGameplayPresentationState GameplayPresentation;
        public readonly BattleOutcomeState BattleOutcome;

        public GamePhaseContext(IGameHost entry, IGameFeatureStore features, IBattleEntityRuntime battleEntities,
            BattleGameplayPresentationState gameplayPresentation = null,
            BattleOutcomeState battleOutcome = null)
        {
            Entry = entry;
            Features = features;
            BattleEntities = battleEntities;
            GameplayPresentation = gameplayPresentation;
            BattleOutcome = battleOutcome;
        }

        public T RequireFeature<T>(string owner) where T : class
        {
            if (Features != null && Features.TryGet(out T feature) && feature != null)
                return feature;
            throw new InvalidOperationException(
                $"{owner} requires battle feature port {typeof(T).Name}.");
        }
    }

    public interface IGamePhase : IPhase<GamePhaseContext>
    {
    }

    public interface IGamePhaseFeature : IPhaseFeature<GamePhaseContext>
    {
    }

    public enum GamePhaseFeatureTickFailureMode
    {
        FaultFlow,
        DisableFeature,
        Continue
    }

    public interface IGamePhaseFeatureTickFailurePolicy
    {
        GamePhaseFeatureTickFailureMode TickFailureMode { get; }
    }

    public interface IOnGUIFeature : IPhaseGuiFeature<GamePhaseContext>
    {
    }
}
