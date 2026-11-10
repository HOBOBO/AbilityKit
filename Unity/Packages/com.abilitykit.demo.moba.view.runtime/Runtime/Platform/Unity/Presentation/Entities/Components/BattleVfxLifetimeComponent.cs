namespace AbilityKit.Game.Battle.Component
{
    public sealed class BattleVfxLifetimeComponent
    {
        public float ExpireAtTime;
    }

    /// <summary>Request-driven effects opt into the shared presentation lifecycle contract.</summary>
    public sealed class BattlePresentationLifetimeComponent
    {
        public AbilityKit.Game.Flow.PresentationRequest Request;
        public AbilityKit.Game.Flow.PresentationLifetimeState State = new AbilityKit.Game.Flow.PresentationLifetimeState();
    }
}
