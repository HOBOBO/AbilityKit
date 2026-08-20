namespace AbilityKit.Core.Eventing
{
    /// <summary>Represents an idempotent handle that removes one event subscription.</summary>
    public interface IEventSubscription
    {
        /// <summary>Removes the subscription; subsequent calls have no effect.</summary>
        void Unsubscribe();
    }
}
