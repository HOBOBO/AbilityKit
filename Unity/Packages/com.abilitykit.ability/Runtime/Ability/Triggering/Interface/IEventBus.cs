namespace AbilityKit.Ability.Triggering
{
    public interface IEventBus
    {
        IEventSubscription Subscribe(string eventId, IEventHandler handler);
        void Publish(in TriggerEvent evt);
    }
}
